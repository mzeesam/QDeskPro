using QDeskPro.Data;

namespace QDeskPro.Domain.Entities;

/// <summary>
/// Represents a single message in an AI conversation
/// </summary>
public class AIMessage : BaseEntity
{
    /// <summary>
    /// Parent conversation ID
    /// </summary>
    public string AIConversationId { get; set; } = string.Empty;

    /// <summary>
    /// Message role: "system", "user", "assistant", "tool"
    /// </summary>
    public string Role { get; set; } = "user";

    /// <summary>
    /// Message content
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Tool call ID (if this is a tool response)
    /// </summary>
    public string? ToolCallId { get; set; }

    /// <summary>
    /// Tool name (if this message invoked a tool)
    /// </summary>
    public string? ToolName { get; set; }

    /// <summary>
    /// Tool execution result (JSON)
    /// </summary>
    public string? ToolResult { get; set; }

    /// <summary>
    /// Tokens used for this message
    /// </summary>
    public int? TokensUsed { get; set; }

    /// <summary>
    /// Message timestamp
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Navigation property
    public virtual AIConversation Conversation { get; set; } = null!;
}
