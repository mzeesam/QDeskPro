namespace QDeskPro.Features.Reports.Services;

using Microsoft.EntityFrameworkCore;
using QDeskPro.Data;
using QDeskPro.Domain.Entities;

/// <summary>
/// Service for generating sales reports with accurate expense calculations
/// CRITICAL: Implements 4-source expense calculation as specified in claude.md
/// </summary>
public class ReportService
{
    private readonly AppDbContext _context;

    public ReportService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Generate complete sales report for a date range
    /// Following exact specifications from implementation_plan.md and claude.md
    /// </summary>
    public async Task<ClerkReportData> GenerateClerkReportAsync(DateTime fromDate, DateTime toDate, string quarryId, string userId)
    {
        var fromStamp = fromDate.ToString("yyyyMMdd");
        var toStamp = toDate.ToString("yyyyMMdd");
        var isSingleDay = fromDate.Date == toDate.Date;

        var report = new ClerkReportData
        {
            FromDate = fromDate,
            ToDate = toDate,
            IsSingleDay = isSingleDay,
            ReportTitle = isSingleDay ? fromDate.ToString("dd/MM/yyyy") : $"{fromDate:dd/MM/yyyy} - {toDate:dd/MM/yyyy}"
        };

        // Get quarry for fee settings
        var quarry = await _context.Quarries
            .Where(q => q.Id == quarryId)
            .FirstOrDefaultAsync();

        if (quarry == null)
            return report;

        report.QuarryName = quarry.QuarryName ?? "";
        report.LandRateVisible = quarry.LandRateFee > 0;

        // Get clerk name
        var clerk = await _context.Users.FindAsync(userId);
        report.ClerkName = clerk?.FullName ?? "";

        // 1. Get Opening Balance (ONLY for single-day reports)
        if (isSingleDay)
        {
            var previousDay = fromDate.AddDays(-1);
            var previousDayStamp = previousDay.ToString("yyyyMMdd");
            var previousDayNote = await _context.DailyNotes
                .Where(n => n.DateStamp == previousDayStamp)
                .Where(n => n.QId == quarryId)
                .Where(n => n.IsActive)
                .FirstOrDefaultAsync();

            report.OpeningBalance = previousDayNote?.ClosingBalance ?? 0;
        }

        // 2. Get Sales
        var sales = await _context.Sales
            .Where(s => s.ApplicationUserId == userId)
            .Where(s => string.Compare(s.DateStamp, fromStamp) >= 0)
            .Where(s => string.Compare(s.DateStamp, toStamp) <= 0)
            .Where(s => s.IsActive)
            .Include(s => s.Product)
            .Include(s => s.Layer)
            .Include(s => s.Broker)
            .OrderBy(s => s.SaleDate)
            .ToListAsync();

        report.Sales = sales;
        report.TotalQuantity = sales.Sum(s => s.Quantity);
        report.TotalSales = sales.Sum(s => s.GrossSaleAmount);
        report.Unpaid = sales.Where(s => s.PaymentStatus != "Paid").Sum(s => s.GrossSaleAmount);
        report.UnpaidOrders = report.Unpaid > 0;

        // 3. Get ALL Expenses from 4 Sources (CRITICAL)
        var allExpenses = await GetExpenseItemsForReportAsync(fromDate, toDate, userId, quarryId);
        report.ExpenseItems = allExpenses;

        // Calculate expense breakdowns by LineType
        report.TotalExpenses = allExpenses.Sum(e => e.Amount);
        report.Commission = allExpenses.Where(e => e.LineType == "Commission Expense").Sum(e => e.Amount);
        report.LoadersFee = allExpenses.Where(e => e.LineType == "Loaders Fee Expense").Sum(e => e.Amount);
        report.LandRateFee = allExpenses.Where(e => e.LineType == "Land Rate Fee Expense").Sum(e => e.Amount);

        // 4. Get Fuel Usage
        var fuelUsages = await _context.FuelUsages
            .Where(f => f.QId == quarryId)
            .Where(f => string.Compare(f.DateStamp!, fromStamp) >= 0)
            .Where(f => string.Compare(f.DateStamp!, toStamp) <= 0)
            .Where(f => f.IsActive)
            .OrderBy(f => f.UsageDate)
            .ToListAsync();

        report.FuelUsages = fuelUsages;

        // 5. Get Banking
        var bankings = await _context.Bankings
            .Where(b => b.ApplicationUserId == userId)
            .Where(b => string.Compare(b.DateStamp, fromStamp) >= 0)
            .Where(b => string.Compare(b.DateStamp, toStamp) <= 0)
            .Where(b => b.IsActive)
            .OrderBy(b => b.BankingDate)
            .ToListAsync();

        report.Bankings = bankings;
        report.Banked = bankings.Sum(b => b.AmountBanked);

        // 6. Get Collections (payments received during report period for sales made BEFORE the period)
        var collections = await _context.Sales
            .Where(s => s.ApplicationUserId == userId)
            .Where(s => s.IsActive)
            .Where(s => s.PaymentStatus == "Paid")
            .Where(s => s.PaymentReceivedDate >= fromDate && s.PaymentReceivedDate <= toDate)
            .Where(s => s.SaleDate < fromDate) // Sale was made before report period
            .Include(s => s.Product)
            .ToListAsync();

        report.CollectionItems = collections.Select(c => new ClerkCollectionItem
        {
            OriginalSaleDate = c.SaleDate ?? DateTime.Today,
            PaymentReceivedDate = c.PaymentReceivedDate ?? DateTime.Today,
            VehicleRegistration = c.VehicleRegistration,
            ProductName = c.Product?.ProductName ?? "",
            Quantity = c.Quantity,
            Amount = c.GrossSaleAmount,
            ClientName = c.ClientName,
            PaymentReference = c.PaymentReference
        }).ToList();

        report.TotalCollections = collections.Sum(c => c.GrossSaleAmount);

        // 7. Get Prepayments (customer deposits received during report period)
        var prepayments = await _context.Prepayments
            .Where(p => p.ApplicationUserId == userId)
            .Where(p => p.IsActive)
            .Where(p => p.PrepaymentDate >= fromDate && p.PrepaymentDate <= toDate)
            .Include(p => p.IntendedProduct)
            .ToListAsync();

        report.PrepaymentItems = prepayments.Select(p => new PrepaymentReportItem
        {
            PrepaymentDate = p.PrepaymentDate,
            VehicleRegistration = p.VehicleRegistration,
            ClientName = p.ClientName,
            ProductName = p.IntendedProduct?.ProductName ?? "Not Specified",
            AmountPaid = p.TotalAmountPaid,
            PaymentReference = p.PaymentReference
        }).ToList();

        report.TotalPrepayments = prepayments.Sum(p => p.TotalAmountPaid);

        // 8. Calculate Report Summary
        // Formula: Net Earnings = (Earnings + Opening Balance + Collections + Prepayments) - Unpaid
        report.Earnings = report.TotalSales - report.TotalExpenses;
        report.NetEarnings = (report.Earnings + report.OpeningBalance + report.TotalCollections + report.TotalPrepayments) - report.Unpaid;
        report.CashInHand = report.NetEarnings - report.Banked;

        // 9. Update/Create Closing Balance for single-day reports
        if (isSingleDay)
        {
            var todayNote = await _context.DailyNotes
                .Where(n => n.DateStamp == fromStamp)
                .Where(n => n.QId == quarryId)
                .Where(n => n.IsActive)
                .FirstOrDefaultAsync();

            if (todayNote != null)
            {
                // Update existing note
                todayNote.ClosingBalance = report.CashInHand;
                todayNote.DateModified = DateTime.UtcNow;
            }
            else
            {
                // Create new note with closing balance
                var newNote = new DailyNote
                {
                    Id = Guid.NewGuid().ToString(),
                    NoteDate = fromDate,
                    DateStamp = fromStamp,
                    QId = quarryId,
                    quarryId = quarryId, // Legacy field
                    ClosingBalance = report.CashInHand,
                    Notes = "",
                    IsActive = true,
                    DateCreated = DateTime.UtcNow,
                    CreatedBy = "System" // Auto-created by report generation
                };
                _context.DailyNotes.Add(newNote);
            }

            await _context.SaveChangesAsync();
        }

        return report;
    }

