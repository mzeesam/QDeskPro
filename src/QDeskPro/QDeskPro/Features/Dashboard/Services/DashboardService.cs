namespace QDeskPro.Features.Dashboard.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QDeskPro.Data;
using QDeskPro.Domain.Entities;
using QDeskPro.Shared.Extensions;

/// <summary>
/// Service for clerk dashboard statistics and data
/// </summary>
public class DashboardService
{
    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(AppDbContext context, IMemoryCache cache, ILogger<DashboardService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Get dashboard statistics for today for a clerk (with caching)
    /// </summary>
    public async Task<DashboardStats> GetDashboardStatsAsync(string userId, string quarryId)
    {
        _logger.LogDebug("Fetching dashboard stats for UserId: {UserId}, QuarryId: {QuarryId}", userId, quarryId);

        var today = DateTime.Today;
        var cacheKey = CachingExtensions.GetUserCacheKey("dashboard:stats", userId, today.ToString("yyyyMMdd"));

        return await _cache.GetOrCreateAsync(cacheKey, async () =>
        {
            _logger.LogInformation("Cache miss for dashboard stats. Computing for UserId: {UserId}, Date: {Date}", userId, today);

            var todayStamp = today.ToString("yyyyMMdd");

            // Get today's sales for this clerk
            var todaySales = await _context.Sales
                .Where(s => s.ApplicationUserId == userId)
                .Where(s => s.DateStamp == todayStamp)
                .Where(s => s.IsActive)
                .Include(s => s.Product)
                .ToListAsync();

            _logger.LogDebug("Found {SalesCount} sales for today for UserId: {UserId}", todaySales.Count, userId);

            var stats = new DashboardStats
            {
                SalesCount = todaySales.Count,
                TotalQuantity = todaySales.Sum(s => s.Quantity),
                TotalSales = todaySales.Sum(s => s.GrossSaleAmount)
            };

            // Get last sale details
            var lastSale = todaySales.OrderByDescending(s => s.DateCreated).FirstOrDefault();
            if (lastSale != null && lastSale.Product != null)
            {
                stats.LastSaleDescription = $"{lastSale.VehicleRegistration}: (KES {lastSale.GrossSaleAmount:N1}) " +
                    $"{lastSale.Quantity:N0} pieces of {lastSale.Product.ProductName} on {lastSale.SaleDate:dd/MM/yy} at {lastSale.SaleDate:hh:mm tt}";
            }

            // Get today's manual expenses
            var todayExpenses = await _context.Expenses
                .Where(e => e.ApplicationUserId == userId)
                .Where(e => e.DateStamp == todayStamp)
                .Where(e => e.IsActive)
                .ToListAsync();

            var manualExpenses = todayExpenses.Sum(e => e.Amount);

            // Calculate auto-generated expenses from sales
            // Get quarry fees for calculation
            var quarry = await _context.Quarries
                .Where(q => q.Id == quarryId)
                .Where(q => q.IsActive)
                .FirstOrDefaultAsync();

            double commissionExpenses = 0;
            double loadersFeeExpenses = 0;
            double landRateFeeExpenses = 0;

            if (quarry != null)
            {
                // Commission expenses from sales with commission
                commissionExpenses = todaySales
                    .Where(s => s.CommissionPerUnit > 0)
                    .Sum(s => s.Quantity * s.CommissionPerUnit);

                // Loaders fee expenses (if configured for quarry, excluding beam and hardcore products)
                if (quarry.LoadersFee.HasValue && quarry.LoadersFee > 0)
                {
                    loadersFeeExpenses = todaySales
                        .Where(s =>
                        {
                            var productName = s.Product?.ProductName ?? "";
                            return !productName.Contains("beam", StringComparison.OrdinalIgnoreCase) &&
                                   !productName.Contains("hardcore", StringComparison.OrdinalIgnoreCase);
                        })
                        .Sum(s => s.Quantity * quarry.LoadersFee.Value);
                }

                // Land rate fee expenses (if configured for quarry)
                if (quarry.LandRateFee.HasValue && quarry.LandRateFee > 0)
                {
                    foreach (var sale in todaySales)
                    {
                        // Skip if land rate is excluded for this sale
                        if (!sale.IncludeLandRate)
                            continue;

                        var productName = sale.Product?.ProductName ?? "";
                        double feeRate;

                        // Use RejectsFee for reject products
                        if (productName.Contains("reject", StringComparison.OrdinalIgnoreCase))
                        {
                            feeRate = quarry.RejectsFee ?? 0;
                        }
                        else
                        {
                            feeRate = quarry.LandRateFee.Value;
                        }

                        landRateFeeExpenses += sale.Quantity * feeRate;
                    }
                }
            }

            // Total expenses = manual + commission + loaders fee + land rate
            stats.TotalExpenses = manualExpenses + commissionExpenses + loadersFeeExpenses + landRateFeeExpenses;

            // Get opening balance (previous day's closing from DailyNote)
            var previousDay = today.AddDays(-1);
            var previousDayStamp = previousDay.ToString("yyyyMMdd");
            var previousDayNote = await _context.DailyNotes
                .Where(n => n.DateStamp == previousDayStamp)
                .Where(n => n.QId == quarryId)
                .Where(n => n.IsActive)
                .FirstOrDefaultAsync();

            stats.OpeningBalance = previousDayNote?.ClosingBalance ?? 0;

            // Get today's prepayments (customer deposits received today)
            var todayPrepayments = await _context.Prepayments
                .Where(p => p.QId == quarryId)
                .Where(p => p.DateStamp == todayStamp)
                .Where(p => p.IsActive)
                .SumAsync(p => p.TotalAmountPaid);

            stats.TotalPrepayments = todayPrepayments;

            // Get today's collections (payments received today for sales made before today)
            var todayCollections = await _context.Sales
                .Where(s => s.QId == quarryId)
                .Where(s => s.IsActive)
                .Where(s => s.PaymentStatus == "Paid")
                .Where(s => s.PaymentReceivedDate.HasValue)
                .Where(s => s.PaymentReceivedDate.Value.Date == today)
                .Where(s => s.SaleDate.HasValue && s.SaleDate.Value.Date < today)
                .SumAsync(s => s.Quantity * s.PricePerUnit);

            stats.TotalCollections = todayCollections;

            // Get total unpaid orders (all unpaid sales for this clerk)
            var totalUnpaid = await _context.Sales
                .Where(s => s.ApplicationUserId == userId)
                .Where(s => s.IsActive)
                .Where(s => s.PaymentStatus == "NotPaid")
                .SumAsync(s => s.Quantity * s.PricePerUnit);

            stats.TotalUnpaid = totalUnpaid;

            return stats;
        }, CacheExpirations.Dashboard); // Cache for 1 minute
    }

    /// <summary>
    /// Get or create today's daily note for a quarry
    /// </summary>
    public async Task<DailyNote> GetOrCreateTodayNoteAsync(string quarryId, string userId)
    {
        _logger.LogDebug("Getting or creating daily note for QuarryId: {QuarryId}, UserId: {UserId}", quarryId, userId);

        var today = DateTime.Today;
        var todayStamp = today.ToString("yyyyMMdd");

        var note = await _context.DailyNotes
            .Where(n => n.DateStamp == todayStamp)
            .Where(n => n.QId == quarryId)
            .Where(n => n.IsActive)
            .FirstOrDefaultAsync();

        if (note == null)
        {
            _logger.LogInformation("Daily note not found for {Date}. Creating new note for QuarryId: {QuarryId}", today, quarryId);

            // Auto-create note for today
            note = new DailyNote
            {
                Id = Guid.NewGuid().ToString(),
                quarryId = quarryId,
                NoteDate = today,
                DateStamp = todayStamp,
                QId = quarryId,
                ClosingBalance = 0,
                Notes = "",
                IsActive = true,
                DateCreated = DateTime.UtcNow,
                CreatedBy = userId
            };

            _context.DailyNotes.Add(note);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Daily note created with Id: {NoteId} for QuarryId: {QuarryId}", note.Id, quarryId);
        }
        else
        {
            _logger.LogDebug("Found existing daily note with Id: {NoteId}", note.Id);
        }

        return note;
    }

    /// <summary>
    /// Save daily note
    /// </summary>
    public async Task<bool> SaveDailyNoteAsync(DailyNote note, string userId)
    {
        _logger.LogDebug("Saving daily note with Id: {NoteId} for UserId: {UserId}", note.Id, userId);

        try
        {
            var existing = await _context.DailyNotes.FindAsync(note.Id);

            if (existing != null)
            {
                existing.Notes = note.Notes;
                existing.DateModified = DateTime.UtcNow;
                existing.ModifiedBy = userId;

                _logger.LogInformation("Daily note updated. NoteId: {NoteId}, ModifiedBy: {UserId}", note.Id, userId);
            }
            else
            {
                _logger.LogWarning("Daily note not found for update. NoteId: {NoteId}", note.Id);
            }

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save daily note. NoteId: {NoteId}, UserId: {UserId}", note.Id, userId);
            return false;
        }
    }
}

/// <summary>
/// Dashboard statistics model
/// </summary>
public class DashboardStats
{
    public int SalesCount { get; set; }
    public double TotalQuantity { get; set; }
    public double TotalSales { get; set; }
    public double TotalExpenses { get; set; }
    public double OpeningBalance { get; set; }
    public string? LastSaleDescription { get; set; }
    public double TotalPrepayments { get; set; }
    public double TotalCollections { get; set; }
    public double TotalUnpaid { get; set; }
}
