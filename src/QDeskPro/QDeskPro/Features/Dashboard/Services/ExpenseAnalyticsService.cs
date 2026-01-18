namespace QDeskPro.Features.Dashboard.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QDeskPro.Data;

/// <summary>
/// Service for expense-focused analytics dashboard.
/// Focus areas: categories, expense items, amounts, and time periods.
/// </summary>
public class ExpenseAnalyticsService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ExpenseAnalyticsService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Get comprehensive expense statistics for a date range and quarry.
    /// </summary>
    public async Task<ExpenseDashboardData> GetExpenseDashboardAsync(string? quarryId, DateTime fromDate, DateTime toDate, bool includeAutoCalculated = false)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var fromStamp = fromDate.ToString("yyyyMMdd");
        var toStamp = toDate.ToString("yyyyMMdd");

        // Base query for manual expenses
        var expenseQuery = context.Expenses
            .Where(e => e.IsActive)
            .Where(e => string.Compare(e.DateStamp, fromStamp) >= 0)
            .Where(e => string.Compare(e.DateStamp, toStamp) <= 0);

        // Filter by quarry if specified
        if (!string.IsNullOrEmpty(quarryId))
        {
            expenseQuery = expenseQuery.Where(e => e.QId == quarryId);
        }

        var expenses = await expenseQuery.ToListAsync();

        // Get quarry info for auto-calculated expenses
        var quarries = await context.Quarries
            .Where(q => q.IsActive)
            .Where(q => string.IsNullOrEmpty(quarryId) || q.Id == quarryId)
            .ToListAsync();

        // Build list of all expense items (manual + optionally auto-calculated)
        var allExpenseItems = new List<ExpenseItemData>();

        // Add manual expenses
        foreach (var expense in expenses)
        {
            allExpenseItems.Add(new ExpenseItemData
            {
                Date = expense.ExpenseDate ?? DateTime.MinValue,
                Item = expense.Item,
                Amount = expense.Amount,
                Category = expense.Category ?? "Uncategorized",
                Reference = expense.TxnReference ?? "",
                ExpenseType = "Manual",
                QuarryId = expense.QId
            });
        }

        // Optionally include auto-calculated expenses from sales
        if (includeAutoCalculated)
        {
            var salesQuery = context.Sales
                .Where(s => s.IsActive)
                .Where(s => string.Compare(s.DateStamp, fromStamp) >= 0)
                .Where(s => string.Compare(s.DateStamp, toStamp) <= 0);

            if (!string.IsNullOrEmpty(quarryId))
            {
                salesQuery = salesQuery.Where(s => s.QId == quarryId);
            }

            var sales = await salesQuery
                .Include(s => s.Product)
                .Include(s => s.Broker)
                .ToListAsync();

            foreach (var sale in sales)
            {
                var quarry = quarries.FirstOrDefault(q => q.Id == sale.QId);
                if (quarry == null) continue;

                // Commission expenses
                if (sale.CommissionPerUnit > 0)
                {
                    var commissionAmount = sale.Quantity * sale.CommissionPerUnit;
                    allExpenseItems.Add(new ExpenseItemData
                    {
                        Date = sale.SaleDate ?? DateTime.MinValue,
                        Item = $"{sale.VehicleRegistration} - {sale.Product?.ProductName ?? "Unknown"} commission" +
                               (sale.Broker != null ? $" to {sale.Broker.BrokerName}" : ""),
                        Amount = commissionAmount,
                        Category = "Commission",
                        Reference = sale.Id,
                        ExpenseType = "Auto-Commission",
                        QuarryId = sale.QId
                    });
                }

                // Loaders fee expenses
                if (quarry.LoadersFee.HasValue && quarry.LoadersFee > 0)
                {
                    var loadersFeeAmount = sale.Quantity * quarry.LoadersFee.Value;
                    allExpenseItems.Add(new ExpenseItemData
                    {
                        Date = sale.SaleDate ?? DateTime.MinValue,
                        Item = $"{sale.VehicleRegistration} loaders fee for {sale.Quantity:N0} pieces",
                        Amount = loadersFeeAmount,
                        Category = "Loaders Fees",
                        Reference = sale.Id,
                        ExpenseType = "Auto-LoadersFee",
                        QuarryId = sale.QId
                    });
                }

                // Land rate fee expenses
                if (quarry.LandRateFee.HasValue && quarry.LandRateFee > 0)
                {
                    var isReject = sale.Product?.ProductName?.ToLower().Contains("reject") == true;
                    var feeRate = isReject ? (quarry.RejectsFee ?? 0) : quarry.LandRateFee.Value;
                    if (feeRate > 0)
                    {
                        var landRateFeeAmount = sale.Quantity * feeRate;
                        allExpenseItems.Add(new ExpenseItemData
                        {
                            Date = sale.SaleDate ?? DateTime.MinValue,
                            Item = $"{sale.VehicleRegistration} land rate fee for {sale.Quantity:N0} pieces",
                            Amount = landRateFeeAmount,
                            Category = "Land Rate",
                            Reference = sale.Id,
                            ExpenseType = "Auto-LandRate",
                            QuarryId = sale.QId
                        });
                    }
                }
            }
        }

        // Calculate summary stats
        var totalExpenses = allExpenseItems.Sum(e => e.Amount);
        var expenseCount = allExpenseItems.Count;
        var avgExpenseAmount = expenseCount > 0 ? totalExpenses / expenseCount : 0;
        var uniqueCategories = allExpenseItems.Select(e => e.Category).Distinct().Count();

        // Expenses by Category
        var expensesByCategory = allExpenseItems
            .GroupBy(e => e.Category)
            .Select(g => new ExpenseByCategory
            {
                Category = g.Key,
                TotalAmount = g.Sum(e => e.Amount),
                ExpenseCount = g.Count(),
                AverageAmount = g.Average(e => e.Amount),
                PercentageOfTotal = totalExpenses > 0 ? (g.Sum(e => e.Amount) / totalExpenses) * 100 : 0
            })
            .OrderByDescending(e => e.TotalAmount)
            .ToList();

        // Top Category
        var topCategory = expensesByCategory.FirstOrDefault()?.Category ?? "N/A";

        // Daily Expense Trend
        var dailyExpenses = allExpenseItems
            .Where(e => e.Date != DateTime.MinValue)
            .GroupBy(e => e.Date.Date)
            .Select(g => new DailyExpenseData
            {
                Date = g.Key,
                TotalAmount = g.Sum(e => e.Amount),
                ExpenseCount = g.Count(),
                Categories = g.Select(e => e.Category).Distinct().Count()
            })
            .OrderBy(d => d.Date)
            .ToList();

        // Weekly Expense Trend
        var weeklyExpenses = allExpenseItems
            .Where(e => e.Date != DateTime.MinValue)
            .GroupBy(e => GetWeekStart(e.Date))
            .Select(g => new WeeklyExpenseData
            {
                WeekStart = g.Key,
                WeekEnd = g.Key.AddDays(6),
                TotalAmount = g.Sum(e => e.Amount),
                ExpenseCount = g.Count(),
                AveragePerDay = g.Sum(e => e.Amount) / 7
            })
            .OrderBy(w => w.WeekStart)
            .ToList();

        // Top Expense Items (largest individual expenses)
        var topExpenseItems = allExpenseItems
            .OrderByDescending(e => e.Amount)
            .Take(20)
            .ToList();

        // Expenses by Type (Manual vs Auto-calculated)
        var expensesByType = allExpenseItems
            .GroupBy(e => e.ExpenseType)
            .Select(g => new ExpenseByType
            {
                ExpenseType = g.Key,
                TotalAmount = g.Sum(e => e.Amount),
                ExpenseCount = g.Count(),
                PercentageOfTotal = totalExpenses > 0 ? (g.Sum(e => e.Amount) / totalExpenses) * 100 : 0
            })
            .OrderByDescending(e => e.TotalAmount)
            .ToList();

        // Calculate averages
        var daysInRange = (toDate - fromDate).Days + 1;
        var avgDailyExpense = daysInRange > 0 ? totalExpenses / daysInRange : 0;

        // Monthly comparison (current vs previous period)
        var previousFromDate = fromDate.AddDays(-(toDate - fromDate).Days - 1);
        var previousToDate = fromDate.AddDays(-1);
        var previousFromStamp = previousFromDate.ToString("yyyyMMdd");
        var previousToStamp = previousToDate.ToString("yyyyMMdd");

        var previousExpenseQuery = context.Expenses
            .Where(e => e.IsActive)
            .Where(e => string.Compare(e.DateStamp, previousFromStamp) >= 0)
            .Where(e => string.Compare(e.DateStamp, previousToStamp) <= 0);

        if (!string.IsNullOrEmpty(quarryId))
        {
            previousExpenseQuery = previousExpenseQuery.Where(e => e.QId == quarryId);
        }

        var previousTotalExpenses = await previousExpenseQuery.SumAsync(e => e.Amount);
        var changePercentage = previousTotalExpenses > 0
            ? ((totalExpenses - previousTotalExpenses) / previousTotalExpenses) * 100
            : 0;

        return new ExpenseDashboardData
        {
            // Summary Stats
            TotalExpenses = totalExpenses,
            ExpenseCount = expenseCount,
            AverageExpenseAmount = avgExpenseAmount,
            AverageDailyExpense = avgDailyExpense,
            UniqueCategories = uniqueCategories,
            TopCategory = topCategory,
            ChangeFromPreviousPeriod = changePercentage,

            // Breakdown Data
            ExpensesByCategory = expensesByCategory,
            DailyExpenses = dailyExpenses,
            WeeklyExpenses = weeklyExpenses,
            TopExpenseItems = topExpenseItems,
            ExpensesByType = expensesByType,

            // Date Range
            FromDate = fromDate,
            ToDate = toDate,
            IncludesAutoCalculated = includeAutoCalculated
        };
    }

    /// <summary>
    /// Get detailed expense data for export purposes.
    /// </summary>
    public async Task<List<ExpenseExportRow>> GetExpenseExportDataAsync(string? quarryId, DateTime fromDate, DateTime toDate, bool includeAutoCalculated = false)
    {
        var dashboardData = await GetExpenseDashboardAsync(quarryId, fromDate, toDate, includeAutoCalculated);

        // Get quarry names
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var quarries = await context.Quarries.Where(q => q.IsActive).ToDictionaryAsync(q => q.Id, q => q.QuarryName);

        return dashboardData.TopExpenseItems
            .Concat(dashboardData.ExpensesByCategory
                .SelectMany(c => dashboardData.TopExpenseItems.Where(e => e.Category == c.Category)))
            .DistinctBy(e => new { e.Date, e.Item, e.Amount })
            .OrderBy(e => e.Date)
            .Select(e => new ExpenseExportRow
            {
                Date = e.Date,
                Item = e.Item,
                Category = e.Category,
                Amount = e.Amount,
                Reference = e.Reference,
                ExpenseType = e.ExpenseType,
                QuarryName = e.QuarryId != null && quarries.ContainsKey(e.QuarryId) ? quarries[e.QuarryId] : "Unknown"
            })
            .ToList();
    }

    /// <summary>
    /// Get all expense items for detailed export.
    /// </summary>
    public async Task<List<ExpenseItemData>> GetAllExpenseItemsAsync(string? quarryId, DateTime fromDate, DateTime toDate, bool includeAutoCalculated = false)
    {
        var dashboardData = await GetExpenseDashboardAsync(quarryId, fromDate, toDate, includeAutoCalculated);

        // Reconstruct full list from category data
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var fromStamp = fromDate.ToString("yyyyMMdd");
        var toStamp = toDate.ToString("yyyyMMdd");

        var expenseQuery = context.Expenses
            .Where(e => e.IsActive)
            .Where(e => string.Compare(e.DateStamp, fromStamp) >= 0)
            .Where(e => string.Compare(e.DateStamp, toStamp) <= 0);

        if (!string.IsNullOrEmpty(quarryId))
        {
            expenseQuery = expenseQuery.Where(e => e.QId == quarryId);
        }

        var expenses = await expenseQuery.OrderBy(e => e.ExpenseDate).ToListAsync();

        var result = expenses.Select(e => new ExpenseItemData
        {
            Date = e.ExpenseDate ?? DateTime.MinValue,
            Item = e.Item,
            Amount = e.Amount,
            Category = e.Category ?? "Uncategorized",
            Reference = e.TxnReference ?? "",
            ExpenseType = "Manual",
            QuarryId = e.QId
        }).ToList();

        if (includeAutoCalculated)
        {
            // Add auto-calculated items from the dashboard data
            // This is a simplified approach - for full data, we'd need to recalculate
            var autoItems = dashboardData.TopExpenseItems
                .Where(e => e.ExpenseType != "Manual")
                .ToList();
            result.AddRange(autoItems);
        }

        return result.OrderBy(e => e.Date).ToList();
    }

    private static DateTime GetWeekStart(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.AddDays(-diff).Date;
    }
}

