namespace QDeskPro.Features.Accounting.Models;

/// <summary>
/// Accounts Payable Summary Report - shows amounts owed to brokers and accrued fees.
/// Helps track commissions and fees payable.
/// </summary>
public class APSummaryReport
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
    /// As-of date for the summary.
    /// </summary>
    public DateTime AsOfDate { get; set; }

    /// <summary>
    /// Report generation timestamp.
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    // ===== BROKER COMMISSIONS =====

    /// <summary>
    /// Broker commission details.
    /// </summary>
    public List<APBrokerPayable> BrokerPayables { get; set; } = new();

    /// <summary>
    /// Total broker commissions payable.
    /// </summary>
    public double TotalBrokerCommissions => BrokerPayables.Sum(b => b.AmountDue);

    // ===== ACCRUED FEES =====

    /// <summary>
    /// Accrued operating fees (Loaders, Land Rate).
    /// </summary>
    public List<APAccruedFee> AccruedFees { get; set; } = new();

    /// <summary>
    /// Total accrued fees.
    /// </summary>
    public double TotalAccruedFees => AccruedFees.Sum(f => f.AmountDue);

    /// <summary>
    /// Accrued loaders fees total.
    /// </summary>
    public double AccruedLoadersFees => AccruedFees.Where(f => f.FeeType == "Loaders Fee").Sum(f => f.AmountDue);

    /// <summary>
    /// Accrued land rate fees total.
    /// </summary>
    public double AccruedLandRateFees => AccruedFees.Where(f => f.FeeType == "Land Rate Fee").Sum(f => f.AmountDue);

    /// <summary>
    /// All commission details flattened for display.
    /// </summary>
    public List<APCommissionDetail> CommissionDetails => BrokerPayables
        .SelectMany(b => b.Sales.Select(s => new APCommissionDetail
        {
            SaleDate = s.SaleDate,
            VehicleRegistration = s.VehicleRegistration,
            BrokerName = b.BrokerName,
            ProductName = s.ProductName,
            Quantity = s.Quantity,
            CommissionPerUnit = s.CommissionPerUnit,
            TotalCommission = s.CommissionAmount
        }))
        .OrderByDescending(c => c.SaleDate)
        .ToList();

    // ===== TOTALS =====

    /// <summary>
    /// Grand total of all accounts payable.
    /// </summary>
    public double TotalAccountsPayable => TotalBrokerCommissions + TotalAccruedFees;

    /// <summary>
    /// Number of brokers with outstanding commissions.
    /// </summary>
    public int BrokerCount => BrokerPayables.Count;
}

/// <summary>
/// Broker commission payable details.
/// </summary>
public class APBrokerPayable
{
    /// <summary>
    /// Broker ID.
    /// </summary>
    public string BrokerId { get; set; } = string.Empty;

    /// <summary>
    /// Broker name.
    /// </summary>
    public string BrokerName { get; set; } = string.Empty;

    /// <summary>
    /// Broker phone number.
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Period for which commission is owed (e.g., "December 2025").
    /// </summary>
    public string Period { get; set; } = string.Empty;

    /// <summary>
    /// Start date of the period.
    /// </summary>
    public DateTime PeriodStart { get; set; }

    /// <summary>
    /// End date of the period.
    /// </summary>
    public DateTime PeriodEnd { get; set; }

    /// <summary>
    /// Number of sales with commission.
    /// </summary>
    public int SalesCount { get; set; }

    /// <summary>
    /// Total quantity sold through this broker.
    /// </summary>
    public double TotalQuantity { get; set; }

    /// <summary>
    /// Total commission amount due.
    /// </summary>
    public double AmountDue { get; set; }

    /// <summary>
    /// Alias for AmountDue for display.
    /// </summary>
    public double CommissionAmount => AmountDue;

    /// <summary>
    /// Individual sales with commission for drill-down.
    /// </summary>
    public List<APCommissionSale> Sales { get; set; } = new();
}

/// <summary>
/// Individual sale with commission details.
/// </summary>
public class APCommissionSale
{
    /// <summary>
    /// Sale ID.
    /// </summary>
    public string SaleId { get; set; } = string.Empty;

    /// <summary>
    /// Sale date.
    /// </summary>
    public DateTime SaleDate { get; set; }

    /// <summary>
    /// Vehicle registration.
    /// </summary>
    public string VehicleRegistration { get; set; } = string.Empty;

    /// <summary>
    /// Product sold.
    /// </summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// Quantity sold.
    /// </summary>
    public double Quantity { get; set; }

    /// <summary>
    /// Commission per unit.
    /// </summary>
    public double CommissionPerUnit { get; set; }

    /// <summary>
    /// Total commission for this sale.
    /// </summary>
    public double CommissionAmount { get; set; }
}

/// <summary>
/// Accrued fee details (Loaders Fees, Land Rate Fees).
/// </summary>
public class APAccruedFee
{
    /// <summary>
    /// Fee type (e.g., "Loaders Fee", "Land Rate Fee").
    /// </summary>
    public string FeeType { get; set; } = string.Empty;

    /// <summary>
    /// Description of the fee.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Period for which fee is owed.
    /// </summary>
    public string Period { get; set; } = string.Empty;

    /// <summary>
    /// Start date of the period.
    /// </summary>
    public DateTime PeriodStart { get; set; }

    /// <summary>
    /// End date of the period.
    /// </summary>
    public DateTime PeriodEnd { get; set; }

    /// <summary>
    /// Number of sales contributing to this fee.
    /// </summary>
    public int SalesCount { get; set; }

    /// <summary>
    /// Total quantity sold.
    /// </summary>
    public double TotalQuantity { get; set; }

    /// <summary>
    /// Fee rate per unit.
    /// </summary>
    public double FeePerUnit { get; set; }

    /// <summary>
    /// Alias for FeePerUnit for display.
    /// </summary>
    public double Rate => FeePerUnit;

    /// <summary>
    /// Total fee amount due.
    /// </summary>
    public double AmountDue { get; set; }

    /// <summary>
    /// Alias for AmountDue for display.
    /// </summary>
    public double Amount => AmountDue;
}

/// <summary>
/// Commission detail for display in flattened list.
/// </summary>
public class APCommissionDetail
{
    /// <summary>
    /// Sale date.
    /// </summary>
    public DateTime SaleDate { get; set; }

    /// <summary>
    /// Vehicle registration.
    /// </summary>
    public string VehicleRegistration { get; set; } = string.Empty;

    /// <summary>
    /// Broker name.
    /// </summary>
    public string BrokerName { get; set; } = string.Empty;

    /// <summary>
    /// Product name.
    /// </summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// Quantity sold.
    /// </summary>
    public double Quantity { get; set; }

    /// <summary>
    /// Commission per unit.
    /// </summary>
    public double CommissionPerUnit { get; set; }

    /// <summary>
    /// Total commission.
    /// </summary>
    public double TotalCommission { get; set; }
}
