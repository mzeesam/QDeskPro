namespace QDeskPro.Features.MasterData.Services;

using ClosedXML.Excel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QDeskPro.Data;
using QDeskPro.Domain.Entities;

/// <summary>
/// Service for managing quarry data operations: clear, import, and export
/// Used by managers to bulk manage their quarry data
/// </summary>
public class DataManagementService
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<DataManagementService> _logger;

    // Valid payment statuses and modes
    private static readonly string[] ValidPaymentStatuses = ["Paid", "NotPaid"];
    private static readonly string[] ValidPaymentModes = ["Cash", "MPESA", "Bank Transfer"];
    private static readonly string[] ValidExpenseCategories = [
        "Fuel", "Transportation Hire", "Maintenance and Repairs", "Commission",
        "Administrative", "Marketing", "Wages", "Loaders Fees",
        "Consumables and Utilities", "Bank Charges", "Cess and Road Fees", "Miscellaneous"
    ];

    public DataManagementService(
        AppDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<DataManagementService> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    #region Clear Data

    /// <summary>
    /// Verify user password before performing sensitive operations
    /// </summary>
    public async Task<bool> VerifyPasswordAsync(string userId, string password)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return false;

        return await _userManager.CheckPasswordAsync(user, password);
    }

    /// <summary>
    /// Clear all operational data for a quarry (soft delete)
    /// Clears: Sales, Expenses, Banking, FuelUsage
    /// Requires password verification before clearing
    /// </summary>
    public async Task<(bool Success, string Message, DataClearSummary? Summary)> ClearQuarryDataAsync(
        string quarryId, string userId, string password)
    {
        try
        {
            // Verify password before proceeding
            if (!await VerifyPasswordAsync(userId, password))
            {
                _logger.LogWarning("Failed password verification for data clear attempt by user {UserId}", userId);
                return (false, "Incorrect password. Please try again.", null);
            }

            _logger.LogWarning("Clearing data for quarry {QuarryId} by user {UserId}", quarryId, userId);

            var now = DateTime.UtcNow;
            var summary = new DataClearSummary();

            // Clear Sales
            var sales = await _context.Sales
                .Where(s => s.QId == quarryId && s.IsActive)
                .ToListAsync();
            foreach (var sale in sales)
            {
                sale.IsActive = false;
                sale.DateModified = now;
                sale.ModifiedBy = userId;
            }
            summary.SalesCleared = sales.Count;

            // Clear Expenses
            var expenses = await _context.Expenses
                .Where(e => e.QId == quarryId && e.IsActive)
                .ToListAsync();
            foreach (var expense in expenses)
            {
                expense.IsActive = false;
                expense.DateModified = now;
                expense.ModifiedBy = userId;
            }
            summary.ExpensesCleared = expenses.Count;

            // Clear Banking
            var bankings = await _context.Bankings
                .Where(b => b.QId == quarryId && b.IsActive)
                .ToListAsync();
            foreach (var banking in bankings)
            {
                banking.IsActive = false;
                banking.DateModified = now;
                banking.ModifiedBy = userId;
            }
            summary.BankingCleared = bankings.Count;

            // Clear Fuel Usage
            var fuelUsages = await _context.FuelUsages
                .Where(f => f.QId == quarryId && f.IsActive)
                .ToListAsync();
            foreach (var fuel in fuelUsages)
            {
                fuel.IsActive = false;
                fuel.DateModified = now;
                fuel.ModifiedBy = userId;
            }
            summary.FuelUsageCleared = fuelUsages.Count;

            // Clear Daily Notes
            var dailyNotes = await _context.DailyNotes
                .Where(d => d.QId == quarryId && d.IsActive)
                .ToListAsync();
            foreach (var note in dailyNotes)
            {
                note.IsActive = false;
                note.DateModified = now;
                note.ModifiedBy = userId;
            }
            summary.DailyNotesCleared = dailyNotes.Count;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Data cleared for quarry {QuarryId}: {Summary}", quarryId, summary);

            return (true, "Data cleared successfully", summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing data for quarry {QuarryId}", quarryId);
            return (false, $"Error clearing data: {ex.Message}", null);
        }
    }

    #endregion

    #region Export Data

    /// <summary>
    /// Export all quarry data to Excel format suitable for import
    /// </summary>
    public async Task<byte[]> ExportQuarryDataAsync(string quarryId)
    {
        _logger.LogInformation("Exporting data for quarry {QuarryId}", quarryId);

        using var workbook = new XLWorkbook();

        // Get quarry info
        var quarry = await _context.Quarries.FindAsync(quarryId);
        var quarryName = quarry?.QuarryName ?? "Unknown";

        // Load master data for reference sheet
        var products = await _context.Products.Where(p => p.IsActive).ToListAsync();
        var layers = await _context.Layers.Where(l => l.QId == quarryId && l.IsActive).ToListAsync();
        var brokers = await _context.Brokers.Where(b => b.quarryId == quarryId && b.IsActive).ToListAsync();

        // Export each data type
        await ExportSalesSheet(workbook, quarryId);
        await ExportExpensesSheet(workbook, quarryId);
        await ExportBankingSheet(workbook, quarryId);
        await ExportFuelUsageSheet(workbook, quarryId);

        // Add Reference Data sheet (helps users know valid values)
        AddReferenceDataSheet(workbook, products, layers, brokers);

        // Add Instructions sheet
        AddInstructionsSheet(workbook, quarryName, products, layers, brokers);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Export quarry data filtered by date range to Excel format
    /// Used by managers to export specific periods
    /// </summary>
    public async Task<byte[]> ExportQuarryDataByDateRangeAsync(string quarryId, DateTime fromDate, DateTime toDate)
    {
        _logger.LogInformation("Exporting data for quarry {QuarryId} from {From} to {To}", quarryId, fromDate, toDate);

        using var workbook = new XLWorkbook();

        // Get quarry info
        var quarry = await _context.Quarries.FindAsync(quarryId);
        var quarryName = quarry?.QuarryName ?? "Unknown";

        var fromStamp = fromDate.ToString("yyyyMMdd");
        var toStamp = toDate.ToString("yyyyMMdd");

        // Export each data type with date filter
        await ExportSalesSheetByDateRange(workbook, quarryId, fromStamp, toStamp);
        await ExportExpensesSheetByDateRange(workbook, quarryId, fromStamp, toStamp);
        await ExportBankingSheetByDateRange(workbook, quarryId, fromStamp, toStamp);
        await ExportFuelUsageSheetByDateRange(workbook, quarryId, fromStamp, toStamp);

        // Add summary sheet
        AddDateRangeSummarySheet(workbook, quarryName, fromDate, toDate);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private async Task ExportSalesSheetByDateRange(IXLWorkbook workbook, string quarryId, string fromStamp, string toStamp)
    {
        var worksheet = workbook.Worksheets.Add("Sales");

        // Headers
        var headers = new[]
        {
            "SaleDate", "VehicleRegistration", "ClientName", "ClientPhone", "Destination",
            "Product", "Layer", "Quantity", "PricePerUnit", "Broker", "CommissionPerUnit",
            "PaymentStatus", "PaymentMode", "PaymentReference", "ClerkName", "GrossAmount"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
        }

        // Header formatting
        var headerRange = worksheet.Range(1, 1, 1, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        // Get sales with related data filtered by date
        var sales = await _context.Sales
            .Where(s => s.QId == quarryId && s.IsActive)
            .Where(s => string.Compare(s.DateStamp, fromStamp) >= 0)
            .Where(s => string.Compare(s.DateStamp, toStamp) <= 0)
            .Include(s => s.Product)
            .Include(s => s.Layer)
            .Include(s => s.Broker)
            .OrderBy(s => s.SaleDate)
            .ToListAsync();

        int row = 2;
        foreach (var sale in sales)
        {
            worksheet.Cell(row, 1).Value = sale.SaleDate?.ToString("yyyy-MM-dd");
            worksheet.Cell(row, 2).Value = sale.VehicleRegistration;
            worksheet.Cell(row, 3).Value = sale.ClientName ?? "";
            worksheet.Cell(row, 4).Value = sale.ClientPhone ?? "";
            worksheet.Cell(row, 5).Value = sale.Destination ?? "";
            worksheet.Cell(row, 6).Value = sale.Product?.ProductName ?? "";
            worksheet.Cell(row, 7).Value = sale.Layer?.LayerLevel ?? "";
            worksheet.Cell(row, 8).Value = sale.Quantity;
            worksheet.Cell(row, 9).Value = sale.PricePerUnit;
            worksheet.Cell(row, 10).Value = sale.Broker?.BrokerName ?? "";
            worksheet.Cell(row, 11).Value = sale.CommissionPerUnit;
            worksheet.Cell(row, 12).Value = sale.PaymentStatus ?? "Paid";
            worksheet.Cell(row, 13).Value = sale.PaymentMode ?? "Cash";
            worksheet.Cell(row, 14).Value = sale.PaymentReference ?? "";
            worksheet.Cell(row, 15).Value = sale.ClerkName ?? "";
            worksheet.Cell(row, 16).Value = sale.GrossSaleAmount;
            row++;
        }

        // Add totals row
        if (sales.Any())
        {
            row++;
            worksheet.Cell(row, 7).Value = "TOTALS:";
            worksheet.Cell(row, 7).Style.Font.Bold = true;
            worksheet.Cell(row, 8).Value = sales.Sum(s => s.Quantity);
            worksheet.Cell(row, 8).Style.Font.Bold = true;
            worksheet.Cell(row, 16).Value = sales.Sum(s => s.GrossSaleAmount);
            worksheet.Cell(row, 16).Style.Font.Bold = true;
        }

        worksheet.Columns().AdjustToContents();
    }

    private async Task ExportExpensesSheetByDateRange(IXLWorkbook workbook, string quarryId, string fromStamp, string toStamp)
    {
        var worksheet = workbook.Worksheets.Add("Expenses");

        // Headers
        var headers = new[] { "ExpenseDate", "Item", "Amount", "Category", "TxnReference" };

        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
        }

        // Header formatting
        var headerRange = worksheet.Range(1, 1, 1, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGreen;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        // Get expenses filtered by date
        var expenses = await _context.Expenses
            .Where(e => e.QId == quarryId && e.IsActive)
            .Where(e => string.Compare(e.DateStamp, fromStamp) >= 0)
            .Where(e => string.Compare(e.DateStamp, toStamp) <= 0)
            .OrderBy(e => e.ExpenseDate)
            .ToListAsync();

        int row = 2;
        foreach (var expense in expenses)
        {
            worksheet.Cell(row, 1).Value = expense.ExpenseDate?.ToString("yyyy-MM-dd");
            worksheet.Cell(row, 2).Value = expense.Item;
            worksheet.Cell(row, 3).Value = expense.Amount;
            worksheet.Cell(row, 4).Value = expense.Category ?? "Miscellaneous";
            worksheet.Cell(row, 5).Value = expense.TxnReference ?? "";
            row++;
        }

        // Add totals row
        if (expenses.Any())
        {
            row++;
            worksheet.Cell(row, 2).Value = "TOTAL:";
            worksheet.Cell(row, 2).Style.Font.Bold = true;
            worksheet.Cell(row, 3).Value = expenses.Sum(e => e.Amount);
            worksheet.Cell(row, 3).Style.Font.Bold = true;
        }

        worksheet.Columns().AdjustToContents();
    }

    private async Task ExportBankingSheetByDateRange(IXLWorkbook workbook, string quarryId, string fromStamp, string toStamp)
    {
        var worksheet = workbook.Worksheets.Add("Banking");

        // Headers
        var headers = new[] { "BankingDate", "Item", "AmountBanked", "TxnReference" };

        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
        }

        // Header formatting
        var headerRange = worksheet.Range(1, 1, 1, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightYellow;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        // Get banking records filtered by date
        var bankings = await _context.Bankings
            .Where(b => b.QId == quarryId && b.IsActive)
            .Where(b => string.Compare(b.DateStamp, fromStamp) >= 0)
            .Where(b => string.Compare(b.DateStamp, toStamp) <= 0)
            .OrderBy(b => b.BankingDate)
            .ToListAsync();

        int row = 2;
        foreach (var banking in bankings)
        {
            worksheet.Cell(row, 1).Value = banking.BankingDate?.ToString("yyyy-MM-dd");
            worksheet.Cell(row, 2).Value = banking.Item;
            worksheet.Cell(row, 3).Value = banking.AmountBanked;
            worksheet.Cell(row, 4).Value = banking.TxnReference ?? "";
            row++;
        }

        // Add totals row
        if (bankings.Any())
        {
            row++;
            worksheet.Cell(row, 2).Value = "TOTAL:";
            worksheet.Cell(row, 2).Style.Font.Bold = true;
            worksheet.Cell(row, 3).Value = bankings.Sum(b => b.AmountBanked);
            worksheet.Cell(row, 3).Style.Font.Bold = true;
        }

        worksheet.Columns().AdjustToContents();
    }

    private async Task ExportFuelUsageSheetByDateRange(IXLWorkbook workbook, string quarryId, string fromStamp, string toStamp)
    {
        var worksheet = workbook.Worksheets.Add("FuelUsage");

        // Headers
        var headers = new[] { "UsageDate", "OldStock", "NewStock", "TotalStock", "MachinesLoaded", "WheelLoadersLoaded", "TotalUsed", "Balance" };

        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
        }

        // Header formatting
        var headerRange = worksheet.Range(1, 1, 1, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightCoral;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        // Get fuel usage records filtered by date
        var fuelUsages = await _context.FuelUsages
            .Where(f => f.QId == quarryId && f.IsActive)
            .Where(f => string.Compare(f.DateStamp, fromStamp) >= 0)
            .Where(f => string.Compare(f.DateStamp, toStamp) <= 0)
            .OrderBy(f => f.UsageDate)
            .ToListAsync();

        int row = 2;
        foreach (var fuel in fuelUsages)
        {
            worksheet.Cell(row, 1).Value = fuel.UsageDate?.ToString("yyyy-MM-dd");
            worksheet.Cell(row, 2).Value = fuel.OldStock;
            worksheet.Cell(row, 3).Value = fuel.NewStock;
            worksheet.Cell(row, 4).Value = fuel.TotalStock;
            worksheet.Cell(row, 5).Value = fuel.MachinesLoaded;
            worksheet.Cell(row, 6).Value = fuel.WheelLoadersLoaded;
            worksheet.Cell(row, 7).Value = fuel.Used;
            worksheet.Cell(row, 8).Value = fuel.Balance;
            row++;
        }

        // Add totals row
        if (fuelUsages.Any())
        {
            row++;
            worksheet.Cell(row, 4).Value = "TOTALS:";
            worksheet.Cell(row, 4).Style.Font.Bold = true;
            worksheet.Cell(row, 5).Value = fuelUsages.Sum(f => f.MachinesLoaded);
            worksheet.Cell(row, 5).Style.Font.Bold = true;
            worksheet.Cell(row, 6).Value = fuelUsages.Sum(f => f.WheelLoadersLoaded);
            worksheet.Cell(row, 6).Style.Font.Bold = true;
            worksheet.Cell(row, 7).Value = fuelUsages.Sum(f => f.Used);
            worksheet.Cell(row, 7).Style.Font.Bold = true;
        }

        worksheet.Columns().AdjustToContents();
    }

    private void AddDateRangeSummarySheet(IXLWorkbook workbook, string quarryName, DateTime fromDate, DateTime toDate)
    {
        var worksheet = workbook.Worksheets.Add("Summary");

        worksheet.Cell(1, 1).Value = "QDeskPro Data Export";
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 14;

        worksheet.Cell(3, 1).Value = $"Quarry: {quarryName}";
        worksheet.Cell(4, 1).Value = $"Date Range: {fromDate:dd/MM/yyyy} - {toDate:dd/MM/yyyy}";
        worksheet.Cell(5, 1).Value = $"Export Date: {DateTime.Now:dd/MM/yyyy HH:mm}";

        worksheet.Cell(7, 1).Value = "This file contains data for the specified date range.";
        worksheet.Cell(8, 1).Value = "Sheets included: Sales, Expenses, Banking, FuelUsage";

        worksheet.Columns().AdjustToContents();
    }

    private async Task ExportSalesSheet(IXLWorkbook workbook, string quarryId)
    {
        var worksheet = workbook.Worksheets.Add("Sales");

        // Headers matching import format
        var headers = new[]
        {
            "SaleDate", "VehicleRegistration", "ClientName", "ClientPhone", "Destination",
            "Product", "Layer", "Quantity", "PricePerUnit", "Broker", "CommissionPerUnit",
            "PaymentStatus", "PaymentMode", "PaymentReference"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
        }

        // Header formatting
        var headerRange = worksheet.Range(1, 1, 1, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        // Get sales with related data
        var sales = await _context.Sales
            .Where(s => s.QId == quarryId && s.IsActive)
            .Include(s => s.Product)
            .Include(s => s.Layer)
            .Include(s => s.Broker)
            .OrderBy(s => s.SaleDate)
            .ToListAsync();

        int row = 2;
        foreach (var sale in sales)
        {
            worksheet.Cell(row, 1).Value = sale.SaleDate?.ToString("yyyy-MM-dd");
            worksheet.Cell(row, 2).Value = sale.VehicleRegistration;
            worksheet.Cell(row, 3).Value = sale.ClientName ?? "";
            worksheet.Cell(row, 4).Value = sale.ClientPhone ?? "";
            worksheet.Cell(row, 5).Value = sale.Destination ?? "";
            worksheet.Cell(row, 6).Value = sale.Product?.ProductName ?? "";
            worksheet.Cell(row, 7).Value = sale.Layer?.LayerLevel ?? "";
            worksheet.Cell(row, 8).Value = sale.Quantity;
            worksheet.Cell(row, 9).Value = sale.PricePerUnit;
            worksheet.Cell(row, 10).Value = sale.Broker?.BrokerName ?? "";
            worksheet.Cell(row, 11).Value = sale.CommissionPerUnit;
            worksheet.Cell(row, 12).Value = sale.PaymentStatus ?? "Paid";
            worksheet.Cell(row, 13).Value = sale.PaymentMode ?? "Cash";
            worksheet.Cell(row, 14).Value = sale.PaymentReference ?? "";
            row++;
        }

        worksheet.Columns().AdjustToContents();
    }

    private async Task ExportExpensesSheet(IXLWorkbook workbook, string quarryId)
    {
        var worksheet = workbook.Worksheets.Add("Expenses");

        // Headers
        var headers = new[] { "ExpenseDate", "Item", "Amount", "Category", "TxnReference" };

        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
        }

        // Header formatting
        var headerRange = worksheet.Range(1, 1, 1, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGreen;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        // Get expenses
        var expenses = await _context.Expenses
            .Where(e => e.QId == quarryId && e.IsActive)
            .OrderBy(e => e.ExpenseDate)
            .ToListAsync();

        int row = 2;
        foreach (var expense in expenses)
        {
            worksheet.Cell(row, 1).Value = expense.ExpenseDate?.ToString("yyyy-MM-dd");
            worksheet.Cell(row, 2).Value = expense.Item;
            worksheet.Cell(row, 3).Value = expense.Amount;
            worksheet.Cell(row, 4).Value = expense.Category ?? "Miscellaneous";
            worksheet.Cell(row, 5).Value = expense.TxnReference ?? "";
            row++;
        }

        worksheet.Columns().AdjustToContents();
    }

    private async Task ExportBankingSheet(IXLWorkbook workbook, string quarryId)
    {
        var worksheet = workbook.Worksheets.Add("Banking");

        // Headers
        var headers = new[] { "BankingDate", "Item", "AmountBanked", "TxnReference" };

        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
        }

        // Header formatting
        var headerRange = worksheet.Range(1, 1, 1, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightYellow;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        // Get banking records
        var bankings = await _context.Bankings
            .Where(b => b.QId == quarryId && b.IsActive)
            .OrderBy(b => b.BankingDate)
            .ToListAsync();

        int row = 2;
        foreach (var banking in bankings)
        {
            worksheet.Cell(row, 1).Value = banking.BankingDate?.ToString("yyyy-MM-dd");
            worksheet.Cell(row, 2).Value = banking.Item;
            worksheet.Cell(row, 3).Value = banking.AmountBanked;
            worksheet.Cell(row, 4).Value = banking.TxnReference ?? "";
            row++;
        }

        worksheet.Columns().AdjustToContents();
    }

    private async Task ExportFuelUsageSheet(IXLWorkbook workbook, string quarryId)
    {
        var worksheet = workbook.Worksheets.Add("FuelUsage");

        // Headers
        var headers = new[] { "UsageDate", "OldStock", "NewStock", "MachinesLoaded", "WheelLoadersLoaded" };

        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
        }

        // Header formatting
        var headerRange = worksheet.Range(1, 1, 1, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightCoral;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        // Get fuel usage records
        var fuelUsages = await _context.FuelUsages
            .Where(f => f.QId == quarryId && f.IsActive)
            .OrderBy(f => f.UsageDate)
            .ToListAsync();

        int row = 2;
        foreach (var fuel in fuelUsages)
        {
            worksheet.Cell(row, 1).Value = fuel.UsageDate?.ToString("yyyy-MM-dd");
            worksheet.Cell(row, 2).Value = fuel.OldStock;
            worksheet.Cell(row, 3).Value = fuel.NewStock;
            worksheet.Cell(row, 4).Value = fuel.MachinesLoaded;
            worksheet.Cell(row, 5).Value = fuel.WheelLoadersLoaded;
            row++;
        }

        worksheet.Columns().AdjustToContents();
    }

    private void AddReferenceDataSheet(IXLWorkbook workbook, List<Product> products, List<Layer> layers, List<Broker> brokers)
    {
        var worksheet = workbook.Worksheets.Add("ReferenceData");

        // Products column
        worksheet.Cell(1, 1).Value = "Valid Products";
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.LightBlue;
        for (int i = 0; i < products.Count; i++)
        {
            worksheet.Cell(i + 2, 1).Value = products[i].ProductName;
        }

        // Layers column
        worksheet.Cell(1, 2).Value = "Valid Layers";
        worksheet.Cell(1, 2).Style.Font.Bold = true;
        worksheet.Cell(1, 2).Style.Fill.BackgroundColor = XLColor.LightGreen;
        for (int i = 0; i < layers.Count; i++)
        {
            worksheet.Cell(i + 2, 2).Value = layers[i].LayerLevel;
        }

        // Brokers column
        worksheet.Cell(1, 3).Value = "Valid Brokers";
        worksheet.Cell(1, 3).Style.Font.Bold = true;
        worksheet.Cell(1, 3).Style.Fill.BackgroundColor = XLColor.LightYellow;
        for (int i = 0; i < brokers.Count; i++)
        {
            worksheet.Cell(i + 2, 3).Value = brokers[i].BrokerName;
        }

        // Payment Status column
        worksheet.Cell(1, 4).Value = "Valid Payment Status";
        worksheet.Cell(1, 4).Style.Font.Bold = true;
        worksheet.Cell(1, 4).Style.Fill.BackgroundColor = XLColor.LightCoral;
        worksheet.Cell(2, 4).Value = "Paid";
        worksheet.Cell(3, 4).Value = "NotPaid";

        // Payment Mode column
        worksheet.Cell(1, 5).Value = "Valid Payment Modes";
        worksheet.Cell(1, 5).Style.Font.Bold = true;
        worksheet.Cell(1, 5).Style.Fill.BackgroundColor = XLColor.LightGray;
        worksheet.Cell(2, 5).Value = "Cash";
        worksheet.Cell(3, 5).Value = "MPESA";
        worksheet.Cell(4, 5).Value = "Bank Transfer";

        // Expense Categories column
        worksheet.Cell(1, 6).Value = "Valid Expense Categories";
        worksheet.Cell(1, 6).Style.Font.Bold = true;
        worksheet.Cell(1, 6).Style.Fill.BackgroundColor = XLColor.LightSkyBlue;
        for (int i = 0; i < ValidExpenseCategories.Length; i++)
        {
            worksheet.Cell(i + 2, 6).Value = ValidExpenseCategories[i];
        }

        worksheet.Columns().AdjustToContents();
    }

    private void AddInstructionsSheet(IXLWorkbook workbook, string quarryName,
        List<Product> products, List<Layer> layers, List<Broker> brokers)
    {
        var worksheet = workbook.Worksheets.Add("Instructions");

        worksheet.Cell(1, 1).Value = "QDeskPro Data Export/Import Instructions";
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 14;

        worksheet.Cell(3, 1).Value = $"Quarry: {quarryName}";
        worksheet.Cell(4, 1).Value = $"Export Date: {DateTime.Now:yyyy-MM-dd HH:mm}";

        worksheet.Cell(6, 1).Value = "IMPORTANT: VALIDATION BEFORE IMPORT";
        worksheet.Cell(6, 1).Style.Font.Bold = true;
        worksheet.Cell(6, 1).Style.Font.FontColor = XLColor.Red;

        worksheet.Cell(7, 1).Value = "The import process will validate ALL data before importing. If any errors are found,";
        worksheet.Cell(8, 1).Value = "NO data will be imported. You must fix all errors and re-upload the file.";

        worksheet.Cell(10, 1).Value = "IMPORT INSTRUCTIONS:";
        worksheet.Cell(10, 1).Style.Font.Bold = true;

        worksheet.Cell(11, 1).Value = "1. Edit the data in the respective sheets (Sales, Expenses, Banking, FuelUsage)";
        worksheet.Cell(12, 1).Value = "2. Keep the column headers exactly as they are";
        worksheet.Cell(13, 1).Value = "3. Use date format: yyyy-MM-dd (e.g., 2025-01-15)";
        worksheet.Cell(14, 1).Value = "4. For Sales: Product and Layer names MUST match values in ReferenceData sheet exactly";
        worksheet.Cell(15, 1).Value = "5. For Sales: Broker name is optional; leave blank if no broker. If provided, must match ReferenceData";
        worksheet.Cell(16, 1).Value = "6. PaymentStatus: Use 'Paid' or 'NotPaid' (case-insensitive)";
        worksheet.Cell(17, 1).Value = "7. PaymentMode: Use 'Cash', 'MPESA', or 'Bank Transfer' (case-insensitive)";
        worksheet.Cell(18, 1).Value = "8. Check the ReferenceData sheet for all valid lookup values";
        worksheet.Cell(19, 1).Value = "9. After editing, save and import the file back into QDeskPro";

        worksheet.Cell(21, 1).Value = "VALID LOOKUP VALUES FOR THIS QUARRY:";
        worksheet.Cell(21, 1).Style.Font.Bold = true;

        worksheet.Cell(22, 1).Value = $"Products: {string.Join(", ", products.Select(p => p.ProductName))}";
        worksheet.Cell(23, 1).Value = $"Layers: {string.Join(", ", layers.Select(l => l.LayerLevel))}";
        worksheet.Cell(24, 1).Value = $"Brokers: {string.Join(", ", brokers.Select(b => b.BrokerName))}";

        worksheet.Cell(26, 1).Value = "COLUMN REFERENCE:";
        worksheet.Cell(26, 1).Style.Font.Bold = true;

        worksheet.Cell(27, 1).Value = "Sales: SaleDate*, VehicleRegistration*, ClientName, ClientPhone, Destination, Product*, Layer*, Quantity*, PricePerUnit*, Broker, CommissionPerUnit, PaymentStatus*, PaymentMode*, PaymentReference";
        worksheet.Cell(28, 1).Value = "Expenses: ExpenseDate*, Item*, Amount*, Category, TxnReference";
        worksheet.Cell(29, 1).Value = "Banking: BankingDate*, Item, AmountBanked*, TxnReference";
        worksheet.Cell(30, 1).Value = "FuelUsage: UsageDate*, OldStock*, NewStock*, MachinesLoaded*, WheelLoadersLoaded*";
        worksheet.Cell(31, 1).Value = "* = Required field";

        worksheet.Columns().AdjustToContents();
    }

    /// <summary>
    /// Generate an import template with headers and sample data
    /// Helps users understand the expected format before importing
    /// </summary>
    public async Task<byte[]> GenerateImportTemplateAsync(string quarryId)
    {
        _logger.LogInformation("Generating import template for quarry {QuarryId}", quarryId);

        using var workbook = new XLWorkbook();

        // Get quarry info
        var quarry = await _context.Quarries.FindAsync(quarryId);
        var quarryName = quarry?.QuarryName ?? "Unknown";

        // Load master data for reference sheet
        var products = await _context.Products.Where(p => p.IsActive).ToListAsync();
        var layers = await _context.Layers.Where(l => l.QId == quarryId && l.IsActive).ToListAsync();
        var brokers = await _context.Brokers.Where(b => b.quarryId == quarryId && b.IsActive).ToListAsync();

        // Create template sheets with headers and sample rows
        CreateSalesTemplateSheet(workbook, products, layers, brokers);
        CreateExpensesTemplateSheet(workbook);
        CreateBankingTemplateSheet(workbook);
        CreateFuelUsageTemplateSheet(workbook);

        // Add Reference Data sheet (helps users know valid values)
        AddReferenceDataSheet(workbook, products, layers, brokers);

        // Add Instructions sheet
        AddInstructionsSheet(workbook, quarryName, products, layers, brokers);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private void CreateSalesTemplateSheet(IXLWorkbook workbook, List<Product> products, List<Layer> layers, List<Broker> brokers)
    {
        var worksheet = workbook.Worksheets.Add("Sales");

        // Headers
        var headers = new[]
        {
            "SaleDate", "VehicleRegistration", "ClientName", "ClientPhone", "Destination",
            "Product", "Layer", "Quantity", "PricePerUnit", "Broker", "CommissionPerUnit",
            "PaymentStatus", "PaymentMode", "PaymentReference"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
        }

        // Header formatting
        var headerRange = worksheet.Range(1, 1, 1, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        // Sample row with example data
        var sampleProduct = products.FirstOrDefault()?.ProductName ?? "Size 6";
        var sampleLayer = layers.FirstOrDefault()?.LayerLevel ?? "Layer -1";
        var sampleBroker = brokers.FirstOrDefault()?.BrokerName ?? "";

        worksheet.Cell(2, 1).Value = DateTime.Today.ToString("yyyy-MM-dd");
        worksheet.Cell(2, 2).Value = "KBZ 123A";
        worksheet.Cell(2, 3).Value = "John Doe";
        worksheet.Cell(2, 4).Value = "0712345678";
        worksheet.Cell(2, 5).Value = "Nairobi";
        worksheet.Cell(2, 6).Value = sampleProduct;
        worksheet.Cell(2, 7).Value = sampleLayer;
        worksheet.Cell(2, 8).Value = 100;
        worksheet.Cell(2, 9).Value = 50;
        worksheet.Cell(2, 10).Value = sampleBroker;
        worksheet.Cell(2, 11).Value = sampleBroker != "" ? 5 : 0;
        worksheet.Cell(2, 12).Value = "Paid";
        worksheet.Cell(2, 13).Value = "Cash";
        worksheet.Cell(2, 14).Value = "";

        // Style sample row as italic to indicate it's an example
        var sampleRange = worksheet.Range(2, 1, 2, headers.Length);
        sampleRange.Style.Font.Italic = true;
        sampleRange.Style.Fill.BackgroundColor = XLColor.LightGray;

        // Add note about sample row
        worksheet.Cell(3, 1).Value = "← Delete this sample row and add your data starting from row 2";
        worksheet.Cell(3, 1).Style.Font.FontColor = XLColor.Red;
        worksheet.Cell(3, 1).Style.Font.Italic = true;

        worksheet.Columns().AdjustToContents();
    }

    private void CreateExpensesTemplateSheet(IXLWorkbook workbook)
    {
        var worksheet = workbook.Worksheets.Add("Expenses");

        // Headers
        var headers = new[] { "ExpenseDate", "Item", "Amount", "Category", "TxnReference" };

        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
        }

        // Header formatting
        var headerRange = worksheet.Range(1, 1, 1, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGreen;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        // Sample row
        worksheet.Cell(2, 1).Value = DateTime.Today.ToString("yyyy-MM-dd");
        worksheet.Cell(2, 2).Value = "Fuel for machines";
        worksheet.Cell(2, 3).Value = 5000;
        worksheet.Cell(2, 4).Value = "Fuel";
        worksheet.Cell(2, 5).Value = "REF001";

        // Style sample row
        var sampleRange = worksheet.Range(2, 1, 2, headers.Length);
        sampleRange.Style.Font.Italic = true;
        sampleRange.Style.Fill.BackgroundColor = XLColor.LightGray;

        // Add note
        worksheet.Cell(3, 1).Value = "← Delete this sample row and add your data starting from row 2";
        worksheet.Cell(3, 1).Style.Font.FontColor = XLColor.Red;
        worksheet.Cell(3, 1).Style.Font.Italic = true;

        worksheet.Columns().AdjustToContents();
    }

    private void CreateBankingTemplateSheet(IXLWorkbook workbook)
    {
        var worksheet = workbook.Worksheets.Add("Banking");

        // Headers
        var headers = new[] { "BankingDate", "Item", "AmountBanked", "TxnReference" };

        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
        }

        // Header formatting
        var headerRange = worksheet.Range(1, 1, 1, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightYellow;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        // Sample row
        worksheet.Cell(2, 1).Value = DateTime.Today.ToString("yyyy-MM-dd");
        worksheet.Cell(2, 2).Value = "Daily deposit";
        worksheet.Cell(2, 3).Value = 50000;
        worksheet.Cell(2, 4).Value = "TXN123456";

        // Style sample row
        var sampleRange = worksheet.Range(2, 1, 2, headers.Length);
        sampleRange.Style.Font.Italic = true;
        sampleRange.Style.Fill.BackgroundColor = XLColor.LightGray;

        // Add note
        worksheet.Cell(3, 1).Value = "← Delete this sample row and add your data starting from row 2";
        worksheet.Cell(3, 1).Style.Font.FontColor = XLColor.Red;
        worksheet.Cell(3, 1).Style.Font.Italic = true;

        worksheet.Columns().AdjustToContents();
    }

    private void CreateFuelUsageTemplateSheet(IXLWorkbook workbook)
    {
        var worksheet = workbook.Worksheets.Add("FuelUsage");

        // Headers
        var headers = new[] { "UsageDate", "OldStock", "NewStock", "MachinesLoaded", "WheelLoadersLoaded" };

        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
        }

        // Header formatting
        var headerRange = worksheet.Range(1, 1, 1, headers.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightCoral;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

        // Sample row
        worksheet.Cell(2, 1).Value = DateTime.Today.ToString("yyyy-MM-dd");
        worksheet.Cell(2, 2).Value = 100;
        worksheet.Cell(2, 3).Value = 200;
        worksheet.Cell(2, 4).Value = 50;
        worksheet.Cell(2, 5).Value = 30;

        // Style sample row
        var sampleRange = worksheet.Range(2, 1, 2, headers.Length);
        sampleRange.Style.Font.Italic = true;
        sampleRange.Style.Fill.BackgroundColor = XLColor.LightGray;

        // Add note
        worksheet.Cell(3, 1).Value = "← Delete this sample row and add your data starting from row 2";
        worksheet.Cell(3, 1).Style.Font.FontColor = XLColor.Red;
        worksheet.Cell(3, 1).Style.Font.Italic = true;

        worksheet.Columns().AdjustToContents();
    }

    #endregion

    #region Validate Import Data

    /// <summary>
    /// Validate import file WITHOUT importing - returns all validation errors
    /// This allows users to see all issues before attempting import
    /// Includes duplicate detection against existing database records
    /// </summary>
    public async Task<ImportValidationResult> ValidateImportFileAsync(string quarryId, byte[] fileBytes)
    {
        var result = new ImportValidationResult();

        try
        {
            _logger.LogInformation("Validating import file for quarry {QuarryId}", quarryId);

            using var stream = new MemoryStream(fileBytes);
            using var workbook = new XLWorkbook(stream);

            // Load master data for lookups
            var products = await _context.Products.Where(p => p.IsActive).ToListAsync();
            var layers = await _context.Layers.Where(l => l.QId == quarryId && l.IsActive).ToListAsync();
            var brokers = await _context.Brokers.Where(b => b.quarryId == quarryId && b.IsActive).ToListAsync();

            // Create lookup dictionaries for case-insensitive matching
            var productLookup = products.ToDictionary(p => p.ProductName.ToLowerInvariant(), p => p);
            var layerLookup = layers.ToDictionary(l => l.LayerLevel.ToLowerInvariant(), l => l);
            var brokerLookup = brokers.ToDictionary(b => b.BrokerName.ToLowerInvariant(), b => b);

            // Load existing data for duplicate detection
            var existingSales = await _context.Sales
                .Where(s => s.QId == quarryId && s.IsActive)
                .Select(s => new { s.SaleDate, s.VehicleRegistration, s.ProductId, s.Quantity })
                .ToListAsync();
            var existingSalesSet = existingSales
                .Select(s => $"{s.SaleDate:yyyy-MM-dd}|{s.VehicleRegistration?.ToLowerInvariant()}|{s.ProductId}|{s.Quantity}")
                .ToHashSet();

            var existingExpenses = await _context.Expenses
                .Where(e => e.QId == quarryId && e.IsActive)
                .Select(e => new { e.ExpenseDate, e.Item, e.Amount })
                .ToListAsync();
            var existingExpensesSet = existingExpenses
                .Select(e => $"{e.ExpenseDate:yyyy-MM-dd}|{e.Item?.ToLowerInvariant()}|{e.Amount}")
                .ToHashSet();

            var existingBanking = await _context.Bankings
                .Where(b => b.QId == quarryId && b.IsActive)
                .Select(b => new { b.BankingDate, b.AmountBanked, b.TxnReference })
                .ToListAsync();
            var existingBankingSet = existingBanking
                .Select(b => $"{b.BankingDate:yyyy-MM-dd}|{b.AmountBanked}|{b.TxnReference?.ToLowerInvariant()}")
                .ToHashSet();

            var existingFuelDates = await _context.FuelUsages
                .Where(f => f.QId == quarryId && f.IsActive)
                .Select(f => f.UsageDate)
                .ToListAsync();
            var existingFuelDatesSet = existingFuelDates
                .Select(d => d?.ToString("yyyy-MM-dd") ?? "")
                .ToHashSet();

            // Validate each sheet with duplicate detection
            if (workbook.TryGetWorksheet("Sales", out var salesSheet))
            {
                var salesValidation = ValidateSalesSheet(salesSheet, productLookup, layerLookup, brokerLookup,
                    products.Select(p => p.ProductName).ToList(),
                    layers.Select(l => l.LayerLevel).ToList(),
                    brokers.Select(b => b.BrokerName).ToList(),
                    existingSalesSet);
                result.SalesRowCount = salesValidation.ValidRows;
                result.Errors.AddRange(salesValidation.Errors);
            }

            if (workbook.TryGetWorksheet("Expenses", out var expensesSheet))
            {
                var expenseValidation = ValidateExpensesSheet(expensesSheet, existingExpensesSet);
                result.ExpensesRowCount = expenseValidation.ValidRows;
                result.Errors.AddRange(expenseValidation.Errors);
            }

            if (workbook.TryGetWorksheet("Banking", out var bankingSheet))
            {
                var bankingValidation = ValidateBankingSheet(bankingSheet, existingBankingSet);
                result.BankingRowCount = bankingValidation.ValidRows;
                result.Errors.AddRange(bankingValidation.Errors);
            }

            if (workbook.TryGetWorksheet("FuelUsage", out var fuelSheet))
            {
                var fuelValidation = ValidateFuelUsageSheet(fuelSheet, existingFuelDatesSet);
                result.FuelUsageRowCount = fuelValidation.ValidRows;
                result.Errors.AddRange(fuelValidation.Errors);
            }

            result.IsValid = !result.Errors.Any();

            if (result.TotalRowCount == 0)
            {
                result.Errors.Add(new ValidationError("File", 0, "No data rows found in any sheet"));
                result.IsValid = false;
            }

            _logger.LogInformation("Validation completed for quarry {QuarryId}: Valid={IsValid}, Rows={Rows}, Errors={Errors}",
                quarryId, result.IsValid, result.TotalRowCount, result.Errors.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating import file for quarry {QuarryId}", quarryId);
            result.Errors.Add(new ValidationError("File", 0, $"Failed to read file: {ex.Message}"));
            result.IsValid = false;
            return result;
        }
    }

    private (int ValidRows, List<ValidationError> Errors) ValidateSalesSheet(
        IXLWorksheet worksheet,
        Dictionary<string, Product> productLookup,
        Dictionary<string, Layer> layerLookup,
        Dictionary<string, Broker> brokerLookup,
        List<string> validProducts,
        List<string> validLayers,
        List<string> validBrokers,
        HashSet<string> existingSalesSet)
    {
        var errors = new List<ValidationError>();
        int validRows = 0;
        var importDuplicates = new HashSet<string>(); // Track duplicates within import file

        var rows = worksheet.RowsUsed().Skip(1).ToList(); // Skip header

        foreach (var row in rows)
        {
            var rowNum = row.RowNumber();
            var rowErrors = new List<string>();

            // Skip empty rows
            var saleDateStr = row.Cell(1).GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(saleDateStr)) continue;

            DateTime saleDate = DateTime.MinValue;
            // Validate Date (required)
            if (!DateTime.TryParse(saleDateStr, out saleDate))
            {
                rowErrors.Add($"Invalid date format '{saleDateStr}'. Use yyyy-MM-dd format.");
            }
            else if (saleDate > DateTime.Today)
            {
                rowErrors.Add($"Sale date cannot be in the future.");
            }

            // Validate Vehicle Registration (required)
            var vehicleReg = row.Cell(2).GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(vehicleReg))
            {
                rowErrors.Add("Vehicle registration is required.");
            }

            // Validate Product (required, must exist)
            var productName = row.Cell(6).GetString()?.Trim();
            string? productId = null;
            if (string.IsNullOrWhiteSpace(productName))
            {
                rowErrors.Add("Product is required.");
            }
            else if (!productLookup.ContainsKey(productName.ToLowerInvariant()))
            {
                rowErrors.Add($"Product '{productName}' not found. Valid options: {string.Join(", ", validProducts)}");
            }
            else
            {
                productId = productLookup[productName.ToLowerInvariant()].Id;
            }

            // Validate Layer (required, must exist)
            var layerLevel = row.Cell(7).GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(layerLevel))
            {
                rowErrors.Add("Layer is required.");
            }
            else if (!layerLookup.ContainsKey(layerLevel.ToLowerInvariant()))
            {
                rowErrors.Add($"Layer '{layerLevel}' not found. Valid options: {string.Join(", ", validLayers)}");
            }

            // Validate Quantity (required, > 0)
            var quantityStr = row.Cell(8).GetString()?.Trim();
            double quantity = 0;
            if (!double.TryParse(quantityStr, out quantity) || quantity <= 0)
            {
                rowErrors.Add($"Quantity must be a positive number. Got: '{quantityStr}'");
            }

            // Validate Price (required, > 0)
            var priceStr = row.Cell(9).GetString()?.Trim();
            if (!double.TryParse(priceStr, out var price) || price <= 0)
            {
                rowErrors.Add($"Price per unit must be a positive number. Got: '{priceStr}'");
            }

            // Validate Broker (optional, but if provided must exist)
            var brokerName = row.Cell(10).GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(brokerName) && !brokerLookup.ContainsKey(brokerName.ToLowerInvariant()))
            {
                rowErrors.Add($"Broker '{brokerName}' not found. Valid options: {string.Join(", ", validBrokers)}. Leave blank for no broker.");
            }

            // Validate Commission (must be >= 0 if provided)
            var commissionStr = row.Cell(11).GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(commissionStr) && (!double.TryParse(commissionStr, out var commission) || commission < 0))
            {
                rowErrors.Add($"Commission must be a non-negative number. Got: '{commissionStr}'");
            }

            // Validate Payment Status (required)
            var paymentStatus = row.Cell(12).GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(paymentStatus))
            {
                paymentStatus = "Paid"; // Default
            }
            else if (!ValidPaymentStatuses.Any(s => s.Equals(paymentStatus, StringComparison.OrdinalIgnoreCase)))
            {
                rowErrors.Add($"Invalid payment status '{paymentStatus}'. Valid options: {string.Join(", ", ValidPaymentStatuses)}");
            }

            // Validate Payment Mode (required)
            var paymentMode = row.Cell(13).GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(paymentMode))
            {
                paymentMode = "Cash"; // Default
            }
            else if (!ValidPaymentModes.Any(m => m.Equals(paymentMode, StringComparison.OrdinalIgnoreCase)))
            {
                rowErrors.Add($"Invalid payment mode '{paymentMode}'. Valid options: {string.Join(", ", ValidPaymentModes)}");
            }

            // Check for duplicates (same date + vehicle + product + quantity)
            if (saleDate != DateTime.MinValue && !string.IsNullOrWhiteSpace(vehicleReg) && productId != null && quantity > 0)
            {
                var duplicateKey = $"{saleDate:yyyy-MM-dd}|{vehicleReg.ToLowerInvariant()}|{productId}|{quantity}";

                // Check against existing database records
                if (existingSalesSet.Contains(duplicateKey))
                {
                    rowErrors.Add($"Duplicate: Sale already exists for {vehicleReg} on {saleDate:yyyy-MM-dd} with same product and quantity.");
                }
                // Check for duplicates within the import file itself
                else if (importDuplicates.Contains(duplicateKey))
                {
                    rowErrors.Add($"Duplicate: This sale appears multiple times in the import file.");
                }
                else
                {
                    importDuplicates.Add(duplicateKey);
                }
            }

            if (rowErrors.Any())
            {
                foreach (var error in rowErrors)
                {
                    errors.Add(new ValidationError("Sales", rowNum, error));
                }
            }
            else
            {
                validRows++;
            }
        }

        return (validRows, errors);
    }

    private (int ValidRows, List<ValidationError> Errors) ValidateExpensesSheet(
        IXLWorksheet worksheet, HashSet<string> existingExpensesSet)
    {
        var errors = new List<ValidationError>();
        int validRows = 0;
        var importDuplicates = new HashSet<string>();

        var rows = worksheet.RowsUsed().Skip(1).ToList();

        foreach (var row in rows)
        {
            var rowNum = row.RowNumber();
            var rowErrors = new List<string>();

            var dateStr = row.Cell(1).GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(dateStr)) continue;

            DateTime expenseDate = DateTime.MinValue;
            // Validate Date
            if (!DateTime.TryParse(dateStr, out expenseDate))
            {
                rowErrors.Add($"Invalid date format '{dateStr}'. Use yyyy-MM-dd format.");
            }
            else if (expenseDate > DateTime.Today)
            {
                rowErrors.Add($"Expense date cannot be in the future.");
            }

            // Validate Item (required)
            var item = row.Cell(2).GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(item))
            {
                rowErrors.Add("Item description is required.");
            }

            // Validate Amount (required, > 0)
            var amountStr = row.Cell(3).GetString()?.Trim();
            double amount = 0;
            if (!double.TryParse(amountStr, out amount) || amount <= 0)
            {
                rowErrors.Add($"Amount must be a positive number. Got: '{amountStr}'");
            }

            // Validate Category (optional, but warn if invalid)
            var category = row.Cell(4).GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(category) &&
                !ValidExpenseCategories.Any(c => c.Equals(category, StringComparison.OrdinalIgnoreCase)))
            {
                // Just a warning - we'll default to Miscellaneous
                rowErrors.Add($"Category '{category}' is not standard. Will be set to 'Miscellaneous'. Valid: {string.Join(", ", ValidExpenseCategories)}");
            }

            // Check for duplicates (same date + item + amount)
            if (expenseDate != DateTime.MinValue && !string.IsNullOrWhiteSpace(item) && amount > 0)
            {
                var duplicateKey = $"{expenseDate:yyyy-MM-dd}|{item.ToLowerInvariant()}|{amount}";

                if (existingExpensesSet.Contains(duplicateKey))
                {
                    rowErrors.Add($"Duplicate: Expense '{item}' with amount {amount:N0} already exists for {expenseDate:yyyy-MM-dd}.");
                }
                else if (importDuplicates.Contains(duplicateKey))
                {
                    rowErrors.Add($"Duplicate: This expense appears multiple times in the import file.");
                }
                else
                {
                    importDuplicates.Add(duplicateKey);
                }
            }

            if (rowErrors.Any())
            {
                foreach (var error in rowErrors)
                {
                    errors.Add(new ValidationError("Expenses", rowNum, error));
                }
            }
            else
            {
                validRows++;
            }
        }

        return (validRows, errors);
    }

    private (int ValidRows, List<ValidationError> Errors) ValidateBankingSheet(
        IXLWorksheet worksheet, HashSet<string> existingBankingSet)
    {
        var errors = new List<ValidationError>();
        int validRows = 0;
        var importDuplicates = new HashSet<string>();

        var rows = worksheet.RowsUsed().Skip(1).ToList();

        foreach (var row in rows)
        {
            var rowNum = row.RowNumber();
            var rowErrors = new List<string>();

            var dateStr = row.Cell(1).GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(dateStr)) continue;

            DateTime bankingDate = DateTime.MinValue;
            // Validate Date
            if (!DateTime.TryParse(dateStr, out bankingDate))
            {
                rowErrors.Add($"Invalid date format '{dateStr}'. Use yyyy-MM-dd format.");
            }
            else if (bankingDate > DateTime.Today)
            {
                rowErrors.Add($"Banking date cannot be in the future.");
            }

            // Validate Amount (required, >= 0)
            var amountStr = row.Cell(3).GetString()?.Trim();
            double amount = 0;
            if (!double.TryParse(amountStr, out amount) || amount < 0)
            {
                rowErrors.Add($"Amount banked must be a non-negative number. Got: '{amountStr}'");
            }

            var txnRef = row.Cell(4).GetString()?.Trim();

            // Check for duplicates (same date + amount + reference)
            if (bankingDate != DateTime.MinValue && amount >= 0)
            {
                var duplicateKey = $"{bankingDate:yyyy-MM-dd}|{amount}|{txnRef?.ToLowerInvariant() ?? ""}";

                if (existingBankingSet.Contains(duplicateKey))
                {
                    rowErrors.Add($"Duplicate: Banking record with amount {amount:N0} already exists for {bankingDate:yyyy-MM-dd}.");
                }
                else if (importDuplicates.Contains(duplicateKey))
                {
                    rowErrors.Add($"Duplicate: This banking record appears multiple times in the import file.");
                }
                else
                {
                    importDuplicates.Add(duplicateKey);
                }
            }

            if (rowErrors.Any())
            {
                foreach (var error in rowErrors)
                {
                    errors.Add(new ValidationError("Banking", rowNum, error));
                }
            }
            else
            {
                validRows++;
            }
        }

        return (validRows, errors);
    }

    private (int ValidRows, List<ValidationError> Errors) ValidateFuelUsageSheet(
        IXLWorksheet worksheet, HashSet<string> existingFuelDatesSet)
    {
        var errors = new List<ValidationError>();
        int validRows = 0;
        var importDuplicates = new HashSet<string>();

        var rows = worksheet.RowsUsed().Skip(1).ToList();

        foreach (var row in rows)
        {
            var rowNum = row.RowNumber();
            var rowErrors = new List<string>();

            var dateStr = row.Cell(1).GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(dateStr)) continue;

            DateTime usageDate = DateTime.MinValue;
            // Validate Date
            if (!DateTime.TryParse(dateStr, out usageDate))
            {
                rowErrors.Add($"Invalid date format '{dateStr}'. Use yyyy-MM-dd format.");
            }
            else if (usageDate > DateTime.Today)
            {
                rowErrors.Add($"Usage date cannot be in the future.");
            }

            // Validate numeric fields (all required, >= 0)
            var fields = new[] { ("OldStock", 2), ("NewStock", 3), ("MachinesLoaded", 4), ("WheelLoadersLoaded", 5) };
            foreach (var (fieldName, col) in fields)
            {
                var valueStr = row.Cell(col).GetString()?.Trim();
                if (!double.TryParse(valueStr, out var value) || value < 0)
                {
                    rowErrors.Add($"{fieldName} must be a non-negative number. Got: '{valueStr}'");
                }
            }

            // Check fuel balance isn't negative
            if (double.TryParse(row.Cell(2).GetString(), out var oldStock) &&
                double.TryParse(row.Cell(3).GetString(), out var newStock) &&
                double.TryParse(row.Cell(4).GetString(), out var machines) &&
                double.TryParse(row.Cell(5).GetString(), out var wheelLoaders))
            {
                var balance = (oldStock + newStock) - (machines + wheelLoaders);
                if (balance < 0)
                {
                    rowErrors.Add($"Fuel usage ({machines + wheelLoaders}) exceeds available stock ({oldStock + newStock}). Balance would be {balance}.");
                }
            }

            // Check for duplicates (only one fuel usage per day)
            if (usageDate != DateTime.MinValue)
            {
                var duplicateKey = usageDate.ToString("yyyy-MM-dd");

                if (existingFuelDatesSet.Contains(duplicateKey))
                {
                    rowErrors.Add($"Duplicate: Fuel usage record already exists for {usageDate:yyyy-MM-dd}. Only one record per day is allowed.");
                }
                else if (importDuplicates.Contains(duplicateKey))
                {
                    rowErrors.Add($"Duplicate: Multiple fuel usage records for {usageDate:yyyy-MM-dd} in import file. Only one per day is allowed.");
                }
                else
                {
                    importDuplicates.Add(duplicateKey);
                }
            }

            if (rowErrors.Any())
            {
                foreach (var error in rowErrors)
                {
                    errors.Add(new ValidationError("FuelUsage", rowNum, error));
                }
            }
            else
            {
                validRows++;
            }
        }

        return (validRows, errors);
    }

    #endregion

    #region Import Data

    /// <summary>
    /// Import quarry data from Excel file
    /// IMPORTANT: Always call ValidateImportFileAsync first to check for errors
    /// This method uses a transaction - all-or-nothing import
    /// </summary>
    public async Task<(bool Success, string Message, DataImportSummary? Summary)> ImportQuarryDataAsync(
        string quarryId, byte[] fileBytes, string userId)
    {
        // First validate the file
        var validationResult = await ValidateImportFileAsync(quarryId, fileBytes);

        if (!validationResult.IsValid)
        {
            return (false,
                $"Validation failed with {validationResult.Errors.Count} error(s). Please fix all errors and try again.",
                new DataImportSummary { Errors = validationResult.Errors.Select(e => e.ToString()).ToList() });
        }

        // Use a transaction for all-or-nothing import
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            _logger.LogInformation("Importing data for quarry {QuarryId} by user {UserId}", quarryId, userId);

            using var stream = new MemoryStream(fileBytes);
            using var workbook = new XLWorkbook(stream);

            var summary = new DataImportSummary();

            // Load master data for lookups
            var products = await _context.Products.Where(p => p.IsActive).ToListAsync();
            var layers = await _context.Layers.Where(l => l.QId == quarryId && l.IsActive).ToListAsync();
            var brokers = await _context.Brokers.Where(b => b.quarryId == quarryId && b.IsActive).ToListAsync();

            var productLookup = products.ToDictionary(p => p.ProductName.ToLowerInvariant(), p => p);
            var layerLookup = layers.ToDictionary(l => l.LayerLevel.ToLowerInvariant(), l => l);
            var brokerLookup = brokers.ToDictionary(b => b.BrokerName.ToLowerInvariant(), b => b);

            // Get the clerk name for imported records
            var user = await _context.Users.FindAsync(userId);
            var clerkName = user?.FullName ?? "Import";
            var now = DateTime.UtcNow;

            // Import each sheet
            if (workbook.TryGetWorksheet("Sales", out var salesSheet))
            {
                summary.SalesImported = await ImportSalesSheetValidated(
                    salesSheet, quarryId, userId, clerkName, now, productLookup, layerLookup, brokerLookup);
            }

            if (workbook.TryGetWorksheet("Expenses", out var expensesSheet))
            {
                summary.ExpensesImported = await ImportExpensesSheetValidated(expensesSheet, quarryId, userId, now);
            }

            if (workbook.TryGetWorksheet("Banking", out var bankingSheet))
            {
                summary.BankingImported = await ImportBankingSheetValidated(bankingSheet, quarryId, userId, now);
            }

            if (workbook.TryGetWorksheet("FuelUsage", out var fuelSheet))
            {
                summary.FuelUsageImported = await ImportFuelUsageSheetValidated(fuelSheet, quarryId, userId, now);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var message = $"Import completed successfully: {summary.TotalImported} records imported";
            _logger.LogInformation("Data import completed for quarry {QuarryId}: {Summary}", quarryId, summary);

            return (true, message, summary);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error importing data for quarry {QuarryId}", quarryId);
            return (false, $"Import failed: {ex.Message}. No data was imported.", null);
        }
    }

    private async Task<int> ImportSalesSheetValidated(
        IXLWorksheet worksheet, string quarryId, string userId, string clerkName, DateTime now,
        Dictionary<string, Product> productLookup,
        Dictionary<string, Layer> layerLookup,
        Dictionary<string, Broker> brokerLookup)
    {
        int imported = 0;
        var rows = worksheet.RowsUsed().Skip(1);

        foreach (var row in rows)
        {
            var saleDateStr = row.Cell(1).GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(saleDateStr)) continue;

            var saleDate = DateTime.Parse(saleDateStr);
            var productName = row.Cell(6).GetString()?.Trim().ToLowerInvariant();
            var layerLevel = row.Cell(7).GetString()?.Trim().ToLowerInvariant();
            var brokerName = row.Cell(10).GetString()?.Trim();

            var product = productLookup[productName!];
            var layer = layerLookup[layerLevel!];
            Broker? broker = null;
            if (!string.IsNullOrWhiteSpace(brokerName))
            {
                brokerLookup.TryGetValue(brokerName.ToLowerInvariant(), out broker);
            }

            var paymentStatus = row.Cell(12).GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(paymentStatus)) paymentStatus = "Paid";
            // Normalize payment status
            paymentStatus = ValidPaymentStatuses.FirstOrDefault(s =>
                s.Equals(paymentStatus, StringComparison.OrdinalIgnoreCase)) ?? "Paid";

            var paymentMode = row.Cell(13).GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(paymentMode)) paymentMode = "Cash";
            // Normalize payment mode
            paymentMode = ValidPaymentModes.FirstOrDefault(m =>
                m.Equals(paymentMode, StringComparison.OrdinalIgnoreCase)) ?? "Cash";

            var sale = new Sale
            {
                Id = Guid.NewGuid().ToString(),
                SaleDate = saleDate,
                VehicleRegistration = row.Cell(2).GetString()?.Trim() ?? "",
                ClientName = row.Cell(3).GetString()?.Trim(),
                ClientPhone = row.Cell(4).GetString()?.Trim(),
                Destination = row.Cell(5).GetString()?.Trim(),
                ProductId = product.Id,
                LayerId = layer.Id,
                Quantity = double.Parse(row.Cell(8).GetString() ?? "0"),
                PricePerUnit = double.Parse(row.Cell(9).GetString() ?? "0"),
                BrokerId = broker?.Id,
                CommissionPerUnit = double.TryParse(row.Cell(11).GetString(), out var comm) ? comm : 0,
                PaymentStatus = paymentStatus,
                PaymentMode = paymentMode,
                PaymentReference = row.Cell(14).GetString()?.Trim(),
                ApplicationUserId = userId,
                ClerkName = clerkName,
                DateStamp = saleDate.ToString("yyyyMMdd"),
                QId = quarryId,
                DateCreated = now,
                CreatedBy = userId,
                IsActive = true
            };

            _context.Sales.Add(sale);
            imported++;
        }

        return imported;
    }

    private async Task<int> ImportExpensesSheetValidated(
        IXLWorksheet worksheet, string quarryId, string userId, DateTime now)
    {
        int imported = 0;
        var rows = worksheet.RowsUsed().Skip(1);

        foreach (var row in rows)
        {
            var dateStr = row.Cell(1).GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(dateStr)) continue;

            var expenseDate = DateTime.Parse(dateStr);
            var category = row.Cell(4).GetString()?.Trim();

            // Normalize category
            if (string.IsNullOrWhiteSpace(category) ||
                !ValidExpenseCategories.Any(c => c.Equals(category, StringComparison.OrdinalIgnoreCase)))
            {
                category = "Miscellaneous";
            }
            else
            {
                category = ValidExpenseCategories.First(c => c.Equals(category, StringComparison.OrdinalIgnoreCase));
            }

            var expense = new Expense
            {
                Id = Guid.NewGuid().ToString(),
                ExpenseDate = expenseDate,
                Item = row.Cell(2).GetString()?.Trim() ?? "",
                Amount = double.Parse(row.Cell(3).GetString() ?? "0"),
                Category = category,
                TxnReference = row.Cell(5).GetString()?.Trim(),
                ApplicationUserId = userId,
                DateStamp = expenseDate.ToString("yyyyMMdd"),
                QId = quarryId,
                DateCreated = now,
                CreatedBy = userId,
                IsActive = true
            };

            _context.Expenses.Add(expense);
            imported++;
        }

        return imported;
    }

    private async Task<int> ImportBankingSheetValidated(
        IXLWorksheet worksheet, string quarryId, string userId, DateTime now)
    {
        int imported = 0;
        var rows = worksheet.RowsUsed().Skip(1);

        foreach (var row in rows)
        {
            var dateStr = row.Cell(1).GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(dateStr)) continue;

            var bankingDate = DateTime.Parse(dateStr);
            var txnRef = row.Cell(4).GetString()?.Trim();

            var banking = new Banking
            {
                Id = Guid.NewGuid().ToString(),
                BankingDate = bankingDate,
                Item = row.Cell(2).GetString()?.Trim() ?? "Deposit",
                AmountBanked = double.Parse(row.Cell(3).GetString() ?? "0"),
                TxnReference = txnRef,
                RefCode = !string.IsNullOrWhiteSpace(txnRef) && txnRef.Length > 10
                    ? txnRef.Substring(0, 10)
                    : txnRef,
                ApplicationUserId = userId,
                DateStamp = bankingDate.ToString("yyyyMMdd"),
                QId = quarryId,
                DateCreated = now,
                CreatedBy = userId,
                IsActive = true
            };

            _context.Bankings.Add(banking);
            imported++;
        }

        return imported;
    }

    private async Task<int> ImportFuelUsageSheetValidated(
        IXLWorksheet worksheet, string quarryId, string userId, DateTime now)
    {
        int imported = 0;
        var rows = worksheet.RowsUsed().Skip(1);

        foreach (var row in rows)
        {
            var dateStr = row.Cell(1).GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(dateStr)) continue;

            var usageDate = DateTime.Parse(dateStr);

            var fuelUsage = new FuelUsage
            {
                Id = Guid.NewGuid().ToString(),
                UsageDate = usageDate,
                OldStock = double.Parse(row.Cell(2).GetString() ?? "0"),
                NewStock = double.Parse(row.Cell(3).GetString() ?? "0"),
                MachinesLoaded = double.Parse(row.Cell(4).GetString() ?? "0"),
                WheelLoadersLoaded = double.Parse(row.Cell(5).GetString() ?? "0"),
                ApplicationUserId = userId,
                DateStamp = usageDate.ToString("yyyyMMdd"),
                QId = quarryId,
                DateCreated = now,
                CreatedBy = userId,
                IsActive = true
            };

            _context.FuelUsages.Add(fuelUsage);
            imported++;
        }

        return imported;
    }

    #endregion

    #region Get Data Counts

    /// <summary>
    /// Get counts of active data for a quarry
    /// </summary>
    public async Task<DataCounts> GetQuarryDataCountsAsync(string quarryId)
    {
        return new DataCounts
        {
            SalesCount = await _context.Sales.CountAsync(s => s.QId == quarryId && s.IsActive),
            ExpensesCount = await _context.Expenses.CountAsync(e => e.QId == quarryId && e.IsActive),
            BankingCount = await _context.Bankings.CountAsync(b => b.QId == quarryId && b.IsActive),
            FuelUsageCount = await _context.FuelUsages.CountAsync(f => f.QId == quarryId && f.IsActive)
        };
    }

    #endregion

    #region SQL Backup

    /// <summary>
    /// Generate SQL backup file containing INSERT statements for all quarry data
    /// Includes: Sales, Expenses, Banking, FuelUsage, DailyNotes, and master data (Layers, Brokers, ProductPrices)
    /// </summary>
    public async Task<byte[]> GenerateSqlBackupAsync(string quarryId)
    {
        _logger.LogInformation("Generating SQL backup for quarry {QuarryId}", quarryId);

        var sb = new System.Text.StringBuilder();
        var quarry = await _context.Quarries.FindAsync(quarryId);
        var quarryName = quarry?.QuarryName ?? "Unknown";

        // Header
        sb.AppendLine("-- ===========================================");
        sb.AppendLine($"-- QDeskPro SQL Backup");
        sb.AppendLine($"-- Quarry: {quarryName}");
        sb.AppendLine($"-- Quarry ID: {quarryId}");
        sb.AppendLine($"-- Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("-- ===========================================");
        sb.AppendLine();
        sb.AppendLine("-- IMPORTANT: This backup contains INSERT statements only.");
        sb.AppendLine("-- The restore process will skip records that already exist.");
        sb.AppendLine();

        // Export master data first (Layers, Brokers, ProductPrices)
        sb.AppendLine("-- ===========================================");
        sb.AppendLine("-- MASTER DATA: Layers");
        sb.AppendLine("-- ===========================================");
        await ExportLayersSql(sb, quarryId);

        sb.AppendLine();
        sb.AppendLine("-- ===========================================");
        sb.AppendLine("-- MASTER DATA: Brokers");
        sb.AppendLine("-- ===========================================");
        await ExportBrokersSql(sb, quarryId);

        sb.AppendLine();
        sb.AppendLine("-- ===========================================");
        sb.AppendLine("-- MASTER DATA: Product Prices");
        sb.AppendLine("-- ===========================================");
        await ExportProductPricesSql(sb, quarryId);

        // Export operational data
        sb.AppendLine();
        sb.AppendLine("-- ===========================================");
        sb.AppendLine("-- OPERATIONAL DATA: Sales");
        sb.AppendLine("-- ===========================================");
        await ExportSalesSql(sb, quarryId);

        sb.AppendLine();
        sb.AppendLine("-- ===========================================");
        sb.AppendLine("-- OPERATIONAL DATA: Expenses");
        sb.AppendLine("-- ===========================================");
        await ExportExpensesSql(sb, quarryId);

        sb.AppendLine();
        sb.AppendLine("-- ===========================================");
        sb.AppendLine("-- OPERATIONAL DATA: Banking");
        sb.AppendLine("-- ===========================================");
        await ExportBankingSql(sb, quarryId);

        sb.AppendLine();
        sb.AppendLine("-- ===========================================");
        sb.AppendLine("-- OPERATIONAL DATA: Fuel Usage");
        sb.AppendLine("-- ===========================================");
        await ExportFuelUsageSql(sb, quarryId);

        sb.AppendLine();
        sb.AppendLine("-- ===========================================");
        sb.AppendLine("-- OPERATIONAL DATA: Daily Notes");
        sb.AppendLine("-- ===========================================");
        await ExportDailyNotesSql(sb, quarryId);

        sb.AppendLine();
        sb.AppendLine("-- ===========================================");
        sb.AppendLine("-- END OF BACKUP");
        sb.AppendLine("-- ===========================================");

        return System.Text.Encoding.UTF8.GetBytes(sb.ToString());
    }

    private string EscapeSql(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "NULL";
        // Escape single quotes by doubling them
        return $"'{value.Replace("'", "''")}'";
    }

    private string FormatDate(DateTime? date)
    {
        if (date == null) return "NULL";
        return $"'{date.Value:yyyy-MM-dd HH:mm:ss}'";
    }

    private string FormatBool(bool value) => value ? "1" : "0";

    private async Task ExportLayersSql(System.Text.StringBuilder sb, string quarryId)
    {
        var layers = await _context.Layers
            .Where(l => l.QId == quarryId && l.IsActive)
            .ToListAsync();

        sb.AppendLine($"-- Layers: {layers.Count} records");

        foreach (var layer in layers)
        {
            sb.AppendLine($"INSERT INTO Layers (Id, LayerLevel, DateStarted, LayerLength, QuarryId, QId, IsActive, DateCreated, CreatedBy, DateModified, ModifiedBy, DateStamp) VALUES (");
            sb.AppendLine($"  {EscapeSql(layer.Id)},");
            sb.AppendLine($"  {EscapeSql(layer.LayerLevel)},");
            sb.AppendLine($"  {FormatDate(layer.DateStarted)},");
            sb.AppendLine($"  {(layer.LayerLength.HasValue ? layer.LayerLength.Value.ToString() : "NULL")},");
            sb.AppendLine($"  {EscapeSql(layer.QuarryId)},");
            sb.AppendLine($"  {EscapeSql(layer.QId)},");
            sb.AppendLine($"  {FormatBool(layer.IsActive)},");
            sb.AppendLine($"  {FormatDate(layer.DateCreated)},");
            sb.AppendLine($"  {EscapeSql(layer.CreatedBy)},");
            sb.AppendLine($"  {FormatDate(layer.DateModified)},");
            sb.AppendLine($"  {EscapeSql(layer.ModifiedBy)},");
            sb.AppendLine($"  {EscapeSql(layer.DateStamp)}");
            sb.AppendLine(");");
        }
    }

    private async Task ExportBrokersSql(System.Text.StringBuilder sb, string quarryId)
    {
        var brokers = await _context.Brokers
            .Where(b => b.quarryId == quarryId && b.IsActive)
            .ToListAsync();

        sb.AppendLine($"-- Brokers: {brokers.Count} records");

        foreach (var broker in brokers)
        {
            sb.AppendLine($"INSERT INTO Brokers (Id, BrokerName, Phone, quarryId, QId, IsActive, DateCreated, CreatedBy, DateModified, ModifiedBy, DateStamp) VALUES (");
            sb.AppendLine($"  {EscapeSql(broker.Id)},");
            sb.AppendLine($"  {EscapeSql(broker.BrokerName)},");
            sb.AppendLine($"  {EscapeSql(broker.Phone)},");
            sb.AppendLine($"  {EscapeSql(broker.quarryId)},");
            sb.AppendLine($"  {EscapeSql(broker.QId)},");
            sb.AppendLine($"  {FormatBool(broker.IsActive)},");
            sb.AppendLine($"  {FormatDate(broker.DateCreated)},");
            sb.AppendLine($"  {EscapeSql(broker.CreatedBy)},");
            sb.AppendLine($"  {FormatDate(broker.DateModified)},");
            sb.AppendLine($"  {EscapeSql(broker.ModifiedBy)},");
            sb.AppendLine($"  {EscapeSql(broker.DateStamp)}");
            sb.AppendLine(");");
        }
    }

    private async Task ExportProductPricesSql(System.Text.StringBuilder sb, string quarryId)
    {
        var prices = await _context.ProductPrices
            .Where(p => p.QuarryId == quarryId && p.IsActive)
            .ToListAsync();

        sb.AppendLine($"-- Product Prices: {prices.Count} records");

        foreach (var price in prices)
        {
            sb.AppendLine($"INSERT INTO ProductPrices (Id, ProductId, QuarryId, Price, QId, IsActive, DateCreated, CreatedBy, DateModified, ModifiedBy, DateStamp) VALUES (");
            sb.AppendLine($"  {EscapeSql(price.Id)},");
            sb.AppendLine($"  {EscapeSql(price.ProductId)},");
            sb.AppendLine($"  {EscapeSql(price.QuarryId)},");
            sb.AppendLine($"  {price.Price},");
            sb.AppendLine($"  {EscapeSql(price.QId)},");
            sb.AppendLine($"  {FormatBool(price.IsActive)},");
            sb.AppendLine($"  {FormatDate(price.DateCreated)},");
            sb.AppendLine($"  {EscapeSql(price.CreatedBy)},");
            sb.AppendLine($"  {FormatDate(price.DateModified)},");
            sb.AppendLine($"  {EscapeSql(price.ModifiedBy)},");
            sb.AppendLine($"  {EscapeSql(price.DateStamp)}");
            sb.AppendLine(");");
        }
    }

    private async Task ExportSalesSql(System.Text.StringBuilder sb, string quarryId)
    {
        var sales = await _context.Sales
            .Where(s => s.QId == quarryId && s.IsActive)
            .ToListAsync();

        sb.AppendLine($"-- Sales: {sales.Count} records");

        foreach (var sale in sales)
        {
            sb.AppendLine($"INSERT INTO Sales (Id, SaleDate, VehicleRegistration, ClientName, ClientPhone, Destination, ProductId, LayerId, Quantity, PricePerUnit, BrokerId, CommissionPerUnit, PaymentStatus, PaymentMode, PaymentReference, PaymentReceivedDate, ApplicationUserId, ClerkName, QId, IsActive, DateCreated, CreatedBy, DateModified, ModifiedBy, DateStamp) VALUES (");
            sb.AppendLine($"  {EscapeSql(sale.Id)},");
            sb.AppendLine($"  {FormatDate(sale.SaleDate)},");
            sb.AppendLine($"  {EscapeSql(sale.VehicleRegistration)},");
            sb.AppendLine($"  {EscapeSql(sale.ClientName)},");
            sb.AppendLine($"  {EscapeSql(sale.ClientPhone)},");
            sb.AppendLine($"  {EscapeSql(sale.Destination)},");
            sb.AppendLine($"  {EscapeSql(sale.ProductId)},");
            sb.AppendLine($"  {EscapeSql(sale.LayerId)},");
            sb.AppendLine($"  {sale.Quantity},");
            sb.AppendLine($"  {sale.PricePerUnit},");
            sb.AppendLine($"  {EscapeSql(sale.BrokerId)},");
            sb.AppendLine($"  {sale.CommissionPerUnit},");
            sb.AppendLine($"  {EscapeSql(sale.PaymentStatus)},");
            sb.AppendLine($"  {EscapeSql(sale.PaymentMode)},");
            sb.AppendLine($"  {EscapeSql(sale.PaymentReference)},");
            sb.AppendLine($"  {FormatDate(sale.PaymentReceivedDate)},");
            sb.AppendLine($"  {EscapeSql(sale.ApplicationUserId)},");
            sb.AppendLine($"  {EscapeSql(sale.ClerkName)},");
            sb.AppendLine($"  {EscapeSql(sale.QId)},");
            sb.AppendLine($"  {FormatBool(sale.IsActive)},");
            sb.AppendLine($"  {FormatDate(sale.DateCreated)},");
            sb.AppendLine($"  {EscapeSql(sale.CreatedBy)},");
            sb.AppendLine($"  {FormatDate(sale.DateModified)},");
            sb.AppendLine($"  {EscapeSql(sale.ModifiedBy)},");
            sb.AppendLine($"  {EscapeSql(sale.DateStamp)}");
            sb.AppendLine(");");
        }
    }

    private async Task ExportExpensesSql(System.Text.StringBuilder sb, string quarryId)
    {
        var expenses = await _context.Expenses
            .Where(e => e.QId == quarryId && e.IsActive)
            .ToListAsync();

        sb.AppendLine($"-- Expenses: {expenses.Count} records");

        foreach (var expense in expenses)
        {
            sb.AppendLine($"INSERT INTO Expenses (Id, ExpenseDate, Item, Amount, Category, TxnReference, ApplicationUserId, QId, IsActive, DateCreated, CreatedBy, DateModified, ModifiedBy, DateStamp) VALUES (");
            sb.AppendLine($"  {EscapeSql(expense.Id)},");
            sb.AppendLine($"  {FormatDate(expense.ExpenseDate)},");
            sb.AppendLine($"  {EscapeSql(expense.Item)},");
            sb.AppendLine($"  {expense.Amount},");
            sb.AppendLine($"  {EscapeSql(expense.Category)},");
            sb.AppendLine($"  {EscapeSql(expense.TxnReference)},");
            sb.AppendLine($"  {EscapeSql(expense.ApplicationUserId)},");
            sb.AppendLine($"  {EscapeSql(expense.QId)},");
            sb.AppendLine($"  {FormatBool(expense.IsActive)},");
            sb.AppendLine($"  {FormatDate(expense.DateCreated)},");
            sb.AppendLine($"  {EscapeSql(expense.CreatedBy)},");
            sb.AppendLine($"  {FormatDate(expense.DateModified)},");
            sb.AppendLine($"  {EscapeSql(expense.ModifiedBy)},");
            sb.AppendLine($"  {EscapeSql(expense.DateStamp)}");
            sb.AppendLine(");");
        }
    }

    private async Task ExportBankingSql(System.Text.StringBuilder sb, string quarryId)
    {
        var bankings = await _context.Bankings
            .Where(b => b.QId == quarryId && b.IsActive)
            .ToListAsync();

        sb.AppendLine($"-- Banking: {bankings.Count} records");

        foreach (var banking in bankings)
        {
            sb.AppendLine($"INSERT INTO Bankings (Id, BankingDate, Item, BalanceBF, AmountBanked, TxnReference, RefCode, ApplicationUserId, QId, IsActive, DateCreated, CreatedBy, DateModified, ModifiedBy, DateStamp) VALUES (");
            sb.AppendLine($"  {EscapeSql(banking.Id)},");
            sb.AppendLine($"  {FormatDate(banking.BankingDate)},");
            sb.AppendLine($"  {EscapeSql(banking.Item)},");
            sb.AppendLine($"  {banking.BalanceBF},");
            sb.AppendLine($"  {banking.AmountBanked},");
            sb.AppendLine($"  {EscapeSql(banking.TxnReference)},");
            sb.AppendLine($"  {EscapeSql(banking.RefCode)},");
            sb.AppendLine($"  {EscapeSql(banking.ApplicationUserId)},");
            sb.AppendLine($"  {EscapeSql(banking.QId)},");
            sb.AppendLine($"  {FormatBool(banking.IsActive)},");
            sb.AppendLine($"  {FormatDate(banking.DateCreated)},");
            sb.AppendLine($"  {EscapeSql(banking.CreatedBy)},");
            sb.AppendLine($"  {FormatDate(banking.DateModified)},");
            sb.AppendLine($"  {EscapeSql(banking.ModifiedBy)},");
            sb.AppendLine($"  {EscapeSql(banking.DateStamp)}");
            sb.AppendLine(");");
        }
    }

    private async Task ExportFuelUsageSql(System.Text.StringBuilder sb, string quarryId)
    {
        var fuelUsages = await _context.FuelUsages
            .Where(f => f.QId == quarryId && f.IsActive)
            .ToListAsync();

        sb.AppendLine($"-- Fuel Usage: {fuelUsages.Count} records");

        foreach (var fuel in fuelUsages)
        {
            sb.AppendLine($"INSERT INTO FuelUsages (Id, UsageDate, OldStock, NewStock, MachinesLoaded, WheelLoadersLoaded, ApplicationUserId, QId, IsActive, DateCreated, CreatedBy, DateModified, ModifiedBy, DateStamp) VALUES (");
            sb.AppendLine($"  {EscapeSql(fuel.Id)},");
            sb.AppendLine($"  {FormatDate(fuel.UsageDate)},");
            sb.AppendLine($"  {fuel.OldStock},");
            sb.AppendLine($"  {fuel.NewStock},");
            sb.AppendLine($"  {fuel.MachinesLoaded},");
            sb.AppendLine($"  {fuel.WheelLoadersLoaded},");
            sb.AppendLine($"  {EscapeSql(fuel.ApplicationUserId)},");
            sb.AppendLine($"  {EscapeSql(fuel.QId)},");
            sb.AppendLine($"  {FormatBool(fuel.IsActive)},");
            sb.AppendLine($"  {FormatDate(fuel.DateCreated)},");
            sb.AppendLine($"  {EscapeSql(fuel.CreatedBy)},");
            sb.AppendLine($"  {FormatDate(fuel.DateModified)},");
            sb.AppendLine($"  {EscapeSql(fuel.ModifiedBy)},");
            sb.AppendLine($"  {EscapeSql(fuel.DateStamp)}");
            sb.AppendLine(");");
        }
    }

    private async Task ExportDailyNotesSql(System.Text.StringBuilder sb, string quarryId)
    {
        var notes = await _context.DailyNotes
            .Where(d => d.QId == quarryId && d.IsActive)
            .ToListAsync();

        sb.AppendLine($"-- Daily Notes: {notes.Count} records");

        foreach (var note in notes)
        {
            sb.AppendLine($"INSERT INTO DailyNotes (Id, NoteDate, Notes, ClosingBalance, quarryId, QId, IsActive, DateCreated, CreatedBy, DateModified, ModifiedBy, DateStamp) VALUES (");
            sb.AppendLine($"  {EscapeSql(note.Id)},");
            sb.AppendLine($"  {FormatDate(note.NoteDate)},");
            sb.AppendLine($"  {EscapeSql(note.Notes)},");
            sb.AppendLine($"  {note.ClosingBalance},");
            sb.AppendLine($"  {EscapeSql(note.quarryId)},");
            sb.AppendLine($"  {EscapeSql(note.QId)},");
            sb.AppendLine($"  {FormatBool(note.IsActive)},");
            sb.AppendLine($"  {FormatDate(note.DateCreated)},");
            sb.AppendLine($"  {EscapeSql(note.CreatedBy)},");
            sb.AppendLine($"  {FormatDate(note.DateModified)},");
            sb.AppendLine($"  {EscapeSql(note.ModifiedBy)},");
            sb.AppendLine($"  {EscapeSql(note.DateStamp)}");
            sb.AppendLine(");");
        }
    }

    #endregion

    #region SQL Restore

    /// <summary>
    /// Restore data from SQL backup file with intelligent duplicate avoidance
    /// Skips records that already exist (based on primary key)
    /// </summary>
    public async Task<(bool Success, string Message, SqlRestoreSummary? Summary)> RestoreFromSqlBackupAsync(
        string quarryId, byte[] fileBytes, string userId)
    {
        var summary = new SqlRestoreSummary();

        try
        {
            _logger.LogInformation("Restoring SQL backup for quarry {QuarryId} by user {UserId}", quarryId, userId);

            var sqlContent = System.Text.Encoding.UTF8.GetString(fileBytes);

            // Validate it's a QDeskPro backup file
            if (!sqlContent.Contains("QDeskPro SQL Backup"))
            {
                return (false, "Invalid backup file. This does not appear to be a QDeskPro SQL backup.", null);
            }

            // Extract quarry ID from backup to verify it matches (or warn user)
            var quarryIdMatch = System.Text.RegularExpressions.Regex.Match(
                sqlContent, @"-- Quarry ID: ([a-f0-9\-]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (quarryIdMatch.Success)
            {
                var backupQuarryId = quarryIdMatch.Groups[1].Value;
                if (!backupQuarryId.Equals(quarryId, StringComparison.OrdinalIgnoreCase))
                {
                    summary.Warnings.Add($"Warning: This backup was created for a different quarry (ID: {backupQuarryId}). Restoring to current quarry.");
                }
            }

            // Load existing IDs to detect duplicates
            var existingLayerIds = await _context.Layers.Where(l => l.QId == quarryId).Select(l => l.Id).ToHashSetAsync();
            var existingBrokerIds = await _context.Brokers.Where(b => b.quarryId == quarryId).Select(b => b.Id).ToHashSetAsync();
            var existingProductPriceIds = await _context.ProductPrices.Where(p => p.QuarryId == quarryId).Select(p => p.Id).ToHashSetAsync();
            var existingSaleIds = await _context.Sales.Where(s => s.QId == quarryId).Select(s => s.Id).ToHashSetAsync();
            var existingExpenseIds = await _context.Expenses.Where(e => e.QId == quarryId).Select(e => e.Id).ToHashSetAsync();
            var existingBankingIds = await _context.Bankings.Where(b => b.QId == quarryId).Select(b => b.Id).ToHashSetAsync();
            var existingFuelIds = await _context.FuelUsages.Where(f => f.QId == quarryId).Select(f => f.Id).ToHashSetAsync();
            var existingNoteIds = await _context.DailyNotes.Where(d => d.QId == quarryId).Select(d => d.Id).ToHashSetAsync();

            // Also check for duplicates by date (for fuel usage - only one per day)
            var existingFuelDates = await _context.FuelUsages
                .Where(f => f.QId == quarryId && f.IsActive)
                .Select(f => f.UsageDate)
                .ToListAsync();
            var existingFuelDateSet = existingFuelDates.Select(d => d?.ToString("yyyy-MM-dd")).ToHashSet();

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var now = DateTime.UtcNow;

                // Parse and process INSERT statements
                var insertPattern = new System.Text.RegularExpressions.Regex(
                    @"INSERT INTO (\w+) \([^)]+\) VALUES \(\s*([\s\S]*?)\s*\);",
                    System.Text.RegularExpressions.RegexOptions.Multiline);

                var matches = insertPattern.Matches(sqlContent);

                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    var tableName = match.Groups[1].Value;
                    var valuesBlock = match.Groups[2].Value;

                    // Parse the ID (first value)
                    var idMatch = System.Text.RegularExpressions.Regex.Match(valuesBlock, @"'([^']+)'");
                    if (!idMatch.Success) continue;

                    var recordId = idMatch.Groups[1].Value;

                    switch (tableName)
                    {
                        case "Layers":
                            if (existingLayerIds.Contains(recordId))
                                summary.LayersSkipped++;
                            else
                            {
                                var restored = await RestoreLayerFromSql(valuesBlock, quarryId, userId, now);
                                if (restored) summary.LayersRestored++;
                            }
                            break;

                        case "Brokers":
                            if (existingBrokerIds.Contains(recordId))
                                summary.BrokersSkipped++;
                            else
                            {
                                var restored = await RestoreBrokerFromSql(valuesBlock, quarryId, userId, now);
                                if (restored) summary.BrokersRestored++;
                            }
                            break;

                        case "ProductPrices":
                            if (existingProductPriceIds.Contains(recordId))
                                summary.ProductPricesSkipped++;
                            else
                            {
                                var restored = await RestoreProductPriceFromSql(valuesBlock, quarryId, userId, now);
                                if (restored) summary.ProductPricesRestored++;
                            }
                            break;

                        case "Sales":
                            if (existingSaleIds.Contains(recordId))
                                summary.SalesSkipped++;
                            else
                            {
                                var restored = await RestoreSaleFromSql(valuesBlock, quarryId, userId, now);
                                if (restored) summary.SalesRestored++;
                            }
                            break;

                        case "Expenses":
                            if (existingExpenseIds.Contains(recordId))
                                summary.ExpensesSkipped++;
                            else
                            {
                                var restored = await RestoreExpenseFromSql(valuesBlock, quarryId, userId, now);
                                if (restored) summary.ExpensesRestored++;
                            }
                            break;

                        case "Bankings":
                            if (existingBankingIds.Contains(recordId))
                                summary.BankingSkipped++;
                            else
                            {
                                var restored = await RestoreBankingFromSql(valuesBlock, quarryId, userId, now);
                                if (restored) summary.BankingRestored++;
                            }
                            break;

                        case "FuelUsages":
                            if (existingFuelIds.Contains(recordId))
                                summary.FuelUsageSkipped++;
                            else
                            {
                                // Also check date uniqueness
                                var dateMatch = System.Text.RegularExpressions.Regex.Match(
                                    valuesBlock, @"'(\d{4}-\d{2}-\d{2})'");
                                if (dateMatch.Success && existingFuelDateSet.Contains(dateMatch.Groups[1].Value))
                                {
                                    summary.FuelUsageSkipped++;
                                    summary.Warnings.Add($"Skipped fuel usage for {dateMatch.Groups[1].Value} - record already exists for that date.");
                                }
                                else
                                {
                                    var restored = await RestoreFuelUsageFromSql(valuesBlock, quarryId, userId, now);
                                    if (restored)
                                    {
                                        summary.FuelUsageRestored++;
                                        if (dateMatch.Success)
                                            existingFuelDateSet.Add(dateMatch.Groups[1].Value);
                                    }
                                }
                            }
                            break;

                        case "DailyNotes":
                            if (existingNoteIds.Contains(recordId))
                                summary.DailyNotesSkipped++;
                            else
                            {
                                var restored = await RestoreDailyNoteFromSql(valuesBlock, quarryId, userId, now);
                                if (restored) summary.DailyNotesRestored++;
                            }
                            break;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var message = $"Restore completed: {summary.TotalRestored} records restored, {summary.TotalSkipped} duplicates skipped.";
                _logger.LogInformation("SQL restore completed for quarry {QuarryId}: {Summary}", quarryId, summary);

                return (true, message, summary);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring SQL backup for quarry {QuarryId}", quarryId);
            return (false, $"Restore failed: {ex.Message}", null);
        }
    }

    private List<string> ParseSqlValues(string valuesBlock)
    {
        var values = new List<string>();
        var currentValue = new System.Text.StringBuilder();
        bool inString = false;
        int depth = 0;

        // Split by comma, respecting quoted strings
        foreach (var line in valuesBlock.Split('\n'))
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine)) continue;
            if (trimmedLine.StartsWith("--")) continue;

            for (int i = 0; i < trimmedLine.Length; i++)
            {
                var c = trimmedLine[i];

                if (c == '\'' && (i == 0 || trimmedLine[i - 1] != '\''))
                {
                    inString = !inString;
                    currentValue.Append(c);
                }
                else if (c == ',' && !inString)
                {
                    values.Add(currentValue.ToString().Trim());
                    currentValue.Clear();
                }
                else
                {
                    currentValue.Append(c);
                }
            }
        }

        // Add the last value
        if (currentValue.Length > 0)
        {
            values.Add(currentValue.ToString().Trim());
        }

        return values;
    }

    private string? ExtractStringValue(string value)
    {
        if (value.Equals("NULL", StringComparison.OrdinalIgnoreCase)) return null;
        // Remove surrounding quotes and unescape
        if (value.StartsWith("'") && value.EndsWith("'"))
        {
            return value.Substring(1, value.Length - 2).Replace("''", "'");
        }
        return value;
    }

    private DateTime? ExtractDateValue(string value)
    {
        var str = ExtractStringValue(value);
        if (str == null) return null;
        if (DateTime.TryParse(str, out var date)) return date;
        return null;
    }

    private double ExtractDoubleValue(string value)
    {
        if (double.TryParse(value.Trim(), out var result)) return result;
        return 0;
    }

    private bool ExtractBoolValue(string value)
    {
        return value.Trim() == "1";
    }

    private async Task<bool> RestoreLayerFromSql(string valuesBlock, string quarryId, string userId, DateTime now)
    {
        try
        {
            var values = ParseSqlValues(valuesBlock);
            if (values.Count < 12) return false;

            var layer = new Layer
            {
                Id = ExtractStringValue(values[0])!,
                LayerLevel = ExtractStringValue(values[1]),
                DateStarted = ExtractDateValue(values[2]),
                LayerLength = ExtractDoubleValue(values[3]),
                QuarryId = quarryId, // Use current quarry
                QId = quarryId,
                IsActive = true,
                DateCreated = now,
                CreatedBy = userId,
                DateStamp = ExtractStringValue(values[11])
            };

            _context.Layers.Add(layer);
            return true;
        }
        catch { return false; }
    }

    private async Task<bool> RestoreBrokerFromSql(string valuesBlock, string quarryId, string userId, DateTime now)
    {
        try
        {
            var values = ParseSqlValues(valuesBlock);
            if (values.Count < 11) return false;

            var broker = new Broker
            {
                Id = ExtractStringValue(values[0])!,
                BrokerName = ExtractStringValue(values[1]),
                Phone = ExtractStringValue(values[2]),
                quarryId = quarryId,
                QId = quarryId,
                IsActive = true,
                DateCreated = now,
                CreatedBy = userId,
                DateStamp = ExtractStringValue(values[10])
            };

            _context.Brokers.Add(broker);
            return true;
        }
        catch { return false; }
    }

    private async Task<bool> RestoreProductPriceFromSql(string valuesBlock, string quarryId, string userId, DateTime now)
    {
        try
        {
            var values = ParseSqlValues(valuesBlock);
            if (values.Count < 11) return false;

            var productId = ExtractStringValue(values[1]);
            // Verify product exists
            if (!await _context.Products.AnyAsync(p => p.Id == productId))
                return false;

            var price = new ProductPrice
            {
                Id = ExtractStringValue(values[0])!,
                ProductId = productId,
                QuarryId = quarryId,
                Price = ExtractDoubleValue(values[3]),
                QId = quarryId,
                IsActive = true,
                DateCreated = now,
                CreatedBy = userId,
                DateStamp = ExtractStringValue(values[10])
            };

            _context.ProductPrices.Add(price);
            return true;
        }
        catch { return false; }
    }

    private async Task<bool> RestoreSaleFromSql(string valuesBlock, string quarryId, string userId, DateTime now)
    {
        try
        {
            var values = ParseSqlValues(valuesBlock);
            if (values.Count < 25) return false;

            var productId = ExtractStringValue(values[6]);
            var layerId = ExtractStringValue(values[7]);

            // Verify product and layer exist
            if (!await _context.Products.AnyAsync(p => p.Id == productId))
                return false;
            if (!await _context.Layers.AnyAsync(l => l.Id == layerId))
            {
                // Try to find a matching layer in the current quarry
                var layer = await _context.Layers.FirstOrDefaultAsync(l => l.QId == quarryId && l.IsActive);
                if (layer == null) return false;
                layerId = layer.Id;
            }

            var sale = new Sale
            {
                Id = ExtractStringValue(values[0])!,
                SaleDate = ExtractDateValue(values[1]),
                VehicleRegistration = ExtractStringValue(values[2]),
                ClientName = ExtractStringValue(values[3]),
                ClientPhone = ExtractStringValue(values[4]),
                Destination = ExtractStringValue(values[5]),
                ProductId = productId,
                LayerId = layerId,
                Quantity = ExtractDoubleValue(values[8]),
                PricePerUnit = ExtractDoubleValue(values[9]),
                BrokerId = ExtractStringValue(values[10]),
                CommissionPerUnit = ExtractDoubleValue(values[11]),
                PaymentStatus = ExtractStringValue(values[12]) ?? "Paid",
                PaymentMode = ExtractStringValue(values[13]) ?? "Cash",
                PaymentReference = ExtractStringValue(values[14]),
                PaymentReceivedDate = ExtractDateValue(values[15]),
                ApplicationUserId = userId,
                ClerkName = ExtractStringValue(values[17]) ?? "Restored",
                QId = quarryId,
                IsActive = true,
                DateCreated = now,
                CreatedBy = userId,
                DateStamp = ExtractStringValue(values[24])
            };

            _context.Sales.Add(sale);
            return true;
        }
        catch { return false; }
    }

    private async Task<bool> RestoreExpenseFromSql(string valuesBlock, string quarryId, string userId, DateTime now)
    {
        try
        {
            var values = ParseSqlValues(valuesBlock);
            if (values.Count < 14) return false;

            var expense = new Expense
            {
                Id = ExtractStringValue(values[0])!,
                ExpenseDate = ExtractDateValue(values[1]),
                Item = ExtractStringValue(values[2]),
                Amount = ExtractDoubleValue(values[3]),
                Category = ExtractStringValue(values[4]) ?? "Miscellaneous",
                TxnReference = ExtractStringValue(values[5]),
                ApplicationUserId = userId,
                QId = quarryId,
                IsActive = true,
                DateCreated = now,
                CreatedBy = userId,
                DateStamp = ExtractStringValue(values[13])
            };

            _context.Expenses.Add(expense);
            return true;
        }
        catch { return false; }
    }

    private async Task<bool> RestoreBankingFromSql(string valuesBlock, string quarryId, string userId, DateTime now)
    {
        try
        {
            var values = ParseSqlValues(valuesBlock);
            if (values.Count < 15) return false;

            var banking = new Banking
            {
                Id = ExtractStringValue(values[0])!,
                BankingDate = ExtractDateValue(values[1]),
                Item = ExtractStringValue(values[2]),
                BalanceBF = ExtractDoubleValue(values[3]),
                AmountBanked = ExtractDoubleValue(values[4]),
                TxnReference = ExtractStringValue(values[5]),
                RefCode = ExtractStringValue(values[6]),
                ApplicationUserId = userId,
                QId = quarryId,
                IsActive = true,
                DateCreated = now,
                CreatedBy = userId,
                DateStamp = ExtractStringValue(values[14])
            };

            _context.Bankings.Add(banking);
            return true;
        }
        catch { return false; }
    }

    private async Task<bool> RestoreFuelUsageFromSql(string valuesBlock, string quarryId, string userId, DateTime now)
    {
        try
        {
            var values = ParseSqlValues(valuesBlock);
            if (values.Count < 14) return false;

            var fuelUsage = new FuelUsage
            {
                Id = ExtractStringValue(values[0])!,
                UsageDate = ExtractDateValue(values[1]),
                OldStock = ExtractDoubleValue(values[2]),
                NewStock = ExtractDoubleValue(values[3]),
                MachinesLoaded = ExtractDoubleValue(values[4]),
                WheelLoadersLoaded = ExtractDoubleValue(values[5]),
                ApplicationUserId = userId,
                QId = quarryId,
                IsActive = true,
                DateCreated = now,
                CreatedBy = userId,
                DateStamp = ExtractStringValue(values[13])
            };

            _context.FuelUsages.Add(fuelUsage);
            return true;
        }
        catch { return false; }
    }

    private async Task<bool> RestoreDailyNoteFromSql(string valuesBlock, string quarryId, string userId, DateTime now)
    {
        try
        {
            var values = ParseSqlValues(valuesBlock);
            if (values.Count < 12) return false;

            var note = new DailyNote
            {
                Id = ExtractStringValue(values[0])!,
                NoteDate = ExtractDateValue(values[1]),
                Notes = ExtractStringValue(values[2]),
                ClosingBalance = ExtractDoubleValue(values[3]),
                quarryId = quarryId,
                QId = quarryId,
                IsActive = true,
                DateCreated = now,
                CreatedBy = userId,
                DateStamp = ExtractStringValue(values[11])
            };

            _context.DailyNotes.Add(note);
            return true;
        }
        catch { return false; }
    }

    #endregion
}

