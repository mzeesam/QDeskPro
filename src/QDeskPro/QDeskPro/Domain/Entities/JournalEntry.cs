namespace QDeskPro.Domain.Entities;

/// <summary>
/// Represents a journal entry for double-entry bookkeeping.
/// Can be auto-generated from transactions or manually created for adjustments.
/// </summary>
public class JournalEntry : BaseEntity
{
    /// <summary>
    /// Date of the journal entry
    /// </summary>
    public DateTime EntryDate { get; set; }

    /// <summary>
    /// Unique reference number (e.g., "JE-2025-001", "ADJ-2025-001")
    /// </summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>
    /// Description of the journal entry
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Entry type: "Auto" (system-generated) or "Manual" (user-created adjustment)
    /// </summary>
    public string EntryType { get; set; } = "Auto";

    /// <summary>
    /// Source entity type for auto-generated entries (e.g., "Sale", "Expense", "Banking")
    /// </summary>
    public string? SourceEntityType { get; set; }

    /// <summary>
    /// Source entity ID for linking back to the original transaction
    /// </summary>
    public string? SourceEntityId { get; set; }

    /// <summary>
    /// Whether the entry has been posted (posted entries cannot be edited)
    /// </summary>
    public bool IsPosted { get; set; }

    /// <summary>
    /// User ID who posted the entry
    /// </summary>
    public string? PostedBy { get; set; }

    /// <summary>
    /// Date when the entry was posted
    /// </summary>
    public DateTime? PostedDate { get; set; }

    /// <summary>
    /// Total debit amount (calculated from lines)
    /// </summary>
    public double TotalDebit { get; set; }

    /// <summary>
    /// Total credit amount (calculated from lines)
    /// </summary>
    public double TotalCredit { get; set; }

    /// <summary>
    /// Fiscal year of the entry
    /// </summary>
    public int FiscalYear { get; set; }

    /// <summary>
    /// Fiscal period (1-12 for monthly)
    /// </summary>
    public int FiscalPeriod { get; set; }

    // Navigation properties

    /// <summary>
    /// Journal entry lines (debits and credits)
    /// </summary>
    public virtual ICollection<JournalEntryLine> Lines { get; set; } = new List<JournalEntryLine>();

    /// <summary>
    /// Indicates if the entry is balanced (debits = credits)
    /// </summary>
    public bool IsBalanced => Math.Abs(TotalDebit - TotalCredit) < 0.01;
}
