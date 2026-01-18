namespace QDeskPro.Domain.Entities;

/// <summary>
/// Manager comments, notes, reminders, and tasks associated with a quarry.
/// Can optionally be linked to specific entities (Sale, Expense, FuelUsage, Banking).
/// </summary>
public class QuarryComment : BaseEntity
{
    /// <summary>
    /// Main comment content (required)
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Optional title for the comment
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Type of comment: "Note", "Reminder", or "Task"
    /// </summary>
    public string CommentType { get; set; } = "Note";

    /// <summary>
    /// Whether the task/reminder has been completed (for Task and Reminder types)
    /// </summary>
    public bool IsCompleted { get; set; }

    /// <summary>
    /// Date when the task was marked as completed
    /// </summary>
    public DateTime? CompletedDate { get; set; }

    /// <summary>
    /// Due date for reminders and tasks
    /// </summary>
    public DateTime? DueDate { get; set; }

    /// <summary>
    /// Priority level: "Low", "Medium", or "High"
    /// </summary>
    public string? Priority { get; set; }

    /// <summary>
    /// Type of entity this comment is linked to (Sale, Expense, FuelUsage, Banking, or null for quarry-level)
    /// </summary>
    public string? LinkedEntityType { get; set; }

    /// <summary>
    /// ID of the linked entity
    /// </summary>
    public string? LinkedEntityId { get; set; }

    /// <summary>
    /// Display reference for linked entity (e.g., vehicle registration, expense item)
    /// </summary>
    public string? LinkedEntityReference { get; set; }

    /// <summary>
    /// User ID of the author (manager who created this comment)
    /// </summary>
    public string ApplicationUserId { get; set; } = string.Empty;

    /// <summary>
    /// Denormalized author name for display
    /// </summary>
    public string AuthorName { get; set; } = string.Empty;

    /// <summary>
    /// Quarry this comment belongs to
    /// </summary>
    public string QuarryId { get; set; } = string.Empty;

    // Navigation properties

    /// <summary>
    /// The quarry this comment is associated with
    /// </summary>
    public virtual Quarry? Quarry { get; set; }

    /// <summary>
    /// The author (manager) who created this comment
    /// </summary>
    public virtual ApplicationUser? Author { get; set; }
}
