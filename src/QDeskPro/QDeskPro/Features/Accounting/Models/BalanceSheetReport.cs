namespace QDeskPro.Features.Accounting.Models;

/// <summary>
/// Balance Sheet Report - shows Assets, Liabilities, and Equity at a point in time.
/// Assets = Liabilities + Equity (the accounting equation).
/// </summary>
public class BalanceSheetReport
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
    /// As-of date for the balance sheet.
    /// </summary>
    public DateTime AsOfDate { get; set; }

    /// <summary>
    /// Report generation timestamp.
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    // ===== ASSETS =====

    /// <summary>
    /// Current Assets (Cash, Bank, Receivables, etc.).
    /// </summary>
    public List<BalanceSheetLineItem> CurrentAssets { get; set; } = new();

    /// <summary>
    /// Total Current Assets.
    /// </summary>
    public double TotalCurrentAssets => CurrentAssets.Sum(a => a.Amount);

    /// <summary>
    /// Non-Current Assets (Fixed Assets, Equipment, etc.).
    /// </summary>
    public List<BalanceSheetLineItem> NonCurrentAssets { get; set; } = new();

    /// <summary>
    /// Total Non-Current Assets.
    /// </summary>
    public double TotalNonCurrentAssets => NonCurrentAssets.Sum(a => a.Amount);

    /// <summary>
    /// Total Assets = Current + Non-Current.
    /// </summary>
    public double TotalAssets => TotalCurrentAssets + TotalNonCurrentAssets;

    // ===== LIABILITIES =====

    /// <summary>
    /// Current Liabilities (Customer Deposits, Accrued Expenses, etc.).
    /// </summary>
    public List<BalanceSheetLineItem> CurrentLiabilities { get; set; } = new();

    /// <summary>
    /// Total Current Liabilities.
    /// </summary>
    public double TotalCurrentLiabilities => CurrentLiabilities.Sum(l => l.Amount);

    /// <summary>
    /// Non-Current Liabilities (Long-term Loans, etc.).
    /// </summary>
    public List<BalanceSheetLineItem> NonCurrentLiabilities { get; set; } = new();

    /// <summary>
    /// Total Non-Current Liabilities.
    /// </summary>
    public double TotalNonCurrentLiabilities => NonCurrentLiabilities.Sum(l => l.Amount);

    /// <summary>
    /// Total Liabilities = Current + Non-Current.
    /// </summary>
    public double TotalLiabilities => TotalCurrentLiabilities + TotalNonCurrentLiabilities;

    // ===== EQUITY =====

    /// <summary>
    /// Equity items (Owner's Equity, Retained Earnings).
    /// </summary>
    public List<BalanceSheetLineItem> EquityItems { get; set; } = new();

    /// <summary>
    /// Current period profit/(loss) - from P&L.
    /// </summary>
    public double CurrentPeriodProfitLoss { get; set; }

    /// <summary>
    /// Total Equity = Equity Items + Current Period Profit/Loss.
    /// </summary>
    public double TotalEquity => EquityItems.Sum(e => e.Amount) + CurrentPeriodProfitLoss;

    // ===== VALIDATION =====

    /// <summary>
    /// Total Liabilities + Equity (should equal Total Assets).
    /// </summary>
    public double TotalLiabilitiesAndEquity => TotalLiabilities + TotalEquity;

    /// <summary>
    /// Whether the balance sheet is balanced (Assets = Liabilities + Equity).
    /// </summary>
    public bool IsBalanced => Math.Abs(TotalAssets - TotalLiabilitiesAndEquity) < 0.01;

    /// <summary>
    /// Difference between Assets and Liabilities+Equity (should be 0).
    /// </summary>
    public double Difference => TotalAssets - TotalLiabilitiesAndEquity;
}

/// <summary>
/// Individual line item in the Balance Sheet report.
/// </summary>
public class BalanceSheetLineItem
{
    /// <summary>
    /// Account code for reference.
    /// </summary>
    public string AccountCode { get; set; } = string.Empty;

    /// <summary>
    /// Display name (e.g., "Cash on Hand", "Customer Deposits").
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Amount for this line item.
    /// </summary>
    public double Amount { get; set; }
}
