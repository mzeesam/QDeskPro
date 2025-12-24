using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QDeskPro.Data;
using QDeskPro.Domain.Entities;
using QDeskPro.Features.Expenses.Services;
using System.Security.Claims;

namespace QDeskPro.Api.Endpoints;

public static class ExpenseEndpoints
{
    public static void MapExpenseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/expenses")
            .WithTags("Expenses")
            .RequireAuthorization()
            .RequireRateLimiting("api");  // API rate limiting

        group.MapGet("", GetExpenses)
            .WithName("GetExpenses")
            .WithDescription("Get expenses with pagination and filtering");

        group.MapGet("{id}", GetExpenseById)
            .WithName("GetExpenseById")
            .WithDescription("Get expense details by ID");

        group.MapPost("", CreateExpense)
            .WithName("CreateExpense")
            .WithDescription("Create a new expense");

        group.MapPut("{id}", UpdateExpense)
            .WithName("UpdateExpense")
            .WithDescription("Update an existing expense");

        group.MapDelete("{id}", DeleteExpense)
            .WithName("DeleteExpense")
            .WithDescription("Soft delete an expense");

        group.MapGet("categories", GetExpenseCategories)
            .WithName("GetExpenseCategories")
            .WithDescription("Get list of expense categories");
    }

    private static async Task<IResult> GetExpenses(
        ClaimsPrincipal user,
        AppDbContext context,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? quarryId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var query = context.Expenses
            .Where(e => e.IsActive);

        // Apply role-based filtering
        if (userRole == "Clerk")
        {
            // Clerks only see their own expenses
            query = query.Where(e => e.ApplicationUserId == userId);
        }
        else if (userRole == "Manager")
        {
            // Managers see expenses from their quarries
            var managerQuarryIds = await context.Quarries
                .Where(q => q.ManagerId == userId)
                .Select(q => q.Id)
                .ToListAsync();

            query = query.Where(e => managerQuarryIds.Contains(e.QId));
        }
        // Administrators see all expenses (no additional filter)

        // Apply quarry filter if specified
        if (!string.IsNullOrEmpty(quarryId))
        {
            query = query.Where(e => e.QId == quarryId);
        }

        // Apply date range filter
        if (fromDate.HasValue)
        {
            query = query.Where(e => e.ExpenseDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(e => e.ExpenseDate <= toDate.Value);
        }

        var totalCount = await query.CountAsync();

        var expenses = await query
            .OrderByDescending(e => e.ExpenseDate)
            .ThenByDescending(e => e.DateCreated)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new ExpenseDto
            {
                Id = e.Id,
                ExpenseDate = e.ExpenseDate,
                Item = e.Item,
                Amount = e.Amount,
                Category = e.Category,
                TxnReference = e.TxnReference,
                DateCreated = e.DateCreated
            })
            .ToListAsync();

        return Results.Ok(new
        {
            expenses,
            totalCount,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    private static async Task<IResult> GetExpenseById(
        string id,
        ClaimsPrincipal user,
        AppDbContext context)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

        var expense = await context.Expenses
            .FirstOrDefaultAsync(e => e.Id == id && e.IsActive);

        if (expense == null)
        {
            return Results.NotFound(new { message = "Expense not found" });
        }

        // Check authorization
        if (userRole == "Clerk" && expense.ApplicationUserId != userId)
        {
            return Results.Forbid();
        }
        else if (userRole == "Manager")
        {
            var hasAccess = await context.Quarries
                .AnyAsync(q => q.Id == expense.QId && q.ManagerId == userId);

            if (!hasAccess)
            {
                return Results.Forbid();
            }
        }

        var expenseDto = new ExpenseDto
        {
            Id = expense.Id,
            ExpenseDate = expense.ExpenseDate,
            Item = expense.Item,
            Amount = expense.Amount,
            Category = expense.Category,
            TxnReference = expense.TxnReference,
            DateCreated = expense.DateCreated
        };

        return Results.Ok(expenseDto);
    }

    private static async Task<IResult> CreateExpense(
        [FromBody] CreateExpenseRequest request,
        ClaimsPrincipal user,
        ExpenseService expenseService,
        AppDbContext context)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        // Only clerks can create expenses
        if (userRole != "Clerk")
        {
            return Results.BadRequest(new { message = "Only clerks can record expenses" });
        }

        // Get user's quarry ID
        var userEntity = await context.Users.FindAsync(userId);
        if (userEntity == null || string.IsNullOrEmpty(userEntity.QuarryId))
        {
            return Results.BadRequest(new { message = "User not assigned to a quarry" });
        }

        // Build Expense object
        var expense = new Expense
        {
            ExpenseDate = request.ExpenseDate,
            Item = request.Item,
            Amount = request.Amount,
            Category = request.Category,
            TxnReference = request.TxnReference
        };

        var result = await expenseService.CreateExpenseAsync(expense, userId, userEntity.QuarryId);

        if (result.Success)
        {
            return Results.Created($"/api/expenses/{result.Expense?.Id}", result.Expense);
        }

        return Results.BadRequest(new { message = result.Message });
    }

    private static async Task<IResult> UpdateExpense(
        string id,
        [FromBody] UpdateExpenseRequest request,
        ClaimsPrincipal user,
        ExpenseService expenseService,
        AppDbContext context)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        // Check if expense exists and user has access
        var existingExpense = await context.Expenses.FindAsync(id);
        if (existingExpense == null || !existingExpense.IsActive)
        {
            return Results.NotFound(new { message = "Expense not found" });
        }

        // Only the clerk who created the expense can update it
        if (existingExpense.ApplicationUserId != userId)
        {
            return Results.Forbid();
        }

        // Build updated Expense object
        var expense = new Expense
        {
            Id = id,
            ExpenseDate = request.ExpenseDate,
            Item = request.Item,
            Amount = request.Amount,
            Category = request.Category,
            TxnReference = request.TxnReference
        };

        var result = await expenseService.UpdateExpenseAsync(expense, userId);

        if (result.Success)
        {
            return Results.Ok(new { message = result.Message });
        }

        return Results.BadRequest(new { message = result.Message });
    }

    private static async Task<IResult> DeleteExpense(
        string id,
        ClaimsPrincipal user,
        ExpenseService expenseService,
        AppDbContext context)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var expense = await context.Expenses.FindAsync(id);
        if (expense == null || !expense.IsActive)
        {
            return Results.NotFound(new { message = "Expense not found" });
        }

        // Only the clerk who created the expense can delete it
        if (expense.ApplicationUserId != userId)
        {
            return Results.Forbid();
        }

        var result = await expenseService.DeleteExpenseAsync(id, userId);

        if (result.Success)
        {
            return Results.Ok(new { message = result.Message });
        }

        return Results.BadRequest(new { message = result.Message });
    }

    private static IResult GetExpenseCategories(ExpenseService expenseService)
    {
        var categories = expenseService.GetExpenseCategories();
        return Results.Ok(categories);
    }
}

public record ExpenseDto
{
    public string Id { get; init; } = string.Empty;
    public DateTime? ExpenseDate { get; init; }
    public string Item { get; init; } = string.Empty;
    public double Amount { get; init; }
    public string Category { get; init; } = string.Empty;
    public string? TxnReference { get; init; }
    public DateTime DateCreated { get; init; }
}

public record CreateExpenseRequest(
    DateTime ExpenseDate,
    string Item,
    double Amount,
    string Category,
    string? TxnReference
);

public record UpdateExpenseRequest(
    DateTime ExpenseDate,
    string Item,
    double Amount,
    string Category,
    string? TxnReference
);
