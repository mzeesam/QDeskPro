namespace QDeskPro.Domain.Entities;

/// <summary>
/// Represents an accounting period (monthly) for financial reporting and period closing.
/// </summary>
public class AccountingPeriod : BaseEntity
{
    /// <summary>
    /// Display name of the period (e.g., "January 2025", "Q1 2025")
    /// </summary>
    public string PeriodName { get; set; } = string.Empty;

    /// <summary>
    /// Start date of the period (inclusive)
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// End date of the period (inclusive)
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Whether the period is closed (closed periods are locked for editing)
    /// </summary>
    public bool IsClosed { get; set; }

    /// <summary>
    /// User ID who closed the period
    /// </summary>
    public string? ClosedBy { get; set; }

    /// <summary>
    /// Date when the period was closed
    /// </summary>
    public DateTime? ClosedDate { get; set; }

    /// <summary>
    /// Fiscal year (e.g., 2025)
    /// </summary>
    public int FiscalYear { get; set; }

    /// <summary>
    /// Period number within the fiscal year (1-12 for monthly)
    /// </summary>
    public int PeriodNumber { get; set; }

    /// <summary>
    /// Period type: "Monthly", "Quarterly", "Annual"
    /// </summary>
    public string PeriodType { get; set; } = "Monthly";

    /// <summary>
    /// Notes about the period closing
    /// </summary>
    public string? ClosingNotes { get; set; }
}
