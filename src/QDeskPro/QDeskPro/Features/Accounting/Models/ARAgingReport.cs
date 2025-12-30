namespace QDeskPro.Features.Accounting.Models;

/// <summary>
/// Accounts Receivable Aging Report - shows unpaid customer sales by age buckets.
/// Helps track overdue payments and collection efforts.
/// </summary>
public class ARAgingReport
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
    /// As-of date for the aging calculation.
    /// </summary>
    public DateTime AsOfDate { get; set; }

    /// <summary>
    /// Report generation timestamp.
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Individual customer aging details.
    /// </summary>
    public List<ARAgingCustomer> Customers { get; set; } = new();

    // ===== AGING BUCKET TOTALS =====

    /// <summary>
    /// Total amount in Current bucket (0 days).
    /// </summary>
    public double TotalCurrent => Customers.Sum(c => c.Current);

    /// <summary>
    /// Total amount in 1-30 days bucket.
    /// </summary>
    public double Total1To30Days => Customers.Sum(c => c.Days1To30);

    /// <summary>
    /// Total amount in 31-60 days bucket.
    /// </summary>
    public double Total31To60Days => Customers.Sum(c => c.Days31To60);

    /// <summary>
    /// Total amount in 61-90 days bucket.
    /// </summary>
    public double Total61To90Days => Customers.Sum(c => c.Days61To90);

    /// <summary>
    /// Total amount over 90 days.
    /// </summary>
    public double TotalOver90Days => Customers.Sum(c => c.Over90Days);

    /// <summary>
    /// Grand total of all outstanding receivables.
    /// </summary>
    public double TotalOutstanding => Customers.Sum(c => c.TotalOutstanding);

    // ===== PERCENTAGES =====

    /// <summary>
    /// Percentage of total in Current bucket.
    /// </summary>
    public double CurrentPercentage => TotalOutstanding > 0 ? (TotalCurrent / TotalOutstanding) * 100 : 0;

    /// <summary>
    /// Percentage of total in 1-30 days bucket.
    /// </summary>
    public double Days1To30Percentage => TotalOutstanding > 0 ? (Total1To30Days / TotalOutstanding) * 100 : 0;

    /// <summary>
    /// Percentage of total in 31-60 days bucket.
    /// </summary>
    public double Days31To60Percentage => TotalOutstanding > 0 ? (Total31To60Days / TotalOutstanding) * 100 : 0;

    /// <summary>
    /// Percentage of total in 61-90 days bucket.
    /// </summary>
    public double Days61To90Percentage => TotalOutstanding > 0 ? (Total61To90Days / TotalOutstanding) * 100 : 0;

    /// <summary>
    /// Percentage of total over 90 days.
    /// </summary>
    public double Over90DaysPercentage => TotalOutstanding > 0 ? (TotalOver90Days / TotalOutstanding) * 100 : 0;

    /// <summary>
    /// Alias for Over90DaysPercentage.
    /// </summary>
    public double Over90Percentage => Over90DaysPercentage;

    /// <summary>
    /// Flattened list of all invoices for display.
    /// </summary>
    public List<ARAgingInvoiceDisplay> Invoices => Customers
        .SelectMany(c => c.Invoices.Select(i => new ARAgingInvoiceDisplay
        {
            InvoiceDate = i.SaleDate,
            VehicleRegistration = c.VehicleRegistration,
            ProductName = i.ProductName,
            Quantity = i.Quantity,
            Amount = i.Amount,
            DaysOverdue = i.DaysOutstanding,
            AgeBucket = GetAgeBucket(i.DaysOutstanding)
        }))
        .OrderByDescending(i => i.DaysOverdue)
        .ToList();

    private static string GetAgeBucket(int days) => days switch
    {
        0 => "Current",
        <= 30 => "1-30 Days",
        <= 60 => "31-60 Days",
        <= 90 => "61-90 Days",
        _ => "90+ Days"
    };

    /// <summary>
    /// Number of customers with outstanding balances.
    /// </summary>
    public int CustomerCount => Customers.Count;

    /// <summary>
    /// Number of unique invoices/sales outstanding.
    /// </summary>
    public int InvoiceCount => Customers.Sum(c => c.InvoiceCount);
}

