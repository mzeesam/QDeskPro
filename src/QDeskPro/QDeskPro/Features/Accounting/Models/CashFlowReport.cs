namespace QDeskPro.Features.Accounting.Models;

/// <summary>
/// Cash Flow Statement - shows cash inflows and outflows for a period.
/// Categorized into Operating, Investing, and Financing activities.
/// </summary>
public class CashFlowReport
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

    // ===== OPENING BALANCE =====

    /// <summary>
    /// Cash balance at the beginning of the period.
    /// </summary>
    public double OpeningCashBalance { get; set; }

    // ===== OPERATING ACTIVITIES =====

    /// <summary>
    /// Cash inflows from operating activities.
    /// </summary>
    public List<CashFlowLineItem> OperatingInflows { get; set; } = new();

    /// <summary>
    /// Total cash received from operating activities.
    /// </summary>
    public double TotalOperatingInflows => OperatingInflows.Sum(i => i.Amount);

    /// <summary>
    /// Cash outflows from operating activities.
    /// </summary>
    public List<CashFlowLineItem> OperatingOutflows { get; set; } = new();

    /// <summary>
    /// Total cash paid for operating activities.
    /// </summary>
    public double TotalOperatingOutflows => OperatingOutflows.Sum(o => o.Amount);

    /// <summary>
    /// Net cash from operating activities.
    /// </summary>
    public double NetCashFromOperations => TotalOperatingInflows - TotalOperatingOutflows;

    // ===== INVESTING ACTIVITIES =====

    /// <summary>
    /// Cash flows from investing activities (equipment purchases, etc.).
    /// </summary>
    public List<CashFlowLineItem> InvestingActivities { get; set; } = new();

    /// <summary>
    /// Net cash from investing activities.
    /// </summary>
    public double NetCashFromInvesting => InvestingActivities.Sum(i => i.Amount);

    // ===== FINANCING ACTIVITIES =====

    /// <summary>
    /// Cash flows from financing activities (loans, capital injections).
    /// </summary>
    public List<CashFlowLineItem> FinancingActivities { get; set; } = new();

    /// <summary>
    /// Net cash from financing activities.
    /// </summary>
    public double NetCashFromFinancing => FinancingActivities.Sum(f => f.Amount);

    // ===== NET CHANGE =====

    /// <summary>
    /// Net change in cash = Operating + Investing + Financing.
    /// </summary>
    public double NetCashChange => NetCashFromOperations + NetCashFromInvesting + NetCashFromFinancing;

    /// <summary>
    /// Closing cash balance = Opening + Net Change.
    /// </summary>
    public double ClosingCashBalance => OpeningCashBalance + NetCashChange;

    // ===== SUMMARY BREAKDOWN =====

    /// <summary>
    /// Cash received from direct sales (paid at time of sale).
    /// </summary>
    public double CashFromDirectSales { get; set; }

    /// <summary>
    /// Cash received from prepayments.
    /// </summary>
    public double CashFromPrepayments { get; set; }

    /// <summary>
    /// Cash received from collections (past unpaid sales).
    /// </summary>
    public double CashFromCollections { get; set; }

    /// <summary>
    /// Cash deposited to bank.
    /// </summary>
    public double CashBanked { get; set; }
}

/// <summary>
/// Individual line item in the Cash Flow report.
/// </summary>
public class CashFlowLineItem
{
    /// <summary>
    /// Display name (e.g., "Cash received from customers").
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Amount (positive for inflows, could be negative for outflows shown as positive).
    /// </summary>
    public double Amount { get; set; }

    /// <summary>
    /// Additional notes or reference.
    /// </summary>
    public string? Notes { get; set; }
}