#region DTOs

/// <summary>
/// Comprehensive expense dashboard data.
/// </summary>
public class ExpenseDashboardData
{
    // Summary Statistics
    public double TotalExpenses { get; set; }
    public int ExpenseCount { get; set; }
    public double AverageExpenseAmount { get; set; }
    public double AverageDailyExpense { get; set; }
    public int UniqueCategories { get; set; }
    public string TopCategory { get; set; } = string.Empty;
    public double ChangeFromPreviousPeriod { get; set; }

    // Breakdown Data
    public List<ExpenseByCategory> ExpensesByCategory { get; set; } = [];
    public List<DailyExpenseData> DailyExpenses { get; set; } = [];
    public List<WeeklyExpenseData> WeeklyExpenses { get; set; } = [];
    public List<ExpenseItemData> TopExpenseItems { get; set; } = [];
    public List<ExpenseByType> ExpensesByType { get; set; } = [];

    // Date Range
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public bool IncludesAutoCalculated { get; set; }
}

/// <summary>
/// Expense breakdown by category.
/// </summary>
public class ExpenseByCategory
{
    public string Category { get; set; } = string.Empty;
    public double TotalAmount { get; set; }
    public int ExpenseCount { get; set; }
    public double AverageAmount { get; set; }
    public double PercentageOfTotal { get; set; }
}

