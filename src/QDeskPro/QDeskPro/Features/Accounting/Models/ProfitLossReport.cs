namespace QDeskPro.Features.Accounting.Models;

/// <summary>
/// Profit & Loss Statement (Income Statement) - shows revenue, expenses, and profit.
/// Revenue - Cost of Sales = Gross Profit
/// Gross Profit - Operating Expenses = Net Profit
/// </summary>
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
    /// Net Profit (same as Operating Profit for now, as no other income/expenses).
    /// </summary>
    public double NetProfit => OperatingProfit;

    /// <summary>
    /// Net Profit Margin = (Net Profit / Revenue) * 100.
    /// </summary>
    public double NetProfitMargin => TotalRevenue > 0 ? (NetProfit / TotalRevenue) * 100 : 0;

    /// <summary>
    /// Total Expenses = Cost of Sales + Operating Expenses.
    /// </summary>
    public double TotalExpenses => TotalCostOfSales + TotalOperatingExpenses;
}

/// <summary>
/// Individual line item in the Profit & Loss report.
/// </summary>
public class ProfitLossLineItem
{
    /// <summary>
    /// Account code for reference.
    /// </summary>
    public string AccountCode { get; set; } = string.Empty;

    /// <summary>
    /// Display name (e.g., "Sales - Size 6", "Commission Expense").
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Amount for this line item.
    /// </summary>
    public double Amount { get; set; }

    /// <summary>
    /// Percentage of total (for the section).
    /// </summary>
    public double Percentage { get; set; }
}