    /// <summary>
    /// Generate complete sales report for managers/admins (without userId filter)
    /// </summary>
    public async Task<ClerkReportData> GenerateReportAsync(string quarryId, DateTime fromDate, DateTime toDate, string? userId = null)
    {
        var fromStamp = fromDate.ToString("yyyyMMdd");
        var toStamp = toDate.ToString("yyyyMMdd");
        var isSingleDay = fromDate.Date == toDate.Date;

        var report = new ClerkReportData
        {
            FromDate = fromDate,
            ToDate = toDate,
            IsSingleDay = isSingleDay,
            ReportTitle = isSingleDay ? fromDate.ToString("dd/MM/yyyy") : $"{fromDate:dd/MM/yyyy} - {toDate:dd/MM/yyyy}"
        };

        // Get quarry for fee settings
        var quarry = await _context.Quarries
            .Where(q => q.Id == quarryId)
            .FirstOrDefaultAsync();

        if (quarry == null)
            return report;

        report.QuarryName = quarry.QuarryName ?? "";
        report.LandRateVisible = quarry.LandRateFee > 0;

        // Get clerk name if userId specified
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var clerk = await _context.Users.FindAsync(userId);
            report.ClerkName = clerk?.FullName ?? "";
        }
        else
        {
            report.ClerkName = "All Clerks";
        }

