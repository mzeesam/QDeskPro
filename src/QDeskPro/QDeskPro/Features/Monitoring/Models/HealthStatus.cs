namespace QDeskPro.Features.Monitoring.Models;

/// <summary>
/// Aggregated health status for the observability dashboard.
/// </summary>
public record HealthStatus
{
    /// <summary>
    /// Overall health status (Healthy, Degraded, Unhealthy).
    /// </summary>
    public string OverallStatus { get; init; } = "Unknown";

    /// <summary>
    /// Individual health check results.
    /// </summary>
    public List<HealthCheckEntry> Checks { get; init; } = new();

    /// <summary>
    /// Timestamp when health was checked.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Application uptime.
    /// </summary>
    public TimeSpan Uptime { get; init; }

    /// <summary>
    /// Formatted uptime string.
    /// </summary>
    public string UptimeFormatted => Uptime.TotalDays >= 1
        ? $"{(int)Uptime.TotalDays}d {Uptime.Hours}h {Uptime.Minutes}m"
        : $"{Uptime.Hours}h {Uptime.Minutes}m {Uptime.Seconds}s";

    /// <summary>
    /// Memory usage in MB.
    /// </summary>
    public double MemoryUsageMB { get; init; }

    /// <summary>
    /// Disk usage percentage.
    /// </summary>
    public double DiskUsagePercent { get; init; }

    /// <summary>
    /// Number of healthy checks.
    /// </summary>
    public int HealthyCount => Checks.Count(c => c.Status == "Healthy");

    /// <summary>
    /// Number of degraded checks.
    /// </summary>
    public int DegradedCount => Checks.Count(c => c.Status == "Degraded");

    /// <summary>
    /// Number of unhealthy checks.
    /// </summary>
    public int UnhealthyCount => Checks.Count(c => c.Status == "Unhealthy");

    /// <summary>
    /// Gets the MudBlazor color for the overall status.
    /// </summary>
    public string StatusColor => OverallStatus switch
    {
        "Healthy" => "Success",
        "Degraded" => "Warning",
        "Unhealthy" => "Error",
        _ => "Default"
    };

    /// <summary>
    /// Gets the icon for the overall status.
    /// </summary>
    public string StatusIcon => OverallStatus switch
    {
        "Healthy" => "Icons.Material.Filled.CheckCircle",
        "Degraded" => "Icons.Material.Filled.Warning",
        "Unhealthy" => "Icons.Material.Filled.Error",
        _ => "Icons.Material.Filled.Help"
    };
}

/// <summary>
/// Individual health check result.
/// </summary>
public record HealthCheckEntry
{
    /// <summary>
    /// Name of the health check.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Display name (formatted).
    /// </summary>
    public string DisplayName => Name switch
    {
        "database" => "Database",
        "disk" => "Disk Space",
        "memory" => "Memory",
        "email" => "Email Service",
        _ => Name.Replace("-", " ").Replace("_", " ")
    };

    /// <summary>
    /// Health status (Healthy, Degraded, Unhealthy).
    /// </summary>
    public string Status { get; init; } = "Unknown";

    /// <summary>
    /// Status description.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Duration of the health check.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Additional data from the health check.
    /// </summary>
    public Dictionary<string, object>? Data { get; init; }

    /// <summary>
    /// Exception message if unhealthy.
    /// </summary>
    public string? Exception { get; init; }

    /// <summary>
    /// Tags associated with the check.
    /// </summary>
    public IEnumerable<string> Tags { get; init; } = Enumerable.Empty<string>();

    /// <summary>
    /// Gets the MudBlazor color for the status.
    /// </summary>
    public string StatusColor => Status switch
    {
        "Healthy" => "Success",
        "Degraded" => "Warning",
        "Unhealthy" => "Error",
        _ => "Default"
    };

    /// <summary>
    /// Gets the icon for the status.
    /// </summary>
    public string StatusIcon => Status switch
    {
        "Healthy" => "Icons.Material.Filled.CheckCircle",
        "Degraded" => "Icons.Material.Filled.Warning",
        "Unhealthy" => "Icons.Material.Filled.Error",
        _ => "Icons.Material.Filled.Help"
    };
}
