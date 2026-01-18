namespace QDeskPro.Features.Monitoring.Models;

/// <summary>
/// Request metrics for the observability dashboard.
/// </summary>
public record RequestMetrics
{
    /// <summary>
    /// Total number of requests processed.
    /// </summary>
    public long TotalRequests { get; init; }

    /// <summary>
    /// Requests in the last hour.
    /// </summary>
    public int RequestsLastHour { get; init; }

    /// <summary>
    /// Requests in the last minute.
    /// </summary>
    public int RequestsLastMinute { get; init; }

    /// <summary>
    /// Average response time in milliseconds.
    /// </summary>
    public double AvgResponseTimeMs { get; init; }

    /// <summary>
    /// Maximum response time in milliseconds.
    /// </summary>
    public long MaxResponseTimeMs { get; init; }

    /// <summary>
    /// Minimum response time in milliseconds.
    /// </summary>
    public long MinResponseTimeMs { get; init; }

    /// <summary>
    /// Total number of errors (4xx and 5xx responses).
    /// </summary>
    public long ErrorCount { get; init; }

    /// <summary>
    /// Error rate as a percentage.
    /// </summary>
    public double ErrorRatePercent { get; init; }

    /// <summary>
    /// Number of unique active users in the last hour.
    /// </summary>
    public int ActiveUsersLastHour { get; init; }

    /// <summary>
    /// Metrics grouped by endpoint.
    /// </summary>
    public List<EndpointMetrics> ByEndpoint { get; init; } = new();

    /// <summary>
    /// Request count by HTTP status code.
    /// </summary>
    public Dictionary<int, int> ByStatusCode { get; init; } = new();

    /// <summary>
    /// Request count by HTTP method.
    /// </summary>
    public Dictionary<string, int> ByMethod { get; init; } = new();

    /// <summary>
    /// When metrics were last updated.
    /// </summary>
    public DateTime LastUpdated { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Metrics for a specific endpoint.
/// </summary>
public record EndpointMetrics
{
    /// <summary>
    /// Endpoint path pattern (e.g., "/api/sales").
    /// </summary>
    public string Endpoint { get; init; } = string.Empty;

    /// <summary>
    /// HTTP method.
    /// </summary>
    public string Method { get; init; } = string.Empty;

    /// <summary>
    /// Total number of requests.
    /// </summary>
    public int RequestCount { get; init; }

    /// <summary>
    /// Average response time in milliseconds.
    /// </summary>
    public double AvgResponseTimeMs { get; init; }

    /// <summary>
    /// Maximum response time in milliseconds.
    /// </summary>
    public long MaxResponseTimeMs { get; init; }

    /// <summary>
    /// Number of errors for this endpoint.
    /// </summary>
    public int ErrorCount { get; init; }

    /// <summary>
    /// Error rate as a percentage.
    /// </summary>
    public double ErrorRatePercent => RequestCount > 0 ? (double)ErrorCount / RequestCount * 100 : 0;

    /// <summary>
    /// Last request timestamp.
    /// </summary>
    public DateTime LastRequestTime { get; init; }
}

/// <summary>
/// Individual request record for metrics collection.
/// </summary>
public record RequestRecord
{
    public string Endpoint { get; init; } = string.Empty;
    public string Method { get; init; } = string.Empty;
    public int StatusCode { get; init; }
    public long DurationMs { get; init; }
    public string? UserId { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public bool IsError => StatusCode >= 400;
    public bool IsClientError => StatusCode >= 400 && StatusCode < 500;
    public bool IsServerError => StatusCode >= 500;
}
