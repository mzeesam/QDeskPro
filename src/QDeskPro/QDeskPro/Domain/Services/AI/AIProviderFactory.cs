using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using QDeskPro.Domain.Models.AI;

namespace QDeskPro.Domain.Services.AI;

/// <summary>
/// Factory for creating OpenAI client instances
/// </summary>
public interface IAIProviderFactory
{
    ChatClient CreateChatClient();
    OpenAIClient CreateClient();
}

public class AIProviderFactory : IAIProviderFactory
{
    private readonly AIConfiguration _config;
    private readonly ILogger<AIProviderFactory> _logger;
    private OpenAIClient? _client;

    public AIProviderFactory(
        IOptions<AIConfiguration> config,
        ILogger<AIProviderFactory> logger)
    {
        _config = config.Value;
        _logger = logger;
    }

    public OpenAIClient CreateClient()
    {
        if (_client is not null)
            return _client;

        if (string.IsNullOrEmpty(_config.OpenAI.ApiKey))
        {
            _logger.LogWarning("OpenAI API key is not configured");
            throw new InvalidOperationException("OpenAI API key is not configured");
        }

        _client = new OpenAIClient(_config.OpenAI.ApiKey);
        _logger.LogInformation("Created OpenAI client with model: {Model}", _config.OpenAI.Model);

        return _client;
    }

    public ChatClient CreateChatClient()
    {
        var client = CreateClient();
        return client.GetChatClient(_config.OpenAI.Model);
    }
}