/// <summary>
/// Individual customer aging details.
/// </summary>
public class ARAgingCustomer
{
    /// <summary>
    /// Customer identifier (Vehicle Registration is primary).
    /// </summary>
    public string CustomerId { get; set; } = string.Empty;

    /// <summary>
    /// Vehicle registration number.
    /// </summary>
    public string VehicleRegistration { get; set; } = string.Empty;

    /// <summary>
    /// Client name if available.
    /// </summary>
    public string? ClientName { get; set; }

    /// <summary>
    /// Client phone if available.
    /// </summary>
    public string? ClientPhone { get; set; }

    /// <summary>
    /// Number of unpaid invoices/sales.
    /// </summary>
    public int InvoiceCount { get; set; }

    // ===== AGING BUCKETS =====

    /// <summary>
    /// Amount due today (0 days old).
    /// </summary>
    public double Current { get; set; }

    /// <summary>
    /// Amount 1-30 days overdue.
    /// </summary>
    public double Days1To30 { get; set; }

    /// <summary>
    /// Amount 31-60 days overdue.
    /// </summary>
    public double Days31To60 { get; set; }

    /// <summary>
    /// Amount 61-90 days overdue.
    /// </summary>
    public double Days61To90 { get; set; }

    /// <summary>
    /// Amount over 90 days overdue.
    /// </summary>
    public double Over90Days { get; set; }

    /// <summary>
    /// Total outstanding for this customer.
    /// </summary>
    public double TotalOutstanding => Current + Days1To30 + Days31To60 + Days61To90 + Over90Days;

    // ===== ALIASES FOR PAGE COMPATIBILITY =====
    public double CurrentAmount => Current;
    public double Days1To30Amount => Days1To30;
    public double Days31To60Amount => Days31To60;
    public double Days61To90Amount => Days61To90;
    public double Over90DaysAmount => Over90Days;
    public double TotalAmount => TotalOutstanding;

    /// <summary>
    /// Oldest invoice date for this customer.
    /// </summary>
    public DateTime? OldestInvoiceDate { get; set; }

    /// <summary>
    /// Days since oldest unpaid invoice.
    /// </summary>
    public int DaysSinceOldest { get; set; }

    /// <summary>
    /// Individual unpaid invoices for drill-down.
    /// </summary>
    public List<ARAgingInvoice> Invoices { get; set; } = new();
}

/// <summary>
/// Individual unpaid invoice/sale details.
/// </summary>
public class ARAgingInvoice
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
    /// Days since sale date.
    /// </summary>
    public int DaysOutstanding { get; set; }

    /// <summary>
    /// Product sold.
    /// </summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// Quantity sold.
    /// </summary>
    public double Quantity { get; set; }

    /// <summary>
    /// Amount owed.
    /// </summary>
    public double Amount { get; set; }

    /// <summary>
    /// Clerk who recorded the sale.
    /// </summary>
    public string? ClerkName { get; set; }
}

/// <summary>
/// Invoice display model for flattened list.
/// </summary>
public class ARAgingInvoiceDisplay
{
    /// <summary>
    /// Invoice/Sale date.
    /// </summary>
    public DateTime InvoiceDate { get; set; }

    /// <summary>
    /// Vehicle registration.
    /// </summary>
    public string VehicleRegistration { get; set; } = string.Empty;

    /// <summary>
    /// Product name.
    /// </summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// Quantity sold.
    /// </summary>
    public double Quantity { get; set; }

    /// <summary>
    /// Amount owed.
    /// </summary>
    public double Amount { get; set; }

    /// <summary>
    /// Days overdue.
    /// </summary>
    public int DaysOverdue { get; set; }

    /// <summary>
    /// Age bucket (Current, 1-30 Days, etc.)
    /// </summary>
    public string AgeBucket { get; set; } = string.Empty;
}
