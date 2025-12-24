namespace QDeskPro.Domain.Models.AI;

/// <summary>
/// Root configuration for AI features
/// </summary>
public class AIConfiguration
{
    public const string SectionName = "AI";

    public OpenAIConfig OpenAI { get; set; } = new();
    public VectorSearchConfig VectorSearch { get; set; } = new();
    public ChatConfig Chat { get; set; } = new();
    public AIFeaturesConfig Features { get; set; } = new();
}

/// <summary>
/// OpenAI API configuration
/// </summary>
public class OpenAIConfig
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-5-nano";
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
    public int MaxTokens { get; set; } = 2000;
    public double Temperature { get; set; } = 0.7;
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
}

/// <summary>
/// Vector search configuration for semantic search
/// </summary>
public class VectorSearchConfig
{
    public int TopK { get; set; } = 5;
    public double MinSimilarityScore { get; set; } = 0.7;
    public int EmbeddingDimensions { get; set; } = 1536;
}

/// <summary>
/// Chat behavior configuration
/// </summary>
public class ChatConfig
{
    public int MaxMessagesInContext { get; set; } = 20;
    public int MaxContextCharacters { get; set; } = 3000;
    public int MaxFunctionCallIterations { get; set; } = 5;
    public Dictionary<string, string> SystemPrompts { get; set; } = new()
    {
        ["SalesAssistant"] = @"You are QDeskPro AI, an intelligent assistant for quarry sales management in Kenya.
You help clerks and managers with sales data, expenses, reports, and analytics.
Always format currency as KES (Kenyan Shillings) with thousand separators.
Be concise but helpful. Use tables and bullet points for clarity when presenting data.
You have access to tools that can query the sales database - use them to provide accurate, real-time information.",

        ["AnalyticsAssistant"] = @"You are QDeskPro Analytics AI, specializing in sales data analysis and business insights.
Focus on trends, comparisons, and actionable recommendations.
When presenting data, include percentage changes and context.
Highlight anomalies and areas that need attention.",

        ["ReportAssistant"] = @"You are QDeskPro Report AI, helping generate and explain financial reports.
Focus on accuracy and completeness of financial data.
Explain calculations clearly and highlight key metrics."
    };
}

/// <summary>
/// Feature flags for AI capabilities
/// </summary>
public class AIFeaturesConfig
{
    public bool EnableAIFeatures { get; set; } = true;
    public bool EnableVectorSearch { get; set; } = true;
    public bool EnableFunctionCalling { get; set; } = true;
    public bool EnableAutoSuggestions { get; set; } = true;
}