        // 1. Get Opening Balance (ONLY for single-day reports)
        if (isSingleDay)
        {
            var previousDay = fromDate.AddDays(-1);
            var previousDayStamp = previousDay.ToString("yyyyMMdd");
            var previousDayNote = await _context.DailyNotes
                .Where(n => n.DateStamp == previousDayStamp)
                .Where(n => n.QId == quarryId)
                .Where(n => n.IsActive)
                .FirstOrDefaultAsync();

            report.OpeningBalance = previousDayNote?.ClosingBalance ?? 0;
        }

        // 2. Get Sales (with optional userId filter)
        var salesQuery = _context.Sales
            .Where(s => s.QId == quarryId)
            .Where(s => string.Compare(s.DateStamp, fromStamp) >= 0)
            .Where(s => string.Compare(s.DateStamp, toStamp) <= 0)
            .Where(s => s.IsActive);

        if (!string.IsNullOrWhiteSpace(userId))
        {
            salesQuery = salesQuery.Where(s => s.ApplicationUserId == userId);
        }

        var sales = await salesQuery
            .Include(s => s.Product)
            .Include(s => s.Layer)
            .Include(s => s.Broker)
            .OrderBy(s => s.SaleDate)
            .ToListAsync();

        report.Sales = sales;
        report.TotalQuantity = sales.Sum(s => s.Quantity);
        report.TotalSales = sales.Sum(s => s.GrossSaleAmount);
        report.Unpaid = sales.Where(s => s.PaymentStatus != "Paid").Sum(s => s.GrossSaleAmount);
        report.UnpaidOrders = report.Unpaid > 0;

        // 3. Get ALL Expenses from 4 Sources
        var allExpenses = await GetManagerExpenseItemsAsync(fromDate, toDate, quarryId, userId);
        report.ExpenseItems = allExpenses;

        // Calculate expense breakdowns by LineType
        report.TotalExpenses = allExpenses.Sum(e => e.Amount);
        report.Commission = allExpenses.Where(e => e.LineType == "Commission Expense").Sum(e => e.Amount);
        report.LoadersFee = allExpenses.Where(e => e.LineType == "Loaders Fee Expense").Sum(e => e.Amount);
        report.LandRateFee = allExpenses.Where(e => e.LineType == "Land Rate Fee Expense").Sum(e => e.Amount);

        // 4. Get Fuel Usage
        var fuelUsages = await _context.FuelUsages
            .Where(f => f.QId == quarryId)
            .Where(f => string.Compare(f.DateStamp!, fromStamp) >= 0)
            .Where(f => string.Compare(f.DateStamp!, toStamp) <= 0)
            .Where(f => f.IsActive)
            .OrderBy(f => f.UsageDate)
            .ToListAsync();

