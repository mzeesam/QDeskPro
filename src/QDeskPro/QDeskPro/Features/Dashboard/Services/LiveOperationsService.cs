using Microsoft.EntityFrameworkCore;
using QDeskPro.Data;
using QDeskPro.Features.Dashboard.Models;
using System.Globalization;

namespace QDeskPro.Features.Dashboard.Services;

/// <summary>
/// Service for calculating live operations metrics across all manager's quarries
/// </summary>
public class LiveOperationsService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LiveOperationsService> _logger;

    public LiveOperationsService(
        IServiceScopeFactory scopeFactory,
        ILogger<LiveOperationsService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Get live operations data for all quarries owned by the manager
    /// </summary>
    /// <param name="managerId">Manager user ID</param>
    /// <param name="isAdmin">If true, returns data for ALL quarries</param>
    /// <returns>List of quarry operations metrics</returns>
    public async Task<List<QuarryLiveOperations>> GetLiveOperationsForManagerAsync(string managerId, bool isAdmin = false)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            // Get quarries for this manager
            var quarries = await GetManagerQuarriesAsync(context, managerId, isAdmin);

            _logger.LogInformation("LiveOperations: Found {QuarryCount} quarries for manager {ManagerId} (isAdmin: {IsAdmin})",
                quarries.Count, managerId, isAdmin);

            if (!quarries.Any())
            {
                _logger.LogWarning("LiveOperations: No quarries found for manager {ManagerId}. Check if user is assigned to any quarries or if ManagerId matches.",
                    managerId);
            }

            // Process each quarry sequentially to avoid DbContext concurrency issues
            // Note: We can't use Task.WhenAll with shared DbContext
            var results = new List<QuarryLiveOperations>();
            foreach (var quarry in quarries)
            {
                var metrics = await CalculateQuarryMetricsAsync(context, quarry.Id, quarry.QuarryName, quarry.Location);
                results.Add(metrics);
            }

            // Log activity summary
            foreach (var result in results)
            {
                _logger.LogInformation("LiveOperations: Quarry {QuarryName} - TotalActivities: {TotalActivities}, HasActivity: {HasActivity}",
                    result.QuarryName, result.TotalActivities, result.HasActivity);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting live operations for manager {ManagerId}", managerId);
            return new List<QuarryLiveOperations>();
        }
    }

    /// <summary>
    /// Get quarries accessible by the manager
    /// </summary>
    private async Task<List<(string Id, string QuarryName, string Location)>> GetManagerQuarriesAsync(
        AppDbContext context,
        string managerId,
        bool isAdmin)
    {
        var query = context.Quarries.Where(q => q.IsActive);

        // Administrators can see all quarries, managers only their own
        if (!isAdmin)
        {
            query = query.Where(q => q.ManagerId == managerId);
        }

        return await query
            .OrderBy(q => q.QuarryName)
            .Select(q => new ValueTuple<string, string, string>(
                q.Id,
                q.QuarryName ?? "Unnamed Quarry",
                q.Location ?? ""))
            .ToListAsync();
    }

    /// <summary>
    /// Calculate all metrics for a single quarry (today's data only)
    /// </summary>
    private async Task<QuarryLiveOperations> CalculateQuarryMetricsAsync(
        AppDbContext context,
        string quarryId,
        string quarryName,
        string location)
    {
        var today = DateTime.Today;
        var todayStamp = today.ToString("yyyyMMdd");

        var metrics = new QuarryLiveOperations
        {
            QuarryId = quarryId,
            QuarryName = quarryName,
            Location = location
        };

        try
        {
            // Get quarry settings (for fee calculations)
            var quarry = await context.Quarries
                .FirstOrDefaultAsync(q => q.Id == quarryId);

            if (quarry == null)
            {
                _logger.LogWarning("Quarry {QuarryId} not found", quarryId);
                return metrics;
            }

            // ================================================
            // SALES METRICS
            // ================================================

            var todaySales = await context.Sales
                .Where(s => s.QId == quarryId)
                .Where(s => s.DateStamp == todayStamp)
                .Where(s => s.IsActive)
                .Include(s => s.Product)
                .Include(s => s.Clerk)
                .ToListAsync();

            metrics.SalesCount = todaySales.Count;
            metrics.TotalQuantity = todaySales.Sum(s => s.Quantity);
            metrics.TotalSales = todaySales.Sum(s => s.Quantity * s.PricePerUnit);

            // ================================================
            // EXPENSE BREAKDOWN (4-Source System)
            // ================================================

            // SOURCE 1: Manual Expenses
            metrics.ManualExpenses = await context.Expenses
                .Where(e => e.QId == quarryId)
                .Where(e => e.DateStamp == todayStamp)
                .Where(e => e.IsActive)
                .SumAsync(e => e.Amount);

            // SOURCE 2: Commission Expenses (from sales)
            metrics.CommissionExpenses = todaySales
                .Where(s => s.CommissionPerUnit > 0)
                .Sum(s => s.Quantity * s.CommissionPerUnit);

            // SOURCE 3: Loaders Fee Expenses (excluding beam and hardcore products)
            if (quarry.LoadersFee.HasValue && quarry.LoadersFee > 0)
            {
                metrics.LoadersFeeExpenses = todaySales
                    .Where(s =>
                    {
                        var productName = s.Product?.ProductName ?? "";
                        return !productName.Contains("beam", StringComparison.OrdinalIgnoreCase) &&
                               !productName.Contains("hardcore", StringComparison.OrdinalIgnoreCase);
                    })
                    .Sum(s => s.Quantity * quarry.LoadersFee.Value);
            }

            // SOURCE 4: Land Rate Fee Expenses (with Reject product handling)
            if (quarry.LandRateFee.HasValue && quarry.LandRateFee > 0)
            {
                foreach (var sale in todaySales)
                {
                    // Skip if land rate excluded for this sale
                    if (!sale.IncludeLandRate)
                        continue;

                    var productName = sale.Product?.ProductName ?? "";
                    double feeRate;

                    // Special case: Reject products use RejectsFee
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
                        metrics.LandRateExpenses += sale.Quantity * feeRate;
                    }
                }
            }

            // Total Expenses
            metrics.TotalExpenses = metrics.ManualExpenses +
                                    metrics.CommissionExpenses +
                                    metrics.LoadersFeeExpenses +
                                    metrics.LandRateExpenses;

            // ================================================
            // FUEL USAGE
            // ================================================

            var todayFuel = await context.FuelUsages
                .Where(f => f.QId == quarryId)
                .Where(f => f.DateStamp == todayStamp)
                .Where(f => f.IsActive)
                .ToListAsync();

            metrics.FuelConsumed = todayFuel.Sum(f => f.MachinesLoaded + f.WheelLoadersLoaded);

            // ================================================
            // COLLECTIONS (Past Unpaid Orders Paid Today)
            // ================================================

            var collections = await context.Sales
                .Where(s => s.QId == quarryId)
                .Where(s => s.IsActive)
                .Where(s => s.PaymentStatus == "Paid")
                .Where(s => s.PaymentReceivedDate != null)
                .Where(s => s.PaymentReceivedDate.Value.Date == today)
                .Where(s => s.SaleDate < today) // Sale was before today
                .ToListAsync();

            metrics.CollectionsCount = collections.Count;
            metrics.TotalCollections = collections.Sum(c => c.Quantity * c.PricePerUnit);

            // ================================================
            // PREPAYMENTS (Customer Deposits Received Today)
            // ================================================

            var todayPrepayments = await context.Prepayments
                .Where(p => p.QId == quarryId)
                .Where(p => p.DateStamp == todayStamp)
                .Where(p => p.IsActive)
                .ToListAsync();

            metrics.PrepaymentsCount = todayPrepayments.Count;
            metrics.TotalPrepayments = todayPrepayments.Sum(p => p.TotalAmountPaid);

            // ================================================
            // BANKING
            // ================================================

            var todayBanking = await context.Bankings
                .Where(b => b.QId == quarryId)
                .Where(b => b.DateStamp == todayStamp)
                .Where(b => b.IsActive)
                .ToListAsync();

            metrics.BankingCount = todayBanking.Count;
            metrics.TotalBanked = todayBanking.Sum(b => b.AmountBanked);

            // ================================================
            // UNPAID ORDERS
            // ================================================

            var unpaidSales = todaySales.Where(s => s.PaymentStatus != "Paid").ToList();
            metrics.UnpaidCount = unpaidSales.Count;
            metrics.UnpaidAmount = unpaidSales.Sum(s => s.Quantity * s.PricePerUnit);

            // ================================================
            // OPENING BALANCE (Yesterday's Closing)
            // ================================================

            var yesterday = today.AddDays(-1);
            var yesterdayStamp = yesterday.ToString("yyyyMMdd");
            var yesterdayNote = await context.DailyNotes
                .Where(n => n.QId == quarryId)
                .Where(n => n.DateStamp == yesterdayStamp)
                .Where(n => n.IsActive)
                .FirstOrDefaultAsync();

            metrics.OpeningBalance = yesterdayNote?.ClosingBalance ?? 0;

            // ================================================
            // ESTIMATED CASH IN HAND
            // ================================================

            // Formula: Opening + Revenue + Collections + Prepayments - Expenses - Banking - Unpaid
            var earnings = metrics.TotalSales - metrics.TotalExpenses;
            metrics.EstimatedCashInHand = metrics.OpeningBalance +
                                          earnings +
                                          metrics.TotalCollections +
                                          metrics.TotalPrepayments -
                                          metrics.UnpaidAmount -
                                          metrics.TotalBanked;

            // ================================================
            // ACTIVITY TRACKING
            // ================================================

            // Get all unique clerks who posted today
            var salesClerks = todaySales
                .Where(s => !string.IsNullOrWhiteSpace(s.ClerkName))
                .Select(s => s.ClerkName!)
                .Distinct();

            var expenseClerks = await context.Expenses
                .Where(e => e.QId == quarryId)
                .Where(e => e.DateStamp == todayStamp)
                .Where(e => e.IsActive)
                .Join(context.Users,
                    e => e.ApplicationUserId,
                    u => u.Id,
                    (e, u) => u.FullName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct()
                .ToListAsync();

            var bankingClerks = await context.Bankings
                .Where(b => b.QId == quarryId)
                .Where(b => b.DateStamp == todayStamp)
                .Where(b => b.IsActive)
                .Join(context.Users,
                    b => b.ApplicationUserId,
                    u => u.Id,
                    (b, u) => u.FullName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct()
                .ToListAsync();

            var fuelClerks = await context.FuelUsages
                .Where(f => f.QId == quarryId)
                .Where(f => f.DateStamp == todayStamp)
                .Where(f => f.IsActive)
                .Join(context.Users,
                    f => f.ApplicationUserId,
                    u => u.Id,
                    (f, u) => u.FullName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct()
                .ToListAsync();

            var prepaymentClerks = todayPrepayments
                .Where(p => !string.IsNullOrWhiteSpace(p.ClerkName))
                .Select(p => p.ClerkName!)
                .Distinct();

            // Combine and deduplicate
            metrics.ActiveClerks = salesClerks
                .Concat(expenseClerks)
                .Concat(bankingClerks)
                .Concat(fuelClerks)
                .Concat(prepaymentClerks)
                .Distinct()
                .OrderBy(name => name)
                .ToList();

            // Total activity count
            var expenseCount = await context.Expenses.CountAsync(e => e.QId == quarryId && e.DateStamp == todayStamp && e.IsActive);
            metrics.TotalActivities = metrics.SalesCount +
                                       expenseCount +
                                       metrics.BankingCount +
                                       todayFuel.Count +
                                       metrics.PrepaymentsCount;

            _logger.LogInformation("LiveOperations metrics for quarry {QuarryId}: Sales={SalesCount}, Expenses={ExpenseCount}, Banking={BankingCount}, Fuel={FuelCount}, Prepayments={PrepaymentCount}, TotalActivities={TotalActivities}, DateStamp={DateStamp}",
                quarryId, metrics.SalesCount, expenseCount, metrics.BankingCount, todayFuel.Count, metrics.PrepaymentsCount, metrics.TotalActivities, todayStamp);

            // Last activity time (most recent transaction)
            var lastSaleTime = todaySales.Any() ? todaySales.Max(s => s.DateCreated) : (DateTime?)null;
            var lastExpenseTime = await context.Expenses
                .Where(e => e.QId == quarryId && e.DateStamp == todayStamp && e.IsActive)
                .MaxAsync(e => (DateTime?)e.DateCreated);
            var lastBankingTime = todayBanking.Any() ? todayBanking.Max(b => b.DateCreated) : (DateTime?)null;
            var lastFuelTime = todayFuel.Any() ? todayFuel.Max(f => f.DateCreated) : (DateTime?)null;
            var lastPrepaymentTime = todayPrepayments.Any() ? todayPrepayments.Max(p => p.DateCreated) : (DateTime?)null;

            var allTimes = new[] { lastSaleTime, lastExpenseTime, lastBankingTime, lastFuelTime, lastPrepaymentTime }
                .Where(t => t.HasValue)
                .ToList();

            metrics.LastActivityTime = allTimes.Any() ? allTimes.Max() : null;

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating metrics for quarry {QuarryId}", quarryId);
            return metrics;
        }
    }
}
