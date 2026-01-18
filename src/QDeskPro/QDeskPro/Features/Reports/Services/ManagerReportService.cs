namespace QDeskPro.Features.Reports.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QDeskPro.Data;
using QDeskPro.Domain.Entities;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

/// <summary>
/// Service for generating manager/admin reports with advanced analytics
/// Supports Sales, Expenses, Fuel, Banking reports with export capabilities
/// Uses IServiceScopeFactory to avoid DbContext threading issues in Blazor Server
/// </summary>
public class ManagerReportService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ManagerReportService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;

        // Configure QuestPDF license (Community license for open source)
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>
    /// Get comprehensive sales report with daily summaries and product breakdown
    /// </summary>
    public async Task<SalesReportData> GetSalesReportAsync(string? quarryId, DateTime fromDate, DateTime toDate)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var fromStamp = fromDate.ToString("yyyyMMdd");
        var toStamp = toDate.ToString("yyyyMMdd");

        var report = new SalesReportData
        {
            FromDate = fromDate,
            ToDate = toDate
        };

        // Build base query
        var salesQuery = context.Sales
            .Where(s => string.Compare(s.DateStamp, fromStamp) >= 0)
            .Where(s => string.Compare(s.DateStamp, toStamp) <= 0)
            .Where(s => s.IsActive);

        if (!string.IsNullOrEmpty(quarryId))
        {
            salesQuery = salesQuery.Where(s => s.QId == quarryId);
        }

        var sales = await salesQuery
            .Include(s => s.Product)
            .Include(s => s.Broker)
            .OrderBy(s => s.SaleDate)
            .ToListAsync();

        // Get quarry for fee calculations
        Quarry? quarry = null;
        if (!string.IsNullOrEmpty(quarryId))
        {
            quarry = await context.Quarries.FindAsync(quarryId);
        }

        // Get manual (other) expenses for the date range
        var expenseQuery = context.Expenses
            .Where(e => string.Compare(e.DateStamp, fromStamp) >= 0)
            .Where(e => string.Compare(e.DateStamp, toStamp) <= 0)
            .Where(e => e.IsActive);

        if (!string.IsNullOrEmpty(quarryId))
        {
            expenseQuery = expenseQuery.Where(e => e.QId == quarryId);
        }

        var manualExpenses = await expenseQuery.ToListAsync();

        // Calculate totals
        report.TotalOrders = sales.Count;
        report.TotalQuantity = sales.Sum(s => s.Quantity);
        report.TotalSales = sales.Sum(s => s.GrossSaleAmount);
        report.UnpaidAmount = sales.Where(s => s.PaymentStatus != "Paid").Sum(s => s.GrossSaleAmount);

        // Calculate expenses from sales (4-source model)
        report.TotalCommission = sales.Sum(s => s.Quantity * s.CommissionPerUnit);

        if (quarry != null)
        {
            // Don't apply loaders fee for beam or hardcore products
            report.TotalLoadersFee = sales
                .Where(s =>
                {
                    var productName = s.Product?.ProductName ?? "";
                    return !productName.Contains("beam", StringComparison.OrdinalIgnoreCase) &&
                           !productName.Contains("hardcore", StringComparison.OrdinalIgnoreCase);
                })
                .Sum(s => s.Quantity * (quarry.LoadersFee ?? 0));
            // Only include land rate for sales where IncludeLandRate is true
            report.TotalLandRateFee = sales.Where(s => s.IncludeLandRate).Sum(s =>
            {
                var productName = s.Product?.ProductName ?? "";
                var feeRate = productName.Contains("reject", StringComparison.OrdinalIgnoreCase)
                    ? (quarry.RejectsFee ?? 0)
                    : (quarry.LandRateFee ?? 0);
                return s.Quantity * feeRate;
            });
        }

        // Calculate Other Expenses (manual user-entered expenses)
        report.TotalOtherExpenses = manualExpenses.Sum(e => e.Amount);

        // Get Opening Balance and Cash In Hand (B/F)
        if (!string.IsNullOrEmpty(quarryId))
        {
            // Actual Opening Balance = closing balance from day before fromDate (e.g., Nov 30 for Dec 1-10 report)
            var openingDayStamp = fromDate.AddDays(-1).ToString("yyyyMMdd");
            var openingDayNote = await context.DailyNotes
                .Where(n => n.DateStamp == openingDayStamp)
                .Where(n => n.QId == quarryId)
                .Where(n => n.IsActive)
                .FirstOrDefaultAsync();
            report.ActualOpeningBalance = openingDayNote?.ClosingBalance ?? 0;

            // Cash In Hand (B/F) = closing balance from day before toDate (e.g., Dec 9 for Dec 1-10 report)
            // This is used for Net Income calculation since balances carry over daily within the range
            var cashInHandDayStamp = toDate.AddDays(-1).ToString("yyyyMMdd");
            var cashInHandNote = await context.DailyNotes
                .Where(n => n.DateStamp == cashInHandDayStamp)
                .Where(n => n.QId == quarryId)
                .Where(n => n.IsActive)
                .FirstOrDefaultAsync();
            report.OpeningBalance = cashInHandNote?.ClosingBalance ?? 0;
        }

        // Collections Query: Payments received during report period for sales made BEFORE the period
        // These are previously unpaid orders that have now been paid
        // Example: Sale on Nov 25 (unpaid), Payment received on Dec 5 → Shows as collection in Dec 1-10 report
        var collectionsQuery = context.Sales
            .Where(s => s.IsActive)
            .Where(s => s.PaymentStatus == "Paid")
            .Where(s => s.PaymentReceivedDate >= fromDate && s.PaymentReceivedDate <= toDate)
            .Where(s => s.SaleDate < fromDate);  // Sale was made before report period

        if (!string.IsNullOrEmpty(quarryId))
        {
            collectionsQuery = collectionsQuery.Where(s => s.QId == quarryId);
        }

        var collections = await collectionsQuery
            .Include(s => s.Product)
            .OrderBy(s => s.PaymentReceivedDate)
            .ToListAsync();

        // Map collections to CollectionItem
        report.CollectionItems = collections.Select(s => new CollectionItem
        {
            OriginalSaleDate = s.SaleDate ?? DateTime.MinValue,
            PaymentReceivedDate = s.PaymentReceivedDate ?? DateTime.Today,
            VehicleRegistration = s.VehicleRegistration,
            ProductName = s.Product?.ProductName ?? "Unknown",
            Quantity = s.Quantity,
            Amount = s.GrossSaleAmount,
            ClientName = s.ClientName,
            PaymentReference = s.PaymentReference
        }).ToList();

        report.TotalCollections = collections.Sum(s => s.GrossSaleAmount);

        // Prepayments Query: Customer deposits received during report period
        var prepaymentsQuery = context.Prepayments
            .Where(p => p.IsActive)
            .Where(p => p.PrepaymentDate >= fromDate && p.PrepaymentDate <= toDate);

        if (!string.IsNullOrEmpty(quarryId))
        {
            prepaymentsQuery = prepaymentsQuery.Where(p => p.QId == quarryId);
        }

        var prepayments = await prepaymentsQuery
            .Include(p => p.IntendedProduct)
            .OrderBy(p => p.PrepaymentDate)
            .ToListAsync();

        // Map prepayments to PrepaymentReportItem
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

        // Net Amount formula: (Earnings + Cash In Hand B/F + Collections + Prepayments) - Unpaid Orders
        // Where Earnings = TotalSales - TotalExpenses
        var totalExpenses = report.TotalCommission + report.TotalLoadersFee + report.TotalLandRateFee + report.TotalOtherExpenses;
        report.NetAmount = (report.TotalSales - report.UnpaidAmount - totalExpenses) + report.OpeningBalance + report.TotalCollections + report.TotalPrepayments;

        // Generate daily summaries
        var dailyGroups = sales.GroupBy(s => s.SaleDate?.Date ?? DateTime.Today);
        foreach (var group in dailyGroups.OrderBy(g => g.Key))
        {
            var daySales = group.ToList();
            var dayCommission = daySales.Sum(s => s.Quantity * s.CommissionPerUnit);
            var dayLoadersFee = quarry != null
                ? daySales.Sum(s => s.Quantity * (quarry.LoadersFee ?? 0))
                : 0;
            // Only include land rate for sales where IncludeLandRate is true
            var dayLandRate = quarry != null
                ? daySales.Where(s => s.IncludeLandRate).Sum(s =>
                {
                    var productName = s.Product?.ProductName ?? "";
                    var feeRate = productName.Contains("reject", StringComparison.OrdinalIgnoreCase)
                        ? (quarry.RejectsFee ?? 0)
                        : (quarry.LandRateFee ?? 0);
                    return s.Quantity * feeRate;
                })
                : 0;

            // Get other expenses for this day
            var dayDateStamp = group.Key.ToString("yyyyMMdd");
            var dayOtherExpenses = manualExpenses
                .Where(e => e.DateStamp == dayDateStamp)
                .Sum(e => e.Amount);

            var dayRevenue = daySales.Sum(s => s.GrossSaleAmount);
            var dayExpenses = dayCommission + dayLoadersFee + dayLandRate + dayOtherExpenses;

            report.DailySummaries.Add(new DailySalesBreakdown
            {
                Date = group.Key,
                OrderCount = daySales.Count,
                Quantity = daySales.Sum(s => s.Quantity),
                Revenue = dayRevenue,
                Commission = dayCommission,
                LoadersFee = dayLoadersFee,
                LandRateFee = dayLandRate,
                OtherExpenses = dayOtherExpenses,
                TotalExpenses = dayExpenses,
                NetAmount = dayRevenue - dayExpenses
            });
        }

        // Generate product breakdown
        var productGroups = sales
            .Where(s => s.Product != null)
            .GroupBy(s => s.Product!.ProductName);

        foreach (var group in productGroups.OrderByDescending(g => g.Sum(s => s.GrossSaleAmount)))
        {
            report.ProductBreakdown.Add(new ProductBreakdownItem
            {
                ProductName = group.Key ?? "Unknown",
                OrderCount = group.Count(),
                Quantity = group.Sum(s => s.Quantity),
                Revenue = group.Sum(s => s.GrossSaleAmount)
            });
        }

        // Generate clerk breakdown
        var clerkGroups = sales.GroupBy(s => new { s.ApplicationUserId, s.ClerkName });
        foreach (var group in clerkGroups.OrderByDescending(g => g.Sum(s => s.GrossSaleAmount)))
        {
            report.ClerkBreakdown.Add(new ClerkBreakdownItem
            {
                ClerkId = group.Key.ApplicationUserId ?? "",
                ClerkName = group.Key.ClerkName ?? "Unknown",
                OrderCount = group.Count(),
                Quantity = group.Sum(s => s.Quantity),
                Revenue = group.Sum(s => s.GrossSaleAmount)
            });
        }

        return report;
    }

    /// <summary>
    /// Get comprehensive expenses report with breakdown by category
    /// </summary>
    public async Task<ExpensesReportData> GetExpensesReportAsync(string? quarryId, DateTime fromDate, DateTime toDate)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var fromStamp = fromDate.ToString("yyyyMMdd");
        var toStamp = toDate.ToString("yyyyMMdd");

        var report = new ExpensesReportData
        {
            FromDate = fromDate,
            ToDate = toDate
        };

        // Get manual expenses
        var expenseQuery = context.Expenses
            .Where(e => string.Compare(e.DateStamp, fromStamp) >= 0)
            .Where(e => string.Compare(e.DateStamp, toStamp) <= 0)
            .Where(e => e.IsActive);

        if (!string.IsNullOrEmpty(quarryId))
        {
            expenseQuery = expenseQuery.Where(e => e.QId == quarryId);
        }

        var manualExpenses = await expenseQuery.ToListAsync();

        // Get sales for calculated expenses
        var salesQuery = context.Sales
            .Where(s => string.Compare(s.DateStamp, fromStamp) >= 0)
            .Where(s => string.Compare(s.DateStamp, toStamp) <= 0)
            .Where(s => s.IsActive);

        if (!string.IsNullOrEmpty(quarryId))
        {
            salesQuery = salesQuery.Where(s => s.QId == quarryId);
        }

        var sales = await salesQuery
            .Include(s => s.Product)
            .Include(s => s.Broker)
            .ToListAsync();

        // Get quarry for fee calculations
        Quarry? quarry = null;
        if (!string.IsNullOrEmpty(quarryId))
        {
            quarry = await context.Quarries.FindAsync(quarryId);
        }

        // Build expense items list
        var expenseItems = new List<ExpenseReportItem>();

        // 1. Other Expenses (manual user-entered expenses)
        foreach (var exp in manualExpenses)
        {
            expenseItems.Add(new ExpenseReportItem
            {
                Date = exp.ExpenseDate ?? DateTime.Today,
                Description = exp.Item ?? "",
                Category = "Other Expenses",
                Amount = exp.Amount
            });
        }

        // 2. Commission expenses from sales
        foreach (var sale in sales.Where(s => s.CommissionPerUnit > 0))
        {
            var brokerName = sale.Broker?.BrokerName ?? "";
            expenseItems.Add(new ExpenseReportItem
            {
                Date = sale.SaleDate ?? DateTime.Today,
                Description = $"{sale.VehicleRegistration} - Commission ({sale.Quantity:N0} pcs){(string.IsNullOrEmpty(brokerName) ? "" : $" to {brokerName}")}",
                Category = "Commission",
                Amount = sale.Quantity * sale.CommissionPerUnit
            });
        }

        // 3. Loaders fee expenses
        if (quarry?.LoadersFee > 0)
        {
            foreach (var sale in sales)
            {
                expenseItems.Add(new ExpenseReportItem
                {
                    Date = sale.SaleDate ?? DateTime.Today,
                    Description = $"{sale.VehicleRegistration} - Loaders Fee ({sale.Quantity:N0} pcs)",
                    Category = "Loaders Fee",
                    Amount = sale.Quantity * quarry.LoadersFee.Value
                });
            }
        }

        // 4. Land rate expenses (only for sales where IncludeLandRate is true)
        if (quarry?.LandRateFee > 0)
        {
            foreach (var sale in sales)
            {
                // Skip land rate if the sale has it excluded
                if (!sale.IncludeLandRate)
                    continue;

                var productName = sale.Product?.ProductName ?? "";
                var feeRate = productName.Contains("reject", StringComparison.OrdinalIgnoreCase)
                    ? (quarry.RejectsFee ?? 0)
                    : quarry.LandRateFee.Value;

                if (feeRate > 0)
                {
                    expenseItems.Add(new ExpenseReportItem
                    {
                        Date = sale.SaleDate ?? DateTime.Today,
                        Description = $"{sale.VehicleRegistration} - Land Rate ({sale.Quantity:N0} pcs)",
                        Category = "Land Rate",
                        Amount = sale.Quantity * feeRate
                    });
                }
            }
        }

        // Sort and assign to report
        report.ExpenseItems = expenseItems.OrderBy(e => e.Date).ThenBy(e => e.Category).ToList();
        report.TotalExpenseItems = expenseItems.Count;

        // Calculate category totals
        report.ManualExpenses = expenseItems.Where(e => e.Category == "Other Expenses").Sum(e => e.Amount);
        report.Commission = expenseItems.Where(e => e.Category == "Commission").Sum(e => e.Amount);
        report.LoadersFee = expenseItems.Where(e => e.Category == "Loaders Fee").Sum(e => e.Amount);
        report.LandRateFee = expenseItems.Where(e => e.Category == "Land Rate").Sum(e => e.Amount);
        report.TotalExpenses = report.ManualExpenses + report.Commission + report.LoadersFee + report.LandRateFee;

        // Generate daily expenses trend
        var dailyExpenses = expenseItems
            .GroupBy(e => e.Date.Date)
            .Select(g => new DailyExpenseItem
            {
                Date = g.Key,
                Amount = g.Sum(e => e.Amount),
                ManualAmount = g.Where(e => e.Category == "Other Expenses").Sum(e => e.Amount),
                CommissionAmount = g.Where(e => e.Category == "Commission").Sum(e => e.Amount),
                LoadersFeeAmount = g.Where(e => e.Category == "Loaders Fee").Sum(e => e.Amount),
                LandRateAmount = g.Where(e => e.Category == "Land Rate").Sum(e => e.Amount)
            })
            .OrderBy(d => d.Date)
            .ToList();

        report.DailyExpenses = dailyExpenses;

        return report;
    }

    /// <summary>
    /// Get fuel usage report with consumption analytics
    /// </summary>
    public async Task<FuelReportData> GetFuelReportAsync(string? quarryId, DateTime fromDate, DateTime toDate)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var fromStamp = fromDate.ToString("yyyyMMdd");
        var toStamp = toDate.ToString("yyyyMMdd");

        var report = new FuelReportData
        {
            FromDate = fromDate,
            ToDate = toDate
        };

        var fuelQuery = context.FuelUsages
            .Where(f => string.Compare(f.DateStamp!, fromStamp) >= 0)
            .Where(f => string.Compare(f.DateStamp!, toStamp) <= 0)
            .Where(f => f.IsActive);

        if (!string.IsNullOrEmpty(quarryId))
        {
            fuelQuery = fuelQuery.Where(f => f.QId == quarryId);
        }

        var fuelUsages = await fuelQuery
            .OrderBy(f => f.UsageDate)
            .ToListAsync();

        // Map to report items
        foreach (var fuel in fuelUsages)
        {
            report.FuelRecords.Add(new FuelUsageRecord
            {
                Date = fuel.UsageDate ?? DateTime.Today,
                OldStock = fuel.OldStock,
                NewStock = fuel.NewStock,
                TotalStock = fuel.TotalStock,
                MachinesLoaded = fuel.MachinesLoaded,
                WheelLoadersLoaded = fuel.WheelLoadersLoaded,
                Balance = fuel.Balance
            });
        }

        // Calculate totals
        report.TotalRecords = fuelUsages.Count;
        report.TotalReceived = fuelUsages.Sum(f => f.NewStock);
        report.MachinesUsage = fuelUsages.Sum(f => f.MachinesLoaded);
        report.WheelLoadersUsage = fuelUsages.Sum(f => f.WheelLoadersLoaded);
        report.TotalUsage = report.MachinesUsage + report.WheelLoadersUsage;

        // Current balance from latest record
        var latestRecord = fuelUsages.LastOrDefault();
        report.CurrentBalance = latestRecord?.Balance ?? 0;

        return report;
    }

    /// <summary>
    /// Get banking report with transaction details
    /// </summary>
    public async Task<BankingReportData> GetBankingReportAsync(string? quarryId, DateTime fromDate, DateTime toDate)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var fromStamp = fromDate.ToString("yyyyMMdd");
        var toStamp = toDate.ToString("yyyyMMdd");

        var report = new BankingReportData
        {
            FromDate = fromDate,
            ToDate = toDate
        };

        var bankingQuery = context.Bankings
            .Where(b => string.Compare(b.DateStamp, fromStamp) >= 0)
            .Where(b => string.Compare(b.DateStamp, toStamp) <= 0)
            .Where(b => b.IsActive);

        if (!string.IsNullOrEmpty(quarryId))
        {
            bankingQuery = bankingQuery.Where(b => b.QId == quarryId);
        }

        var bankings = await bankingQuery
            .OrderBy(b => b.BankingDate)
            .ToListAsync();

        // Map to report items
        foreach (var banking in bankings)
        {
            report.BankingRecords.Add(new BankingReportItem
            {
                Date = banking.BankingDate ?? DateTime.Today,
                Description = banking.Item ?? "Deposit",
                Reference = banking.TxnReference ?? banking.RefCode ?? "",
                Amount = banking.AmountBanked
            });
        }

        // Calculate totals
        report.TotalRecords = bankings.Count;
        report.TotalBanked = bankings.Sum(b => b.AmountBanked);

        // Calculate average per day
        var totalDays = (toDate - fromDate).Days + 1;
        report.AveragePerDay = totalDays > 0 ? report.TotalBanked / totalDays : report.TotalBanked;

        // Generate daily banking trend
        var dailyBanking = bankings
            .GroupBy(b => (b.BankingDate ?? DateTime.Today).Date)
            .Select(g => new DailyBankingItem
            {
                Date = g.Key,
                Amount = g.Sum(b => b.AmountBanked),
                TransactionCount = g.Count()
            })
            .OrderBy(d => d.Date)
            .ToList();

        report.DailyBanking = dailyBanking;

        return report;
    }

    /// <summary>
    /// Export report to Excel with charts
    /// </summary>
    public async Task<byte[]> ExportToExcelAsync(string? quarryId, DateTime fromDate, DateTime toDate, string reportType)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        using var workbook = new XLWorkbook();

        // Get quarry name for header
        var quarryName = "All Quarries";
        if (!string.IsNullOrEmpty(quarryId))
        {
            var quarry = await context.Quarries.FindAsync(quarryId);
            quarryName = quarry?.QuarryName ?? "Unknown";
        }

        var dateRange = $"{fromDate:dd MMM yyyy} - {toDate:dd MMM yyyy}";

        switch (reportType.ToLower())
        {
            case "sales":
                await AddSalesWorksheetAsync(workbook, quarryId, fromDate, toDate, quarryName, dateRange);
                break;
            case "expenses":
                await AddExpensesWorksheetAsync(workbook, quarryId, fromDate, toDate, quarryName, dateRange);
                break;
            case "fuel":
                await AddFuelWorksheetAsync(workbook, quarryId, fromDate, toDate, quarryName, dateRange);
                break;
            case "banking":
                await AddBankingWorksheetAsync(workbook, quarryId, fromDate, toDate, quarryName, dateRange);
                break;
            case "comprehensive":
                await AddSalesWorksheetAsync(workbook, quarryId, fromDate, toDate, quarryName, dateRange);
                await AddExpensesWorksheetAsync(workbook, quarryId, fromDate, toDate, quarryName, dateRange);
                await AddFuelWorksheetAsync(workbook, quarryId, fromDate, toDate, quarryName, dateRange);
                await AddBankingWorksheetAsync(workbook, quarryId, fromDate, toDate, quarryName, dateRange);
                await AddSummaryWorksheetAsync(workbook, quarryId, fromDate, toDate, quarryName, dateRange);
                break;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private async Task AddSalesWorksheetAsync(XLWorkbook workbook, string? quarryId, DateTime fromDate, DateTime toDate, string quarryName, string dateRange)
    {
        var salesData = await GetSalesReportAsync(quarryId, fromDate, toDate);
        var ws = workbook.Worksheets.Add("Sales Report");

        // Header
        ws.Cell("A1").Value = "QDESKPRO SALES REPORT";
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 16;
        ws.Range("A1:G1").Merge();

        ws.Cell("A2").Value = $"Quarry: {quarryName} | Period: {dateRange}";
        ws.Range("A2:G2").Merge();
        ws.Cell("A2").Style.Font.Italic = true;

        // Summary section
        var row = 4;
        ws.Cell(row, 1).Value = "SUMMARY";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Range(row, 1, row, 2).Merge();

        row++;
        ws.Cell(row, 1).Value = "Total Orders:";
        ws.Cell(row, 2).Value = salesData.TotalOrders;
        row++;
        ws.Cell(row, 1).Value = "Total Quantity:";
        ws.Cell(row, 2).Value = salesData.TotalQuantity;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
        row++;
        ws.Cell(row, 1).Value = "Total Revenue:";
        ws.Cell(row, 2).Value = salesData.TotalSales;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
        row++;
        ws.Cell(row, 1).Value = "Total Commission:";
        ws.Cell(row, 2).Value = salesData.TotalCommission;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
        row++;
        ws.Cell(row, 1).Value = "Total Loaders Fee:";
        ws.Cell(row, 2).Value = salesData.TotalLoadersFee;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
        row++;
        ws.Cell(row, 1).Value = "Other Expenses:";
        ws.Cell(row, 2).Value = salesData.TotalOtherExpenses;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
        row++;
        ws.Cell(row, 1).Value = "Net Amount:";
        ws.Cell(row, 2).Value = salesData.NetAmount;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(row, 2).Style.Font.Bold = true;
        row++;
        ws.Cell(row, 1).Value = "Unpaid Amount:";
        ws.Cell(row, 2).Value = salesData.UnpaidAmount;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(row, 2).Style.Font.FontColor = XLColor.Red;

        // Daily breakdown table
        row += 2;
        ws.Cell(row, 1).Value = "DAILY BREAKDOWN";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Range(row, 1, row, 8).Merge();

        row++;
        var headerRow = row;
        ws.Cell(row, 1).Value = "Date";
        ws.Cell(row, 2).Value = "Orders";
        ws.Cell(row, 3).Value = "Quantity";
        ws.Cell(row, 4).Value = "Revenue";
        ws.Cell(row, 5).Value = "Commission";
        ws.Cell(row, 6).Value = "Loaders Fee";
        ws.Cell(row, 7).Value = "Other Exp";
        ws.Cell(row, 8).Value = "Net Amount";
        ws.Range(headerRow, 1, headerRow, 8).Style.Font.Bold = true;
        ws.Range(headerRow, 1, headerRow, 8).Style.Fill.BackgroundColor = XLColor.LightBlue;

        foreach (var day in salesData.DailySummaries)
        {
            row++;
            ws.Cell(row, 1).Value = day.Date.ToString("dd/MM/yyyy");
            ws.Cell(row, 2).Value = day.OrderCount;
            ws.Cell(row, 3).Value = day.Quantity;
            ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 4).Value = day.Revenue;
            ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 5).Value = day.Commission;
            ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 6).Value = day.LoadersFee;
            ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 7).Value = day.OtherExpenses;
            ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 8).Value = day.NetAmount;
            ws.Cell(row, 8).Style.NumberFormat.Format = "#,##0.00";
        }

        // Product breakdown table
        row += 2;
        ws.Cell(row, 1).Value = "PRODUCT BREAKDOWN";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Range(row, 1, row, 4).Merge();

        row++;
        ws.Cell(row, 1).Value = "Product";
        ws.Cell(row, 2).Value = "Orders";
        ws.Cell(row, 3).Value = "Quantity";
        ws.Cell(row, 4).Value = "Revenue";
        ws.Range(row, 1, row, 4).Style.Font.Bold = true;
        ws.Range(row, 1, row, 4).Style.Fill.BackgroundColor = XLColor.LightGreen;

        foreach (var product in salesData.ProductBreakdown)
        {
            row++;
            ws.Cell(row, 1).Value = product.ProductName;
            ws.Cell(row, 2).Value = product.OrderCount;
            ws.Cell(row, 3).Value = product.Quantity;
            ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 4).Value = product.Revenue;
            ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
        }

        ws.Columns().AdjustToContents();
    }

    private async Task AddExpensesWorksheetAsync(XLWorkbook workbook, string? quarryId, DateTime fromDate, DateTime toDate, string quarryName, string dateRange)
    {
        var expensesData = await GetExpensesReportAsync(quarryId, fromDate, toDate);
        var ws = workbook.Worksheets.Add("Expenses Report");

        // Header
        ws.Cell("A1").Value = "QDESKPRO EXPENSES REPORT";
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 16;
        ws.Range("A1:D1").Merge();

        ws.Cell("A2").Value = $"Quarry: {quarryName} | Period: {dateRange}";
        ws.Range("A2:D2").Merge();
        ws.Cell("A2").Style.Font.Italic = true;

        // Summary section
        var row = 4;
        ws.Cell(row, 1).Value = "SUMMARY";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Range(row, 1, row, 2).Merge();

        row++;
        ws.Cell(row, 1).Value = "Total Expenses:";
        ws.Cell(row, 2).Value = expensesData.TotalExpenses;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(row, 2).Style.Font.Bold = true;
        row++;
        ws.Cell(row, 1).Value = "Commission:";
        ws.Cell(row, 2).Value = expensesData.Commission;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
        row++;
        ws.Cell(row, 1).Value = "Loaders Fee:";
        ws.Cell(row, 2).Value = expensesData.LoadersFee;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
        row++;
        ws.Cell(row, 1).Value = "Land Rate:";
        ws.Cell(row, 2).Value = expensesData.LandRateFee;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
        row++;
        ws.Cell(row, 1).Value = "Other Expenses:";
        ws.Cell(row, 2).Value = expensesData.ManualExpenses;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";

        // Expense details table
        row += 2;
        ws.Cell(row, 1).Value = "EXPENSE DETAILS";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Range(row, 1, row, 4).Merge();

        row++;
        ws.Cell(row, 1).Value = "Date";
        ws.Cell(row, 2).Value = "Description";
        ws.Cell(row, 3).Value = "Category";
        ws.Cell(row, 4).Value = "Amount";
        ws.Range(row, 1, row, 4).Style.Font.Bold = true;
        ws.Range(row, 1, row, 4).Style.Fill.BackgroundColor = XLColor.LightCoral;

        foreach (var item in expensesData.ExpenseItems)
        {
            row++;
            ws.Cell(row, 1).Value = item.Date.ToString("dd/MM/yyyy");
            ws.Cell(row, 2).Value = item.Description;
            ws.Cell(row, 3).Value = item.Category;
            ws.Cell(row, 4).Value = item.Amount;
            ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
        }

        ws.Columns().AdjustToContents();
    }

    private async Task AddFuelWorksheetAsync(XLWorkbook workbook, string? quarryId, DateTime fromDate, DateTime toDate, string quarryName, string dateRange)
    {
        var fuelData = await GetFuelReportAsync(quarryId, fromDate, toDate);
        var ws = workbook.Worksheets.Add("Fuel Usage Report");

        // Header
        ws.Cell("A1").Value = "QDESKPRO FUEL USAGE REPORT";
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 16;
        ws.Range("A1:G1").Merge();

        ws.Cell("A2").Value = $"Quarry: {quarryName} | Period: {dateRange}";
        ws.Range("A2:G2").Merge();
        ws.Cell("A2").Style.Font.Italic = true;

        // Summary section
        var row = 4;
        ws.Cell(row, 1).Value = "SUMMARY";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Range(row, 1, row, 2).Merge();

        row++;
        ws.Cell(row, 1).Value = "Total Fuel Received:";
        ws.Cell(row, 2).Value = fuelData.TotalReceived;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.0";
        row++;
        ws.Cell(row, 1).Value = "Machines Usage:";
        ws.Cell(row, 2).Value = fuelData.MachinesUsage;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.0";
        row++;
        ws.Cell(row, 1).Value = "W/Loaders Usage:";
        ws.Cell(row, 2).Value = fuelData.WheelLoadersUsage;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.0";
        row++;
        ws.Cell(row, 1).Value = "Total Usage:";
        ws.Cell(row, 2).Value = fuelData.TotalUsage;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.0";
        ws.Cell(row, 2).Style.Font.Bold = true;
        row++;
        ws.Cell(row, 1).Value = "Current Balance:";
        ws.Cell(row, 2).Value = fuelData.CurrentBalance;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.0";
        ws.Cell(row, 2).Style.Font.FontColor = XLColor.Green;

        // Fuel details table
        row += 2;
        ws.Cell(row, 1).Value = "FUEL USAGE DETAILS";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Range(row, 1, row, 7).Merge();

        row++;
        ws.Cell(row, 1).Value = "Date";
        ws.Cell(row, 2).Value = "Old Stock";
        ws.Cell(row, 3).Value = "New Stock";
        ws.Cell(row, 4).Value = "Total";
        ws.Cell(row, 5).Value = "Machines";
        ws.Cell(row, 6).Value = "W/Loaders";
        ws.Cell(row, 7).Value = "Balance";
        ws.Range(row, 1, row, 7).Style.Font.Bold = true;
        ws.Range(row, 1, row, 7).Style.Fill.BackgroundColor = XLColor.LightYellow;

        foreach (var fuel in fuelData.FuelRecords)
        {
            row++;
            ws.Cell(row, 1).Value = fuel.Date.ToString("dd/MM/yyyy");
            ws.Cell(row, 2).Value = fuel.OldStock;
            ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.0";
            ws.Cell(row, 3).Value = fuel.NewStock;
            ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.0";
            ws.Cell(row, 4).Value = fuel.TotalStock;
            ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.0";
            ws.Cell(row, 5).Value = fuel.MachinesLoaded;
            ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.0";
            ws.Cell(row, 6).Value = fuel.WheelLoadersLoaded;
            ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0.0";
            ws.Cell(row, 7).Value = fuel.Balance;
            ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0.0";
            ws.Cell(row, 7).Style.Font.FontColor = XLColor.Green;
        }

        ws.Columns().AdjustToContents();
    }

    private async Task AddBankingWorksheetAsync(XLWorkbook workbook, string? quarryId, DateTime fromDate, DateTime toDate, string quarryName, string dateRange)
    {
        var bankingData = await GetBankingReportAsync(quarryId, fromDate, toDate);
        var ws = workbook.Worksheets.Add("Banking Report");

        // Header
        ws.Cell("A1").Value = "QDESKPRO BANKING REPORT";
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 16;
        ws.Range("A1:D1").Merge();

        ws.Cell("A2").Value = $"Quarry: {quarryName} | Period: {dateRange}";
        ws.Range("A2:D2").Merge();
        ws.Cell("A2").Style.Font.Italic = true;

        // Summary section
        var row = 4;
        ws.Cell(row, 1).Value = "SUMMARY";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Range(row, 1, row, 2).Merge();

        row++;
        ws.Cell(row, 1).Value = "Total Banked:";
        ws.Cell(row, 2).Value = bankingData.TotalBanked;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(row, 2).Style.Font.Bold = true;
        row++;
        ws.Cell(row, 1).Value = "Total Transactions:";
        ws.Cell(row, 2).Value = bankingData.TotalRecords;
        row++;
        ws.Cell(row, 1).Value = "Average per Day:";
        ws.Cell(row, 2).Value = bankingData.AveragePerDay;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";

        // Banking details table
        row += 2;
        ws.Cell(row, 1).Value = "BANKING DETAILS";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Range(row, 1, row, 4).Merge();

        row++;
        ws.Cell(row, 1).Value = "Date";
        ws.Cell(row, 2).Value = "Description";
        ws.Cell(row, 3).Value = "Reference";
        ws.Cell(row, 4).Value = "Amount";
        ws.Range(row, 1, row, 4).Style.Font.Bold = true;
        ws.Range(row, 1, row, 4).Style.Fill.BackgroundColor = XLColor.LightSkyBlue;

        foreach (var banking in bankingData.BankingRecords)
        {
            row++;
            ws.Cell(row, 1).Value = banking.Date.ToString("dd/MM/yyyy");
            ws.Cell(row, 2).Value = banking.Description;
            ws.Cell(row, 3).Value = banking.Reference;
            ws.Cell(row, 4).Value = banking.Amount;
            ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
        }

        // Total row
        row++;
        ws.Cell(row, 1).Value = "TOTAL";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Range(row, 1, row, 3).Merge();
        ws.Cell(row, 4).Value = bankingData.TotalBanked;
        ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(row, 4).Style.Font.Bold = true;
        ws.Range(row, 1, row, 4).Style.Fill.BackgroundColor = XLColor.LightBlue;

        ws.Columns().AdjustToContents();
    }

    private async Task AddSummaryWorksheetAsync(XLWorkbook workbook, string? quarryId, DateTime fromDate, DateTime toDate, string quarryName, string dateRange)
    {
        var salesData = await GetSalesReportAsync(quarryId, fromDate, toDate);
        var expensesData = await GetExpensesReportAsync(quarryId, fromDate, toDate);
        var fuelData = await GetFuelReportAsync(quarryId, fromDate, toDate);
        var bankingData = await GetBankingReportAsync(quarryId, fromDate, toDate);

        var ws = workbook.Worksheets.Add("Summary");

        // Header
        ws.Cell("A1").Value = "QDESKPRO COMPREHENSIVE SUMMARY";
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 18;
        ws.Range("A1:C1").Merge();

        ws.Cell("A2").Value = $"Quarry: {quarryName}";
        ws.Cell("A3").Value = $"Period: {dateRange}";
        ws.Cell("A4").Value = $"Generated: {DateTime.Now:dd MMM yyyy HH:mm}";

        // Sales Summary
        var row = 6;
        ws.Cell(row, 1).Value = "SALES SUMMARY";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 14;
        ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.LightGreen;
        ws.Range(row, 1, row, 3).Merge();

        row++;
        ws.Cell(row, 1).Value = "Total Orders:";
        ws.Cell(row, 2).Value = salesData.TotalOrders;
        row++;
        ws.Cell(row, 1).Value = "Total Quantity:";
        ws.Cell(row, 2).Value = salesData.TotalQuantity;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
        row++;
        ws.Cell(row, 1).Value = "Total Revenue:";
        ws.Cell(row, 2).Value = salesData.TotalSales;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(row, 2).Style.Font.Bold = true;
        row++;
        ws.Cell(row, 1).Value = "Unpaid Amount:";
        ws.Cell(row, 2).Value = salesData.UnpaidAmount;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(row, 2).Style.Font.FontColor = XLColor.Red;

        // Expenses Summary
        row += 2;
        ws.Cell(row, 1).Value = "EXPENSES SUMMARY";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 14;
        ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.LightCoral;
        ws.Range(row, 1, row, 3).Merge();

        row++;
        ws.Cell(row, 1).Value = "Commission:";
        ws.Cell(row, 2).Value = expensesData.Commission;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
        row++;
        ws.Cell(row, 1).Value = "Loaders Fee:";
        ws.Cell(row, 2).Value = expensesData.LoadersFee;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
        row++;
        ws.Cell(row, 1).Value = "Land Rate:";
        ws.Cell(row, 2).Value = expensesData.LandRateFee;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
        row++;
        ws.Cell(row, 1).Value = "Other Expenses:";
        ws.Cell(row, 2).Value = expensesData.ManualExpenses;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
        row++;
        ws.Cell(row, 1).Value = "Total Expenses:";
        ws.Cell(row, 2).Value = expensesData.TotalExpenses;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(row, 2).Style.Font.Bold = true;

        // Fuel Summary
        row += 2;
        ws.Cell(row, 1).Value = "FUEL SUMMARY";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 14;
        ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.LightYellow;
        ws.Range(row, 1, row, 3).Merge();

        row++;
        ws.Cell(row, 1).Value = "Fuel Received:";
        ws.Cell(row, 2).Value = fuelData.TotalReceived;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.0 L";
        row++;
        ws.Cell(row, 1).Value = "Total Usage:";
        ws.Cell(row, 2).Value = fuelData.TotalUsage;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.0 L";
        row++;
        ws.Cell(row, 1).Value = "Current Balance:";
        ws.Cell(row, 2).Value = fuelData.CurrentBalance;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.0 L";
        ws.Cell(row, 2).Style.Font.FontColor = XLColor.Green;

        // Banking Summary
        row += 2;
        ws.Cell(row, 1).Value = "BANKING SUMMARY";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 14;
        ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.LightSkyBlue;
        ws.Range(row, 1, row, 3).Merge();

        row++;
        ws.Cell(row, 1).Value = "Total Banked:";
        ws.Cell(row, 2).Value = bankingData.TotalBanked;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(row, 2).Style.Font.Bold = true;
        row++;
        ws.Cell(row, 1).Value = "Transactions:";
        ws.Cell(row, 2).Value = bankingData.TotalRecords;
        row++;
        ws.Cell(row, 1).Value = "Average/Day:";
        ws.Cell(row, 2).Value = bankingData.AveragePerDay;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";

        // Collections Summary (payments received for older unpaid sales)
        if (salesData.CollectionItems.Count > 0)
        {
            row += 2;
            ws.Cell(row, 1).Value = "COLLECTIONS SUMMARY";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 14;
            ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.MediumPurple;
            ws.Cell(row, 1).Style.Font.FontColor = XLColor.White;
            ws.Range(row, 1, row, 3).Merge();

            row++;
            ws.Cell(row, 1).Value = "Total Collections:";
            ws.Cell(row, 2).Value = salesData.TotalCollections;
            ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 2).Style.Font.Bold = true;
            ws.Cell(row, 2).Style.Font.FontColor = XLColor.MediumPurple;
            row++;
            ws.Cell(row, 1).Value = "Number of Payments:";
            ws.Cell(row, 2).Value = salesData.CollectionItems.Count;

            // Detailed collections table
            row += 2;
            ws.Cell(row, 1).Value = "Sale Date";
            ws.Cell(row, 2).Value = "Paid On";
            ws.Cell(row, 3).Value = "Vehicle";
            ws.Cell(row, 4).Value = "Client";
            ws.Cell(row, 5).Value = "Product";
            ws.Cell(row, 6).Value = "Qty";
            ws.Cell(row, 7).Value = "Amount";
            ws.Range(row, 1, row, 7).Style.Font.Bold = true;
            ws.Range(row, 1, row, 7).Style.Fill.BackgroundColor = XLColor.MediumPurple;
            ws.Range(row, 1, row, 7).Style.Font.FontColor = XLColor.White;

            foreach (var collection in salesData.CollectionItems)
            {
                row++;
                ws.Cell(row, 1).Value = collection.OriginalSaleDate.ToString("dd/MM/yyyy");
                ws.Cell(row, 2).Value = collection.PaymentReceivedDate.ToString("dd/MM/yyyy");
                ws.Cell(row, 3).Value = collection.VehicleRegistration;
                ws.Cell(row, 4).Value = collection.ClientName ?? "-";
                ws.Cell(row, 5).Value = collection.ProductName;
                ws.Cell(row, 6).Value = collection.Quantity;
                ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0";
                ws.Cell(row, 7).Value = collection.Amount;
                ws.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";
            }
        }

        // Net Summary
        row += 2;
        ws.Cell(row, 1).Value = "NET SUMMARY";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 14;
        ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.Lavender;
        ws.Range(row, 1, row, 3).Merge();

        // Net Income formula: (TotalSales - UnpaidAmount - TotalExpenses) + OpeningBalance + Collections + Prepayments
        var earnings = salesData.TotalSales - expensesData.TotalExpenses;
        var netIncome = (salesData.TotalSales - salesData.UnpaidAmount - expensesData.TotalExpenses) + salesData.OpeningBalance + salesData.TotalCollections + salesData.TotalPrepayments;
        var cashInHandEnd = netIncome - bankingData.TotalBanked;

        row++;
        ws.Cell(row, 1).Value = "Opening Balance:";
        ws.Cell(row, 2).Value = salesData.ActualOpeningBalance;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
        row++;
        ws.Cell(row, 1).Value = "Cash In Hand (B/F):";
        ws.Cell(row, 2).Value = salesData.OpeningBalance;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
        row++;
        ws.Cell(row, 1).Value = "Earnings (Sales - Expenses):";
        ws.Cell(row, 2).Value = earnings;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
        row++;
        ws.Cell(row, 1).Value = "Collections (Previous Unpaid):";
        ws.Cell(row, 2).Value = salesData.TotalCollections;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(row, 2).Style.Font.FontColor = salesData.TotalCollections > 0 ? XLColor.MediumPurple : XLColor.Black;
        row++;
        ws.Cell(row, 1).Value = "Prepayments Received:";
        ws.Cell(row, 2).Value = salesData.TotalPrepayments;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(row, 2).Style.Font.FontColor = salesData.TotalPrepayments > 0 ? XLColor.Green : XLColor.Black;
        row++;
        ws.Cell(row, 1).Value = "Unpaid Orders:";
        ws.Cell(row, 2).Value = salesData.UnpaidAmount;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(row, 2).Style.Font.FontColor = salesData.UnpaidAmount > 0 ? XLColor.Red : XLColor.Black;
        row++;
        ws.Cell(row, 1).Value = "Net Income:";
        ws.Cell(row, 2).Value = netIncome;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(row, 2).Style.Font.Bold = true;
        ws.Cell(row, 2).Style.Font.FontColor = netIncome >= 0 ? XLColor.Green : XLColor.Red;
        row++;
        ws.Cell(row, 1).Value = "Banked:";
        ws.Cell(row, 2).Value = bankingData.TotalBanked;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
        row++;
        ws.Cell(row, 1).Value = "Cash in Hand (End):";
        ws.Cell(row, 2).Value = cashInHandEnd;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(row, 2).Style.Font.Bold = true;

        // Formula explanation
        row += 2;
        ws.Cell(row, 1).Value = "Formula: Net Income = (Earnings + Cash In Hand B/F + Collections + Prepayments) - Unpaid Orders";
        ws.Cell(row, 1).Style.Font.Italic = true;
        ws.Cell(row, 1).Style.Font.FontColor = XLColor.Gray;
        ws.Range(row, 1, row, 4).Merge();

        ws.Columns().AdjustToContents();
    }

    /// <summary>
    /// Export report to PDF using QuestPDF - Comprehensive landscape report with stats cards
    /// </summary>
    public async Task<byte[]> ExportToPdfAsync(string? quarryId, DateTime fromDate, DateTime toDate, string reportType)
    {
        // Route to type-specific PDF generation based on reportType
        switch (reportType.ToLower())
        {
            case "fuel":
                return await ExportFuelPdfAsync(quarryId, fromDate, toDate);
            case "banking":
                return await ExportBankingPdfAsync(quarryId, fromDate, toDate);
            case "sales":
                return await ExportSalesPdfAsync(quarryId, fromDate, toDate);
            case "expenses":
                return await ExportExpensesPdfAsync(quarryId, fromDate, toDate);
            case "comprehensive":
            default:
                // Fall through to existing comprehensive implementation
                break;
        }

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var salesData = await GetSalesReportAsync(quarryId, fromDate, toDate);
        var expensesData = await GetExpensesReportAsync(quarryId, fromDate, toDate);
        var fuelData = await GetFuelReportAsync(quarryId, fromDate, toDate);
        var bankingData = await GetBankingReportAsync(quarryId, fromDate, toDate);

        // Get quarry name
        var quarryName = "All Quarries";
        if (!string.IsNullOrEmpty(quarryId))
        {
            var quarry = await context.Quarries.FindAsync(quarryId);
            quarryName = quarry?.QuarryName ?? "Unknown";
        }

        // Get individual sales for detailed breakdown
        var fromStamp = fromDate.ToString("yyyyMMdd");
        var toStamp = toDate.ToString("yyyyMMdd");
        var salesQuery = context.Sales
            .Where(s => string.Compare(s.DateStamp, fromStamp) >= 0)
            .Where(s => string.Compare(s.DateStamp, toStamp) <= 0)
            .Where(s => s.IsActive);

        if (!string.IsNullOrEmpty(quarryId))
        {
            salesQuery = salesQuery.Where(s => s.QId == quarryId);
        }

        var individualSales = await salesQuery
            .Include(s => s.Product)
            .Include(s => s.Broker)
            .OrderBy(s => s.SaleDate)
            .ThenBy(s => s.DateCreated)
            .ToListAsync();

        var dateRange = $"{fromDate:dd MMM yyyy} - {toDate:dd MMM yyyy}";
        var totalExpenses = salesData.TotalCommission + salesData.TotalLoadersFee + salesData.TotalLandRateFee + salesData.TotalOtherExpenses;
        var earnings = salesData.TotalSales - totalExpenses;
        var netIncome = (salesData.TotalSales - salesData.UnpaidAmount - totalExpenses) + salesData.OpeningBalance + salesData.TotalCollections + salesData.TotalPrepayments;
        var cashInHand = netIncome - bankingData.TotalBanked;
        var profitMargin = salesData.TotalSales > 0 ? (earnings / salesData.TotalSales) * 100 : 0;

        // Brand colors
        var primaryColor = "#1976D2";
        var successColor = "#4CAF50";
        var errorColor = "#F44336";
        var warningColor = "#FF9800";
        var infoColor = "#2196F3";

        // Generate PDF using QuestPDF - LANDSCAPE FORMAT
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());  // LANDSCAPE
                page.Margin(25);
                page.DefaultTextStyle(x => x.FontSize(9));

                // HEADER with Stats Cards
                page.Header().Element(header =>
                {
                    header.Column(col =>
                    {
                        // Title row with logo area
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(titleCol =>
                            {
                                titleCol.Item().Text("QDesk Sales Report")
                                    .FontSize(22).Bold().FontColor(primaryColor);
                                titleCol.Item().Text($"{quarryName}")
                                    .FontSize(14).FontColor(Colors.Grey.Darken2);
                                titleCol.Item().Text($"Period: {dateRange} | Generated: {DateTime.Now:dd MMM yyyy HH:mm}")
                                    .FontSize(9).FontColor(Colors.Grey.Medium);
                            });
                        });

                        col.Item().PaddingTop(10);

                        // STATS CARDS ROW - 6 cards across
                        col.Item().Row(row =>
                        {
                            // Card 1: Total Revenue
                            row.RelativeItem().Padding(3).Element(e => ComposeStatsCard(e,
                                "Total Revenue", $"KES {salesData.TotalSales:N0}",
                                $"{salesData.TotalOrders} orders", successColor, true));

                            // Card 2: Total Expenses
                            row.RelativeItem().Padding(3).Element(e => ComposeStatsCard(e,
                                "Total Expenses", $"KES {totalExpenses:N0}",
                                $"Commission + Fees", errorColor, false));

                            // Card 3: Net Earnings
                            row.RelativeItem().Padding(3).Element(e => ComposeStatsCard(e,
                                "Earnings", $"KES {earnings:N0}",
                                $"{profitMargin:N1}% margin", earnings >= 0 ? successColor : errorColor, true));

                            // Card 4: Unpaid Orders
                            row.RelativeItem().Padding(3).Element(e => ComposeStatsCard(e,
                                "Unpaid Orders", $"KES {salesData.UnpaidAmount:N0}",
                                "Outstanding", salesData.UnpaidAmount > 0 ? warningColor : successColor, false));

                            // Card 5: Total Banked
                            row.RelativeItem().Padding(3).Element(e => ComposeStatsCard(e,
                                "Total Banked", $"KES {bankingData.TotalBanked:N0}",
                                $"{bankingData.TotalRecords} deposits", infoColor, true));

                            // Card 6: Cash in Hand (End) - final balance after banking
                            row.RelativeItem().Padding(3).Element(e => ComposeStatsCard(e,
                                "Cash in Hand (End)", $"KES {cashInHand:N0}",
                                "Final Balance", cashInHand >= 0 ? successColor : errorColor, true));
                        });

                        col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    });
                });

                // CONTENT
                page.Content().Element(content =>
                {
                    content.PaddingTop(10).Column(col =>
                    {
                        // Net Summary Box - Main highlight
                        col.Item().Background(Colors.Grey.Lighten4).Border(1).BorderColor(Colors.Grey.Lighten2)
                            .Padding(12).Row(summaryRow =>
                        {
                            summaryRow.RelativeItem().Column(sumCol =>
                            {
                                sumCol.Item().Text("FINANCIAL SUMMARY").Bold().FontSize(11);
                                // First row: Opening Balance and Total Sales
                                sumCol.Item().PaddingTop(5).Row(r =>
                                {
                                    r.RelativeItem().Text(t => {
                                        t.Span("Opening Balance: ").Bold();
                                        t.Span($"KES {salesData.ActualOpeningBalance:N2}");
                                    });
                                    r.RelativeItem().Text(t => {
                                        t.Span("Total Sales: ").Bold();
                                        t.Span($"KES {salesData.TotalSales:N2}");
                                    });
                                    r.RelativeItem().Text(t => {
                                        t.Span("Total Qty: ").Bold();
                                        t.Span($"{salesData.TotalQuantity:N0} pcs");
                                    });
                                });
                                // Second row: Cash In Hand (B/F), Collections and Prepayments
                                sumCol.Item().PaddingTop(3).Row(r =>
                                {
                                    r.RelativeItem().Text(t => {
                                        t.Span("Cash In Hand (B/F): ").Bold();
                                        t.Span($"KES {salesData.OpeningBalance:N2}");
                                    });
                                    r.RelativeItem().Text(t => {
                                        t.Span("Collections: ").Bold();
                                        t.Span($"KES {salesData.TotalCollections:N2}").FontColor(salesData.TotalCollections > 0 ? successColor : Colors.Black);
                                    });
                                    r.RelativeItem().Text(t => {
                                        t.Span("Prepayments: ").Bold();
                                        t.Span($"KES {salesData.TotalPrepayments:N2}").FontColor(salesData.TotalPrepayments > 0 ? successColor : Colors.Black);
                                    });
                                });

                                // Third row: Unpaid Orders
                                sumCol.Item().PaddingTop(3).Row(r =>
                                {
                                    r.RelativeItem().Text(t => {
                                        t.Span("Unpaid Orders: ").Bold();
                                        t.Span($"KES {salesData.UnpaidAmount:N2}").FontColor(salesData.UnpaidAmount > 0 ? warningColor : Colors.Black);
                                    });
                                });
                            });
                            summaryRow.ConstantItem(220).Column(netCol =>
                            {
                                // Net Income with formula in smaller font
                                netCol.Item().AlignRight().Text("Net Income").Bold().FontSize(10);
                                netCol.Item().AlignRight().Text("(Earnings + Cash In Hand + Collections + Prepayments - Unpaid)")
                                    .FontSize(7).FontColor(Colors.Grey.Darken1);
                                netCol.Item().AlignRight().Text($"KES {netIncome:N2}")
                                    .Bold().FontSize(14)
                                    .FontColor(netIncome >= 0 ? successColor : errorColor);
                            });
                        });

                        // Two-column layout for detailed breakdowns
                        col.Item().PaddingTop(10).Row(mainRow =>
                        {
                            // LEFT COLUMN - Sales Breakdown
                            mainRow.RelativeItem(3).Padding(3).Column(leftCol =>
                            {
                                // Product Breakdown Summary
                                leftCol.Item().Element(e => SectionHeader(e, "Product Summary", successColor));
                                if (salesData.ProductBreakdown.Count > 0)
                                {
                                    leftCol.Item().Table(table =>
                                    {
                                        table.ColumnsDefinition(columns =>
                                        {
                                            columns.RelativeColumn(2);
                                            columns.RelativeColumn(1);
                                            columns.RelativeColumn(1);
                                            columns.RelativeColumn(1.5f);
                                        });

                                        table.Header(header =>
                                        {
                                            header.Cell().Background(successColor).Padding(4)
                                                .Text("Product").FontColor(Colors.White).Bold().FontSize(8);
                                            header.Cell().Background(successColor).Padding(4)
                                                .Text("Orders").FontColor(Colors.White).Bold().FontSize(8);
                                            header.Cell().Background(successColor).Padding(4)
                                                .Text("Qty").FontColor(Colors.White).Bold().FontSize(8);
                                            header.Cell().Background(successColor).Padding(4)
                                                .Text("Revenue").FontColor(Colors.White).Bold().FontSize(8);
                                        });

                                        foreach (var product in salesData.ProductBreakdown)
                                        {
                                            var idx = salesData.ProductBreakdown.IndexOf(product);
                                            var bgColor = idx % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;

                                            table.Cell().Background(bgColor).Padding(3).Text(product.ProductName).FontSize(8);
                                            table.Cell().Background(bgColor).Padding(3).Text($"{product.OrderCount}").FontSize(8);
                                            table.Cell().Background(bgColor).Padding(3).Text($"{product.Quantity:N0}").FontSize(8);
                                            table.Cell().Background(bgColor).Padding(3).Text($"KES {product.Revenue:N0}").FontSize(8);
                                        }
                                    });
                                }

                                // Expense Breakdown Summary
                                leftCol.Item().PaddingTop(8).Element(e => SectionHeader(e, "Expense Breakdown", errorColor));
                                leftCol.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(1.5f);
                                    });

                                    void AddExpenseRow(string label, double amount, string color)
                                    {
                                        table.Cell().Padding(3).Text(label).FontSize(8);
                                        table.Cell().Padding(3).AlignRight().Text($"KES {amount:N0}").FontSize(8);
                                    }

                                    table.Cell().Background(errorColor).Padding(4).Text("Category").FontColor(Colors.White).Bold().FontSize(8);
                                    table.Cell().Background(errorColor).Padding(4).AlignRight().Text("Amount").FontColor(Colors.White).Bold().FontSize(8);

                                    AddExpenseRow("Commission", expensesData.Commission, "");
                                    AddExpenseRow("Loaders Fee", expensesData.LoadersFee, "");
                                    AddExpenseRow("Land Rate", expensesData.LandRateFee, "");
                                    AddExpenseRow("Other Expenses", expensesData.ManualExpenses, "");

                                    table.Cell().Background(Colors.Grey.Lighten3).Padding(3).Text("TOTAL").Bold().FontSize(8);
                                    table.Cell().Background(Colors.Grey.Lighten3).Padding(3).AlignRight()
                                        .Text($"KES {expensesData.TotalExpenses:N0}").Bold().FontSize(8);
                                });
                            });

                            // RIGHT COLUMN - Fuel & Banking
                            mainRow.RelativeItem(2).Padding(3).Column(rightCol =>
                            {
                                // Fuel Summary
                                rightCol.Item().Element(e => SectionHeader(e, "Fuel Usage", warningColor));
                                rightCol.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(1.5f);
                                    });

                                    table.Cell().Background(warningColor).Padding(4).Text("Item").FontColor(Colors.White).Bold().FontSize(8);
                                    table.Cell().Background(warningColor).Padding(4).AlignRight().Text("Liters").FontColor(Colors.White).Bold().FontSize(8);

                                    table.Cell().Padding(3).Text("Fuel Received").FontSize(8);
                                    table.Cell().Padding(3).AlignRight().Text($"{fuelData.TotalReceived:N1} L").FontSize(8);
                                    table.Cell().Background(Colors.Grey.Lighten4).Padding(3).Text("Machines Usage").FontSize(8);
                                    table.Cell().Background(Colors.Grey.Lighten4).Padding(3).AlignRight().Text($"{fuelData.MachinesUsage:N1} L").FontSize(8);
                                    table.Cell().Padding(3).Text("W/Loaders Usage").FontSize(8);
                                    table.Cell().Padding(3).AlignRight().Text($"{fuelData.WheelLoadersUsage:N1} L").FontSize(8);
                                    table.Cell().Background(successColor).Padding(3).Text("Balance").FontColor(Colors.White).Bold().FontSize(8);
                                    table.Cell().Background(successColor).Padding(3).AlignRight().Text($"{fuelData.CurrentBalance:N1} L").FontColor(Colors.White).Bold().FontSize(8);
                                });

                                // Banking Summary
                                rightCol.Item().PaddingTop(8).Element(e => SectionHeader(e, "Banking Records", infoColor));
                                if (bankingData.BankingRecords.Count > 0)
                                {
                                    rightCol.Item().Table(table =>
                                    {
                                        table.ColumnsDefinition(columns =>
                                        {
                                            columns.RelativeColumn(1);
                                            columns.RelativeColumn(1.5f);
                                            columns.RelativeColumn(1.2f);
                                        });

                                        table.Header(header =>
                                        {
                                            header.Cell().Background(infoColor).Padding(4).Text("Date").FontColor(Colors.White).Bold().FontSize(8);
                                            header.Cell().Background(infoColor).Padding(4).Text("Reference").FontColor(Colors.White).Bold().FontSize(8);
                                            header.Cell().Background(infoColor).Padding(4).AlignRight().Text("Amount").FontColor(Colors.White).Bold().FontSize(8);
                                        });

                                        foreach (var banking in bankingData.BankingRecords.Take(10))
                                        {
                                            var idx = bankingData.BankingRecords.IndexOf(banking);
                                            var bgColor = idx % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;

                                            table.Cell().Background(bgColor).Padding(2).Text(banking.Date.ToString("dd/MM")).FontSize(7);
                                            table.Cell().Background(bgColor).Padding(2).Text(banking.Reference.Length > 15 ? banking.Reference.Substring(0, 15) + ".." : banking.Reference).FontSize(7);
                                            table.Cell().Background(bgColor).Padding(2).AlignRight().Text($"KES {banking.Amount:N0}").FontSize(7);
                                        }

                                        table.Cell().ColumnSpan(2).Background(Colors.Grey.Lighten3).Padding(3).Text("TOTAL").Bold().FontSize(8);
                                        table.Cell().Background(Colors.Grey.Lighten3).Padding(3).AlignRight().Text($"KES {bankingData.TotalBanked:N0}").Bold().FontSize(8);
                                    });
                                }
                            });
                        });

                        // PAGE BREAK - Detailed Sales Orders
                        col.Item().PageBreak();
                        col.Item().Element(e => SectionHeader(e, "Detailed Sales Orders", primaryColor));

                        // Group sales by date
                        var salesByDate = individualSales.GroupBy(s => s.SaleDate?.Date ?? DateTime.Today).OrderBy(g => g.Key);

                        foreach (var dateGroup in salesByDate)
                        {
                            // Date header
                            col.Item().PaddingTop(8).Background(Colors.Grey.Lighten3).Padding(5).Row(dateRow =>
                            {
                                dateRow.RelativeItem().Text($"{dateGroup.Key:dddd, dd MMMM yyyy}").Bold().FontSize(10);
                                dateRow.ConstantItem(150).AlignRight().Text($"{dateGroup.Count()} orders | KES {dateGroup.Sum(s => s.GrossSaleAmount):N0}").FontSize(9);
                            });

                            // Sales table for this date
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(70);   // Vehicle
                                    columns.RelativeColumn(1.5f); // Client
                                    columns.RelativeColumn(1);   // Product
                                    columns.ConstantColumn(50);   // Qty
                                    columns.ConstantColumn(60);   // Price
                                    columns.ConstantColumn(80);   // Amount
                                    columns.RelativeColumn(1);   // Payment
                                    columns.ConstantColumn(50);   // Status
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Background(primaryColor).Padding(3).Text("Vehicle").FontColor(Colors.White).Bold().FontSize(7);
                                    header.Cell().Background(primaryColor).Padding(3).Text("Client").FontColor(Colors.White).Bold().FontSize(7);
                                    header.Cell().Background(primaryColor).Padding(3).Text("Product").FontColor(Colors.White).Bold().FontSize(7);
                                    header.Cell().Background(primaryColor).Padding(3).Text("Qty").FontColor(Colors.White).Bold().FontSize(7);
                                    header.Cell().Background(primaryColor).Padding(3).Text("Price").FontColor(Colors.White).Bold().FontSize(7);
                                    header.Cell().Background(primaryColor).Padding(3).Text("Amount").FontColor(Colors.White).Bold().FontSize(7);
                                    header.Cell().Background(primaryColor).Padding(3).Text("Payment").FontColor(Colors.White).Bold().FontSize(7);
                                    header.Cell().Background(primaryColor).Padding(3).Text("Status").FontColor(Colors.White).Bold().FontSize(7);
                                });

                                foreach (var sale in dateGroup)
                                {
                                    var saleIdx = dateGroup.ToList().IndexOf(sale);
                                    var bgColor = saleIdx % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
                                    var isPaid = sale.PaymentStatus == "Paid";

                                    table.Cell().Background(bgColor).Padding(2).Text(sale.VehicleRegistration ?? "").FontSize(7);
                                    table.Cell().Background(bgColor).Padding(2).Text(sale.ClientName ?? "-").FontSize(7);
                                    table.Cell().Background(bgColor).Padding(2).Text(sale.Product?.ProductName ?? "").FontSize(7);
                                    table.Cell().Background(bgColor).Padding(2).Text($"{sale.Quantity:N0}").FontSize(7);
                                    table.Cell().Background(bgColor).Padding(2).Text($"{sale.PricePerUnit:N0}").FontSize(7);
                                    table.Cell().Background(bgColor).Padding(2).Text($"KES {sale.GrossSaleAmount:N0}").FontSize(7);
                                    table.Cell().Background(bgColor).Padding(2).Text(sale.PaymentMode ?? "").FontSize(7);
                                    table.Cell().Background(isPaid ? Colors.Green.Lighten4 : Colors.Red.Lighten4).Padding(2)
                                        .Text(isPaid ? "PAID" : "UNPAID").FontSize(7)
                                        .FontColor(isPaid ? successColor : errorColor);
                                }
                            });
                        }

                        // PAGE BREAK - Detailed Expenses
                        if (expensesData.ExpenseItems.Count > 0)
                        {
                            col.Item().PageBreak();
                            col.Item().Element(e => SectionHeader(e, "Detailed Expense Records", errorColor));

                            // Group expenses by date
                            var expensesByDate = expensesData.ExpenseItems.GroupBy(e => e.Date.Date).OrderBy(g => g.Key);

                            foreach (var dateGroup in expensesByDate)
                            {
                                // Date header
                                col.Item().PaddingTop(8).Background(Colors.Grey.Lighten3).Padding(5).Row(dateRow =>
                                {
                                    dateRow.RelativeItem().Text($"{dateGroup.Key:dddd, dd MMMM yyyy}").Bold().FontSize(10);
                                    dateRow.ConstantItem(150).AlignRight().Text($"{dateGroup.Count()} items | KES {dateGroup.Sum(e => e.Amount):N0}").FontSize(9);
                                });

                                // Expenses table for this date
                                col.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(3);    // Description
                                        columns.RelativeColumn(1);    // Category
                                        columns.ConstantColumn(100);   // Amount
                                    });

                                    table.Header(header =>
                                    {
                                        header.Cell().Background(errorColor).Padding(4).Text("Description").FontColor(Colors.White).Bold().FontSize(8);
                                        header.Cell().Background(errorColor).Padding(4).Text("Category").FontColor(Colors.White).Bold().FontSize(8);
                                        header.Cell().Background(errorColor).Padding(4).AlignRight().Text("Amount").FontColor(Colors.White).Bold().FontSize(8);
                                    });

                                    foreach (var expense in dateGroup)
                                    {
                                        var expIdx = dateGroup.ToList().IndexOf(expense);
                                        var bgColor = expIdx % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;

                                        table.Cell().Background(bgColor).Padding(3).Text(expense.Description.Length > 70 ? expense.Description.Substring(0, 70) + "..." : expense.Description).FontSize(8);
                                        table.Cell().Background(bgColor).Padding(3).Text(expense.Category).FontSize(8);
                                        table.Cell().Background(bgColor).Padding(3).AlignRight().Text($"KES {expense.Amount:N0}").FontSize(8);
                                    }
                                });
                            }

                            // Total row at the end
                            col.Item().PaddingTop(5).Background(Colors.Grey.Lighten3).Padding(5).Row(totalRow =>
                            {
                                totalRow.RelativeItem().Text("TOTAL EXPENSES").Bold().FontSize(10);
                                totalRow.ConstantItem(150).AlignRight().Text($"KES {expensesData.TotalExpenses:N0}").Bold().FontSize(10);
                            });
                        }

                        // Detailed Fuel Usage Records
                        if (fuelData.FuelRecords.Count > 0)
                        {
                            col.Item().PaddingTop(15).Element(e => SectionHeader(e, "Detailed Fuel Usage Records", warningColor));

                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(70);    // Date
                                    columns.RelativeColumn(1);   // Old Stock
                                    columns.RelativeColumn(1);   // New Stock
                                    columns.RelativeColumn(1);   // Total
                                    columns.RelativeColumn(1);   // Machines
                                    columns.RelativeColumn(1);   // W/Loaders
                                    columns.RelativeColumn(1);   // Balance
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Background(warningColor).Padding(4).Text("Date").FontColor(Colors.White).Bold().FontSize(8);
                                    header.Cell().Background(warningColor).Padding(4).AlignRight().Text("Old Stock").FontColor(Colors.White).Bold().FontSize(8);
                                    header.Cell().Background(warningColor).Padding(4).AlignRight().Text("New Stock").FontColor(Colors.White).Bold().FontSize(8);
                                    header.Cell().Background(warningColor).Padding(4).AlignRight().Text("Total").FontColor(Colors.White).Bold().FontSize(8);
                                    header.Cell().Background(warningColor).Padding(4).AlignRight().Text("Machines").FontColor(Colors.White).Bold().FontSize(8);
                                    header.Cell().Background(warningColor).Padding(4).AlignRight().Text("W/Loaders").FontColor(Colors.White).Bold().FontSize(8);
                                    header.Cell().Background(warningColor).Padding(4).AlignRight().Text("Balance").FontColor(Colors.White).Bold().FontSize(8);
                                });

                                foreach (var fuel in fuelData.FuelRecords)
                                {
                                    var idx = fuelData.FuelRecords.IndexOf(fuel);
                                    var bgColor = idx % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;

                                    table.Cell().Background(bgColor).Padding(3).Text(fuel.Date.ToString("dd/MM/yy")).FontSize(8);
                                    table.Cell().Background(bgColor).Padding(3).AlignRight().Text($"{fuel.OldStock:N1}").FontSize(8);
                                    table.Cell().Background(bgColor).Padding(3).AlignRight().Text($"{fuel.NewStock:N1}").FontSize(8);
                                    table.Cell().Background(bgColor).Padding(3).AlignRight().Text($"{fuel.TotalStock:N1}").FontSize(8);
                                    table.Cell().Background(bgColor).Padding(3).AlignRight().Text($"{fuel.MachinesLoaded:N1}").FontSize(8);
                                    table.Cell().Background(bgColor).Padding(3).AlignRight().Text($"{fuel.WheelLoadersLoaded:N1}").FontSize(8);
                                    table.Cell().Background(successColor).Padding(3).AlignRight().Text($"{fuel.Balance:N1}").FontColor(Colors.White).Bold().FontSize(8);
                                }
                            });
                        }

                        // Detailed Collections Records (payments received for older unpaid sales)
                        if (salesData.CollectionItems.Count > 0)
                        {
                            var collectionsColor = "#9C27B0"; // Purple for collections
                            col.Item().PaddingTop(15).Element(e => SectionHeader(e, "Collections (Payments for Previous Unpaid Orders)", collectionsColor));

                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(70);    // Original Sale Date
                                    columns.ConstantColumn(70);    // Payment Received
                                    columns.ConstantColumn(80);    // Vehicle
                                    columns.RelativeColumn(1.5f);  // Client
                                    columns.RelativeColumn(1);     // Product
                                    columns.ConstantColumn(50);    // Qty
                                    columns.ConstantColumn(90);    // Amount
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Background(collectionsColor).Padding(4).Text("Sale Date").FontColor(Colors.White).Bold().FontSize(7);
                                    header.Cell().Background(collectionsColor).Padding(4).Text("Paid On").FontColor(Colors.White).Bold().FontSize(7);
                                    header.Cell().Background(collectionsColor).Padding(4).Text("Vehicle").FontColor(Colors.White).Bold().FontSize(7);
                                    header.Cell().Background(collectionsColor).Padding(4).Text("Client").FontColor(Colors.White).Bold().FontSize(7);
                                    header.Cell().Background(collectionsColor).Padding(4).Text("Product").FontColor(Colors.White).Bold().FontSize(7);
                                    header.Cell().Background(collectionsColor).Padding(4).Text("Qty").FontColor(Colors.White).Bold().FontSize(7);
                                    header.Cell().Background(collectionsColor).Padding(4).AlignRight().Text("Amount").FontColor(Colors.White).Bold().FontSize(7);
                                });

                                foreach (var collection in salesData.CollectionItems)
                                {
                                    var idx = salesData.CollectionItems.IndexOf(collection);
                                    var bgColor = idx % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;

                                    table.Cell().Background(bgColor).Padding(3).Text(collection.OriginalSaleDate.ToString("dd/MM/yy")).FontSize(7);
                                    table.Cell().Background(bgColor).Padding(3).Text(collection.PaymentReceivedDate.ToString("dd/MM/yy")).FontSize(7);
                                    table.Cell().Background(bgColor).Padding(3).Text(collection.VehicleRegistration).FontSize(7);
                                    table.Cell().Background(bgColor).Padding(3).Text(collection.ClientName ?? "-").FontSize(7);
                                    table.Cell().Background(bgColor).Padding(3).Text(collection.ProductName).FontSize(7);
                                    table.Cell().Background(bgColor).Padding(3).Text($"{collection.Quantity:N0}").FontSize(7);
                                    table.Cell().Background(bgColor).Padding(3).AlignRight().Text($"KES {collection.Amount:N0}").FontSize(7);
                                }

                                // Total row
                                table.Cell().ColumnSpan(6).Background(Colors.Grey.Lighten3).Padding(3).Text("TOTAL COLLECTIONS").Bold().FontSize(8);
                                table.Cell().Background(Colors.Grey.Lighten3).Padding(3).AlignRight().Text($"KES {salesData.TotalCollections:N0}").Bold().FontSize(8);
                            });
                        }
                    });
                });

                // FOOTER
                page.Footer().Element(footer =>
                {
                    footer.Row(row =>
                    {
                        row.RelativeItem().Text("QDesk Sales Report - Quarry Management System")
                            .FontSize(8).FontColor(Colors.Grey.Darken1);
                        row.RelativeItem().AlignCenter().Text(text =>
                        {
                            text.DefaultTextStyle(TextStyle.Default.FontSize(8).FontColor(Colors.Grey.Darken1));
                            text.Span("Page ");
                            text.CurrentPageNumber();
                            text.Span(" of ");
                            text.TotalPages();
                        });
                        row.RelativeItem().AlignRight().Text($"Generated: {DateTime.Now:dd/MM/yyyy HH:mm}")
                            .FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                });
            });
        });

        using var stream = new MemoryStream();
        document.GeneratePdf(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Compose a stats card for the PDF header
    /// </summary>
    private static void ComposeStatsCard(IContainer container, string title, string value, string subtitle, string color, bool isPositive)
    {
        container.Border(1).BorderColor(color).Background(Colors.White).Padding(8).Column(col =>
        {
            col.Item().Text(title).FontSize(8).FontColor(Colors.Grey.Darken1);
            col.Item().PaddingTop(2).Text(value).FontSize(12).Bold().FontColor(color);
            col.Item().Text(subtitle).FontSize(7).FontColor(Colors.Grey.Medium);
        });
    }

    private static void SectionHeader(IContainer container, string title, string color)
    {
        container.PaddingTop(8).PaddingBottom(5).Row(row =>
        {
            row.AutoItem().Width(4).Height(16).Background(color);
            row.RelativeItem().PaddingLeft(8).Text(title).FontSize(11).Bold().FontColor(color);
        });
    }

    #region Type-Specific PDF Export Methods

    /// <summary>
    /// Export Fuel Usage report to PDF
    /// </summary>
    private async Task<byte[]> ExportFuelPdfAsync(string? quarryId, DateTime fromDate, DateTime toDate)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var fuelData = await GetFuelReportAsync(quarryId, fromDate, toDate);
        var quarryName = "All Quarries";
        if (!string.IsNullOrEmpty(quarryId))
        {
            var quarry = await context.Quarries.FindAsync(quarryId);
            quarryName = quarry?.QuarryName ?? "Unknown";
        }

        var dateRange = $"{fromDate:dd MMM yyyy} - {toDate:dd MMM yyyy}";
        var warningColor = "#FF9800";
        var successColor = "#4CAF50";
        var primaryColor = "#1976D2";

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10));

                // Header
                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(titleCol =>
                        {
                            titleCol.Item().Text("QDesk Fuel Usage Report")
                                .FontSize(20).Bold().FontColor(primaryColor);
                            titleCol.Item().Text($"{quarryName}")
                                .FontSize(12).FontColor(Colors.Grey.Darken2);
                            titleCol.Item().Text($"Period: {dateRange} | Generated: {DateTime.Now:dd MMM yyyy HH:mm}")
                                .FontSize(9).FontColor(Colors.Grey.Medium);
                        });
                    });
                    col.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                // Content
                page.Content().PaddingTop(15).Column(col =>
                {
                    // Summary Cards
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Padding(5).Element(e => ComposeStatsCard(e,
                            "Total Received", $"{fuelData.TotalReceived:N1} L",
                            "Fuel received", successColor, true));
                        row.RelativeItem().Padding(5).Element(e => ComposeStatsCard(e,
                            "Machines Usage", $"{fuelData.MachinesUsage:N1} L",
                            "Excavators", warningColor, false));
                        row.RelativeItem().Padding(5).Element(e => ComposeStatsCard(e,
                            "W/Loaders Usage", $"{fuelData.WheelLoadersUsage:N1} L",
                            "Wheel loaders", warningColor, false));
                        row.RelativeItem().Padding(5).Element(e => ComposeStatsCard(e,
                            "Current Balance", $"{fuelData.CurrentBalance:N1} L",
                            "Available", successColor, true));
                    });

                    col.Item().PaddingTop(15);

                    // Fuel Records Table
                    if (fuelData.FuelRecords.Count > 0)
                    {
                        col.Item().Element(e => SectionHeader(e, "Fuel Usage Details", warningColor));

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(80);  // Date
                                columns.RelativeColumn(1);   // Old Stock
                                columns.RelativeColumn(1);   // New Stock
                                columns.RelativeColumn(1);   // Total
                                columns.RelativeColumn(1);   // Machines
                                columns.RelativeColumn(1);   // W/Loaders
                                columns.RelativeColumn(1);   // Balance
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background(warningColor).Padding(5).Text("Date").FontColor(Colors.White).Bold();
                                header.Cell().Background(warningColor).Padding(5).AlignRight().Text("Old Stock").FontColor(Colors.White).Bold();
                                header.Cell().Background(warningColor).Padding(5).AlignRight().Text("New Stock").FontColor(Colors.White).Bold();
                                header.Cell().Background(warningColor).Padding(5).AlignRight().Text("Total").FontColor(Colors.White).Bold();
                                header.Cell().Background(warningColor).Padding(5).AlignRight().Text("Machines").FontColor(Colors.White).Bold();
                                header.Cell().Background(warningColor).Padding(5).AlignRight().Text("W/Loaders").FontColor(Colors.White).Bold();
                                header.Cell().Background(warningColor).Padding(5).AlignRight().Text("Balance").FontColor(Colors.White).Bold();
                            });

                            foreach (var fuel in fuelData.FuelRecords)
                            {
                                var idx = fuelData.FuelRecords.IndexOf(fuel);
                                var bgColor = idx % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;

                                table.Cell().Background(bgColor).Padding(4).Text(fuel.Date.ToString("dd/MM/yy"));
                                table.Cell().Background(bgColor).Padding(4).AlignRight().Text($"{fuel.OldStock:N1}");
                                table.Cell().Background(bgColor).Padding(4).AlignRight().Text($"{fuel.NewStock:N1}");
                                table.Cell().Background(bgColor).Padding(4).AlignRight().Text($"{fuel.TotalStock:N1}");
                                table.Cell().Background(bgColor).Padding(4).AlignRight().Text($"{fuel.MachinesLoaded:N1}");
                                table.Cell().Background(bgColor).Padding(4).AlignRight().Text($"{fuel.WheelLoadersLoaded:N1}");
                                table.Cell().Background(successColor).Padding(4).AlignRight().Text($"{fuel.Balance:N1}").FontColor(Colors.White).Bold();
                            }
                        });
                    }
                    else
                    {
                        col.Item().PaddingTop(20).AlignCenter().Text("No fuel usage records found for this period.")
                            .FontSize(12).FontColor(Colors.Grey.Medium);
                    }
                });

                // Footer
                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text("QDesk Fuel Usage Report")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                    row.RelativeItem().AlignCenter().Text(text =>
                    {
                        text.DefaultTextStyle(TextStyle.Default.FontSize(8).FontColor(Colors.Grey.Darken1));
                        text.Span("Page ");
                        text.CurrentPageNumber();
                        text.Span(" of ");
                        text.TotalPages();
                    });
                    row.RelativeItem().AlignRight().Text($"Generated: {DateTime.Now:dd/MM/yyyy HH:mm}")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        });

        using var stream = new MemoryStream();
        document.GeneratePdf(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Export Banking report to PDF
    /// </summary>
    private async Task<byte[]> ExportBankingPdfAsync(string? quarryId, DateTime fromDate, DateTime toDate)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var bankingData = await GetBankingReportAsync(quarryId, fromDate, toDate);
        var quarryName = "All Quarries";
        if (!string.IsNullOrEmpty(quarryId))
        {
            var quarry = await context.Quarries.FindAsync(quarryId);
            quarryName = quarry?.QuarryName ?? "Unknown";
        }

        var dateRange = $"{fromDate:dd MMM yyyy} - {toDate:dd MMM yyyy}";
        var primaryColor = "#1976D2";
        var successColor = "#4CAF50";
        var infoColor = "#2196F3";

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10));

                // Header
                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(titleCol =>
                        {
                            titleCol.Item().Text("QDesk Banking Report")
                                .FontSize(20).Bold().FontColor(primaryColor);
                            titleCol.Item().Text($"{quarryName}")
                                .FontSize(12).FontColor(Colors.Grey.Darken2);
                            titleCol.Item().Text($"Period: {dateRange} | Generated: {DateTime.Now:dd MMM yyyy HH:mm}")
                                .FontSize(9).FontColor(Colors.Grey.Medium);
                        });
                    });
                    col.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                // Content
                page.Content().PaddingTop(15).Column(col =>
                {
                    // Summary Cards
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Padding(5).Element(e => ComposeStatsCard(e,
                            "Total Banked", $"KES {bankingData.TotalBanked:N0}",
                            $"{bankingData.TotalRecords} deposits", successColor, true));
                        row.RelativeItem().Padding(5).Element(e => ComposeStatsCard(e,
                            "Average per Day", $"KES {bankingData.AveragePerDay:N0}",
                            "Daily average", infoColor, true));
                        row.RelativeItem().Padding(5).Element(e => ComposeStatsCard(e,
                            "Total Transactions", $"{bankingData.TotalRecords}",
                            "Bank deposits", primaryColor, true));
                    });

                    col.Item().PaddingTop(15);

                    // Banking Records Table
                    if (bankingData.BankingRecords.Count > 0)
                    {
                        col.Item().Element(e => SectionHeader(e, "Banking Details", infoColor));

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(80);  // Date
                                columns.RelativeColumn(2);   // Description
                                columns.RelativeColumn(1.5f);// Reference
                                columns.ConstantColumn(100); // Amount
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background(infoColor).Padding(5).Text("Date").FontColor(Colors.White).Bold();
                                header.Cell().Background(infoColor).Padding(5).Text("Description").FontColor(Colors.White).Bold();
                                header.Cell().Background(infoColor).Padding(5).Text("Reference").FontColor(Colors.White).Bold();
                                header.Cell().Background(infoColor).Padding(5).AlignRight().Text("Amount").FontColor(Colors.White).Bold();
                            });

                            foreach (var banking in bankingData.BankingRecords)
                            {
                                var idx = bankingData.BankingRecords.IndexOf(banking);
                                var bgColor = idx % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;

                                table.Cell().Background(bgColor).Padding(4).Text(banking.Date.ToString("dd/MM/yy"));
                                table.Cell().Background(bgColor).Padding(4).Text(banking.Description ?? "-");
                                table.Cell().Background(bgColor).Padding(4).Text(banking.Reference ?? "-");
                                table.Cell().Background(bgColor).Padding(4).AlignRight().Text($"KES {banking.Amount:N0}");
                            }

                            // Total row
                            table.Cell().ColumnSpan(3).Background(Colors.Grey.Lighten3).Padding(5).Text("TOTAL").Bold();
                            table.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text($"KES {bankingData.TotalBanked:N0}").Bold();
                        });
                    }
                    else
                    {
                        col.Item().PaddingTop(20).AlignCenter().Text("No banking records found for this period.")
                            .FontSize(12).FontColor(Colors.Grey.Medium);
                    }
                });

                // Footer
                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text("QDesk Banking Report")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                    row.RelativeItem().AlignCenter().Text(text =>
                    {
                        text.DefaultTextStyle(TextStyle.Default.FontSize(8).FontColor(Colors.Grey.Darken1));
                        text.Span("Page ");
                        text.CurrentPageNumber();
                        text.Span(" of ");
                        text.TotalPages();
                    });
                    row.RelativeItem().AlignRight().Text($"Generated: {DateTime.Now:dd/MM/yyyy HH:mm}")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        });

        using var stream = new MemoryStream();
        document.GeneratePdf(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Export Sales report to PDF
    /// </summary>
    private async Task<byte[]> ExportSalesPdfAsync(string? quarryId, DateTime fromDate, DateTime toDate)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var salesData = await GetSalesReportAsync(quarryId, fromDate, toDate);
        var quarryName = "All Quarries";
        if (!string.IsNullOrEmpty(quarryId))
        {
            var quarry = await context.Quarries.FindAsync(quarryId);
            quarryName = quarry?.QuarryName ?? "Unknown";
        }

        var dateRange = $"{fromDate:dd MMM yyyy} - {toDate:dd MMM yyyy}";
        var primaryColor = "#1976D2";
        var successColor = "#4CAF50";
        var warningColor = "#FF9800";
        var errorColor = "#F44336";

        var totalExpenses = salesData.TotalCommission + salesData.TotalLoadersFee + salesData.TotalLandRateFee;
        var earnings = salesData.TotalSales - totalExpenses;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(25);
                page.DefaultTextStyle(x => x.FontSize(9));

                // Header
                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(titleCol =>
                        {
                            titleCol.Item().Text("QDesk Sales Report")
                                .FontSize(20).Bold().FontColor(primaryColor);
                            titleCol.Item().Text($"{quarryName}")
                                .FontSize(12).FontColor(Colors.Grey.Darken2);
                            titleCol.Item().Text($"Period: {dateRange} | Generated: {DateTime.Now:dd MMM yyyy HH:mm}")
                                .FontSize(9).FontColor(Colors.Grey.Medium);
                        });
                    });

                    col.Item().PaddingTop(8);

                    // Stats Cards
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Padding(3).Element(e => ComposeStatsCard(e,
                            "Total Revenue", $"KES {salesData.TotalSales:N0}",
                            $"{salesData.TotalOrders} orders", successColor, true));
                        row.RelativeItem().Padding(3).Element(e => ComposeStatsCard(e,
                            "Total Quantity", $"{salesData.TotalQuantity:N0}",
                            "pieces sold", primaryColor, true));
                        row.RelativeItem().Padding(3).Element(e => ComposeStatsCard(e,
                            "Commission", $"KES {salesData.TotalCommission:N0}",
                            "broker fees", errorColor, false));
                        row.RelativeItem().Padding(3).Element(e => ComposeStatsCard(e,
                            "Loaders Fee", $"KES {salesData.TotalLoadersFee:N0}",
                            "loading fees", errorColor, false));
                        row.RelativeItem().Padding(3).Element(e => ComposeStatsCard(e,
                            "Unpaid Orders", $"KES {salesData.UnpaidAmount:N0}",
                            "outstanding", salesData.UnpaidAmount > 0 ? warningColor : successColor, false));
                        row.RelativeItem().Padding(3).Element(e => ComposeStatsCard(e,
                            "Net Earnings", $"KES {earnings:N0}",
                            "after fees", earnings >= 0 ? successColor : errorColor, true));
                    });

                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                // Content - Daily Sales Breakdown
                page.Content().PaddingTop(10).Column(col =>
                {
                    col.Item().Element(e => SectionHeader(e, "Daily Sales Breakdown", primaryColor));

                    if (salesData.DailySummaries.Count > 0)
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(70);  // Date
                                columns.ConstantColumn(50);  // Orders
                                columns.ConstantColumn(60);  // Qty
                                columns.ConstantColumn(90);  // Revenue
                                columns.ConstantColumn(80);  // Commission
                                columns.ConstantColumn(80);  // Loaders
                                columns.ConstantColumn(80);  // Land Rate
                                columns.ConstantColumn(80);  // Other
                                columns.ConstantColumn(90);  // Net
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background(primaryColor).Padding(4).Text("Date").FontColor(Colors.White).Bold().FontSize(8);
                                header.Cell().Background(primaryColor).Padding(4).AlignRight().Text("Orders").FontColor(Colors.White).Bold().FontSize(8);
                                header.Cell().Background(primaryColor).Padding(4).AlignRight().Text("Qty").FontColor(Colors.White).Bold().FontSize(8);
                                header.Cell().Background(primaryColor).Padding(4).AlignRight().Text("Revenue").FontColor(Colors.White).Bold().FontSize(8);
                                header.Cell().Background(primaryColor).Padding(4).AlignRight().Text("Commission").FontColor(Colors.White).Bold().FontSize(8);
                                header.Cell().Background(primaryColor).Padding(4).AlignRight().Text("Loaders").FontColor(Colors.White).Bold().FontSize(8);
                                header.Cell().Background(primaryColor).Padding(4).AlignRight().Text("Land Rate").FontColor(Colors.White).Bold().FontSize(8);
                                header.Cell().Background(primaryColor).Padding(4).AlignRight().Text("Other").FontColor(Colors.White).Bold().FontSize(8);
                                header.Cell().Background(primaryColor).Padding(4).AlignRight().Text("Net").FontColor(Colors.White).Bold().FontSize(8);
                            });

                            foreach (var day in salesData.DailySummaries)
                            {
                                var idx = salesData.DailySummaries.IndexOf(day);
                                var bgColor = idx % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
                                var net = day.Revenue - day.Commission - day.LoadersFee - day.LandRateFee - day.OtherExpenses;

                                table.Cell().Background(bgColor).Padding(3).Text(day.Date.ToString("dd/MM/yy")).FontSize(8);
                                table.Cell().Background(bgColor).Padding(3).AlignRight().Text($"{day.OrderCount}").FontSize(8);
                                table.Cell().Background(bgColor).Padding(3).AlignRight().Text($"{day.Quantity:N0}").FontSize(8);
                                table.Cell().Background(bgColor).Padding(3).AlignRight().Text($"KES {day.Revenue:N0}").FontSize(8);
                                table.Cell().Background(bgColor).Padding(3).AlignRight().Text($"KES {day.Commission:N0}").FontSize(8);
                                table.Cell().Background(bgColor).Padding(3).AlignRight().Text($"KES {day.LoadersFee:N0}").FontSize(8);
                                table.Cell().Background(bgColor).Padding(3).AlignRight().Text($"KES {day.LandRateFee:N0}").FontSize(8);
                                table.Cell().Background(bgColor).Padding(3).AlignRight().Text($"KES {day.OtherExpenses:N0}").FontSize(8);
                                table.Cell().Background(bgColor).Padding(3).AlignRight().Text($"KES {net:N0}").Bold().FontSize(8);
                            }

                            // Total row
                            var totalNet = salesData.TotalSales - salesData.TotalCommission - salesData.TotalLoadersFee - salesData.TotalLandRateFee - salesData.TotalOtherExpenses;
                            table.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("TOTAL").Bold().FontSize(9);
                            table.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text($"{salesData.TotalOrders}").Bold().FontSize(9);
                            table.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text($"{salesData.TotalQuantity:N0}").Bold().FontSize(9);
                            table.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text($"KES {salesData.TotalSales:N0}").Bold().FontSize(9);
                            table.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text($"KES {salesData.TotalCommission:N0}").Bold().FontSize(9);
                            table.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text($"KES {salesData.TotalLoadersFee:N0}").Bold().FontSize(9);
                            table.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text($"KES {salesData.TotalLandRateFee:N0}").Bold().FontSize(9);
                            table.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text($"KES {salesData.TotalOtherExpenses:N0}").Bold().FontSize(9);
                            table.Cell().Background(successColor).Padding(4).AlignRight().Text($"KES {totalNet:N0}").Bold().FontColor(Colors.White).FontSize(9);
                        });
                    }
                    else
                    {
                        col.Item().PaddingTop(20).AlignCenter().Text("No sales found for this period.")
                            .FontSize(12).FontColor(Colors.Grey.Medium);
                    }
                });

                // Footer
                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text("QDesk Sales Report")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                    row.RelativeItem().AlignCenter().Text(text =>
                    {
                        text.DefaultTextStyle(TextStyle.Default.FontSize(8).FontColor(Colors.Grey.Darken1));
                        text.Span("Page ");
                        text.CurrentPageNumber();
                        text.Span(" of ");
                        text.TotalPages();
                    });
                    row.RelativeItem().AlignRight().Text($"Generated: {DateTime.Now:dd/MM/yyyy HH:mm}")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        });

        using var stream = new MemoryStream();
        document.GeneratePdf(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Export Expenses report to PDF
    /// </summary>
    private async Task<byte[]> ExportExpensesPdfAsync(string? quarryId, DateTime fromDate, DateTime toDate)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var expensesData = await GetExpensesReportAsync(quarryId, fromDate, toDate);
        var quarryName = "All Quarries";
        if (!string.IsNullOrEmpty(quarryId))
        {
            var quarry = await context.Quarries.FindAsync(quarryId);
            quarryName = quarry?.QuarryName ?? "Unknown";
        }

        var dateRange = $"{fromDate:dd MMM yyyy} - {toDate:dd MMM yyyy}";
        var primaryColor = "#1976D2";
        var errorColor = "#F44336";

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10));

                // Header
                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(titleCol =>
                        {
                            titleCol.Item().Text("QDesk Expenses Report")
                                .FontSize(20).Bold().FontColor(primaryColor);
                            titleCol.Item().Text($"{quarryName}")
                                .FontSize(12).FontColor(Colors.Grey.Darken2);
                            titleCol.Item().Text($"Period: {dateRange} | Generated: {DateTime.Now:dd MMM yyyy HH:mm}")
                                .FontSize(9).FontColor(Colors.Grey.Medium);
                        });
                    });

                    col.Item().PaddingTop(8);

                    // Summary Card
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Padding(5).Element(e => ComposeStatsCard(e,
                            "Total Expenses", $"KES {expensesData.TotalExpenses:N0}",
                            $"{expensesData.ExpenseItems.Count} items", errorColor, false));
                    });

                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                // Content
                page.Content().PaddingTop(15).Column(col =>
                {
                    col.Item().Element(e => SectionHeader(e, "Expense Details", errorColor));

                    if (expensesData.ExpenseItems.Count > 0)
                    {
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(80);  // Date
                                columns.RelativeColumn(3);   // Description
                                columns.RelativeColumn(1);   // Category
                                columns.ConstantColumn(100); // Amount
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background(errorColor).Padding(5).Text("Date").FontColor(Colors.White).Bold();
                                header.Cell().Background(errorColor).Padding(5).Text("Description").FontColor(Colors.White).Bold();
                                header.Cell().Background(errorColor).Padding(5).Text("Category").FontColor(Colors.White).Bold();
                                header.Cell().Background(errorColor).Padding(5).AlignRight().Text("Amount").FontColor(Colors.White).Bold();
                            });

                            foreach (var expense in expensesData.ExpenseItems)
                            {
                                var idx = expensesData.ExpenseItems.IndexOf(expense);
                                var bgColor = idx % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;

                                table.Cell().Background(bgColor).Padding(4).Text(expense.Date.ToString("dd/MM/yy"));
                                table.Cell().Background(bgColor).Padding(4).Text(expense.Description);
                                table.Cell().Background(bgColor).Padding(4).Text(expense.Category);
                                table.Cell().Background(bgColor).Padding(4).AlignRight().Text($"KES {expense.Amount:N0}");
                            }

                            // Total row
                            table.Cell().ColumnSpan(3).Background(Colors.Grey.Lighten3).Padding(5).Text("TOTAL").Bold();
                            table.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text($"KES {expensesData.TotalExpenses:N0}").Bold();
                        });
                    }
                    else
                    {
                        col.Item().PaddingTop(20).AlignCenter().Text("No expenses found for this period.")
                            .FontSize(12).FontColor(Colors.Grey.Medium);
                    }
                });

                // Footer
                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text("QDesk Expenses Report")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                    row.RelativeItem().AlignCenter().Text(text =>
                    {
                        text.DefaultTextStyle(TextStyle.Default.FontSize(8).FontColor(Colors.Grey.Darken1));
                        text.Span("Page ");
                        text.CurrentPageNumber();
                        text.Span(" of ");
                        text.TotalPages();
                    });
                    row.RelativeItem().AlignRight().Text($"Generated: {DateTime.Now:dd/MM/yyyy HH:mm}")
                        .FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        });

        using var stream = new MemoryStream();
        document.GeneratePdf(stream);
        return stream.ToArray();
    }

    #endregion

    #region Unpaid Orders Report Methods

    /// <summary>
    /// Get comprehensive unpaid orders report with aging analysis
    /// </summary>
    public async Task<UnpaidOrdersReportData> GetUnpaidOrdersReportAsync(string? quarryId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var report = new UnpaidOrdersReportData
        {
            FromDate = fromDate,
            ToDate = toDate
        };

        // Build base query for unpaid sales
        var salesQuery = context.Sales
            .Where(s => s.PaymentStatus != "Paid")
            .Where(s => s.IsActive);

        // Filter by quarry if specified
        if (!string.IsNullOrEmpty(quarryId))
        {
            salesQuery = salesQuery.Where(s => s.QId == quarryId);
        }

        // Filter by date range if specified (for sale date)
        if (fromDate.HasValue)
        {
            salesQuery = salesQuery.Where(s => s.SaleDate >= fromDate.Value);
        }
        if (toDate.HasValue)
        {
            salesQuery = salesQuery.Where(s => s.SaleDate <= toDate.Value);
        }

        var unpaidSales = await salesQuery
            .Include(s => s.Product)
            .Include(s => s.Broker)
            .OrderBy(s => s.SaleDate) // Oldest first
            .ToListAsync();

        // Get quarry names for the items
        var quarryIds = unpaidSales.Select(s => s.QId).Distinct().ToList();
        var quarries = await context.Quarries
            .Where(q => quarryIds.Contains(q.Id))
            .ToDictionaryAsync(q => q.Id, q => q.QuarryName);

        // Build report items
        foreach (var sale in unpaidSales)
        {
            var daysUnpaid = sale.SaleDate.HasValue
                ? (int)(DateTime.Today - sale.SaleDate.Value.Date).TotalDays
                : 0;

            report.Items.Add(new UnpaidOrderItem
            {
                SaleId = sale.Id,
                SaleDate = sale.SaleDate ?? DateTime.Today,
                DaysUnpaid = daysUnpaid,
                VehicleRegistration = sale.VehicleRegistration,
                ProductName = sale.Product?.ProductName ?? "Unknown",
                Quantity = sale.Quantity,
                Amount = sale.GrossSaleAmount,
                ClientName = sale.ClientName,
                ClientPhone = sale.ClientPhone,
                ClerkName = sale.ClerkName ?? "Unknown",
                QuarryName = quarries.GetValueOrDefault(sale.QId, "Unknown"),
                QuarryId = sale.QId
            });
        }

        // Calculate summary statistics
        report.TotalCount = report.Items.Count;
        report.TotalAmount = report.Items.Sum(i => i.Amount);

        // Aging breakdown
        report.Over30DaysCount = report.Items.Count(i => i.DaysUnpaid > 30);
        report.Over30DaysAmount = report.Items.Where(i => i.DaysUnpaid > 30).Sum(i => i.Amount);
        report.Over60DaysCount = report.Items.Count(i => i.DaysUnpaid > 60);
        report.Over60DaysAmount = report.Items.Where(i => i.DaysUnpaid > 60).Sum(i => i.Amount);

        // Average days unpaid
        if (report.Items.Any())
        {
            report.AverageDaysUnpaid = (int)report.Items.Average(i => i.DaysUnpaid);
            report.OldestDays = report.Items.Max(i => i.DaysUnpaid);
        }

        return report;
    }

    /// <summary>
    /// Export unpaid orders report to Excel
    /// </summary>
    public async Task<byte[]> ExportUnpaidOrdersToExcelAsync(string? quarryId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var reportData = await GetUnpaidOrdersReportAsync(quarryId, fromDate, toDate);

        // Get quarry name
        var quarryName = "All Quarries";
        if (!string.IsNullOrEmpty(quarryId))
        {
            var quarry = await context.Quarries.FindAsync(quarryId);
            quarryName = quarry?.QuarryName ?? "Unknown";
        }

        var dateRange = "All Dates";
        if (fromDate.HasValue && toDate.HasValue)
        {
            dateRange = $"{fromDate.Value:dd MMM yyyy} - {toDate.Value:dd MMM yyyy}";
        }
        else if (fromDate.HasValue)
        {
            dateRange = $"From {fromDate.Value:dd MMM yyyy}";
        }
        else if (toDate.HasValue)
        {
            dateRange = $"Up to {toDate.Value:dd MMM yyyy}";
        }

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Unpaid Orders");

        // Header
        ws.Cell("A1").Value = "QDESKPRO UNPAID ORDERS REPORT";
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 16;
        ws.Range("A1:J1").Merge();

        ws.Cell("A2").Value = $"Quarry: {quarryName} | Period: {dateRange}";
        ws.Range("A2:J2").Merge();
        ws.Cell("A2").Style.Font.Italic = true;

        ws.Cell("A3").Value = $"Generated: {DateTime.Now:dd MMM yyyy HH:mm}";
        ws.Range("A3:J3").Merge();
        ws.Cell("A3").Style.Font.FontColor = XLColor.Gray;

        // Summary section
        var row = 5;
        ws.Cell(row, 1).Value = "SUMMARY";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Range(row, 1, row, 3).Merge();
        ws.Range(row, 1, row, 3).Style.Fill.BackgroundColor = XLColor.LightGray;

        row++;
        ws.Cell(row, 1).Value = "Total Unpaid Orders:";
        ws.Cell(row, 2).Value = reportData.TotalCount;
        row++;
        ws.Cell(row, 1).Value = "Total Unpaid Amount:";
        ws.Cell(row, 2).Value = reportData.TotalAmount;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(row, 2).Style.Font.Bold = true;
        ws.Cell(row, 2).Style.Font.FontColor = XLColor.Red;
        row++;
        ws.Cell(row, 1).Value = "30+ Days Overdue:";
        ws.Cell(row, 2).Value = reportData.Over30DaysCount;
        ws.Cell(row, 3).Value = reportData.Over30DaysAmount;
        ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(row, 3).Style.Font.FontColor = XLColor.Orange;
        row++;
        ws.Cell(row, 1).Value = "60+ Days Overdue:";
        ws.Cell(row, 2).Value = reportData.Over60DaysCount;
        ws.Cell(row, 3).Value = reportData.Over60DaysAmount;
        ws.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(row, 3).Style.Font.FontColor = XLColor.Red;
        row++;
        ws.Cell(row, 1).Value = "Average Days Unpaid:";
        ws.Cell(row, 2).Value = reportData.AverageDaysUnpaid;
        row++;
        ws.Cell(row, 1).Value = "Oldest Unpaid:";
        ws.Cell(row, 2).Value = $"{reportData.OldestDays} days";

        // Detail table
        row += 2;
        ws.Cell(row, 1).Value = "UNPAID ORDERS DETAILS";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Range(row, 1, row, 10).Merge();
        ws.Range(row, 1, row, 10).Style.Fill.BackgroundColor = XLColor.LightGray;

        row++;
        var headerRow = row;
        ws.Cell(row, 1).Value = "Sale Date";
        ws.Cell(row, 2).Value = "Days";
        ws.Cell(row, 3).Value = "Vehicle";
        ws.Cell(row, 4).Value = "Product";
        ws.Cell(row, 5).Value = "Qty";
        ws.Cell(row, 6).Value = "Amount";
        ws.Cell(row, 7).Value = "Client";
        ws.Cell(row, 8).Value = "Phone";
        ws.Cell(row, 9).Value = "Clerk";
        ws.Cell(row, 10).Value = "Quarry";
        ws.Range(row, 1, row, 10).Style.Font.Bold = true;
        ws.Range(row, 1, row, 10).Style.Fill.BackgroundColor = XLColor.Red;
        ws.Range(row, 1, row, 10).Style.Font.FontColor = XLColor.White;

        foreach (var item in reportData.Items)
        {
            row++;
            ws.Cell(row, 1).Value = item.SaleDate.ToString("dd/MM/yyyy");
            ws.Cell(row, 2).Value = item.DaysUnpaid;
            ws.Cell(row, 3).Value = item.VehicleRegistration;
            ws.Cell(row, 4).Value = item.ProductName;
            ws.Cell(row, 5).Value = item.Quantity;
            ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0";
            ws.Cell(row, 6).Value = item.Amount;
            ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 7).Value = item.ClientName ?? "-";
            ws.Cell(row, 8).Value = item.ClientPhone ?? "-";
            ws.Cell(row, 9).Value = item.ClerkName;
            ws.Cell(row, 10).Value = item.QuarryName;

            // Color code based on aging
            if (item.DaysUnpaid > 60)
            {
                ws.Range(row, 1, row, 10).Style.Fill.BackgroundColor = XLColor.LightCoral;
            }
            else if (item.DaysUnpaid > 30)
            {
                ws.Range(row, 1, row, 10).Style.Fill.BackgroundColor = XLColor.LightSalmon;
            }
            else if (item.DaysUnpaid > 14)
            {
                ws.Range(row, 1, row, 10).Style.Fill.BackgroundColor = XLColor.LightYellow;
            }
        }

        // Total row
        row++;
        ws.Cell(row, 1).Value = "TOTAL";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Range(row, 1, row, 5).Merge();
        ws.Cell(row, 6).Value = reportData.TotalAmount;
        ws.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(row, 6).Style.Font.Bold = true;
        ws.Range(row, 1, row, 10).Style.Fill.BackgroundColor = XLColor.LightCoral;

        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Export unpaid orders report to PDF
    /// </summary>
    public async Task<byte[]> ExportUnpaidOrdersToPdfAsync(string? quarryId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var reportData = await GetUnpaidOrdersReportAsync(quarryId, fromDate, toDate);

        // Get quarry name
        var quarryName = "All Quarries";
        if (!string.IsNullOrEmpty(quarryId))
        {
            var quarry = await context.Quarries.FindAsync(quarryId);
            quarryName = quarry?.QuarryName ?? "Unknown";
        }

        var dateRange = "All Dates";
        if (fromDate.HasValue && toDate.HasValue)
        {
            dateRange = $"{fromDate.Value:dd MMM yyyy} - {toDate.Value:dd MMM yyyy}";
        }
        else if (fromDate.HasValue)
        {
            dateRange = $"From {fromDate.Value:dd MMM yyyy}";
        }
        else if (toDate.HasValue)
        {
            dateRange = $"Up to {toDate.Value:dd MMM yyyy}";
        }

        // Colors
        var primaryColor = "#1976D2";
        var errorColor = "#F44336";
        var warningColor = "#FF9800";
        var criticalColor = "#D32F2F";

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(25);
                page.DefaultTextStyle(x => x.FontSize(9));

                // Header
                page.Header().Element(header =>
                {
                    header.Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(titleCol =>
                            {
                                titleCol.Item().Text("QDesk Unpaid Orders Report")
                                    .FontSize(22).Bold().FontColor(errorColor);
                                titleCol.Item().Text($"{quarryName}")
                                    .FontSize(14).FontColor(Colors.Grey.Darken2);
                                titleCol.Item().Text($"Period: {dateRange} | Generated: {DateTime.Now:dd MMM yyyy HH:mm}")
                                    .FontSize(9).FontColor(Colors.Grey.Medium);
                            });
                        });

                        col.Item().PaddingTop(10);

                        // Stats cards row
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Padding(3).Element(e => ComposeStatsCard(e,
                                "Total Unpaid", $"KES {reportData.TotalAmount:N0}",
                                $"{reportData.TotalCount} orders", errorColor, false));

                            row.RelativeItem().Padding(3).Element(e => ComposeStatsCard(e,
                                "30+ Days Overdue", $"KES {reportData.Over30DaysAmount:N0}",
                                $"{reportData.Over30DaysCount} orders", warningColor, false));

                            row.RelativeItem().Padding(3).Element(e => ComposeStatsCard(e,
                                "60+ Days Critical", $"KES {reportData.Over60DaysAmount:N0}",
                                $"{reportData.Over60DaysCount} orders", criticalColor, false));

                            row.RelativeItem().Padding(3).Element(e => ComposeStatsCard(e,
                                "Avg. Days Unpaid", $"{reportData.AverageDaysUnpaid} days",
                                $"Oldest: {reportData.OldestDays} days", primaryColor, false));
                        });

                        col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    });
                });

                // Content
                page.Content().Element(content =>
                {
                    content.PaddingTop(10).Column(col =>
                    {
                        col.Item().Element(e => SectionHeader(e, "Unpaid Orders Details", errorColor));

                        // Group by quarry if showing all quarries
                        var itemsByQuarry = reportData.Items.GroupBy(i => i.QuarryName).OrderBy(g => g.Key);

                        foreach (var quarryGroup in itemsByQuarry)
                        {
                            // Quarry header if multiple quarries
                            if (string.IsNullOrEmpty(quarryId))
                            {
                                col.Item().PaddingTop(8).Background(Colors.Grey.Lighten3).Padding(5).Row(qRow =>
                                {
                                    qRow.RelativeItem().Text($"{quarryGroup.Key}").Bold().FontSize(10);
                                    qRow.ConstantItem(200).AlignRight().Text($"{quarryGroup.Count()} orders | KES {quarryGroup.Sum(i => i.Amount):N0}").FontSize(9);
                                });
                            }

                            // Unpaid orders table
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(65);    // Sale Date
                                    columns.ConstantColumn(40);    // Days
                                    columns.ConstantColumn(75);    // Vehicle
                                    columns.RelativeColumn(1);     // Product
                                    columns.ConstantColumn(45);    // Qty
                                    columns.ConstantColumn(80);    // Amount
                                    columns.RelativeColumn(1.2f);  // Client
                                    columns.RelativeColumn(1);     // Phone
                                    columns.RelativeColumn(1);     // Clerk
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Background(errorColor).Padding(3).Text("Sale Date").FontColor(Colors.White).Bold().FontSize(7);
                                    header.Cell().Background(errorColor).Padding(3).Text("Days").FontColor(Colors.White).Bold().FontSize(7);
                                    header.Cell().Background(errorColor).Padding(3).Text("Vehicle").FontColor(Colors.White).Bold().FontSize(7);
                                    header.Cell().Background(errorColor).Padding(3).Text("Product").FontColor(Colors.White).Bold().FontSize(7);
                                    header.Cell().Background(errorColor).Padding(3).Text("Qty").FontColor(Colors.White).Bold().FontSize(7);
                                    header.Cell().Background(errorColor).Padding(3).Text("Amount").FontColor(Colors.White).Bold().FontSize(7);
                                    header.Cell().Background(errorColor).Padding(3).Text("Client").FontColor(Colors.White).Bold().FontSize(7);
                                    header.Cell().Background(errorColor).Padding(3).Text("Phone").FontColor(Colors.White).Bold().FontSize(7);
                                    header.Cell().Background(errorColor).Padding(3).Text("Clerk").FontColor(Colors.White).Bold().FontSize(7);
                                });

                                foreach (var item in quarryGroup.OrderBy(i => i.SaleDate))
                                {
                                    var idx = quarryGroup.ToList().IndexOf(item);
                                    string bgColor;
                                    if (item.DaysUnpaid > 60)
                                        bgColor = "#FFCDD2"; // Light red
                                    else if (item.DaysUnpaid > 30)
                                        bgColor = "#FFE0B2"; // Light orange
                                    else if (item.DaysUnpaid > 14)
                                        bgColor = "#FFF9C4"; // Light yellow
                                    else
                                        bgColor = idx % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;

                                    table.Cell().Background(bgColor).Padding(2).Text(item.SaleDate.ToString("dd/MM/yy")).FontSize(7);
                                    table.Cell().Background(bgColor).Padding(2).Text($"{item.DaysUnpaid}").FontSize(7).Bold();
                                    table.Cell().Background(bgColor).Padding(2).Text(item.VehicleRegistration).FontSize(7);
                                    table.Cell().Background(bgColor).Padding(2).Text(item.ProductName).FontSize(7);
                                    table.Cell().Background(bgColor).Padding(2).Text($"{item.Quantity:N0}").FontSize(7);
                                    table.Cell().Background(bgColor).Padding(2).Text($"KES {item.Amount:N0}").FontSize(7);
                                    table.Cell().Background(bgColor).Padding(2).Text(item.ClientName ?? "-").FontSize(7);
                                    table.Cell().Background(bgColor).Padding(2).Text(item.ClientPhone ?? "-").FontSize(7);
                                    table.Cell().Background(bgColor).Padding(2).Text(item.ClerkName).FontSize(7);
                                }
                            });
                        }

                        // Total row
                        col.Item().PaddingTop(5).Background(Colors.Grey.Lighten3).Padding(5).Row(totalRow =>
                        {
                            totalRow.RelativeItem().Text("TOTAL UNPAID").Bold().FontSize(10);
                            totalRow.ConstantItem(150).AlignRight().Text($"KES {reportData.TotalAmount:N0}").Bold().FontSize(10).FontColor(errorColor);
                        });

                        // Aging legend
                        col.Item().PaddingTop(15).Row(legendRow =>
                        {
                            legendRow.AutoItem().Text("Aging Legend: ").Bold().FontSize(8);
                            legendRow.AutoItem().Background("#FFF9C4").Padding(3).Text("15-30 days").FontSize(7);
                            legendRow.AutoItem().PaddingLeft(5).Background("#FFE0B2").Padding(3).Text("31-60 days").FontSize(7);
                            legendRow.AutoItem().PaddingLeft(5).Background("#FFCDD2").Padding(3).Text("60+ days (Critical)").FontSize(7);
                        });
                    });
                });

                // Footer
                page.Footer().Element(footer =>
                {
                    footer.Row(row =>
                    {
                        row.RelativeItem().Text("QDesk Unpaid Orders Report - Quarry Management System")
                            .FontSize(8).FontColor(Colors.Grey.Darken1);
                        row.RelativeItem().AlignCenter().Text(text =>
                        {
                            text.DefaultTextStyle(TextStyle.Default.FontSize(8).FontColor(Colors.Grey.Darken1));
                            text.Span("Page ");
                            text.CurrentPageNumber();
                            text.Span(" of ");
                            text.TotalPages();
                        });
                        row.RelativeItem().AlignRight().Text($"Generated: {DateTime.Now:dd/MM/yyyy HH:mm}")
                            .FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                });
            });
        });

        using var stream = new MemoryStream();
        document.GeneratePdf(stream);
        return stream.ToArray();
    }

    #endregion

    #region ROI Analysis Export

    /// <summary>
    /// Export ROI Analysis to Excel with multiple worksheets
    /// </summary>
    public async Task<byte[]> ExportROIAnalysisToExcelAsync(
        Dashboard.Services.ROIAnalysisData roiData,
        string quarryName,
        DateTime fromDate,
        DateTime toDate)
    {
        using var workbook = new XLWorkbook();
        var dateRange = $"{fromDate:dd MMM yyyy} - {toDate:dd MMM yyyy}";

        // Summary Worksheet
        var ws = workbook.Worksheets.Add("ROI Summary");

        // Header
        ws.Cell("A1").Value = "QDESKPRO ROI ANALYSIS REPORT";
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 16;
        ws.Range("A1:F1").Merge();

        ws.Cell("A2").Value = $"Quarry: {quarryName} | Period: {dateRange}";
        ws.Range("A2:F2").Merge();
        ws.Cell("A2").Style.Font.Italic = true;

        ws.Cell("A3").Value = $"Generated: {DateTime.Now:dd MMM yyyy HH:mm}";
        ws.Range("A3:F3").Merge();
        ws.Cell("A3").Style.Font.FontSize = 9;
        ws.Cell("A3").Style.Font.FontColor = XLColor.Gray;

        // Investment Overview Section
        var row = 5;
        ws.Cell(row, 1).Value = "INVESTMENT OVERVIEW";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.LightBlue;
        ws.Range(row, 1, row, 4).Merge();

        row++;
        ws.Cell(row, 1).Value = "Initial Investment:";
        ws.Cell(row, 2).Value = roiData.TotalInvestment;
        ws.Cell(row, 2).Style.NumberFormat.Format = "\"KES\" #,##0";
        ws.Cell(row, 3).Value = "Operations Start:";
        ws.Cell(row, 4).Value = roiData.OperationsStartDate.ToString("dd/MM/yyyy");

        row++;
        ws.Cell(row, 1).Value = "Operating Months:";
        ws.Cell(row, 2).Value = roiData.OperatingMonths;
        ws.Cell(row, 3).Value = "Operating Days:";
        ws.Cell(row, 4).Value = roiData.OperatingDays;

        // Core ROI Metrics Section
        row += 2;
        ws.Cell(row, 1).Value = "CORE ROI METRICS";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.LightGreen;
        ws.Range(row, 1, row, 4).Merge();

        row++;
        ws.Cell(row, 1).Value = "Basic ROI:";
        ws.Cell(row, 2).Value = roiData.BasicROI / 100;
        ws.Cell(row, 2).Style.NumberFormat.Format = "0.0%";
        ws.Cell(row, 2).Style.Font.Bold = true;
        ws.Cell(row, 3).Value = "Annualized ROI:";
        ws.Cell(row, 4).Value = roiData.AnnualizedROI / 100;
        ws.Cell(row, 4).Style.NumberFormat.Format = "0.0%";

        row++;
        ws.Cell(row, 1).Value = "Investment Recovery:";
        ws.Cell(row, 2).Value = roiData.InvestmentRecoveryPercent / 100;
        ws.Cell(row, 2).Style.NumberFormat.Format = "0.0%";
        ws.Cell(row, 2).Style.Font.FontColor = roiData.InvestmentRecoveryPercent >= 100 ? XLColor.Green : XLColor.Orange;
        ws.Cell(row, 3).Value = "Cumulative Net Profit:";
        ws.Cell(row, 4).Value = roiData.CumulativeNetProfit;
        ws.Cell(row, 4).Style.NumberFormat.Format = "\"KES\" #,##0";

        row++;
        ws.Cell(row, 1).Value = "Payback Period:";
        ws.Cell(row, 2).Value = roiData.PaybackPeriodMonths < 1000 ? $"{roiData.PaybackPeriodMonths:N1} months" : "N/A";
        ws.Cell(row, 3).Value = "Est. Recovery Date:";
        ws.Cell(row, 4).Value = roiData.EstimatedRecoveryDate?.ToString("dd/MM/yyyy") ?? "N/A";

        row++;
        ws.Cell(row, 1).Value = "Remaining to Recover:";
        ws.Cell(row, 2).Value = roiData.RemainingToRecover;
        ws.Cell(row, 2).Style.NumberFormat.Format = "\"KES\" #,##0";
        ws.Cell(row, 2).Style.Font.FontColor = roiData.RemainingToRecover > 0 ? XLColor.Red : XLColor.Green;

        // Profitability Metrics Section
        row += 2;
        ws.Cell(row, 1).Value = "PROFITABILITY METRICS";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.LightYellow;
        ws.Range(row, 1, row, 4).Merge();

        row++;
        ws.Cell(row, 1).Value = "Gross Profit Margin:";
        ws.Cell(row, 2).Value = roiData.GrossProfitMargin / 100;
        ws.Cell(row, 2).Style.NumberFormat.Format = "0.0%";
        ws.Cell(row, 3).Value = "Net Profit Margin:";
        ws.Cell(row, 4).Value = roiData.NetProfitMargin / 100;
        ws.Cell(row, 4).Style.NumberFormat.Format = "0.0%";

        row++;
        ws.Cell(row, 1).Value = "Revenue per Piece:";
        ws.Cell(row, 2).Value = roiData.RevenuePerPiece;
        ws.Cell(row, 2).Style.NumberFormat.Format = "\"KES\" #,##0.00";
        ws.Cell(row, 3).Value = "Cost per Piece:";
        ws.Cell(row, 4).Value = roiData.CostPerPiece;
        ws.Cell(row, 4).Style.NumberFormat.Format = "\"KES\" #,##0.00";

        row++;
        ws.Cell(row, 1).Value = "Profit per Piece:";
        ws.Cell(row, 2).Value = roiData.ProfitPerPiece;
        ws.Cell(row, 2).Style.NumberFormat.Format = "\"KES\" #,##0.00";
        ws.Cell(row, 2).Style.Font.Bold = true;
        ws.Cell(row, 2).Style.Font.FontColor = roiData.ProfitPerPiece >= 0 ? XLColor.Green : XLColor.Red;

        // Efficiency Metrics Section
        row += 2;
        ws.Cell(row, 1).Value = "EFFICIENCY METRICS";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.LightCyan;
        ws.Range(row, 1, row, 4).Merge();

        row++;
        ws.Cell(row, 1).Value = "Fuel Efficiency:";
        ws.Cell(row, 2).Value = $"{roiData.FuelEfficiency:N1} pcs/L";
        ws.Cell(row, 3).Value = "Capacity Utilization:";
        ws.Cell(row, 4).Value = roiData.CapacityUtilization / 100;
        ws.Cell(row, 4).Style.NumberFormat.Format = "0.0%";

        row++;
        ws.Cell(row, 1).Value = "Commission Ratio:";
        ws.Cell(row, 2).Value = roiData.CommissionRatio / 100;
        ws.Cell(row, 2).Style.NumberFormat.Format = "0.0%";
        ws.Cell(row, 3).Value = "Collection Efficiency:";
        ws.Cell(row, 4).Value = roiData.CollectionEfficiency / 100;
        ws.Cell(row, 4).Style.NumberFormat.Format = "0.0%";

        // Break-Even Analysis Section
        row += 2;
        ws.Cell(row, 1).Value = "BREAK-EVEN ANALYSIS";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.LightPink;
        ws.Range(row, 1, row, 4).Merge();

        row++;
        ws.Cell(row, 1).Value = "Fixed Costs (Monthly):";
        ws.Cell(row, 2).Value = roiData.BreakEven.FixedCosts;
        ws.Cell(row, 2).Style.NumberFormat.Format = "\"KES\" #,##0";
        ws.Cell(row, 3).Value = "Variable Cost/Unit:";
        ws.Cell(row, 4).Value = roiData.BreakEven.VariableCostPerUnit;
        ws.Cell(row, 4).Style.NumberFormat.Format = "\"KES\" #,##0.00";

        row++;
        ws.Cell(row, 1).Value = "Average Price/Unit:";
        ws.Cell(row, 2).Value = roiData.BreakEven.AveragePricePerUnit;
        ws.Cell(row, 2).Style.NumberFormat.Format = "\"KES\" #,##0.00";
        ws.Cell(row, 3).Value = "Contribution Margin:";
        ws.Cell(row, 4).Value = roiData.BreakEven.ContributionMargin;
        ws.Cell(row, 4).Style.NumberFormat.Format = "\"KES\" #,##0.00";

        row++;
        ws.Cell(row, 1).Value = "Break-Even Pieces:";
        ws.Cell(row, 2).Value = roiData.BreakEven.BreakEvenPieces;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
        ws.Cell(row, 3).Value = "Break-Even Revenue:";
        ws.Cell(row, 4).Value = roiData.BreakEven.BreakEvenRevenue;
        ws.Cell(row, 4).Style.NumberFormat.Format = "\"KES\" #,##0";

        row++;
        ws.Cell(row, 1).Value = "Current Monthly Pieces:";
        ws.Cell(row, 2).Value = roiData.BreakEven.CurrentMonthlyPieces;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0";
        ws.Cell(row, 3).Value = "Margin of Safety:";
        ws.Cell(row, 4).Value = roiData.BreakEven.MarginOfSafetyPercent / 100;
        ws.Cell(row, 4).Style.NumberFormat.Format = "0.0%";
        ws.Cell(row, 4).Style.Font.FontColor = roiData.BreakEven.MarginOfSafetyPercent > 0 ? XLColor.Green : XLColor.Red;

        // Period Totals Section
        row += 2;
        ws.Cell(row, 1).Value = "PERIOD TOTALS";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.LightGray;
        ws.Range(row, 1, row, 4).Merge();

        row++;
        ws.Cell(row, 1).Value = "Total Revenue:";
        ws.Cell(row, 2).Value = roiData.TotalRevenue;
        ws.Cell(row, 2).Style.NumberFormat.Format = "\"KES\" #,##0";
        ws.Cell(row, 3).Value = "Total Expenses:";
        ws.Cell(row, 4).Value = roiData.TotalExpenses;
        ws.Cell(row, 4).Style.NumberFormat.Format = "\"KES\" #,##0";

        row++;
        ws.Cell(row, 1).Value = "Net Profit:";
        ws.Cell(row, 2).Value = roiData.NetProfit;
        ws.Cell(row, 2).Style.NumberFormat.Format = "\"KES\" #,##0";
        ws.Cell(row, 2).Style.Font.Bold = true;
        ws.Cell(row, 2).Style.Font.FontColor = roiData.NetProfit >= 0 ? XLColor.Green : XLColor.Red;
        ws.Cell(row, 3).Value = "Total Quantity:";
        ws.Cell(row, 4).Value = roiData.TotalQuantity;
        ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0";

        row++;
        ws.Cell(row, 1).Value = "Total Orders:";
        ws.Cell(row, 2).Value = roiData.TotalOrders;
        ws.Cell(row, 3).Value = "Fuel Consumed:";
        ws.Cell(row, 4).Value = $"{roiData.TotalFuelConsumed:N0} L";

        // Expense Breakdown
        row += 2;
        ws.Cell(row, 1).Value = "EXPENSE BREAKDOWN";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Range(row, 1, row, 2).Merge();

        row++;
        ws.Cell(row, 1).Value = "Manual Expenses:";
        ws.Cell(row, 2).Value = roiData.ManualExpenses;
        ws.Cell(row, 2).Style.NumberFormat.Format = "\"KES\" #,##0";

        row++;
        ws.Cell(row, 1).Value = "Commission:";
        ws.Cell(row, 2).Value = roiData.Commission;
        ws.Cell(row, 2).Style.NumberFormat.Format = "\"KES\" #,##0";

        row++;
        ws.Cell(row, 1).Value = "Loaders Fee:";
        ws.Cell(row, 2).Value = roiData.LoadersFee;
        ws.Cell(row, 2).Style.NumberFormat.Format = "\"KES\" #,##0";

        row++;
        ws.Cell(row, 1).Value = "Land Rate Fee:";
        ws.Cell(row, 2).Value = roiData.LandRateFee;
        ws.Cell(row, 2).Style.NumberFormat.Format = "\"KES\" #,##0";

        ws.Columns().AdjustToContents();

        // Monthly Performance Worksheet
        if (roiData.MonthlyHistory.Count > 0)
        {
            var monthlyWs = workbook.Worksheets.Add("Monthly Performance");

            monthlyWs.Cell("A1").Value = "MONTHLY PERFORMANCE";
            monthlyWs.Cell("A1").Style.Font.Bold = true;
            monthlyWs.Cell("A1").Style.Font.FontSize = 14;
            monthlyWs.Range("A1:H1").Merge();

            // Headers
            var headerRow = 3;
            monthlyWs.Cell(headerRow, 1).Value = "Month";
            monthlyWs.Cell(headerRow, 2).Value = "Revenue";
            monthlyWs.Cell(headerRow, 3).Value = "Expenses";
            monthlyWs.Cell(headerRow, 4).Value = "Net Profit";
            monthlyWs.Cell(headerRow, 5).Value = "Quantity";
            monthlyWs.Cell(headerRow, 6).Value = "Cumulative Profit";
            monthlyWs.Cell(headerRow, 7).Value = "ROI %";
            monthlyWs.Range(headerRow, 1, headerRow, 7).Style.Font.Bold = true;
            monthlyWs.Range(headerRow, 1, headerRow, 7).Style.Fill.BackgroundColor = XLColor.LightBlue;

            row = headerRow + 1;
            foreach (var month in roiData.MonthlyHistory)
            {
                monthlyWs.Cell(row, 1).Value = month.MonthName;
                monthlyWs.Cell(row, 2).Value = month.Revenue;
                monthlyWs.Cell(row, 2).Style.NumberFormat.Format = "\"KES\" #,##0";
                monthlyWs.Cell(row, 3).Value = month.Expenses;
                monthlyWs.Cell(row, 3).Style.NumberFormat.Format = "\"KES\" #,##0";
                monthlyWs.Cell(row, 4).Value = month.NetProfit;
                monthlyWs.Cell(row, 4).Style.NumberFormat.Format = "\"KES\" #,##0";
                monthlyWs.Cell(row, 4).Style.Font.FontColor = month.NetProfit >= 0 ? XLColor.Green : XLColor.Red;
                monthlyWs.Cell(row, 5).Value = month.Quantity;
                monthlyWs.Cell(row, 5).Style.NumberFormat.Format = "#,##0";
                monthlyWs.Cell(row, 6).Value = month.CumulativeProfit;
                monthlyWs.Cell(row, 6).Style.NumberFormat.Format = "\"KES\" #,##0";
                monthlyWs.Cell(row, 7).Value = month.ROI / 100;
                monthlyWs.Cell(row, 7).Style.NumberFormat.Format = "0.0%";
                row++;
            }

            monthlyWs.Columns().AdjustToContents();
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return await Task.FromResult(stream.ToArray());
    }

    /// <summary>
    /// Export ROI Analysis to PDF with comprehensive metrics
    /// </summary>
    public async Task<byte[]> ExportROIAnalysisToPdfAsync(
        Dashboard.Services.ROIAnalysisData roiData,
        string quarryName,
        DateTime fromDate,
        DateTime toDate)
    {
        var dateRange = $"{fromDate:dd MMM yyyy} - {toDate:dd MMM yyyy}";

        // Brand colors
        var primaryColor = "#1976D2";
        var successColor = "#4CAF50";
        var errorColor = "#F44336";
        var warningColor = "#FF9800";

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(25);
                page.DefaultTextStyle(x => x.FontSize(9));

                // Header
                page.Header().Element(header =>
                {
                    header.Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(titleCol =>
                            {
                                titleCol.Item().Text("ROI Analysis Report")
                                    .FontSize(22).Bold().FontColor(primaryColor);
                                titleCol.Item().Text(quarryName)
                                    .FontSize(14).FontColor(Colors.Grey.Darken2);
                                titleCol.Item().Text($"Period: {dateRange} | Generated: {DateTime.Now:dd MMM yyyy HH:mm}")
                                    .FontSize(9).FontColor(Colors.Grey.Medium);
                            });
                        });

                        col.Item().PaddingTop(10);

                        // Stats Cards Row
                        col.Item().Row(row =>
                        {
                            // Investment Card
                            row.RelativeItem().Padding(3).Element(e => ComposeStatsCard(e,
                                "Investment", $"KES {roiData.TotalInvestment:N0}",
                                $"{roiData.OperatingMonths} months operating", primaryColor, true));

                            // Recovery Card
                            var recoveryColor = roiData.InvestmentRecoveryPercent >= 100 ? successColor :
                                roiData.InvestmentRecoveryPercent >= 50 ? warningColor : errorColor;
                            row.RelativeItem().Padding(3).Element(e => ComposeStatsCard(e,
                                "Recovery", $"{roiData.InvestmentRecoveryPercent:N1}%",
                                roiData.InvestmentRecoveryPercent >= 100 ? "Fully Recovered" : $"KES {roiData.RemainingToRecover:N0} remaining",
                                recoveryColor, true));

                            // Basic ROI Card
                            row.RelativeItem().Padding(3).Element(e => ComposeStatsCard(e,
                                "Basic ROI", $"{roiData.BasicROI:N1}%",
                                $"Annualized: {roiData.AnnualizedROI:N1}%", successColor, true));

                            // Profit Margin Card
                            row.RelativeItem().Padding(3).Element(e => ComposeStatsCard(e,
                                "Net Margin", $"{roiData.NetProfitMargin:N1}%",
                                $"KES {roiData.ProfitPerPiece:N0}/pc profit",
                                roiData.NetProfitMargin >= 0 ? successColor : errorColor, true));

                            // Net Profit Card
                            row.RelativeItem().Padding(3).Element(e => ComposeStatsCard(e,
                                "Net Profit", $"KES {roiData.NetProfit:N0}",
                                $"Period total", roiData.NetProfit >= 0 ? successColor : errorColor, true));

                            // Payback Period Card
                            row.RelativeItem().Padding(3).Element(e => ComposeStatsCard(e,
                                "Payback Period", roiData.PaybackPeriodMonths < 1000 ? $"{roiData.PaybackPeriodMonths:N1} mo" : "N/A",
                                roiData.EstimatedRecoveryDate.HasValue ? $"Est: {roiData.EstimatedRecoveryDate:MMM yyyy}" : "Calculating...",
                                primaryColor, false));
                        });

                        col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    });
                });

                // Content
                page.Content().Element(content =>
                {
                    content.PaddingTop(10).Column(col =>
                    {
                        // Two-column layout
                        col.Item().Row(mainRow =>
                        {
                            // Left Column - Break-Even Analysis
                            mainRow.RelativeItem().Padding(5).Column(leftCol =>
                            {
                                leftCol.Item().Element(e => SectionHeader(e, "Break-Even Analysis", warningColor));
                                leftCol.Item().PaddingTop(5).Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(1);
                                    });

                                    table.Cell().Text("Fixed Costs (Monthly):").FontSize(9);
                                    table.Cell().AlignRight().Text($"KES {roiData.BreakEven.FixedCosts:N0}").FontSize(9);

                                    table.Cell().Text("Variable Cost/Unit:").FontSize(9);
                                    table.Cell().AlignRight().Text($"KES {roiData.BreakEven.VariableCostPerUnit:N2}").FontSize(9);

                                    table.Cell().Text("Average Price/Unit:").FontSize(9);
                                    table.Cell().AlignRight().Text($"KES {roiData.BreakEven.AveragePricePerUnit:N2}").FontSize(9);

                                    table.Cell().Text("Contribution Margin:").FontSize(9);
                                    table.Cell().AlignRight().Text($"KES {roiData.BreakEven.ContributionMargin:N2}").FontSize(9);

                                    table.Cell().BorderTop(1).BorderColor(Colors.Grey.Lighten2)
                                        .Text("Break-Even Pieces:").Bold().FontSize(9);
                                    table.Cell().BorderTop(1).BorderColor(Colors.Grey.Lighten2)
                                        .AlignRight().Text($"{roiData.BreakEven.BreakEvenPieces:N0} pcs").Bold().FontSize(9);

                                    table.Cell().Text("Break-Even Revenue:").FontSize(9);
                                    table.Cell().AlignRight().Text($"KES {roiData.BreakEven.BreakEvenRevenue:N0}").FontSize(9);

                                    table.Cell().Text("Current Monthly Pieces:").FontSize(9);
                                    table.Cell().AlignRight().Text($"{roiData.BreakEven.CurrentMonthlyPieces:N0} pcs").FontSize(9);

                                    table.Cell().Text("Margin of Safety:").Bold().FontSize(9);
                                    table.Cell().AlignRight().Text($"{roiData.BreakEven.MarginOfSafetyPercent:N1}%")
                                        .Bold().FontSize(9)
                                        .FontColor(roiData.BreakEven.MarginOfSafetyPercent > 0 ? successColor : errorColor);
                                });

                                // Efficiency Metrics
                                leftCol.Item().PaddingTop(15).Element(e => SectionHeader(e, "Efficiency Metrics", "#2196F3"));
                                leftCol.Item().PaddingTop(5).Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(2);
                                        columns.RelativeColumn(1);
                                    });

                                    table.Cell().Text("Fuel Efficiency:").FontSize(9);
                                    table.Cell().AlignRight().Text($"{roiData.FuelEfficiency:N1} pcs/L").FontSize(9);

                                    table.Cell().Text("Capacity Utilization:").FontSize(9);
                                    table.Cell().AlignRight().Text($"{roiData.CapacityUtilization:N1}%").FontSize(9);

                                    table.Cell().Text("Commission Ratio:").FontSize(9);
                                    table.Cell().AlignRight().Text($"{roiData.CommissionRatio:N1}%").FontSize(9);

                                    table.Cell().Text("Collection Efficiency:").FontSize(9);
                                    table.Cell().AlignRight().Text($"{roiData.CollectionEfficiency:N1}%").FontSize(9);
                                });
                            });

                            // Right Column - Monthly Performance
                            mainRow.RelativeItem(2).Padding(5).Column(rightCol =>
                            {
                                rightCol.Item().Element(e => SectionHeader(e, "Monthly Performance", successColor));

                                if (roiData.MonthlyHistory.Count > 0)
                                {
                                    rightCol.Item().PaddingTop(5).Table(table =>
                                    {
                                        table.ColumnsDefinition(columns =>
                                        {
                                            columns.RelativeColumn(1.2f);  // Month
                                            columns.RelativeColumn(1);     // Revenue
                                            columns.RelativeColumn(1);     // Expenses
                                            columns.RelativeColumn(1);     // Net Profit
                                            columns.RelativeColumn(0.8f);  // Qty
                                            columns.RelativeColumn(1);     // Cumulative
                                            columns.RelativeColumn(0.6f);  // ROI
                                        });

                                        // Header
                                        table.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Month").Bold().FontSize(8);
                                        table.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Revenue").Bold().FontSize(8);
                                        table.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Expenses").Bold().FontSize(8);
                                        table.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Net Profit").Bold().FontSize(8);
                                        table.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Qty").Bold().FontSize(8);
                                        table.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Cumulative").Bold().FontSize(8);
                                        table.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("ROI").Bold().FontSize(8);

                                        // Data rows (limit to last 12 months for PDF)
                                        var monthsToShow = roiData.MonthlyHistory.TakeLast(12);
                                        foreach (var month in monthsToShow)
                                        {
                                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(3)
                                                .Text(month.MonthName).FontSize(8);
                                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(3)
                                                .AlignRight().Text($"{month.Revenue:N0}").FontSize(8);
                                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(3)
                                                .AlignRight().Text($"{month.Expenses:N0}").FontSize(8);
                                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(3)
                                                .AlignRight().Text($"{month.NetProfit:N0}").FontSize(8)
                                                .FontColor(month.NetProfit >= 0 ? successColor : errorColor);
                                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(3)
                                                .AlignRight().Text($"{month.Quantity:N0}").FontSize(8);
                                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(3)
                                                .AlignRight().Text($"{month.CumulativeProfit:N0}").FontSize(8);
                                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(3)
                                                .AlignRight().Text($"{month.ROI:N1}%").FontSize(8);
                                        }
                                    });
                                }
                                else
                                {
                                    rightCol.Item().PaddingTop(10).AlignCenter()
                                        .Text("No monthly data available").FontSize(10).FontColor(Colors.Grey.Medium);
                                }
                            });
                        });

                        // Period Summary Row
                        col.Item().PaddingTop(15).Background(Colors.Grey.Lighten4).Border(1).BorderColor(Colors.Grey.Lighten2)
                            .Padding(12).Row(summaryRow =>
                        {
                            summaryRow.RelativeItem().Column(sumCol =>
                            {
                                sumCol.Item().Text("PERIOD SUMMARY").Bold().FontSize(11);
                                sumCol.Item().PaddingTop(5).Row(r =>
                                {
                                    r.RelativeItem().Text(t => {
                                        t.Span("Total Revenue: ").Bold();
                                        t.Span($"KES {roiData.TotalRevenue:N0}");
                                    });
                                    r.RelativeItem().Text(t => {
                                        t.Span("Total Expenses: ").Bold();
                                        t.Span($"KES {roiData.TotalExpenses:N0}");
                                    });
                                    r.RelativeItem().Text(t => {
                                        t.Span("Total Quantity: ").Bold();
                                        t.Span($"{roiData.TotalQuantity:N0} pcs");
                                    });
                                    r.RelativeItem().Text(t => {
                                        t.Span("Total Orders: ").Bold();
                                        t.Span($"{roiData.TotalOrders:N0}");
                                    });
                                });
                            });
                            summaryRow.ConstantItem(200).Column(netCol =>
                            {
                                netCol.Item().AlignRight().Text("Cumulative Net Profit").Bold().FontSize(10);
                                netCol.Item().AlignRight().Text($"KES {roiData.CumulativeNetProfit:N0}")
                                    .Bold().FontSize(14)
                                    .FontColor(roiData.CumulativeNetProfit >= 0 ? successColor : errorColor);
                            });
                        });
                    });
                });

                // Footer
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("QDeskPro ROI Analysis Report | Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        });

        return await Task.FromResult(document.GeneratePdf());
    }

    #endregion
}

