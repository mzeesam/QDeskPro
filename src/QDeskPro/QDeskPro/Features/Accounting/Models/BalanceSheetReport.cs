namespace QDeskPro.Features.Accounting.Models;

/// <summary>
/// Statement of Financial Position (Balance Sheet) - shows Assets, Liabilities, and Equity at a point in time.
/// Prepared in accordance with IFRS for SMEs Third Edition (February 2025).
/// Assets = Liabilities + Equity (the accounting equation).
/// </summary>
/// <remarks>
/// IFRS for SMEs Section 4.2 specifies minimum line items to be presented.
/// This report follows the current/non-current classification approach.
/// </remarks>
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
    /// As-of date for the statement of financial position.
    /// </summary>
    public DateTime AsOfDate { get; set; }

    /// <summary>
    /// Report generation timestamp.
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// IFRS-compliant report title.
    /// </summary>
    public string ReportTitle => "Statement of Financial Position";

    /// <summary>
    /// Accounting standard reference.
    /// </summary>
    public string AccountingStandard => "IFRS for SMEs Third Edition (2025)";

    // ===== COMPARATIVE PERIOD SUPPORT (IFRS Requirement) =====

    /// <summary>
    /// Comparative period date (prior year same date).
    /// IFRS for SMEs requires comparative information for the preceding period.
    /// </summary>
    public DateTime? ComparativeDate { get; set; }

    /// <summary>
    /// Whether comparative data is included in this report.
    /// </summary>
    public bool HasComparativeData => ComparativeDate.HasValue;

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

    // ===== COMPARATIVE ASSETS (Prior Period) =====

    /// <summary>
    /// Comparative Current Assets (prior period).
    /// </summary>
    public List<BalanceSheetLineItem> ComparativeCurrentAssets { get; set; } = new();

    /// <summary>
    /// Comparative Non-Current Assets (prior period).
    /// </summary>
    public List<BalanceSheetLineItem> ComparativeNonCurrentAssets { get; set; } = new();

    /// <summary>
    /// Total Comparative Current Assets.
    /// </summary>
    public double TotalComparativeCurrentAssets => ComparativeCurrentAssets.Sum(a => a.Amount);

    /// <summary>
    /// Total Comparative Non-Current Assets.
    /// </summary>
    public double TotalComparativeNonCurrentAssets => ComparativeNonCurrentAssets.Sum(a => a.Amount);

    /// <summary>
    /// Total Comparative Assets.
    /// </summary>
    public double TotalComparativeAssets => TotalComparativeCurrentAssets + TotalComparativeNonCurrentAssets;

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

    // ===== COMPARATIVE LIABILITIES (Prior Period) =====

    /// <summary>
    /// Comparative Current Liabilities (prior period).
    /// </summary>
    public List<BalanceSheetLineItem> ComparativeCurrentLiabilities { get; set; } = new();

    /// <summary>
    /// Comparative Non-Current Liabilities (prior period).
    /// </summary>
    public List<BalanceSheetLineItem> ComparativeNonCurrentLiabilities { get; set; } = new();

    /// <summary>
    /// Total Comparative Current Liabilities.
    /// </summary>
    public double TotalComparativeCurrentLiabilities => ComparativeCurrentLiabilities.Sum(l => l.Amount);

    /// <summary>
    /// Total Comparative Non-Current Liabilities.
    /// </summary>
    public double TotalComparativeNonCurrentLiabilities => ComparativeNonCurrentLiabilities.Sum(l => l.Amount);

    /// <summary>
    /// Total Comparative Liabilities.
    /// </summary>
    public double TotalComparativeLiabilities => TotalComparativeCurrentLiabilities + TotalComparativeNonCurrentLiabilities;

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

    // ===== COMPARATIVE EQUITY (Prior Period) =====

    /// <summary>
    /// Comparative Equity items (prior period).
    /// </summary>
    public List<BalanceSheetLineItem> ComparativeEquityItems { get; set; } = new();

    /// <summary>
    /// Comparative period profit/(loss).
    /// </summary>
    public double ComparativePeriodProfitLoss { get; set; }

    /// <summary>
    /// Total Comparative Equity.
    /// </summary>
    public double TotalComparativeEquity => ComparativeEquityItems.Sum(e => e.Amount) + ComparativePeriodProfitLoss;

    /// <summary>
    /// Total Comparative Liabilities and Equity.
    /// </summary>
    public double TotalComparativeLiabilitiesAndEquity => TotalComparativeLiabilities + TotalComparativeEquity;

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
/// Individual line item in the Statement of Financial Position.
/// Per IFRS for SMEs Section 4.2, each material class of similar items should be presented separately.
/// </summary>
public class BalanceSheetLineItem
{
    /// <summary>
    /// Account code for reference.
    /// </summary>
    public string AccountCode { get; set; } = string.Empty;

    /// <summary>
    /// Display name (e.g., "Cash and Cash Equivalents", "Trade and Other Payables").
    /// Uses IFRS-compliant terminology.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Amount for this line item (current period).
    /// </summary>
    public double Amount { get; set; }

    /// <summary>
    /// Comparative amount (prior period) for IFRS compliance.
    /// </summary>
    public double ComparativeAmount { get; set; }

    /// <summary>
    /// Notes reference for disclosure cross-referencing.
    /// </summary>
    public string? NotesReference { get; set; }
}