#region DTOs

public class DataClearSummary
{
    public int SalesCleared { get; set; }
    public int ExpensesCleared { get; set; }
    public int BankingCleared { get; set; }
    public int FuelUsageCleared { get; set; }
    public int DailyNotesCleared { get; set; }

    public int TotalCleared => SalesCleared + ExpensesCleared + BankingCleared + FuelUsageCleared + DailyNotesCleared;

    public override string ToString() =>
        $"Sales: {SalesCleared}, Expenses: {ExpensesCleared}, Banking: {BankingCleared}, Fuel: {FuelUsageCleared}, Notes: {DailyNotesCleared}";
}

public class DataImportSummary
{
    public int SalesImported { get; set; }
    public int ExpensesImported { get; set; }
    public int BankingImported { get; set; }
    public int FuelUsageImported { get; set; }
    public List<string> Errors { get; set; } = new();

    public int TotalImported => SalesImported + ExpensesImported + BankingImported + FuelUsageImported;

    public override string ToString() =>
        $"Sales: {SalesImported}, Expenses: {ExpensesImported}, Banking: {BankingImported}, Fuel: {FuelUsageImported}";
}

public class DataCounts
{
    public int SalesCount { get; set; }
    public int ExpensesCount { get; set; }
    public int BankingCount { get; set; }
    public int FuelUsageCount { get; set; }