#region Report Data Models

/// <summary>
/// Sales report data with daily summaries and breakdowns
/// </summary>
public class SalesReportData
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    public int TotalOrders { get; set; }
    public double TotalQuantity { get; set; }
    public double TotalSales { get; set; }
    public double UnpaidAmount { get; set; }

    public double TotalCommission { get; set; }
    public double TotalLoadersFee { get; set; }
    public double TotalLandRateFee { get; set; }
    public double TotalOtherExpenses { get; set; }

    // Opening Balance - closing balance from day BEFORE the first date in range (e.g., Nov 30 for Dec 1-10)
    public double ActualOpeningBalance { get; set; }

    // Cash In Hand (B/F) - closing balance from day BEFORE the last date in range (e.g., Dec 9 for Dec 1-10)
    // This is used for the Net Income calculation since balances carry over daily
    public double OpeningBalance { get; set; }

    // Net Amount = (Earnings + Cash In Hand B/F + Collections) - Unpaid Orders
    public double NetAmount { get; set; }

    // Collections - payments received during report period for sales made BEFORE the period
    // These are previously unpaid orders that have now been paid
    public double TotalCollections { get; set; }
    public List<CollectionItem> CollectionItems { get; set; } = new();

    // Prepayments - customer deposits received during report period
    public double TotalPrepayments { get; set; }
    public List<PrepaymentReportItem> PrepaymentItems { get; set; } = new();

    public List<DailySalesBreakdown> DailySummaries { get; set; } = new();
    public List<ProductBreakdownItem> ProductBreakdown { get; set; } = new();
    public List<ClerkBreakdownItem> ClerkBreakdown { get; set; } = new();
}

