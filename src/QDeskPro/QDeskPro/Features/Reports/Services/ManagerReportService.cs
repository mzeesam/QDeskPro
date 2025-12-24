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
            report.TotalLoadersFee = sales.Sum(s => s.Quantity * (quarry.LoadersFee ?? 0));
            report.TotalLandRateFee = sales.Sum(s =>
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

        // Get Opening Balance - cumulative for date ranges
        // Opening balance for each day = previous day's closing balance (cash-in-hand)
        // For multi-day ranges, sum all opening balances across the range
        if (!string.IsNullOrEmpty(quarryId))
        {
            // For each day in the range, opening balance = previous day's closing
            // So we need DailyNotes from (fromDate - 1) to (toDate - 1)
            var openingBalanceStartStamp = fromDate.AddDays(-1).ToString("yyyyMMdd");
            var openingBalanceEndStamp = toDate.AddDays(-1).ToString("yyyyMMdd");

            var dailyNotes = await context.DailyNotes
                .Where(n => string.Compare(n.DateStamp, openingBalanceStartStamp) >= 0)
                .Where(n => string.Compare(n.DateStamp, openingBalanceEndStamp) <= 0)
                .Where(n => n.QId == quarryId)
                .Where(n => n.IsActive)
                .ToListAsync();

            // Sum all closing balances (these become opening balances for subsequent days)
            report.OpeningBalance = dailyNotes.Sum(n => n.ClosingBalance);
        }

        // Net Amount formula: (TotalSales - UnpaidAmount - TotalExpenses) + OpeningBalance
        // This matches the clerk report formula for consistency
        var totalExpenses = report.TotalCommission + report.TotalLoadersFee + report.TotalLandRateFee + report.TotalOtherExpenses;
        report.NetAmount = (report.TotalSales - report.UnpaidAmount - totalExpenses) + report.OpeningBalance;

        // Generate daily summaries
        var dailyGroups = sales.GroupBy(s => s.SaleDate?.Date ?? DateTime.Today);
        foreach (var group in dailyGroups.OrderBy(g => g.Key))
        {
            var daySales = group.ToList();
            var dayCommission = daySales.Sum(s => s.Quantity * s.CommissionPerUnit);
            var dayLoadersFee = quarry != null
                ? daySales.Sum(s => s.Quantity * (quarry.LoadersFee ?? 0))
                : 0;
            var dayLandRate = quarry != null
                ? daySales.Sum(s =>
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

        // 4. Land rate expenses
        if (quarry?.LandRateFee > 0)
        {
            foreach (var sale in sales)
            {
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

        // Net Summary
        row += 2;
        ws.Cell(row, 1).Value = "NET SUMMARY";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 14;
        ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.Lavender;
        ws.Range(row, 1, row, 3).Merge();

        var netIncome = salesData.TotalSales - expensesData.TotalExpenses;
        var cashInHand = netIncome - bankingData.TotalBanked;

        row++;
        ws.Cell(row, 1).Value = "Net Income:";
        ws.Cell(row, 2).Value = netIncome;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(row, 2).Style.Font.Bold = true;
        ws.Cell(row, 2).Style.Font.FontColor = netIncome >= 0 ? XLColor.Green : XLColor.Red;
        row++;
        ws.Cell(row, 1).Value = "Cash in Hand:";
        ws.Cell(row, 2).Value = cashInHand;
        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(row, 2).Style.Font.Bold = true;

        ws.Columns().AdjustToContents();
    }

    /// <summary>
    /// Export report to PDF using QuestPDF - Comprehensive landscape report with stats cards
    /// </summary>
    public async Task<byte[]> ExportToPdfAsync(string? quarryId, DateTime fromDate, DateTime toDate, string reportType)
    {
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
        var netIncome = (salesData.TotalSales - salesData.UnpaidAmount - totalExpenses) + salesData.OpeningBalance;
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
                                titleCol.Item().Text("QDesk Comprehensive Sales Report")
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

                            // Card 6: Cash in Hand
                            row.RelativeItem().Padding(3).Element(e => ComposeStatsCard(e,
                                "Cash in Hand", $"KES {cashInHand:N0}",
                                "Balance", cashInHand >= 0 ? successColor : errorColor, true));
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
                                sumCol.Item().PaddingTop(5).Row(r =>
                                {
                                    r.RelativeItem().Text(t => {
                                        t.Span("Opening Balance: ").Bold();
                                        t.Span($"KES {salesData.OpeningBalance:N2}");
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
                            });
                            summaryRow.ConstantItem(220).Column(netCol =>
                            {
                                // Net Income with formula in smaller font
                                netCol.Item().AlignRight().Text("Net Income").Bold().FontSize(10);
                                netCol.Item().AlignRight().Text("(Earnings + Opening balance - Unpaid orders)")
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
                        col.Item().Element(e => SectionHeader(e, "Detailed Sales Orders (Grouped by Date)", primaryColor));

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

                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(70);    // Date
                                    columns.RelativeColumn(3);    // Description
                                    columns.RelativeColumn(1);    // Category
                                    columns.ConstantColumn(100);   // Amount
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Background(errorColor).Padding(4).Text("Date").FontColor(Colors.White).Bold().FontSize(8);
                                    header.Cell().Background(errorColor).Padding(4).Text("Description").FontColor(Colors.White).Bold().FontSize(8);
                                    header.Cell().Background(errorColor).Padding(4).Text("Category").FontColor(Colors.White).Bold().FontSize(8);
                                    header.Cell().Background(errorColor).Padding(4).AlignRight().Text("Amount").FontColor(Colors.White).Bold().FontSize(8);
                                });

                                foreach (var expense in expensesData.ExpenseItems)
                                {
                                    var idx = expensesData.ExpenseItems.IndexOf(expense);
                                    var bgColor = idx % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;

                                    table.Cell().Background(bgColor).Padding(3).Text(expense.Date.ToString("dd/MM/yy")).FontSize(8);
                                    table.Cell().Background(bgColor).Padding(3).Text(expense.Description.Length > 60 ? expense.Description.Substring(0, 60) + "..." : expense.Description).FontSize(8);
                                    table.Cell().Background(bgColor).Padding(3).Text(expense.Category).FontSize(8);
                                    table.Cell().Background(bgColor).Padding(3).AlignRight().Text($"KES {expense.Amount:N0}").FontSize(8);
                                }

                                table.Cell().ColumnSpan(3).Background(Colors.Grey.Lighten3).Padding(4).Text("TOTAL EXPENSES").Bold().FontSize(9);
                                table.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text($"KES {expensesData.TotalExpenses:N0}").Bold().FontSize(9);
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
                    });
                });

                // FOOTER
                page.Footer().Element(footer =>
                {
                    footer.Row(row =>
                    {
                        row.RelativeItem().Text("QDesk Comprehensive Sales Report - Quarry Management System")
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

    // Opening balance (previous day's closing balance for the first day of range)
    public double OpeningBalance { get; set; }

    // Net Amount = (TotalSales - UnpaidAmount - TotalExpenses) + OpeningBalance
    // This matches the clerk report formula for consistency
    public double NetAmount { get; set; }

    public List<DailySalesBreakdown> DailySummaries { get; set; } = new();
    public List<ProductBreakdownItem> ProductBreakdown { get; set; } = new();
    public List<ClerkBreakdownItem> ClerkBreakdown { get; set; } = new();
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

#endregion
