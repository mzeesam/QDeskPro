using System.Collections.Concurrent;
using QDeskPro.Features.Monitoring.Models;

namespace QDeskPro.Features.Monitoring.Services;

/// <summary>
/// Service that collects and aggregates request metrics for the observability dashboard.
/// Uses a circular buffer to store recent requests for analysis.
/// </summary>
public class MetricsCollectorService
{
    private readonly ConcurrentQueue<RequestRecord> _requestBuffer = new();
    private readonly ConcurrentDictionary<string, long> _endpointTotalRequests = new();
    private readonly ConcurrentDictionary<string, long> _endpointTotalErrors = new();
    private readonly ConcurrentDictionary<string, long> _endpointTotalDuration = new();
    private readonly ILogger<MetricsCollectorService> _logger;

    private long _totalRequests;
    private long _totalErrors;
    private const int MaxBufferSize = 10000; // Keep last 10k requests for analysis
    private const int MetricsRetentionMinutes = 60; // Keep detailed metrics for 1 hour

    public MetricsCollectorService(ILogger<MetricsCollectorService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Records a request for metrics collection.
    /// Called by the RequestLoggingMiddleware.
    /// </summary>
    public void RecordRequest(string endpoint, string method, int statusCode, long durationMs, string? userId)
    {
        var record = new RequestRecord
        {
            Endpoint = NormalizeEndpoint(endpoint),
            Method = method.ToUpperInvariant(),
            StatusCode = statusCode,
            DurationMs = durationMs,
            UserId = userId,
            Timestamp = DateTime.UtcNow
        };

        // Add to buffer
        _requestBuffer.Enqueue(record);

        // Trim buffer if exceeded
        while (_requestBuffer.Count > MaxBufferSize && _requestBuffer.TryDequeue(out _)) { }

        // Update aggregate counters
        Interlocked.Increment(ref _totalRequests);
        if (record.IsError)
        {
            Interlocked.Increment(ref _totalErrors);
        }

        // Update per-endpoint counters
        var endpointKey = $"{record.Method}:{record.Endpoint}";
        _endpointTotalRequests.AddOrUpdate(endpointKey, 1, (_, count) => count + 1);
        _endpointTotalDuration.AddOrUpdate(endpointKey, durationMs, (_, total) => total + durationMs);
        if (record.IsError)
        {
            _endpointTotalErrors.AddOrUpdate(endpointKey, 1, (_, count) => count + 1);
        }
    }

    /// <summary>
    /// Gets current request metrics.
    /// </summary>
    public RequestMetrics GetMetrics()
    {
        var now = DateTime.UtcNow;
        var oneHourAgo = now.AddHours(-1);
        var oneMinuteAgo = now.AddMinutes(-1);

        var recentRequests = _requestBuffer.Where(r => r.Timestamp >= oneHourAgo).ToList();
        var lastMinuteRequests = recentRequests.Where(r => r.Timestamp >= oneMinuteAgo).ToList();

        // Calculate response time stats
        double avgResponseTime = 0;
        long maxResponseTime = 0;
        long minResponseTime = 0;

        if (recentRequests.Count > 0)
        {
            avgResponseTime = recentRequests.Average(r => r.DurationMs);
            maxResponseTime = recentRequests.Max(r => r.DurationMs);
            minResponseTime = recentRequests.Min(r => r.DurationMs);
        }

        // Calculate error rate
        var recentErrors = recentRequests.Count(r => r.IsError);
        var errorRate = recentRequests.Count > 0 ? (double)recentErrors / recentRequests.Count * 100 : 0;

        // Get active users
        var activeUsers = recentRequests
            .Where(r => !string.IsNullOrEmpty(r.UserId))
            .Select(r => r.UserId)
            .Distinct()
            .Count();

        // Get endpoint metrics (top 20 by request count)
        var endpointMetrics = recentRequests
            .GroupBy(r => new { r.Method, r.Endpoint })
            .Select(g => new EndpointMetrics
            {
                Endpoint = g.Key.Endpoint,
                Method = g.Key.Method,
                RequestCount = g.Count(),
                AvgResponseTimeMs = Math.Round(g.Average(r => r.DurationMs), 2),
                MaxResponseTimeMs = g.Max(r => r.DurationMs),
                ErrorCount = g.Count(r => r.IsError),
                LastRequestTime = g.Max(r => r.Timestamp)
            })
            .OrderByDescending(e => e.RequestCount)
            .Take(20)
            .ToList();

        // Status code distribution
        var statusCodeDist = recentRequests
            .GroupBy(r => r.StatusCode)
            .ToDictionary(g => g.Key, g => g.Count());

        // Method distribution
        var methodDist = recentRequests
            .GroupBy(r => r.Method)
            .ToDictionary(g => g.Key, g => g.Count());

        return new RequestMetrics
        {
            TotalRequests = _totalRequests,
            RequestsLastHour = recentRequests.Count,
            RequestsLastMinute = lastMinuteRequests.Count,
            AvgResponseTimeMs = Math.Round(avgResponseTime, 2),
            MaxResponseTimeMs = maxResponseTime,
            MinResponseTimeMs = minResponseTime,
            ErrorCount = _totalErrors,
            ErrorRatePercent = Math.Round(errorRate, 2),
            ActiveUsersLastHour = activeUsers,
            ByEndpoint = endpointMetrics,
            ByStatusCode = statusCodeDist,
            ByMethod = methodDist,
            LastUpdated = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Gets recent request history for a specific endpoint.
    /// </summary>
    public List<RequestRecord> GetEndpointHistory(string endpoint, string? method = null, int maxResults = 100)
    {
        var normalizedEndpoint = NormalizeEndpoint(endpoint);
        var now = DateTime.UtcNow;
        var oneHourAgo = now.AddHours(-1);

        var query = _requestBuffer
            .Where(r => r.Timestamp >= oneHourAgo)
            .Where(r => r.Endpoint.Equals(normalizedEndpoint, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(method))
        {
            query = query.Where(r => r.Method.Equals(method, StringComparison.OrdinalIgnoreCase));
        }

        return query
            .OrderByDescending(r => r.Timestamp)
            .Take(maxResults)
            .ToList();
    }

    /// <summary>
    /// Gets request rate over time for charting.
    /// </summary>
    public List<TimeSeriesPoint> GetRequestRateTimeSeries(int minutesBack = 60, int bucketMinutes = 5)
    {
        var now = DateTime.UtcNow;
        var startTime = now.AddMinutes(-minutesBack);

        var requests = _requestBuffer
            .Where(r => r.Timestamp >= startTime)
            .ToList();

        var buckets = new List<TimeSeriesPoint>();
        var currentBucket = startTime;

        while (currentBucket < now)
        {
            var bucketEnd = currentBucket.AddMinutes(bucketMinutes);
            var bucketRequests = requests
                .Where(r => r.Timestamp >= currentBucket && r.Timestamp < bucketEnd)
                .ToList();

            buckets.Add(new TimeSeriesPoint
            {
                Timestamp = currentBucket,
                Value = bucketRequests.Count,
                ErrorCount = bucketRequests.Count(r => r.IsError),
                AvgDurationMs = bucketRequests.Count > 0 ? Math.Round(bucketRequests.Average(r => r.DurationMs), 2) : 0
            });

            currentBucket = bucketEnd;
        }

        return buckets;
    }

    /// <summary>
    /// Gets error rate over time for charting.
    /// </summary>
    public List<TimeSeriesPoint> GetErrorRateTimeSeries(int minutesBack = 60, int bucketMinutes = 5)
    {
        var rateData = GetRequestRateTimeSeries(minutesBack, bucketMinutes);
        return rateData.Select(p => new TimeSeriesPoint
        {
            Timestamp = p.Timestamp,
            Value = p.Value > 0 ? Math.Round((double)p.ErrorCount / p.Value * 100, 2) : 0,
            ErrorCount = p.ErrorCount,
            AvgDurationMs = p.AvgDurationMs
        }).ToList();
    }

    /// <summary>
    /// Normalizes endpoint paths by removing IDs and query strings.
    /// </summary>
    private static string NormalizeEndpoint(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;

        // Remove query string
        var queryIndex = path.IndexOf('?');
        if (queryIndex >= 0)
        {
            path = path[..queryIndex];
        }

        // Normalize GUIDs in paths to {id}
        path = System.Text.RegularExpressions.Regex.Replace(
            path,
            @"/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
            "/{id}");

        // Normalize numeric IDs to {id}
        path = System.Text.RegularExpressions.Regex.Replace(
            path,
            @"/\d+(?=/|$)",
            "/{id}");

        return path.ToLowerInvariant();
    }

    /// <summary>
    /// Clears all collected metrics.
    /// </summary>
    public void Clear()
    {
        while (_requestBuffer.TryDequeue(out _)) { }
        _endpointTotalRequests.Clear();
        _endpointTotalErrors.Clear();
        _endpointTotalDuration.Clear();
        Interlocked.Exchange(ref _totalRequests, 0);
        Interlocked.Exchange(ref _totalErrors, 0);
    }
}

/// <summary>
/// Time series data point for charting.
/// </summary>
public record TimeSeriesPoint
{
    public DateTime Timestamp { get; init; }
    public double Value { get; init; }
    public int ErrorCount { get; init; }
    public double AvgDurationMs { get; init; }
}