/// <summary>
/// Collection item representing a previously unpaid sale that was paid during the report period
/// </summary>
public class CollectionItem
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
/// Prepayment item representing a customer deposit received during the report period
/// </summary>
public class PrepaymentReportItem
{
    public DateTime PrepaymentDate { get; set; }
    public string VehicleRegistration { get; set; } = "";
    public string? ClientName { get; set; }
    public string ProductName { get; set; } = "";
    public double AmountPaid { get; set; }
    public string? PaymentReference { get; set; }
}

public class DailySalesBreakdown
{
    public DateTime Date { get; set; }
    public int OrderCount { get; set; }
    public double Quantity { get; set; }
    public double Revenue { get; set; }
    public double Commission { get; set; }
    public double LoadersFee { get; set; }
    public double LandRateFee { get; set; }
    public double OtherExpenses { get; set; }
    public double TotalExpenses { get; set; }
    public double NetAmount { get; set; }
}

public class ProductBreakdownItem
{
    public string ProductName { get; set; } = "";
    public int OrderCount { get; set; }
    public double Quantity { get; set; }
    public double Revenue { get; set; }
}

public class ClerkBreakdownItem
{
    public string ClerkId { get; set; } = "";
    public string ClerkName { get; set; } = "";
    public int OrderCount { get; set; }
    public double Quantity { get; set; }
    public double Revenue { get; set; }
}