        report.FuelUsages = fuelUsages;

        // 5. Get Banking (with optional userId filter)
        var bankingQuery = _context.Bankings
            .Where(b => b.QId == quarryId)
            .Where(b => string.Compare(b.DateStamp, fromStamp) >= 0)
            .Where(b => string.Compare(b.DateStamp, toStamp) <= 0)
            .Where(b => b.IsActive);

        if (!string.IsNullOrWhiteSpace(userId))
        {
            bankingQuery = bankingQuery.Where(b => b.ApplicationUserId == userId);
        }

        var bankings = await bankingQuery
            .OrderBy(b => b.BankingDate)
            .ToListAsync();

        report.Bankings = bankings;
        report.Banked = bankings.Sum(b => b.AmountBanked);

        // 6. Get Collections (payments received during report period for sales made BEFORE the period)
        var collectionsQuery = _context.Sales
            .Where(s => s.QId == quarryId)
            .Where(s => s.IsActive)
            .Where(s => s.PaymentStatus == "Paid")
            .Where(s => s.PaymentReceivedDate >= fromDate && s.PaymentReceivedDate <= toDate)
            .Where(s => s.SaleDate < fromDate); // Sale was made before report period

        if (!string.IsNullOrWhiteSpace(userId))
        {
            collectionsQuery = collectionsQuery.Where(s => s.ApplicationUserId == userId);
        }

        var collections = await collectionsQuery
            .Include(s => s.Product)
            .ToListAsync();

        report.CollectionItems = collections.Select(c => new ClerkCollectionItem
        {
            OriginalSaleDate = c.SaleDate ?? DateTime.Today,
            PaymentReceivedDate = c.PaymentReceivedDate ?? DateTime.Today,
            VehicleRegistration = c.VehicleRegistration,
            ProductName = c.Product?.ProductName ?? "",
            Quantity = c.Quantity,
            Amount = c.GrossSaleAmount,
            ClientName = c.ClientName,
            PaymentReference = c.PaymentReference
        }).ToList();

        report.TotalCollections = collections.Sum(c => c.GrossSaleAmount);

        // 7. Get Prepayments (customer deposits received during report period)
        var prepaymentsQuery = _context.Prepayments
            .Where(p => p.QId == quarryId)
            .Where(p => p.IsActive)
            .Where(p => p.PrepaymentDate >= fromDate && p.PrepaymentDate <= toDate);

        if (!string.IsNullOrWhiteSpace(userId))
        {
            prepaymentsQuery = prepaymentsQuery.Where(p => p.ApplicationUserId == userId);
        }

        var prepayments = await prepaymentsQuery
            .Include(p => p.IntendedProduct)
            .ToListAsync();

        report.PrepaymentItems = prepayments.Select(p => new PrepaymentReportItem
        {
            PrepaymentDate = p.PrepaymentDate,
            VehicleRegistration = p.VehicleRegistration,
            ClientName = p.ClientName,
            ProductName = p.IntendedProduct?.ProductName ?? "Not Specified",
            AmountPaid = p.TotalAmountPaid,
            PaymentReference = p.PaymentReference
        }).ToList();

        report.TotalPrepayments = prepayments.Sum(p => p.TotalAmountPaid);

        // 8. Calculate Report Summary
        // Formula: Net Earnings = (Earnings + Opening Balance + Collections + Prepayments) - Unpaid
        report.Earnings = report.TotalSales - report.TotalExpenses;
        report.NetEarnings = (report.Earnings + report.OpeningBalance + report.TotalCollections + report.TotalPrepayments) - report.Unpaid;
        report.CashInHand = report.NetEarnings - report.Banked;