/// <summary>
/// Daily expense data.
/// </summary>
public class DailyExpenseData
{
    public DateTime Date { get; set; }
    public double TotalAmount { get; set; }
    public int ExpenseCount { get; set; }
    public int Categories { get; set; }
}

/// <summary>
/// Weekly expense data.
/// </summary>
public class WeeklyExpenseData
{
    public DateTime WeekStart { get; set; }
    public DateTime WeekEnd { get; set; }
    public double TotalAmount { get; set; }
    public int ExpenseCount { get; set; }
    public double AveragePerDay { get; set; }
}

/// <summary>
/// Individual expense item data.
/// </summary>
public class ExpenseItemData
{
    public DateTime Date { get; set; }
    public string Item { get; set; } = string.Empty;
    public double Amount { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string ExpenseType { get; set; } = string.Empty; // Manual, Auto-Commission, Auto-LoadersFee, Auto-LandRate
    public string? QuarryId { get; set; }
}

/// <summary>
/// Expense breakdown by type (Manual vs Auto-calculated).
/// </summary>
public class ExpenseByType
{
    public string ExpenseType { get; set; } = string.Empty;
    public double TotalAmount { get; set; }
    public int ExpenseCount { get; set; }
    public double PercentageOfTotal { get; set; }
}

/// <summary>
/// Row data for expense export.
/// </summary>
public class ExpenseExportRow
{
    public DateTime Date { get; set; }
    public string Item { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public double Amount { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string ExpenseType { get; set; } = string.Empty;
    public string QuarryName { get; set; } = string.Empty;
}

#endregion