/// <summary>
/// Expenses report data with category breakdown
/// </summary>
public class ExpensesReportData
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    public int TotalExpenseItems { get; set; }
    public double TotalExpenses { get; set; }
    public double Commission { get; set; }
    public double LoadersFee { get; set; }
    public double LandRateFee { get; set; }
    public double ManualExpenses { get; set; }

    public List<ExpenseReportItem> ExpenseItems { get; set; } = new();
    public List<DailyExpenseItem> DailyExpenses { get; set; } = new();
}

public class ExpenseReportItem
{
    public DateTime Date { get; set; }
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public double Amount { get; set; }
}

public class DailyExpenseItem
{
    public DateTime Date { get; set; }
    public double Amount { get; set; }
    public double ManualAmount { get; set; }
    public double CommissionAmount { get; set; }
    public double LoadersFeeAmount { get; set; }
    public double LandRateAmount { get; set; }
}

/// <summary>
/// Fuel usage report data
/// </summary>
public class FuelReportData
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    public int TotalRecords { get; set; }
    public double TotalReceived { get; set; }
    public double MachinesUsage { get; set; }
    public double WheelLoadersUsage { get; set; }
    public double TotalUsage { get; set; }
    public double CurrentBalance { get; set; }

    public List<FuelUsageRecord> FuelRecords { get; set; } = new();
}