        return report;
    }

    /// <summary>
    /// CRITICAL: Get ALL expense items from 4 different sources
    /// This is the core expense calculation logic from claude.md
    /// </summary>
    private async Task<List<SaleReportLineItem>> GetExpenseItemsForReportAsync(DateTime fromDate, DateTime toDate, string userId, string quarryId)
    {
        var allExpenses = new List<SaleReportLineItem>();

        var fromStamp = fromDate.ToString("yyyyMMdd");
        var toStamp = toDate.ToString("yyyyMMdd");

        // SOURCE 1: User Expenses (Manual Entries)
        var userExpenses = await _context.Expenses
            .Where(e => e.ApplicationUserId == userId)
            .Where(e => string.Compare(e.DateStamp, fromStamp) >= 0)
            .Where(e => string.Compare(e.DateStamp, toStamp) <= 0)
            .Where(e => e.IsActive)
            .ToListAsync();

        foreach (var item in userExpenses)
        {
            allExpenses.Add(new SaleReportLineItem
            {
                ItemDate = DateOnly.FromDateTime(item.ExpenseDate ?? DateTime.Today),
                LineItem = item.Item ?? "",
                Amount = item.Amount,
                LineType = "User Expense"
            });
        }

        // Get all sales in range for expense calculations (sources 2, 3, 4)
        var sales = await _context.Sales
            .Where(s => s.ApplicationUserId == userId)
            .Where(s => string.Compare(s.DateStamp, fromStamp) >= 0)
            .Where(s => string.Compare(s.DateStamp, toStamp) <= 0)
            .Where(s => s.IsActive)
            .Include(s => s.Product)
            .Include(s => s.Broker)
            .ToListAsync();

        // Get quarry for fee rates
        var quarry = await _context.Quarries.FindAsync(quarryId);

        if (quarry == null)
            return allExpenses;

        // SOURCE 2: Commission Expenses (Auto-generated from Sales)
        var commissionSales = sales.Where(s => s.CommissionPerUnit > 0).ToList();

        foreach (var sale in commissionSales)
        {
            var brokerName = sale.Broker != null ? $" to {sale.Broker.BrokerName}" : "";
            allExpenses.Add(new SaleReportLineItem
            {
                ItemDate = DateOnly.FromDateTime(sale.SaleDate ?? DateTime.Today),
                LineItem = $"{sale.VehicleRegistration} | {sale.Product?.ProductName} - {sale.Quantity:N0} pieces sale commission{brokerName}",
                Amount = sale.Quantity * sale.CommissionPerUnit,
                LineType = "Commission Expense"
            });
        }

        // SOURCE 3: Loaders Fee Expenses (Auto-generated from Sales, excluding beam and hardcore products)
        if (quarry.LoadersFee > 0)
        {
            foreach (var sale in sales)
            {
                var productName = sale.Product?.ProductName ?? "";
                // Don't apply loaders fee for beam or hardcore products
                if (productName.Contains("beam", StringComparison.OrdinalIgnoreCase) ||
                    productName.Contains("hardcore", StringComparison.OrdinalIgnoreCase))
                    continue;

                allExpenses.Add(new SaleReportLineItem
                {
                    ItemDate = DateOnly.FromDateTime(sale.SaleDate ?? DateTime.Today),
                    LineItem = $"{sale.VehicleRegistration} loaders fee for {sale.Quantity:N0} pieces",
                    Amount = sale.Quantity * quarry.LoadersFee.Value,
                    LineType = "Loaders Fee Expense"
                });
            }
        }

        // SOURCE 4: Land Rate Fee Expenses (Auto-generated from Sales)
        // IMPORTANT: Only create land rate expense if sale.IncludeLandRate is true
        if (quarry.LandRateFee > 0)
        {
            foreach (var sale in sales)
            {
                // Skip land rate expense if the sale has it excluded
                if (!sale.IncludeLandRate)
                    continue;

                var productName = sale.Product?.ProductName ?? "";
                double feeRate;

                // SPECIAL CASE: Reject products use RejectsFee instead
                if (productName.Contains("reject", StringComparison.OrdinalIgnoreCase))
                {
                    feeRate = quarry.RejectsFee ?? 0;
                }
                else
                {
                    feeRate = quarry.LandRateFee.Value;
                }

                if (feeRate > 0)
                {
                    allExpenses.Add(new SaleReportLineItem
                    {
                        ItemDate = DateOnly.FromDateTime(sale.SaleDate ?? DateTime.Today),
                        LineItem = $"{sale.VehicleRegistration} land rate fee for {sale.Quantity:N0} pieces",
                        Amount = sale.Quantity * feeRate,
                        LineType = "Land Rate Fee Expense"
                    });
                }
            }
        }

        // Sort all expenses by date
        return allExpenses.OrderBy(e => e.ItemDate).ToList();
    }

    /// <summary>
    /// Get expense items for managers/admins (with optional userId filter)
    /// </summary>
    private async Task<List<SaleReportLineItem>> GetManagerExpenseItemsAsync(DateTime fromDate, DateTime toDate, string quarryId, string? userId = null)
    {
        var allExpenses = new List<SaleReportLineItem>();

        var fromStamp = fromDate.ToString("yyyyMMdd");
        var toStamp = toDate.ToString("yyyyMMdd");

        // SOURCE 1: User Expenses (Manual Entries)
        var expensesQuery = _context.Expenses
            .Where(e => e.QId == quarryId)
            .Where(e => string.Compare(e.DateStamp, fromStamp) >= 0)
            .Where(e => string.Compare(e.DateStamp, toStamp) <= 0)
            .Where(e => e.IsActive);

        if (!string.IsNullOrWhiteSpace(userId))
        {
            expensesQuery = expensesQuery.Where(e => e.ApplicationUserId == userId);
        }

        var userExpenses = await expensesQuery.ToListAsync();

        foreach (var item in userExpenses)
        {
            allExpenses.Add(new SaleReportLineItem
            {
                ItemDate = DateOnly.FromDateTime(item.ExpenseDate ?? DateTime.Today),
                LineItem = item.Item ?? "",
                Amount = item.Amount,
                LineType = "User Expense"
            });
        }

        // Get all sales in range for expense calculations (sources 2, 3, 4)
        var salesQuery = _context.Sales
            .Where(s => s.QId == quarryId)
            .Where(s => string.Compare(s.DateStamp, fromStamp) >= 0)
            .Where(s => string.Compare(s.DateStamp, toStamp) <= 0)
            .Where(s => s.IsActive);

        if (!string.IsNullOrWhiteSpace(userId))
        {
            salesQuery = salesQuery.Where(s => s.ApplicationUserId == userId);
        }

        var sales = await salesQuery
            .Include(s => s.Product)
            .Include(s => s.Broker)
            .ToListAsync();

        // Get quarry for fee rates
        var quarry = await _context.Quarries.FindAsync(quarryId);

        if (quarry == null)
            return allExpenses;

        // SOURCE 2: Commission Expenses (Auto-generated from Sales)
        var commissionSales = sales.Where(s => s.CommissionPerUnit > 0).ToList();

        foreach (var sale in commissionSales)
        {
            var brokerName = sale.Broker != null ? $" to {sale.Broker.BrokerName}" : "";
            allExpenses.Add(new SaleReportLineItem
            {
                ItemDate = DateOnly.FromDateTime(sale.SaleDate ?? DateTime.Today),
                LineItem = $"{sale.VehicleRegistration} | {sale.Product?.ProductName} - {sale.Quantity:N0} pieces sale commission{brokerName}",
                Amount = sale.Quantity * sale.CommissionPerUnit,
                LineType = "Commission Expense"
            });
        }

        // SOURCE 3: Loaders Fee Expenses (Auto-generated from Sales, excluding beam and hardcore products)
        if (quarry.LoadersFee > 0)
        {
            foreach (var sale in sales)
            {
                var productName = sale.Product?.ProductName ?? "";
                // Don't apply loaders fee for beam or hardcore products
                if (productName.Contains("beam", StringComparison.OrdinalIgnoreCase) ||
                    productName.Contains("hardcore", StringComparison.OrdinalIgnoreCase))
                    continue;

                allExpenses.Add(new SaleReportLineItem
                {
                    ItemDate = DateOnly.FromDateTime(sale.SaleDate ?? DateTime.Today),
                    LineItem = $"{sale.VehicleRegistration} loaders fee for {sale.Quantity:N0} pieces",
                    Amount = sale.Quantity * quarry.LoadersFee.Value,
                    LineType = "Loaders Fee Expense"
                });
            }
        }

        // SOURCE 4: Land Rate Fee Expenses (Auto-generated from Sales)
        // IMPORTANT: Only create land rate expense if sale.IncludeLandRate is true
        if (quarry.LandRateFee > 0)
        {
            foreach (var sale in sales)
            {
                // Skip land rate expense if the sale has it excluded
                if (!sale.IncludeLandRate)
                    continue;

                var productName = sale.Product?.ProductName ?? "";
                double feeRate;

                // SPECIAL CASE: Reject products use RejectsFee instead
                if (productName.Contains("reject", StringComparison.OrdinalIgnoreCase))
                {
                    feeRate = quarry.RejectsFee ?? 0;
                }
                else
                {
                    feeRate = quarry.LandRateFee.Value;
                }

                if (feeRate > 0)
                {
                    allExpenses.Add(new SaleReportLineItem
                    {
                        ItemDate = DateOnly.FromDateTime(sale.SaleDate ?? DateTime.Today),
                        LineItem = $"{sale.VehicleRegistration} land rate fee for {sale.Quantity:N0} pieces",
                        Amount = sale.Quantity * feeRate,
                        LineType = "Land Rate Fee Expense"
                    });
                }
            }
        }

        // Sort all expenses by date
        return allExpenses.OrderBy(e => e.ItemDate).ToList();
    }
}

