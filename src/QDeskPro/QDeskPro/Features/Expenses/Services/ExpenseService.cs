namespace QDeskPro.Features.Expenses.Services;

using Microsoft.EntityFrameworkCore;
using QDeskPro.Data;
using QDeskPro.Domain.Entities;
using QDeskPro.Features.Reports.Services;

/// <summary>
/// Service for expense operations (CRUD)
/// </summary>
public class ExpenseService
{
    private readonly AppDbContext _context;
    private readonly ReportService _reportService;
    private readonly ILogger<ExpenseService> _logger;

    public ExpenseService(AppDbContext context, ReportService reportService, ILogger<ExpenseService> logger)
    {
        _context = context;
        _reportService = reportService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new expense with all required audit fields
    /// Following exact specifications from implementation_plan.md
    /// </summary>
    public async Task<(bool Success, string Message, Expense? Expense)> CreateExpenseAsync(Expense expense, string userId, string quarryId)
    {
        try
        {
            // Validation
            var validationErrors = ValidateExpense(expense);
            if (validationErrors.Any())
            {
                return (false, string.Join(", ", validationErrors), null);
            }

            // Set audit fields as per specification
            expense.Id = Guid.NewGuid().ToString();
            expense.DateStamp = expense.ExpenseDate!.Value.ToString("yyyyMMdd");
            expense.QId = quarryId;
            expense.ApplicationUserId = userId;
            expense.DateCreated = DateTime.UtcNow;
            expense.CreatedBy = userId;
            expense.IsActive = true;

            _context.Expenses.Add(expense);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Expense created: {ExpenseId} | Item: {Item} | Category: {Category} | Amount: {Amount:N0} | By: {UserId}",
                expense.Id, expense.Item, expense.Category, expense.Amount, userId);

            // Trigger cascade recalculation for backdated entries
            if (expense.ExpenseDate!.Value.Date < DateTime.Today)
            {
                await RecalculateClosingBalancesFromDate(expense.ExpenseDate.Value, quarryId, userId);
            }

            return (true, "New expense has been captured!", expense);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create expense: {Item}", expense.Item);
            return (false, $"Error saving expense: {ex.Message}", null);
        }
    }

    /// <summary>
    /// Get expenses for a clerk (last 5 days)
    /// </summary>
    public async Task<List<Expense>> GetExpensesForClerkAsync(string userId)
    {
        var cutoffDate = DateTime.Today.AddDays(-5);

        return await _context.Expenses
            .Where(e => e.ApplicationUserId == userId)
            .Where(e => e.ExpenseDate >= cutoffDate)
            .Where(e => e.IsActive)
            .OrderByDescending(e => e.ExpenseDate)
            .ToListAsync();
    }

