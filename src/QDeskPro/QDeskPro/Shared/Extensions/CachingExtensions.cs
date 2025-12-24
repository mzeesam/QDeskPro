using Microsoft.Extensions.Caching.Memory;

namespace QDeskPro.Shared.Extensions;

/// <summary>
/// Extension methods for caching operations
/// </summary>
public static class CachingExtensions
{
    /// <summary>
    /// Gets or creates a cached value
    /// </summary>
    public static async Task<T> GetOrCreateAsync<T>(
        this IMemoryCache cache,
        string key,
        Func<Task<T>> factory,
        TimeSpan? absoluteExpirationRelativeToNow = null)
    {
        if (cache.TryGetValue(key, out T? cachedValue) && cachedValue != null)
        {
            return cachedValue;
        }

        var value = await factory();

        var cacheEntryOptions = new MemoryCacheEntryOptions();

        if (absoluteExpirationRelativeToNow.HasValue)
        {
            cacheEntryOptions.SetAbsoluteExpiration(absoluteExpirationRelativeToNow.Value);
        }
        else
        {
            // Default expiration: 5 minutes
            cacheEntryOptions.SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
        }

        cache.Set(key, value, cacheEntryOptions);
        return value;
    }

    /// <summary>
    /// Invalidates cache entries by pattern
    /// </summary>
    public static void RemoveByPattern(this IMemoryCache cache, string pattern)
    {
        // Note: IMemoryCache doesn't support pattern-based removal out of the box
        // This is a limitation of the in-memory cache
        // For pattern-based removal, consider using distributed cache (Redis)
    }

    /// <summary>
    /// Generates a cache key for quarry-specific data
    /// </summary>
    public static string GetQuarryCacheKey(string prefix, string quarryId, params object[] parameters)
    {
        var paramString = string.Join("_", parameters.Select(p => p?.ToString() ?? "null"));
        return $"{prefix}:quarry:{quarryId}:{paramString}";
    }

    /// <summary>
    /// Generates a cache key for user-specific data
    /// </summary>
    public static string GetUserCacheKey(string prefix, string userId, params object[] parameters)
    {
        var paramString = string.Join("_", parameters.Select(p => p?.ToString() ?? "null"));
        return $"{prefix}:user:{userId}:{paramString}";
    }

    /// <summary>
    /// Generates a cache key for date-range data
    /// </summary>
    public static string GetDateRangeCacheKey(string prefix, DateTime fromDate, DateTime toDate, params object[] parameters)
    {
        var paramString = string.Join("_", parameters.Select(p => p?.ToString() ?? "null"));
        return $"{prefix}:daterange:{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}:{paramString}";
    }
}

/// <summary>
/// Cache key constants
/// </summary>
public static class CacheKeys
{
    // Master data cache keys (long expiration - 1 hour)
    public const string Products = "masterdata:products";
    public const string Quarries = "masterdata:quarries";
    public const string QuarryLayers = "masterdata:quarry:{0}:layers";
    public const string QuarryBrokers = "masterdata:quarry:{0}:brokers";
    public const string QuarryProductPrices = "masterdata:quarry:{0}:prices";

    // Dashboard cache keys (short expiration - 1 minute)
    public const string DashboardStats = "dashboard:stats:{0}:{1}"; // quarryId:date
    public const string DashboardTrends = "dashboard:trends:{0}:{1}:{2}"; // quarryId:fromDate:toDate

    // Report cache keys (medium expiration - 5 minutes)
    public const string DailySalesReport = "report:daily:{0}:{1}"; // quarryId:date
    public const string SalesSummary = "report:summary:{0}:{1}:{2}"; // quarryId:fromDate:toDate
}

/// <summary>
/// Cache expiration times
/// </summary>
public static class CacheExpirations
{
    public static readonly TimeSpan MasterData = TimeSpan.FromHours(1);
    public static readonly TimeSpan Dashboard = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan Reports = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan ShortLived = TimeSpan.FromSeconds(30);
}