/// <summary>
/// Clerk report data model
/// </summary>
public class ClerkReportData
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public bool IsSingleDay { get; set; }
    public string ReportTitle { get; set; } = "";
    public string QuarryName { get; set; } = "";
    public string ClerkName { get; set; } = "";
    public bool LandRateVisible { get; set; }

    // Opening/Closing Balance
    public double OpeningBalance { get; set; }
    public double CashInHand { get; set; } // Closing Balance

    // Sales
    public List<Sale> Sales { get; set; } = new();
    public double TotalQuantity { get; set; }
    public double TotalSales { get; set; }
    public double Unpaid { get; set; }
    public bool UnpaidOrders { get; set; }

    // Collections (payments received for older unpaid sales)
    public List<ClerkCollectionItem> CollectionItems { get; set; } = new();
    public double TotalCollections { get; set; }

    // Prepayments (customer deposits received)
    public List<PrepaymentReportItem> PrepaymentItems { get; set; } = new();
    public double TotalPrepayments { get; set; }

    // Expenses (from 4 sources)
    public List<SaleReportLineItem> ExpenseItems { get; set; } = new();
    public double TotalExpenses { get; set; }
    public double Commission { get; set; }
    public double LoadersFee { get; set; }
    public double LandRateFee { get; set; }

    // Fuel Usage
    public List<FuelUsage> FuelUsages { get; set; } = new();

    // Banking
    public List<Banking> Bankings { get; set; } = new();
    public double Banked { get; set; }

    // Summary
    public double Earnings { get; set; } // TotalSales - TotalExpenses
    public double NetEarnings { get; set; } // (Earnings + OpeningBalance + Collections + Prepayments) - Unpaid
}

/// <summary>
/// Collection item for clerk reports (payments received for older unpaid sales)
/// </summary>
public class ClerkCollectionItem
{
    public DateTime OriginalSaleDate { get; set; }
    public DateTime PaymentReceivedDate { get; set; }
    public string VehicleRegistration { get; set; } = "";
    public string ProductName { get; set; } = "";
    public double Quantity { get; set; }
    public double Amount { get; set; }
    public string? ClientName { get; set; }
    public string? PaymentReference { get; set; }
}

/// <summary>
/// Sale report line item (for expenses section)
/// </summary>
public class SaleReportLineItem
{
    public DateOnly ItemDate { get; set; }
    public string LineItem { get; set; } = "";
    public string Product { get; set; } = "";
    public double Quantity { get; set; }
    public double Amount { get; set; }
    public bool Paid { get; set; }
    public string LineType { get; set; } = ""; // "User Expense", "Commission Expense", "Loaders Fee Expense", "Land Rate Fee Expense"
}
