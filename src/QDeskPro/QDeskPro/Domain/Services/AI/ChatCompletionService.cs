using System.ClientModel;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using QDeskPro.Data;
using QDeskPro.Domain.Entities;
using QDeskPro.Domain.Models.AI;

namespace QDeskPro.Domain.Services.AI;

/// <summary>
/// Main service for handling AI chat completions with function calling
/// </summary>
public interface IChatCompletionService
{
    Task<ChatResponse> SendMessageAsync(string conversationId, string message, string userId, string? quarryId = null);
    IAsyncEnumerable<StreamingChatChunk> SendMessageStreamingAsync(string conversationId, string message, string userId, string? quarryId = null);
    Task<AIConversation> CreateConversationAsync(string userId, string? quarryId = null, string chatType = "general");
    Task<List<AIConversation>> GetUserConversationsAsync(string userId, int limit = 20);
    Task<AIConversation?> GetConversationAsync(string conversationId);
    Task<List<AIMessage>> GetConversationMessagesAsync(string conversationId);
    Task DeleteConversationAsync(string conversationId);
}

public class ChatResponse
{
    public string Message { get; set; } = string.Empty;
    public int TokensUsed { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<ToolExecution>? ToolExecutions { get; set; }
}

public class ToolExecution
{
    public string ToolName { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
}

public class StreamingChatChunk
{
    public string? Content { get; set; }
    public bool IsComplete { get; set; }
    public bool IsToolCall { get; set; }
    public string? ToolName { get; set; }
    public string? Error { get; set; }
    public int TokensUsed { get; set; }
    public List<ToolExecution>? ToolExecutions { get; set; }
}

public class ChatCompletionService : IChatCompletionService
{
    private readonly IAIProviderFactory _providerFactory;
    private readonly ISalesQueryService _queryService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AIConfiguration _config;
    private readonly ILogger<ChatCompletionService> _logger;

    public ChatCompletionService(
        IAIProviderFactory providerFactory,
        ISalesQueryService queryService,
        IServiceScopeFactory scopeFactory,
        IOptions<AIConfiguration> config,
        ILogger<ChatCompletionService> logger)
    {
        _providerFactory = providerFactory;
        _queryService = queryService;
        _scopeFactory = scopeFactory;
        _config = config.Value;
        _logger = logger;
    }

    public async Task<AIConversation> CreateConversationAsync(string userId, string? quarryId = null, string chatType = "general")
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var conversation = new AIConversation
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            QuarryId = quarryId,
            ChatType = chatType,
            Title = "New Conversation",
            DateCreated = DateTime.UtcNow,
            CreatedBy = userId,
            IsActive = true
        };

        context.AIConversations.Add(conversation);
        await context.SaveChangesAsync();

        _logger.LogInformation("Created new conversation {ConversationId} for user {UserId}", conversation.Id, userId);
        return conversation;
    }

