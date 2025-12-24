using QDeskPro.Data;

namespace QDeskPro.Domain.Entities;

/// <summary>
/// Represents an AI chat conversation/session
/// </summary>
public class AIConversation : BaseEntity
{
    /// <summary>
    /// User who owns this conversation
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Conversation title (auto-generated or user-defined)
    /// </summary>
    public string Title { get; set; } = "New Conversation";

    /// <summary>
    /// Type of chat: "sales", "analytics", "report", "general"
    /// </summary>
    public string ChatType { get; set; } = "general";

    /// <summary>
    /// Optional: Scope conversation to specific quarry
    /// </summary>
    public string? QuarryId { get; set; }

    /// <summary>
    /// Timestamp of last message in this conversation
    /// </summary>
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Total tokens used in this conversation
    /// </summary>
    public int TotalTokensUsed { get; set; }

    // Navigation properties
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual Quarry? Quarry { get; set; }
    public virtual ICollection<AIMessage> Messages { get; set; } = new List<AIMessage>();
}