public class FuelUsageRecord
{
    public DateTime Date { get; set; }
    public double OldStock { get; set; }
    public double NewStock { get; set; }
    public double TotalStock { get; set; }
    public double MachinesLoaded { get; set; }
    public double WheelLoadersLoaded { get; set; }
    public double Balance { get; set; }
}

/// <summary>
/// Banking report data
/// </summary>
public class BankingReportData
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    public int TotalRecords { get; set; }
    public double TotalBanked { get; set; }
    public double AveragePerDay { get; set; }

    public List<BankingReportItem> BankingRecords { get; set; } = new();
    public List<DailyBankingItem> DailyBanking { get; set; } = new();
}

public class BankingReportItem
{
    public DateTime Date { get; set; }
    public string Description { get; set; } = "";
    public string Reference { get; set; } = "";
    public double Amount { get; set; }
}

public class DailyBankingItem
{
    public DateTime Date { get; set; }
    public double Amount { get; set; }
    public int TransactionCount { get; set; }
}

/// <summary>
/// Unpaid orders report data with aging analysis
/// </summary>
public class UnpaidOrdersReportData
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }

    public int TotalCount { get; set; }
    public double TotalAmount { get; set; }

    // Aging breakdown
    public int Over30DaysCount { get; set; }
    public double Over30DaysAmount { get; set; }
    public int Over60DaysCount { get; set; }
    public double Over60DaysAmount { get; set; }

    public int AverageDaysUnpaid { get; set; }
    public int OldestDays { get; set; }

    public List<UnpaidOrderItem> Items { get; set; } = new();
}

/// <summary>
/// Individual unpaid order item for reports
/// </summary>
public class UnpaidOrderItem
{
    public string SaleId { get; set; } = "";
    public DateTime SaleDate { get; set; }
    public int DaysUnpaid { get; set; }
    public string VehicleRegistration { get; set; } = "";
    public string ProductName { get; set; } = "";
    public double Quantity { get; set; }
    public double Amount { get; set; }
    public string? ClientName { get; set; }
    public string? ClientPhone { get; set; }
    public string ClerkName { get; set; } = "";
    public string QuarryName { get; set; } = "";
    public string QuarryId { get; set; } = "";
}

#endregion