    public async Task<List<AIConversation>> GetUserConversationsAsync(string userId, int limit = 20)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await context.AIConversations
            .Where(c => c.UserId == userId && c.IsActive)
            .OrderByDescending(c => c.LastMessageAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<AIConversation?> GetConversationAsync(string conversationId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await context.AIConversations
            .Include(c => c.Messages.OrderBy(m => m.Timestamp))
            .FirstOrDefaultAsync(c => c.Id == conversationId && c.IsActive);
    }

    public async Task<List<AIMessage>> GetConversationMessagesAsync(string conversationId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await context.AIMessages
            .Where(m => m.AIConversationId == conversationId)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();
    }

    public async Task DeleteConversationAsync(string conversationId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var conversation = await context.AIConversations.FindAsync(conversationId);
        if (conversation != null)
        {
            conversation.IsActive = false;
            await context.SaveChangesAsync();
        }
    }

    public async Task<ChatResponse> SendMessageAsync(string conversationId, string message, string userId, string? quarryId = null)
    {
        var toolExecutions = new List<ToolExecution>();

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Get or create conversation
            var conversation = await context.AIConversations
                .Include(c => c.Messages.OrderBy(m => m.Timestamp))
                .FirstOrDefaultAsync(c => c.Id == conversationId && c.IsActive);

            if (conversation == null)
            {
                return new ChatResponse { Success = false, Error = "Conversation not found" };
            }

            // Get the system prompt based on chat type
            var systemPrompt = GetSystemPrompt(conversation.ChatType, quarryId);

            // Build message history
            var messages = new List<ChatMessage>();

            // Add system message
            messages.Add(ChatMessage.CreateSystemMessage(systemPrompt));

            // Get conversation history from the already loaded messages
            var history = conversation.Messages?.ToList() ?? [];
            var recentHistory = history
                .TakeLast(_config.Chat.MaxMessagesInContext)
                .ToList();

            foreach (var msg in recentHistory)
            {
                messages.Add(msg.Role switch
                {
                    "user" => ChatMessage.CreateUserMessage(msg.Content),
                    "assistant" => ChatMessage.CreateAssistantMessage(msg.Content),
                    _ => ChatMessage.CreateUserMessage(msg.Content)
                });
            }

            // Add new user message
            messages.Add(ChatMessage.CreateUserMessage(message));

            // Save user message
            var userMessage = new AIMessage
            {
                Id = Guid.NewGuid().ToString(),
                AIConversationId = conversationId,
                Role = "user",
                Content = message,
                Timestamp = DateTime.UtcNow,
                DateCreated = DateTime.UtcNow,
                CreatedBy = userId,
                IsActive = true
            };
            context.AIMessages.Add(userMessage);

            // Create chat client with tools
            var chatClient = _providerFactory.CreateChatClient();
            var tools = SalesQueryTools.GetAllTools().ToList();

            var options = new ChatCompletionOptions
            {
                MaxOutputTokenCount = _config.OpenAI.MaxTokens
            };

            foreach (var tool in tools)
            {
                options.Tools.Add(tool);
            }

            // Execute chat with function calling loop
            var response = await ExecuteChatWithToolsAsync(
                chatClient, messages, options, quarryId, toolExecutions);

            // Save assistant response
            var assistantMessage = new AIMessage
            {
                Id = Guid.NewGuid().ToString(),
                AIConversationId = conversationId,
                Role = "assistant",
                Content = response.Message,
                TokensUsed = response.TokensUsed,
                Timestamp = DateTime.UtcNow,
                DateCreated = DateTime.UtcNow,
                CreatedBy = "system",
                IsActive = true
            };
            context.AIMessages.Add(assistantMessage);

            // Update conversation
            conversation.LastMessageAt = DateTime.UtcNow;
            conversation.TotalTokensUsed += response.TokensUsed;

            // Generate title from first message if needed
            if (conversation.Title == "New Conversation" && history.Count == 0)
            {
                conversation.Title = GenerateTitle(message);
            }

            await context.SaveChangesAsync();

            response.ToolExecutions = toolExecutions;
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in chat completion for conversation {ConversationId}", conversationId);
            return new ChatResponse
            {
                Success = false,
                Error = ex.Message,
                ToolExecutions = toolExecutions
            };
        }
    }