    /// <summary>
    /// Update an existing expense
    /// </summary>
    public async Task<(bool Success, string Message)> UpdateExpenseAsync(Expense expense, string userId)
    {
        try
        {
            var existing = await _context.Expenses.FindAsync(expense.Id);
            if (existing == null)
            {
                return (false, "Expense not found");
            }

            // Validation
            var validationErrors = ValidateExpense(expense);
            if (validationErrors.Any())
            {
                return (false, string.Join(", ", validationErrors));
            }

            // Update fields
            existing.ExpenseDate = expense.ExpenseDate;
            existing.Item = expense.Item;
            existing.Amount = expense.Amount;
            existing.Category = expense.Category;
            existing.TxnReference = expense.TxnReference;
            existing.DateStamp = expense.ExpenseDate!.Value.ToString("yyyyMMdd");
            existing.DateModified = DateTime.UtcNow;
            existing.ModifiedBy = userId;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Expense updated: {ExpenseId} | Item: {Item} | Category: {Category} | Amount: {Amount:N0} | By: {UserId}",
                expense.Id, existing.Item, existing.Category, existing.Amount, userId);

            // Trigger cascade recalculation for all days from edited date to today
            await RecalculateClosingBalancesFromDate(existing.ExpenseDate.Value, existing.QId, userId);

            return (true, "Expense has been updated!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update expense {ExpenseId}", expense.Id);
            return (false, $"Error updating expense: {ex.Message}");
        }
    }

    /// <summary>
    /// Soft delete an expense
    /// </summary>
    public async Task<(bool Success, string Message)> DeleteExpenseAsync(string expenseId, string userId)
    {
        try
        {
            var expense = await _context.Expenses.FindAsync(expenseId);
            if (expense == null)
            {
                return (false, "Expense not found");
            }

            expense.IsActive = false;
            expense.DateModified = DateTime.UtcNow;
            expense.ModifiedBy = userId;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Expense deleted: {ExpenseId} | Item: {Item} | Amount: {Amount:N0} | By: {UserId}",
                expenseId, expense.Item, expense.Amount, userId);

            // Trigger cascade recalculation for all days from deleted expense date to today
            await RecalculateClosingBalancesFromDate(expense.ExpenseDate!.Value, expense.QId, userId);

            return (true, "Expense deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete expense {ExpenseId}", expenseId);
            return (false, $"Error deleting expense: {ex.Message}");
        }
    }

    /// <summary>
    /// Validate expense according to exact rules from implementation_plan.md
    /// </summary>
    private List<string> ValidateExpense(Expense expense)
    {
        var errors = new List<string>();

        // Item: Required, not empty
        if (string.IsNullOrWhiteSpace(expense.Item))
        {
            errors.Add("Please provide the description details");
        }

        // Amount: Must be > 0
        if (expense.Amount <= 0)
        {
            errors.Add("Please provide the amount details");
        }

        // Expense Date: Required
        if (expense.ExpenseDate == null)
        {
            errors.Add("Expense Date is required");
        }
        else
        {
            // Date validation: Max today, Min 5 days ago
            if (expense.ExpenseDate < DateTime.Today.AddDays(-5))
            {
                errors.Add("Cannot backdate expense more than 5 days");
            }

            if (expense.ExpenseDate > DateTime.Today)
            {
                errors.Add("Expense date cannot be in the future");
            }
        }

        return errors;
    }

    /// <summary>
    /// Get expense categories (fixed list from spec)
    /// </summary>
    public List<string> GetExpenseCategories()
    {
        return new List<string>
        {
            "Fuel",
            "Transportation Hire",
            "Maintenance and Repairs",
            "Commission",
            "Administrative",
            "Marketing",
            "Wages",
            "Loaders Fees",
            "Consumables and Utilities",
            "Bank Charges",
            "Cess and Road Fees",
            "Miscellaneous"
        };
    }

    /// <summary>
    /// Recalculate closing balances for all days from startDate to today
    /// This ensures the opening balance chain remains accurate after editing historical data
    /// </summary>
    private async Task RecalculateClosingBalancesFromDate(DateTime startDate, string quarryId, string userId)
    {
        var startStamp = startDate.ToString("yyyyMMdd");
        var todayStamp = DateTime.Today.ToString("yyyyMMdd");

        // Get all dates that need recalculation (from startDate to today)
        var affectedDates = await _context.DailyNotes
            .Where(n => string.Compare(n.DateStamp, startStamp) >= 0)
            .Where(n => string.Compare(n.DateStamp, todayStamp) <= 0)
            .Where(n => n.QId == quarryId)
            .Where(n => n.IsActive)
            .OrderBy(n => n.DateStamp)
            .Select(n => n.DateStamp)
            .ToListAsync();

        // Recalculate each affected day's report (this auto-updates closing balance)
        foreach (var dateStamp in affectedDates)
        {
            var date = DateTime.ParseExact(dateStamp, "yyyyMMdd",
                System.Globalization.CultureInfo.InvariantCulture);

            // Regenerate the report - this will recalculate and save the closing balance
            await _reportService.GenerateClerkReportAsync(date, date, quarryId, userId);
        }
    }
}
