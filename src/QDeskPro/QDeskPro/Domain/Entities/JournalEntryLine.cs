namespace QDeskPro.Domain.Entities;

/// <summary>
/// Represents a single line item in a journal entry (either a debit or credit).
/// </summary>
public class JournalEntryLine : BaseEntity
{
    /// <summary>
    /// Parent journal entry ID
    /// </summary>
    public string JournalEntryId { get; set; } = string.Empty;

    /// <summary>
    /// Ledger account ID being affected
    /// </summary>
    public string LedgerAccountId { get; set; } = string.Empty;

    /// <summary>
    /// Debit amount (positive value, 0 if credit line)
    /// </summary>
    public double DebitAmount { get; set; }

    /// <summary>
    /// Credit amount (positive value, 0 if debit line)
    /// </summary>
    public double CreditAmount { get; set; }

    /// <summary>
    /// Optional memo/description for this specific line
    /// </summary>
    public string? Memo { get; set; }

    /// <summary>
    /// Line number within the journal entry (for ordering)
    /// </summary>
    public int LineNumber { get; set; }

    // Navigation properties

    /// <summary>
    /// Parent journal entry
    /// </summary>
    public virtual JournalEntry JournalEntry { get; set; } = null!;

    /// <summary>
    /// Ledger account being affected
    /// </summary>
    public virtual LedgerAccount LedgerAccount { get; set; } = null!;
}
