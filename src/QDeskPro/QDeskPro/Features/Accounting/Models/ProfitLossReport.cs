namespace QDeskPro.Features.Accounting.Models;

/// <summary>
/// Statement of Comprehensive Income (Profit & Loss Statement) - shows revenue, expenses, and profit.
/// Prepared in accordance with IFRS for SMEs Third Edition (February 2025).
/// Revenue - Cost of Sales = Gross Profit
/// Gross Profit - Operating Expenses = Profit from Operations
/// Profit from Operations +/- Other Items = Profit for the Period
/// Profit for the Period +/- Other Comprehensive Income = Total Comprehensive Income
/// </summary>
/// <remarks>
/// IFRS for SMEs Section 5 specifies presentation requirements for the income statement.
/// This report presents expenses by function (Cost of Sales, Operating Expenses).
/// </remarks>
public class ProfitLossReport
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
    /// IFRS-compliant report title.
    /// </summary>
    public string ReportTitle => "Statement of Comprehensive Income";

    /// <summary>
    /// Alternative title per IFRS for SMEs (can use either).
    /// </summary>
    public string AlternativeTitle => "Statement of Profit or Loss and Other Comprehensive Income";

    /// <summary>
    /// Accounting standard reference.
    /// </summary>
    public string AccountingStandard => "IFRS for SMEs Third Edition (2025)";

    // ===== COMPARATIVE PERIOD SUPPORT (IFRS Requirement) =====

    /// <summary>
    /// Comparative period start date (prior year).
    /// </summary>
    public DateTime? ComparativePeriodStart { get; set; }

    /// <summary>
    /// Comparative period end date (prior year).
    /// </summary>
    public DateTime? ComparativePeriodEnd { get; set; }

    /// <summary>
    /// Whether comparative data is included in this report.
    /// </summary>
    public bool HasComparativeData => ComparativePeriodStart.HasValue;

    // ===== REVENUE SECTION =====

    /// <summary>
    /// Revenue breakdown by product/category.
    /// </summary>
    public List<ProfitLossLineItem> RevenueItems { get; set; } = new();

    /// <summary>
    /// Total revenue from all sources.
    /// </summary>
    public double TotalRevenue => RevenueItems.Sum(r => r.Amount);

    // ===== COST OF SALES SECTION =====

    /// <summary>
    /// Cost of Sales breakdown (Commission, Loaders Fees, Land Rate).
    /// </summary>
    public List<ProfitLossLineItem> CostOfSalesItems { get; set; } = new();

    /// <summary>
    /// Total cost of sales.
    /// </summary>
    public double TotalCostOfSales => CostOfSalesItems.Sum(c => c.Amount);

    /// <summary>
    /// Gross Profit = Revenue - Cost of Sales.
    /// </summary>
    public double GrossProfit => TotalRevenue - TotalCostOfSales;

    /// <summary>
    /// Gross Profit Margin = (Gross Profit / Revenue) * 100.
    /// </summary>
    public double GrossProfitMargin => TotalRevenue > 0 ? (GrossProfit / TotalRevenue) * 100 : 0;

    // ===== OPERATING EXPENSES SECTION =====

    /// <summary>
    /// Operating expenses breakdown by category.
    /// </summary>
    public List<ProfitLossLineItem> OperatingExpenses { get; set; } = new();

    /// <summary>
    /// Total operating expenses.
    /// </summary>
    public double TotalOperatingExpenses => OperatingExpenses.Sum(e => e.Amount);

    // ===== PROFIT CALCULATIONS =====

    /// <summary>
    /// Operating Profit = Gross Profit - Operating Expenses.
    /// </summary>
    public double OperatingProfit => GrossProfit - TotalOperatingExpenses;

    /// <summary>
    /// Operating Profit Margin = (Operating Profit / Revenue) * 100.
    /// </summary>
    public double OperatingProfitMargin => TotalRevenue > 0 ? (OperatingProfit / TotalRevenue) * 100 : 0;

    /// <summary>
    /// Profit for the Period (previously called Net Profit).
    /// IFRS for SMEs terminology.
    /// </summary>
    public double ProfitForThePeriod => OperatingProfit;

    /// <summary>
    /// Net Profit (alias for backward compatibility).
    /// </summary>
    public double NetProfit => ProfitForThePeriod;

    /// <summary>
    /// Net Profit Margin = (Profit for the Period / Revenue) * 100.
    /// </summary>
    public double NetProfitMargin => TotalRevenue > 0 ? (ProfitForThePeriod / TotalRevenue) * 100 : 0;

    /// <summary>
    /// Total Expenses = Cost of Sales + Operating Expenses.
    /// </summary>
    public double TotalExpenses => TotalCostOfSales + TotalOperatingExpenses;

    // ===== OTHER COMPREHENSIVE INCOME (IFRS for SMEs Requirement) =====

    /// <summary>
    /// Other Comprehensive Income items.
    /// Per IFRS for SMEs, this includes items that will not be reclassified to profit or loss.
    /// For most SMEs, this section is often empty.
    /// </summary>
    public List<ProfitLossLineItem> OtherComprehensiveIncome { get; set; } = new();

    /// <summary>
    /// Total Other Comprehensive Income.
    /// </summary>
    public double TotalOtherComprehensiveIncome => OtherComprehensiveIncome.Sum(o => o.Amount);

    /// <summary>
    /// Total Comprehensive Income for the Period = Profit for the Period + Other Comprehensive Income.
    /// This is the bottom line per IFRS for SMEs Section 5.
    /// </summary>
    public double TotalComprehensiveIncome => ProfitForThePeriod + TotalOtherComprehensiveIncome;

    // ===== COMPARATIVE AMOUNTS (Prior Period) =====

    /// <summary>
    /// Comparative Revenue Items (prior period).
    /// </summary>
    public List<ProfitLossLineItem> ComparativeRevenueItems { get; set; } = new();

    /// <summary>
    /// Comparative Cost of Sales Items (prior period).
    /// </summary>
    public List<ProfitLossLineItem> ComparativeCostOfSalesItems { get; set; } = new();

    /// <summary>
    /// Comparative Operating Expenses (prior period).
    /// </summary>
    public List<ProfitLossLineItem> ComparativeOperatingExpenses { get; set; } = new();

    /// <summary>
    /// Comparative Other Comprehensive Income (prior period).
    /// </summary>
    public List<ProfitLossLineItem> ComparativeOtherComprehensiveIncome { get; set; } = new();

    /// <summary>
    /// Total Comparative Revenue.
    /// </summary>
    public double TotalComparativeRevenue => ComparativeRevenueItems.Sum(r => r.Amount);

    /// <summary>
    /// Total Comparative Cost of Sales.
    /// </summary>
    public double TotalComparativeCostOfSales => ComparativeCostOfSalesItems.Sum(c => c.Amount);

    /// <summary>
    /// Comparative Gross Profit.
    /// </summary>
    public double ComparativeGrossProfit => TotalComparativeRevenue - TotalComparativeCostOfSales;

    /// <summary>
    /// Total Comparative Operating Expenses.
    /// </summary>
    public double TotalComparativeOperatingExpenses => ComparativeOperatingExpenses.Sum(e => e.Amount);

    /// <summary>
    /// Comparative Profit for the Period.
    /// </summary>
    public double ComparativeProfitForThePeriod => ComparativeGrossProfit - TotalComparativeOperatingExpenses;

    /// <summary>
    /// Comparative Total Comprehensive Income.
    /// </summary>
    public double ComparativeTotalComprehensiveIncome => ComparativeProfitForThePeriod + ComparativeOtherComprehensiveIncome.Sum(o => o.Amount);
}

/// <summary>
/// Individual line item in the Statement of Comprehensive Income.
/// Per IFRS for SMEs Section 5, each material class of similar items should be presented separately.
/// </summary>
public class ProfitLossLineItem
{
    /// <summary>
    /// Account code for reference.
    /// </summary>
    public string AccountCode { get; set; } = string.Empty;

    /// <summary>
    /// Display name (e.g., "Sales - Size 6", "Commission Expense").
    /// Uses IFRS-compliant terminology where applicable.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Amount for this line item (current period).
    /// </summary>
    public double Amount { get; set; }

    /// <summary>
    /// Comparative amount (prior period) for IFRS compliance.
    /// IFRS for SMEs requires comparative information for the preceding period.
    /// </summary>
    public double ComparativeAmount { get; set; }

    /// <summary>
    /// Percentage of total (for the section).
    /// </summary>
    public double Percentage { get; set; }

    /// <summary>
    /// Notes reference for disclosure cross-referencing.
    /// Per IFRS for SMEs, significant items should be cross-referenced to notes.
    /// </summary>
    public string? NotesReference { get; set; }
}
