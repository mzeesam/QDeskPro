namespace QDeskPro.Domain.Entities;

/// <summary>
/// Represents a generated report that can be saved and retrieved later.
/// Enables persistent report history functionality.
/// </summary>
public class GeneratedReport : BaseEntity
{
    /// <summary>
    /// Display name for the report (e.g., "Sales Report - Jan 2026")
    /// </summary>
    public string ReportName { get; set; } = string.Empty;

    /// <summary>
    /// Type of report: "Sales", "DailySummary", "Expenses", "FuelUsage", etc.
    /// </summary>
    public string ReportType { get; set; } = string.Empty;

    /// <summary>
    /// Start date of the reporting period
    /// </summary>
    public DateTime FromDate { get; set; }

    /// <summary>
    /// End date of the reporting period
    /// </summary>
    public DateTime ToDate { get; set; }

    /// <summary>
    /// Quarry this report was generated for
    /// </summary>
    public string QuarryId { get; set; } = string.Empty;

    /// <summary>
    /// Optional clerk filter - if report was filtered by specific clerk
    /// </summary>
    public string? ClerkId { get; set; }

    /// <summary>
    /// Clerk name for display (when filtered by clerk)
    /// </summary>
    public string? ClerkName { get; set; }

    /// <summary>
    /// JSON serialized report data for caching/quick retrieval
    /// </summary>
    public string? ReportDataJson { get; set; }

    /// <summary>
    /// Total sales amount in the report (for quick display in history)
    /// </summary>
    public double? TotalSales { get; set; }

    /// <summary>
    /// Total expenses amount in the report (for quick display in history)
    /// </summary>
    public double? TotalExpenses { get; set; }

    /// <summary>
    /// Number of orders in the report (for quick display in history)
    /// </summary>
    public int? OrderCount { get; set; }

    /// <summary>
    /// Total quantity sold in the report
    /// </summary>
    public double? TotalQuantity { get; set; }

    /// <summary>
    /// Net profit/earnings from the report
    /// </summary>
    public double? NetEarnings { get; set; }

    /// <summary>
    /// User ID who generated this report
    /// </summary>
    public string GeneratedByUserId { get; set; } = string.Empty;

    /// <summary>
    /// Denormalized name of the user who generated this report
    /// </summary>
    public string GeneratedByName { get; set; } = string.Empty;

    /// <summary>
    /// Path to exported file (if report was exported to PDF/Excel)
    /// </summary>
    public string? ExportedFilePath { get; set; }

    /// <summary>
    /// Format of exported file: "PDF", "Excel", or null if not exported
    /// </summary>
    public string? ExportFormat { get; set; }

    // Navigation properties

    /// <summary>
    /// The quarry this report was generated for
    /// </summary>
    public virtual Quarry? Quarry { get; set; }

    /// <summary>
    /// The user who generated this report
    /// </summary>
    public virtual ApplicationUser? GeneratedBy { get; set; }
}
