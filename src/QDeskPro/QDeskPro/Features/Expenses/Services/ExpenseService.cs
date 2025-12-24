namespace QDeskPro.Features.Expenses.Services;

using Microsoft.EntityFrameworkCore;
using QDeskPro.Data;
using QDeskPro.Domain.Entities;

/// <summary>
/// Service for expense operations (CRUD)
/// </summary>
public class ExpenseService
{
    private readonly AppDbContext _context;

    public ExpenseService(AppDbContext context)
    {
        _context = context;
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

            return (true, "New expense has been captured!", expense);
        }
        catch (Exception ex)
        {
            return (false, $"Error saving expense: {ex.Message}", null);
        }
    }

    /// <summary>
    /// Get expenses for a clerk (last 14 days)
    /// </summary>
    public async Task<List<Expense>> GetExpensesForClerkAsync(string userId)
    {
        var cutoffDate = DateTime.Today.AddDays(-14);

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

            return (true, "Expense has been updated!");
        }
        catch (Exception ex)
        {
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

            return (true, "Expense deleted successfully");
        }
        catch (Exception ex)
        {
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
            // Date validation: Max today, Min 14 days ago
            if (expense.ExpenseDate < DateTime.Today.AddDays(-14))
            {
                errors.Add("Cannot backdate expense more than 14 days");
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
}
