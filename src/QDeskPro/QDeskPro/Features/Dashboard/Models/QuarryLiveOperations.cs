namespace QDeskPro.Features.Dashboard.Models;

/// <summary>
/// Represents live operations metrics for a single quarry (today's data only)
/// </summary>
public class QuarryLiveOperations
{
    // ==========================================
    // Quarry Information
    // ==========================================

    public string QuarryId { get; set; } = string.Empty;
    public string QuarryName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;

    // ==========================================
    // Priority Metrics (Top Display)
    // ==========================================

    /// <summary>
    /// Total sales revenue generated today
    /// </summary>
    public double TotalSales { get; set; }

    /// <summary>
    /// Number of sales transactions today
    /// </summary>
    public int SalesCount { get; set; }

    /// <summary>
    /// Estimated cash in hand = OpeningBalance + Revenue - Expenses - Banking
    /// </summary>
    public double EstimatedCashInHand { get; set; }

    // ==========================================
    // Sales Metrics
    // ==========================================

    /// <summary>
    /// Total quantity (pieces) sold today
    /// </summary>
    public double TotalQuantity { get; set; }

    // ==========================================
    // Expense Breakdown (4-Source System)
    // ==========================================

    /// <summary>
    /// Total expenses = Manual + Commission + Loaders Fee + Land Rate
    /// </summary>
    public double TotalExpenses { get; set; }

    /// <summary>
    /// Commission expenses (from sales with commission)
    /// </summary>
    public double CommissionExpenses { get; set; }

    /// <summary>
    /// Loaders fee expenses (calculated from quarry settings)
    /// </summary>
    public double LoadersFeeExpenses { get; set; }

    /// <summary>
    /// Land rate expenses (with special handling for Reject products)
    /// </summary>
    public double LandRateExpenses { get; set; }

    /// <summary>
    /// User-entered manual expenses
    /// </summary>
    public double ManualExpenses { get; set; }

    // ==========================================
    // Fuel Tracking
    // ==========================================

    /// <summary>
    /// Total fuel consumed today (Machines + Wheel Loaders)
    /// </summary>
    public double FuelConsumed { get; set; }

    // ==========================================
    // Collections (Past Unpaid Orders Paid Today)
    // ==========================================

    /// <summary>
    /// Number of past unpaid orders paid today
    /// </summary>
    public int CollectionsCount { get; set; }

    /// <summary>
    /// Total amount collected today from past orders
    /// </summary>
    public double TotalCollections { get; set; }

    // ==========================================
    // Prepayments (Customer Deposits)
    // ==========================================

    /// <summary>
    /// Number of prepayments received today
    /// </summary>
    public int PrepaymentsCount { get; set; }

    /// <summary>
    /// Total prepayment amounts received today
    /// </summary>
    public double TotalPrepayments { get; set; }

    // ==========================================
    // Banking
    // ==========================================

    /// <summary>
    /// Number of banking transactions today
    /// </summary>
    public int BankingCount { get; set; }

    /// <summary>
    /// Total amount banked/deposited today
    /// </summary>
    public double TotalBanked { get; set; }

    // ==========================================
    // Unpaid Orders
    // ==========================================

    /// <summary>
    /// Number of unpaid orders today
    /// </summary>
    public int UnpaidCount { get; set; }

    /// <summary>
    /// Total amount of unpaid orders today
    /// </summary>
    public double UnpaidAmount { get; set; }

    // ==========================================
    // Cash Flow Tracking
    // ==========================================

    /// <summary>
    /// Opening balance (yesterday's closing balance)
    /// </summary>
    public double OpeningBalance { get; set; }

    // ==========================================
    // Activity Tracking
    // ==========================================

    /// <summary>
    /// Timestamp of the most recent transaction
    /// </summary>
    public DateTime? LastActivityTime { get; set; }

    /// <summary>
    /// List of clerk names who posted data today
    /// </summary>
    public List<string> ActiveClerks { get; set; } = new();

    /// <summary>
    /// Total number of activities today (Sales + Expenses + Banking + Fuel entries)
    /// </summary>
    public int TotalActivities { get; set; }

    // ==========================================
    // Status Indicators
    // ==========================================

    /// <summary>
    /// Indicates if quarry has any activity today
    /// </summary>
    public bool HasActivity => TotalActivities > 0;

    /// <summary>
    /// Performance indicator: Green (>50k), Orange (10k-50k), Red (<10k), Grey (no activity)
    /// </summary>
    public string SalesPerformanceLevel
    {
        get
        {
            if (!HasActivity) return "none";
            if (TotalSales > 50000) return "excellent";
            if (TotalSales >= 10000) return "good";
            return "needs-attention";
        }
    }

    /// <summary>
    /// Cash in hand status: Positive (green) or Negative (red)
    /// </summary>
    public string CashStatus => EstimatedCashInHand >= 0 ? "positive" : "negative";

    /// <summary>
    /// Unpaid orders status: None (green), Few (orange), Many (red)
    /// </summary>
    public string UnpaidStatus
    {
        get
        {
            if (UnpaidCount == 0) return "good";
            if (UnpaidCount <= 3) return "warning";
            return "alert";
        }
    }
}
