using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QDeskPro.Data;
using QDeskPro.Domain.Entities;

namespace QDeskPro.Features.Reports.Services;

public class ExcelExportService
{
    private readonly AppDbContext _context;
    private readonly ReportService _reportService;

    public ExcelExportService(AppDbContext context, ReportService reportService)
    {
        _context = context;
        _reportService = reportService;
    }

    public async Task<byte[]> GenerateSalesReportAsync(
        string quarryId,
        DateTime fromDate,
        DateTime toDate,
        string? userId = null)
    {
        using var workbook = new XLWorkbook();

        // Get report data
        var reportData = await _reportService.GenerateReportAsync(quarryId, fromDate, toDate, userId);

        // Add worksheets
        AddSalesWorksheet(workbook, reportData.Sales);
        AddExpensesWorksheet(workbook, reportData.ExpenseItems);

        if (reportData.FuelUsages.Any())
        {
            AddFuelUsageWorksheet(workbook, reportData.FuelUsages);
        }

        if (reportData.Bankings.Any())
        {
            AddBankingWorksheet(workbook, reportData.Bankings);
        }

        AddSummaryWorksheet(workbook, reportData, fromDate, toDate);

        // Save to memory stream
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private void AddSalesWorksheet(IXLWorkbook workbook, List<Sale> sales)
    {
        var worksheet = workbook.Worksheets.Add("Sales");

        // Headers
        worksheet.Cell(1, 1).Value = "Date";
        worksheet.Cell(1, 2).Value = "Time";
        worksheet.Cell(1, 3).Value = "Vehicle";
        worksheet.Cell(1, 4).Value = "Client";
        worksheet.Cell(1, 5).Value = "Product";
        worksheet.Cell(1, 6).Value = "Layer";
        worksheet.Cell(1, 7).Value = "Quantity";
        worksheet.Cell(1, 8).Value = "Price/Unit";
        worksheet.Cell(1, 9).Value = "Amount";
        worksheet.Cell(1, 10).Value = "Payment Mode";
        worksheet.Cell(1, 11).Value = "Payment Status";
        worksheet.Cell(1, 12).Value = "Reference";
        worksheet.Cell(1, 13).Value = "Clerk";

        // Header formatting
        var headerRange = worksheet.Range(1, 1, 1, 13);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        // Data rows
        int row = 2;
        foreach (var sale in sales.OrderBy(s => s.SaleDate))
        {
            worksheet.Cell(row, 1).Value = sale.SaleDate?.ToString("dd/MM/yyyy");
            worksheet.Cell(row, 2).Value = sale.SaleDate?.ToString("HH:mm");
            worksheet.Cell(row, 3).Value = sale.VehicleRegistration;
            worksheet.Cell(row, 4).Value = sale.ClientName ?? "";
            worksheet.Cell(row, 5).Value = sale.Product?.ProductName ?? "";
            worksheet.Cell(row, 6).Value = sale.Layer?.LayerLevel ?? "";
            worksheet.Cell(row, 7).Value = sale.Quantity;
            worksheet.Cell(row, 8).Value = sale.PricePerUnit;
            worksheet.Cell(row, 9).Value = sale.GrossSaleAmount;
            worksheet.Cell(row, 10).Value = sale.PaymentMode;
            worksheet.Cell(row, 11).Value = sale.PaymentStatus;
            worksheet.Cell(row, 12).Value = sale.PaymentReference ?? "";
            worksheet.Cell(row, 13).Value = sale.ClerkName ?? "";

            // Highlight unpaid orders
            if (sale.PaymentStatus != "Paid")
            {
                worksheet.Range(row, 1, row, 13).Style.Fill.BackgroundColor = XLColor.LightPink;
            }

            row++;
        }

        // Totals row
        if (sales.Any())
        {
            worksheet.Cell(row, 6).Value = "TOTAL:";
            worksheet.Cell(row, 6).Style.Font.Bold = true;
            worksheet.Cell(row, 7).Value = sales.Sum(s => s.Quantity);
            worksheet.Cell(row, 9).Value = sales.Sum(s => s.GrossSaleAmount);
            worksheet.Range(row, 6, row, 9).Style.Font.Bold = true;
            worksheet.Range(row, 6, row, 9).Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();
    }

    private void AddExpensesWorksheet(IXLWorkbook workbook, List<SaleReportLineItem> expenses)
    {
        var worksheet = workbook.Worksheets.Add("Expenses");

        // Headers
        worksheet.Cell(1, 1).Value = "Date";
        worksheet.Cell(1, 2).Value = "Description";
        worksheet.Cell(1, 3).Value = "Type";
        worksheet.Cell(1, 4).Value = "Amount";

        // Header formatting
        var headerRange = worksheet.Range(1, 1, 1, 4);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        // Data rows
        int row = 2;
        foreach (var expense in expenses.OrderBy(e => e.ItemDate).ThenBy(e => e.LineType))
        {
            worksheet.Cell(row, 1).Value = expense.ItemDate.ToString("dd/MM/yyyy");
            worksheet.Cell(row, 2).Value = expense.LineItem;
            worksheet.Cell(row, 3).Value = expense.LineType;
            worksheet.Cell(row, 4).Value = expense.Amount;
            row++;
        }

        // Category totals
        if (expenses.Any())
        {
            row++; // Empty row

            var commissionTotal = expenses.Where(e => e.LineType == "Commission Expense").Sum(e => e.Amount);
            var loadersFeeTotal = expenses.Where(e => e.LineType == "Loaders Fee Expense").Sum(e => e.Amount);
            var landRateTotal = expenses.Where(e => e.LineType == "Land Rate Fee Expense").Sum(e => e.Amount);
            var userExpensesTotal = expenses.Where(e => e.LineType == "User Expense").Sum(e => e.Amount);

            worksheet.Cell(row, 2).Value = "Commission Total:";
            worksheet.Cell(row, 4).Value = commissionTotal;
            row++;

            worksheet.Cell(row, 2).Value = "Loaders Fee Total:";
            worksheet.Cell(row, 4).Value = loadersFeeTotal;
            row++;

            if (landRateTotal > 0)
            {
                worksheet.Cell(row, 2).Value = "Land Rate Total:";
                worksheet.Cell(row, 4).Value = landRateTotal;
                row++;
            }

            worksheet.Cell(row, 2).Value = "User Expenses Total:";
            worksheet.Cell(row, 4).Value = userExpensesTotal;
            row++;

            row++; // Empty row
            worksheet.Cell(row, 2).Value = "TOTAL EXPENSES:";
            worksheet.Cell(row, 4).Value = expenses.Sum(e => e.Amount);
            worksheet.Range(row, 2, row, 4).Style.Font.Bold = true;
            worksheet.Range(row, 2, row, 4).Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();
    }

    private void AddFuelUsageWorksheet(IXLWorkbook workbook, List<Domain.Entities.FuelUsage> fuelUsages)
    {
        var worksheet = workbook.Worksheets.Add("Fuel Usage");

        // Headers
        worksheet.Cell(1, 1).Value = "Date";
        worksheet.Cell(1, 2).Value = "Old Stock (L)";
        worksheet.Cell(1, 3).Value = "New Stock (L)";
        worksheet.Cell(1, 4).Value = "Total Stock (L)";
        worksheet.Cell(1, 5).Value = "Machines (L)";
        worksheet.Cell(1, 6).Value = "Wheel Loaders (L)";
        worksheet.Cell(1, 7).Value = "Balance (L)";

        // Header formatting
        var headerRange = worksheet.Range(1, 1, 1, 7);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        // Data rows
        int row = 2;
        foreach (var fuel in fuelUsages.OrderBy(f => f.UsageDate))
        {
            worksheet.Cell(row, 1).Value = fuel.UsageDate?.ToString("dd/MM/yyyy");
            worksheet.Cell(row, 2).Value = fuel.OldStock;
            worksheet.Cell(row, 3).Value = fuel.NewStock;
            worksheet.Cell(row, 4).Value = fuel.TotalStock;
            worksheet.Cell(row, 5).Value = fuel.MachinesLoaded;
            worksheet.Cell(row, 6).Value = fuel.WheelLoadersLoaded;
            worksheet.Cell(row, 7).Value = fuel.Balance;
            row++;
        }

        // Totals row
        if (fuelUsages.Any())
        {
            worksheet.Cell(row, 1).Value = "TOTAL:";
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 3).Value = fuelUsages.Sum(f => f.NewStock);
            worksheet.Cell(row, 5).Value = fuelUsages.Sum(f => f.MachinesLoaded);
            worksheet.Cell(row, 6).Value = fuelUsages.Sum(f => f.WheelLoadersLoaded);
            worksheet.Range(row, 1, row, 7).Style.Font.Bold = true;
            worksheet.Range(row, 1, row, 7).Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();
    }

    private void AddBankingWorksheet(IXLWorkbook workbook, List<Domain.Entities.Banking> bankings)
    {
        var worksheet = workbook.Worksheets.Add("Banking");

        // Headers
        worksheet.Cell(1, 1).Value = "Date";
        worksheet.Cell(1, 2).Value = "Description";
        worksheet.Cell(1, 3).Value = "Amount Banked";
        worksheet.Cell(1, 4).Value = "Reference";

        // Header formatting
        var headerRange = worksheet.Range(1, 1, 1, 4);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        // Data rows
        int row = 2;
        foreach (var banking in bankings.OrderBy(b => b.BankingDate))
        {
            worksheet.Cell(row, 1).Value = banking.BankingDate?.ToString("dd/MM/yyyy");
            worksheet.Cell(row, 2).Value = banking.Item;
            worksheet.Cell(row, 3).Value = banking.AmountBanked;
            worksheet.Cell(row, 4).Value = banking.TxnReference ?? "";
            row++;
        }

        // Totals row
        if (bankings.Any())
        {
            worksheet.Cell(row, 2).Value = "TOTAL BANKED:";
            worksheet.Cell(row, 2).Style.Font.Bold = true;
            worksheet.Cell(row, 3).Value = bankings.Sum(b => b.AmountBanked);
            worksheet.Range(row, 2, row, 3).Style.Font.Bold = true;
            worksheet.Range(row, 2, row, 3).Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();
    }

    private void AddSummaryWorksheet(IXLWorkbook workbook, ClerkReportData reportData, DateTime fromDate, DateTime toDate)
    {
        var worksheet = workbook.Worksheets.Add("Summary");

        // Title
        worksheet.Cell(1, 1).Value = "Sales Report Summary";
        worksheet.Cell(1, 1).Style.Font.FontSize = 16;
        worksheet.Cell(1, 1).Style.Font.Bold = true;

        // Quarry and date range
        var quarry = _context.Quarries.Find(reportData.Sales.FirstOrDefault()?.QId);
        worksheet.Cell(2, 1).Value = $"Quarry: {quarry?.QuarryName ?? "Unknown"}";
        worksheet.Cell(3, 1).Value = $"Period: {fromDate:dd/MM/yyyy} to {toDate:dd/MM/yyyy}";

        int row = 5;

        // Metrics
        worksheet.Cell(row, 1).Value = "Metric";
        worksheet.Cell(row, 2).Value = "Value";
        worksheet.Range(row, 1, row, 2).Style.Font.Bold = true;
        worksheet.Range(row, 1, row, 2).Style.Fill.BackgroundColor = XLColor.LightBlue;
        row++;

        // Opening Balance (only for single-day reports)
        if (fromDate.Date == toDate.Date && reportData.OpeningBalance > 0)
        {
            worksheet.Cell(row, 1).Value = "Opening Balance";
            worksheet.Cell(row, 2).Value = reportData.OpeningBalance;
            row++;
        }

        worksheet.Cell(row, 1).Value = "Total Quantity (pieces)";
        worksheet.Cell(row, 2).Value = reportData.TotalQuantity;
        row++;

        worksheet.Cell(row, 1).Value = "Total Sales";
        worksheet.Cell(row, 2).Value = reportData.TotalSales;
        row++;

        worksheet.Cell(row, 1).Value = "Commission";
        worksheet.Cell(row, 2).Value = reportData.Commission;
        row++;

        worksheet.Cell(row, 1).Value = "Loaders Fee";
        worksheet.Cell(row, 2).Value = reportData.LoadersFee;
        row++;

        if (reportData.LandRateFee > 0)
        {
            worksheet.Cell(row, 1).Value = "Land Rate Fee";
            worksheet.Cell(row, 2).Value = reportData.LandRateFee;
            row++;
        }

        worksheet.Cell(row, 1).Value = "Total Expenses";
        worksheet.Cell(row, 2).Value = reportData.TotalExpenses;
        row++;

        worksheet.Cell(row, 1).Value = "Earnings";
        worksheet.Cell(row, 2).Value = reportData.Earnings;
        row++;

        if (reportData.Unpaid > 0)
        {
            worksheet.Cell(row, 1).Value = "Unpaid Orders";
            worksheet.Cell(row, 2).Value = reportData.Unpaid;
            worksheet.Range(row, 1, row, 2).Style.Fill.BackgroundColor = XLColor.LightPink;
            row++;
        }

        worksheet.Cell(row, 1).Value = "Net Earnings";
        worksheet.Cell(row, 2).Value = reportData.NetEarnings;
        row++;

        worksheet.Cell(row, 1).Value = "Total Banked";
        worksheet.Cell(row, 2).Value = reportData.Banked;
        row++;

        // Closing Balance (only for single-day reports)
        if (fromDate.Date == toDate.Date)
        {
            worksheet.Cell(row, 1).Value = "Closing Balance (Cash in Hand)";
            worksheet.Cell(row, 2).Value = reportData.CashInHand;
            worksheet.Range(row, 1, row, 2).Style.Font.Bold = true;
            worksheet.Range(row, 1, row, 2).Style.Fill.BackgroundColor = XLColor.LightGreen;
        }

        // Format currency columns
        worksheet.Column(2).Style.NumberFormat.Format = "#,##0.00";

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();
    }

    public async Task<byte[]> GenerateCashFlowReportAsync(
        string quarryId,
        DateTime fromDate,
        DateTime toDate)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Cash Flow");

        // Get all sales and expenses for the period
        var fromStamp = DateOnly.FromDateTime(fromDate).ToString("yyyyMMdd");
        var toStamp = DateOnly.FromDateTime(toDate).ToString("yyyyMMdd");

        var sales = await _context.Sales
            .Include(s => s.Product)
            .Where(s => s.QId == quarryId)
            .Where(s => string.Compare(s.DateStamp, fromStamp) >= 0 && string.Compare(s.DateStamp, toStamp) <= 0)
            .ToListAsync();

        var bankings = await _context.Bankings
            .Where(b => b.QId == quarryId)
            .Where(b => string.Compare(b.DateStamp, fromStamp) >= 0 && string.Compare(b.DateStamp, toStamp) <= 0)
            .ToListAsync();

        // Headers
        worksheet.Cell(1, 1).Value = "Date";
        worksheet.Cell(1, 2).Value = "Description";
        worksheet.Cell(1, 3).Value = "Cash In";
        worksheet.Cell(1, 4).Value = "Cash Out";
        worksheet.Cell(1, 5).Value = "Balance";

        // Header formatting
        var headerRange = worksheet.Range(1, 1, 1, 5);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;

        int row = 2;
        double runningBalance = 0;

        // Combine and sort transactions
        var transactions = new List<(DateTime Date, string Description, double CashIn, double CashOut)>();

        foreach (var sale in sales.Where(s => s.PaymentStatus == "Paid"))
        {
            transactions.Add((
                sale.SaleDate ?? DateTime.MinValue,
                $"Sale: {sale.VehicleRegistration} - {sale.Product?.ProductName}",
                sale.GrossSaleAmount,
                0
            ));
        }

        foreach (var banking in bankings)
        {
            transactions.Add((
                banking.BankingDate ?? DateTime.MinValue,
                $"Banking: {banking.Item}",
                0,
                banking.AmountBanked
            ));
        }

        foreach (var transaction in transactions.OrderBy(t => t.Date))
        {
            runningBalance += transaction.CashIn - transaction.CashOut;

            worksheet.Cell(row, 1).Value = transaction.Date.ToString("dd/MM/yyyy HH:mm");
            worksheet.Cell(row, 2).Value = transaction.Description;
            worksheet.Cell(row, 3).Value = transaction.CashIn;
            worksheet.Cell(row, 4).Value = transaction.CashOut;
            worksheet.Cell(row, 5).Value = runningBalance;
            row++;
        }

        // Format currency columns
        worksheet.Columns(3, 5).Style.NumberFormat.Format = "#,##0.00";

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();

        // Save to memory stream
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