    public int TotalCount => SalesCount + ExpensesCount + BankingCount + FuelUsageCount;
}

public class ImportValidationResult
{
    public bool IsValid { get; set; }
    public int SalesRowCount { get; set; }
    public int ExpensesRowCount { get; set; }
    public int BankingRowCount { get; set; }
    public int FuelUsageRowCount { get; set; }
    public List<ValidationError> Errors { get; set; } = new();

    public int TotalRowCount => SalesRowCount + ExpensesRowCount + BankingRowCount + FuelUsageRowCount;
}

public class ValidationError
{
    public string Sheet { get; set; }
    public int RowNumber { get; set; }
    public string Message { get; set; }

    public ValidationError(string sheet, int rowNumber, string message)
    {
        Sheet = sheet;
        RowNumber = rowNumber;
        Message = message;
    }

    public override string ToString() =>
        RowNumber > 0 ? $"[{Sheet} Row {RowNumber}] {Message}" : $"[{Sheet}] {Message}";
}

public class SqlRestoreSummary
{
    // Master data
    public int LayersRestored { get; set; }
    public int LayersSkipped { get; set; }
    public int BrokersRestored { get; set; }
    public int BrokersSkipped { get; set; }
    public int ProductPricesRestored { get; set; }
    public int ProductPricesSkipped { get; set; }

