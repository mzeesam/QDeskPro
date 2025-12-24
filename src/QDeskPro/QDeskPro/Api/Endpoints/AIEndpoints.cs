using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using QDeskPro.Domain.Services.AI;

namespace QDeskPro.Api.Endpoints;

/// <summary>
/// API endpoints for AI chat functionality
/// </summary>
public static class AIEndpoints
{
    public static void MapAIEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ai")
            .WithTags("AI")
            .RequireAuthorization()
            .RequireRateLimiting("api");  // API rate limiting

        // Conversations
        group.MapGet("/conversations", GetConversationsAsync)
            .WithName("GetConversations")
            .WithDescription("Get user's chat conversations");

        group.MapPost("/conversations", CreateConversationAsync)
            .WithName("CreateConversation")
            .WithDescription("Create a new chat conversation");

        group.MapGet("/conversations/{id}", GetConversationAsync)
            .WithName("GetConversation")
            .WithDescription("Get a specific conversation with messages");

        group.MapDelete("/conversations/{id}", DeleteConversationAsync)
            .WithName("DeleteConversation")
            .WithDescription("Delete a conversation");

        // Messages
        group.MapPost("/conversations/{id}/messages", SendMessageAsync)
            .WithName("SendMessage")
            .WithDescription("Send a message to the AI and get a response");

        group.MapGet("/conversations/{id}/messages", GetMessagesAsync)
            .WithName("GetMessages")
            .WithDescription("Get all messages in a conversation");

        // Health check
        group.MapGet("/health", CheckAIHealthAsync)
            .WithName("CheckAIHealth")
            .WithDescription("Check if AI services are configured and available");
    }

    private static async Task<IResult> GetConversationsAsync(
        IChatCompletionService chatService,
        ClaimsPrincipal user,
        [FromQuery] int limit = 20)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        var conversations = await chatService.GetUserConversationsAsync(userId, limit);
        return Results.Ok(conversations.Select(c => new
        {
            c.Id,
            c.Title,
            c.ChatType,
            c.QuarryId,
            c.LastMessageAt,
            c.TotalTokensUsed,
            c.DateCreated
        }));
    }

    private static async Task<IResult> CreateConversationAsync(
        IChatCompletionService chatService,
        ClaimsPrincipal user,
        [FromBody] CreateConversationRequest request)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        var conversation = await chatService.CreateConversationAsync(
            userId,
            request.QuarryId,
            request.ChatType ?? "general");

        return Results.Created($"/api/ai/conversations/{conversation.Id}", new
        {
            conversation.Id,
            conversation.Title,
            conversation.ChatType,
            conversation.QuarryId,
            conversation.DateCreated
        });
    }

    private static async Task<IResult> GetConversationAsync(
        IChatCompletionService chatService,
        ClaimsPrincipal user,
        string id)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        var conversation = await chatService.GetConversationAsync(id);
        if (conversation == null)
            return Results.NotFound();

        if (conversation.UserId != userId)
            return Results.Forbid();

        return Results.Ok(new
        {
            conversation.Id,
            conversation.Title,
            conversation.ChatType,
            conversation.QuarryId,
            conversation.LastMessageAt,
            conversation.TotalTokensUsed,
            Messages = conversation.Messages.Select(m => new
            {
                m.Id,
                m.Role,
                m.Content,
                m.ToolName,
                m.TokensUsed,
                m.Timestamp
            })
        });
    }

    private static async Task<IResult> DeleteConversationAsync(
        IChatCompletionService chatService,
        ClaimsPrincipal user,
        string id)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        var conversation = await chatService.GetConversationAsync(id);
        if (conversation == null)
            return Results.NotFound();

        if (conversation.UserId != userId)
            return Results.Forbid();

        await chatService.DeleteConversationAsync(id);
        return Results.NoContent();
    }

    private static async Task<IResult> SendMessageAsync(
        IChatCompletionService chatService,
        ClaimsPrincipal user,
        string id,
        [FromBody] SendMessageRequest request)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        var conversation = await chatService.GetConversationAsync(id);
        if (conversation == null)
            return Results.NotFound();

        if (conversation.UserId != userId)
            return Results.Forbid();

        var response = await chatService.SendMessageAsync(
            id,
            request.Message,
            userId,
            request.QuarryId ?? conversation.QuarryId);

        if (!response.Success)
            return Results.BadRequest(new { error = response.Error });

        return Results.Ok(new
        {
            message = response.Message,
            tokensUsed = response.TokensUsed,
            toolExecutions = response.ToolExecutions?.Select(t => new
            {
                t.ToolName,
                arguments = t.Arguments,
                hasResult = !string.IsNullOrEmpty(t.Result)
            })
        });
    }

    private static async Task<IResult> GetMessagesAsync(
        IChatCompletionService chatService,
        ClaimsPrincipal user,
        string id)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        var conversation = await chatService.GetConversationAsync(id);
        if (conversation == null)
            return Results.NotFound();

        if (conversation.UserId != userId)
            return Results.Forbid();

        var messages = await chatService.GetConversationMessagesAsync(id);
        return Results.Ok(messages.Select(m => new
        {
            m.Id,
            m.Role,
            m.Content,
            m.ToolName,
            m.TokensUsed,
            m.Timestamp
        }));
    }

    private static Task<IResult> CheckAIHealthAsync(
        IAIProviderFactory providerFactory,
        ILogger<Program> logger)
    {
        try
        {
            // Just try to create the client - don't make actual API calls
            var client = providerFactory.CreateClient();
            return Task.FromResult(Results.Ok(new
            {
                status = "healthy",
                message = "AI services are configured and ready"
            }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI health check failed");
            return Task.FromResult(Results.Ok(new
            {
                status = "unhealthy",
                message = "AI services are not properly configured",
                error = ex.Message
            }));
        }
    }
}

public record CreateConversationRequest(string? QuarryId, string? ChatType);
public record SendMessageRequest(string Message, string? QuarryId);
