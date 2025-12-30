using QDeskPro.Domain.Enums;

namespace QDeskPro.Features.Accounting.Models;

/// <summary>
/// General Ledger Report - detailed transaction listing for a specific account.
/// Shows all debits and credits with running balance.
/// </summary>
public class GeneralLedgerReport
{
    /// <summary>
    /// The quarry this report is generated for.
    /// </summary>
    public string QuarryId { get; set; } = string.Empty;

    /// <summary>
    /// Quarry name for display.
    /// </summary>
    public string QuarryName { get; set; } = string.Empty;

    /// <summary>
    /// Account ID being reported on.
    /// </summary>
    public string AccountId { get; set; } = string.Empty;

    /// <summary>
    /// Account code.
    /// </summary>
    public string AccountCode { get; set; } = string.Empty;

    /// <summary>
    /// Account name.
    /// </summary>
    public string AccountName { get; set; } = string.Empty;

    /// <summary>
    /// Account category.
    /// </summary>
    public AccountCategory Category { get; set; }

    /// <summary>
    /// Start date of the reporting period.
    /// </summary>
    public DateTime PeriodStart { get; set; }

    /// <summary>
    /// End date of the reporting period.
    /// </summary>
    public DateTime PeriodEnd { get; set; }

    /// <summary>
    /// Report generation timestamp.
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Opening balance at the start of the period.
    /// </summary>
    public double OpeningBalance { get; set; }

    /// <summary>
    /// Individual transaction entries.
    /// </summary>
    public List<GeneralLedgerEntry> Entries { get; set; } = new();

    /// <summary>
    /// Total debits for the period.
    /// </summary>
    public double TotalDebits => Entries.Sum(e => e.DebitAmount);

    /// <summary>
    /// Total credits for the period.
    /// </summary>
    public double TotalCredits => Entries.Sum(e => e.CreditAmount);

    /// <summary>
    /// Net activity for the period.
    /// </summary>
    public double NetActivity => TotalDebits - TotalCredits;

    /// <summary>
    /// Closing balance at the end of the period.
    /// </summary>
    public double ClosingBalance => OpeningBalance + NetActivity;
}

/// <summary>
/// Individual entry in the General Ledger report.
/// </summary>
public class GeneralLedgerEntry
{
    /// <summary>
    /// Transaction date.
    /// </summary>
    public DateTime EntryDate { get; set; }

    /// <summary>
    /// Journal entry reference (e.g., "JE-2025-001").
    /// </summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>
    /// Transaction description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Source entity type (e.g., "Sale", "Expense").
    /// </summary>
    public string? SourceType { get; set; }

    /// <summary>
    /// Source entity ID for drill-down.
    /// </summary>
    public string? SourceId { get; set; }

    /// <summary>
    /// Debit amount.
    /// </summary>
    public double DebitAmount { get; set; }

    /// <summary>
    /// Credit amount.
    /// </summary>
    public double CreditAmount { get; set; }

    /// <summary>
    /// Running balance after this entry.
    /// </summary>
    public double RunningBalance { get; set; }

    /// <summary>
    /// Whether this entry is posted.
    /// </summary>
    public bool IsPosted { get; set; }

    /// <summary>
    /// User who created the entry.
    /// </summary>
    public string? CreatedBy { get; set; }
}