    // Operational data
    public int SalesRestored { get; set; }
    public int SalesSkipped { get; set; }
    public int ExpensesRestored { get; set; }
    public int ExpensesSkipped { get; set; }
    public int BankingRestored { get; set; }
    public int BankingSkipped { get; set; }
    public int FuelUsageRestored { get; set; }
    public int FuelUsageSkipped { get; set; }
    public int DailyNotesRestored { get; set; }
    public int DailyNotesSkipped { get; set; }

    public List<string> Warnings { get; set; } = new();

    public int TotalRestored => LayersRestored + BrokersRestored + ProductPricesRestored +
                                 SalesRestored + ExpensesRestored + BankingRestored +
                                 FuelUsageRestored + DailyNotesRestored;

    public int TotalSkipped => LayersSkipped + BrokersSkipped + ProductPricesSkipped +
                                SalesSkipped + ExpensesSkipped + BankingSkipped +
                                FuelUsageSkipped + DailyNotesSkipped;

    public override string ToString() =>
        $"Restored: Layers={LayersRestored}, Brokers={BrokersRestored}, Prices={ProductPricesRestored}, " +
        $"Sales={SalesRestored}, Expenses={ExpensesRestored}, Banking={BankingRestored}, " +
        $"Fuel={FuelUsageRestored}, Notes={DailyNotesRestored} | " +
        $"Skipped: {TotalSkipped} duplicates";
}

#endregion