    public async IAsyncEnumerable<StreamingChatChunk> SendMessageStreamingAsync(
        string conversationId,
        string message,
        string userId,
        string? quarryId = null)
    {
        var toolExecutions = new List<ToolExecution>();
        var totalTokens = 0;
        string? initError = null;

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        AIConversation? conversation = null;
        try
        {
            conversation = await context.AIConversations
                .Include(c => c.Messages.OrderBy(m => m.Timestamp))
                .FirstOrDefaultAsync(c => c.Id == conversationId && c.IsActive);

            if (conversation == null)
            {
                initError = "Conversation not found";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading conversation {ConversationId}", conversationId);
            initError = ex.Message;
        }

        if (initError != null)
        {
            yield return new StreamingChatChunk { Error = initError, IsComplete = true };
            yield break;
        }

        // Get the system prompt based on chat type
        var systemPrompt = GetSystemPrompt(conversation.ChatType, quarryId);

        // Build message history
        var messages = new List<ChatMessage> { ChatMessage.CreateSystemMessage(systemPrompt) };

        var history = conversation.Messages?.ToList() ?? [];
        var recentHistory = history.TakeLast(_config.Chat.MaxMessagesInContext).ToList();

        foreach (var msg in recentHistory)
        {
            messages.Add(msg.Role switch
            {
                "user" => ChatMessage.CreateUserMessage(msg.Content),
                "assistant" => ChatMessage.CreateAssistantMessage(msg.Content),
                _ => ChatMessage.CreateUserMessage(msg.Content)
            });
        }

        messages.Add(ChatMessage.CreateUserMessage(message));

        // Save user message
        var userMessage = new AIMessage
        {
            Id = Guid.NewGuid().ToString(),
            AIConversationId = conversationId,
            Role = "user",
            Content = message,
            Timestamp = DateTime.UtcNow,
            DateCreated = DateTime.UtcNow,
            CreatedBy = userId,
            IsActive = true
        };
        context.AIMessages.Add(userMessage);
        await context.SaveChangesAsync();

        // Create chat client with tools
        var chatClient = _providerFactory.CreateChatClient();
        var tools = SalesQueryTools.GetAllTools().ToList();

        var options = new ChatCompletionOptions { MaxOutputTokenCount = _config.OpenAI.MaxTokens };
        foreach (var tool in tools) options.Tools.Add(tool);

        var iterations = 0;
        var maxIterations = _config.Chat.MaxFunctionCallIterations;
        var fullResponse = new System.Text.StringBuilder();

        while (iterations < maxIterations)
        {
            iterations++;

            // First, check if we need to handle tool calls (non-streaming for tool detection)
            ChatCompletion? toolCheckCompletion = null;
            string? apiError = null;
            try
            {
                toolCheckCompletion = await chatClient.CompleteChatAsync(messages, options);
            }
            catch (ClientResultException ex)
            {
                _logger.LogError(ex, "OpenAI API error during streaming");
                apiError = $"AI service error: {ex.Message}";
            }

            if (apiError != null)
            {
                yield return new StreamingChatChunk { Error = apiError, IsComplete = true };
                yield break;
            }

            totalTokens += toolCheckCompletion!.Usage?.TotalTokenCount ?? 0;

            if (toolCheckCompletion.FinishReason == ChatFinishReason.ToolCalls)
            {
                // Handle tool calls
                messages.Add(ChatMessage.CreateAssistantMessage(toolCheckCompletion));

                foreach (var toolCall in toolCheckCompletion.ToolCalls)
                {
                    yield return new StreamingChatChunk
                    {
                        IsToolCall = true,
                        ToolName = toolCall.FunctionName
                    };

                    _logger.LogInformation("Executing tool: {ToolName}", toolCall.FunctionName);

                    var arguments = JsonDocument.Parse(toolCall.FunctionArguments.ToString()).RootElement;
                    var result = await _queryService.ExecuteToolAsync(toolCall.FunctionName, arguments, quarryId);

                    toolExecutions.Add(new ToolExecution
                    {
                        ToolName = toolCall.FunctionName,
                        Arguments = toolCall.FunctionArguments.ToString(),
                        Result = result
                    });

                    messages.Add(ChatMessage.CreateToolMessage(toolCall.Id, result));
                }

                continue;
            }

            // Normal completion - stream the response
            // For streaming, we need to make another call without tools to get streaming
            var streamOptions = new ChatCompletionOptions { MaxOutputTokenCount = _config.OpenAI.MaxTokens };

            await foreach (var update in chatClient.CompleteChatStreamingAsync(messages, streamOptions))
            {
                foreach (var contentPart in update.ContentUpdate)
                {
                    if (!string.IsNullOrEmpty(contentPart.Text))
                    {
                        fullResponse.Append(contentPart.Text);
                        yield return new StreamingChatChunk { Content = contentPart.Text };
                    }
                }

                if (update.Usage != null)
                {
                    totalTokens += update.Usage.TotalTokenCount;
                }
            }

            // Save assistant response
            var assistantMessage = new AIMessage
            {
                Id = Guid.NewGuid().ToString(),
                AIConversationId = conversationId,
                Role = "assistant",
                Content = fullResponse.ToString(),
                TokensUsed = totalTokens,
                Timestamp = DateTime.UtcNow,
                DateCreated = DateTime.UtcNow,
                CreatedBy = "system",
                IsActive = true
            };
            context.AIMessages.Add(assistantMessage);

            // Update conversation
            conversation.LastMessageAt = DateTime.UtcNow;
            conversation.TotalTokensUsed += totalTokens;

            if (conversation.Title == "New Conversation" && history.Count == 0)
            {
                conversation.Title = GenerateTitle(message);
            }

            await context.SaveChangesAsync();

            yield return new StreamingChatChunk
            {
                IsComplete = true,
                TokensUsed = totalTokens,
                ToolExecutions = toolExecutions
            };

            yield break;
        }

        yield return new StreamingChatChunk
        {
            Error = "Maximum tool call iterations exceeded",
            IsComplete = true,
            TokensUsed = totalTokens
        };
    }

    private async Task<ChatResponse> ExecuteChatWithToolsAsync(
        ChatClient chatClient,
        List<ChatMessage> messages,
        ChatCompletionOptions options,
        string? quarryId,
        List<ToolExecution> toolExecutions)
    {
        var totalTokens = 0;
        var iterations = 0;
        var maxIterations = _config.Chat.MaxFunctionCallIterations;

        while (iterations < maxIterations)
        {
            iterations++;

            ChatCompletion completion;
            try
            {
                completion = await chatClient.CompleteChatAsync(messages, options);
            }
            catch (ClientResultException ex)
            {
                _logger.LogError(ex, "OpenAI API error");
                return new ChatResponse
                {
                    Success = false,
                    Error = $"AI service error: {ex.Message}",
                    TokensUsed = totalTokens
                };
            }

            totalTokens += completion.Usage?.TotalTokenCount ?? 0;

            // Check if we need to execute tool calls
            if (completion.FinishReason == ChatFinishReason.ToolCalls)
            {
                // Add assistant message with tool calls
                messages.Add(ChatMessage.CreateAssistantMessage(completion));

                // Execute each tool call
                foreach (var toolCall in completion.ToolCalls)
                {
                    _logger.LogInformation("Executing tool: {ToolName}", toolCall.FunctionName);

                    var arguments = JsonDocument.Parse(toolCall.FunctionArguments.ToString()).RootElement;
                    var result = await _queryService.ExecuteToolAsync(toolCall.FunctionName, arguments, quarryId);

                    toolExecutions.Add(new ToolExecution
                    {
                        ToolName = toolCall.FunctionName,
                        Arguments = toolCall.FunctionArguments.ToString(),
                        Result = result
                    });

                    // Add tool result to messages
                    messages.Add(ChatMessage.CreateToolMessage(toolCall.Id, result));
                }

                continue;
            }

            // Normal completion - return the response
            var responseContent = completion.Content.FirstOrDefault()?.Text ?? "";

            return new ChatResponse
            {
                Success = true,
                Message = responseContent,
                TokensUsed = totalTokens
            };
        }

        return new ChatResponse
        {
            Success = false,
            Error = "Maximum tool call iterations exceeded",
            TokensUsed = totalTokens
        };
    }

    private string GetSystemPrompt(string chatType, string? quarryId)
    {
        var basePrompt = chatType switch
        {
            "sales" => _config.Chat.SystemPrompts.GetValueOrDefault("SalesAssistant", GetDefaultSalesPrompt()),
            "analytics" => _config.Chat.SystemPrompts.GetValueOrDefault("AnalyticsAssistant", GetDefaultAnalyticsPrompt()),
            "report" => _config.Chat.SystemPrompts.GetValueOrDefault("ReportAssistant", GetDefaultReportPrompt()),
            _ => _config.Chat.SystemPrompts.GetValueOrDefault("SalesAssistant", GetDefaultSalesPrompt())
        };

        // Add context about current date
        var contextAddition = $"\n\nCurrent date: {DateTime.Today:dddd, MMMM d, yyyy}";

        if (!string.IsNullOrEmpty(quarryId))
        {
            contextAddition += $"\nUser's quarry context is set.";
        }

        return basePrompt + contextAddition;
    }

    private static string GetDefaultSalesPrompt() =>
        """
        You are QDeskPro AI, an intelligent assistant for quarry sales management in Kenya.
        You help clerks and managers with sales data, expenses, reports, and analytics.
        Always format currency as KES (Kenyan Shillings) with thousand separators.
        Be concise but helpful. Use tables and bullet points for clarity when presenting data.
        You have access to tools that can query the sales database - use them to provide accurate, real-time information.
        """;

    private static string GetDefaultAnalyticsPrompt() =>
        """
        You are QDeskPro Analytics AI, specializing in sales data analysis and business insights.
        Focus on trends, comparisons, and actionable recommendations.
        When presenting data, include percentage changes and context.
        Highlight anomalies and areas that need attention.
        """;

    private static string GetDefaultReportPrompt() =>
        """
        You are QDeskPro Report AI, helping generate and explain financial reports.
        Focus on accuracy and completeness of financial data.
        Explain calculations clearly and highlight key metrics.
        """;

    private static string GenerateTitle(string firstMessage)
    {
        // Generate a short title from the first message
        var title = firstMessage.Length > 50
            ? firstMessage[..47] + "..."
            : firstMessage;

        return title.Trim();
    }
}
