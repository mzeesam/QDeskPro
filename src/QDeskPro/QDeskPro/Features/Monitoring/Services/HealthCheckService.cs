using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using QDeskPro.Features.Monitoring.Models;

namespace QDeskPro.Features.Monitoring.Services;

/// <summary>
/// Service that aggregates health check results for the observability dashboard.
/// </summary>
public class MonitoringHealthService
{
    private readonly HealthCheckService _healthCheckService;
    private readonly ILogger<MonitoringHealthService> _logger;
    private static readonly DateTime _startTime = DateTime.UtcNow;

    public MonitoringHealthService(
        HealthCheckService healthCheckService,
        ILogger<MonitoringHealthService> logger)
    {
        _healthCheckService = healthCheckService;
        _logger = logger;
    }

    /// <summary>
    /// Gets detailed health status for the dashboard.
    /// </summary>
    public async Task<Models.HealthStatus> GetDetailedHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var report = await _healthCheckService.CheckHealthAsync(cancellationToken);

            var checks = report.Entries.Select(entry => new HealthCheckEntry
            {
                Name = entry.Key,
                Status = entry.Value.Status.ToString(),
                Description = entry.Value.Description ?? string.Empty,
                Duration = entry.Value.Duration,
                Data = entry.Value.Data?.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value),
                Exception = entry.Value.Exception?.Message,
                Tags = entry.Value.Tags
            }).ToList();

            // Get memory and disk info for summary
            var memoryMB = GetMemoryUsageMB();
            var diskPercent = GetDiskUsagePercent();

            return new Models.HealthStatus
            {
                OverallStatus = report.Status.ToString(),
                Checks = checks,
                Timestamp = DateTime.UtcNow,
                Uptime = DateTime.UtcNow - _startTime,
                MemoryUsageMB = memoryMB,
                DiskUsagePercent = diskPercent
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get health status");
            return new Models.HealthStatus
            {
                OverallStatus = "Unhealthy",
                Checks = new List<Models.HealthCheckEntry>
                {
                    new()
                    {
                        Name = "health-service",
                        Status = "Unhealthy",
                        Description = "Health check service failed",
                        Exception = ex.Message
                    }
                },
                Timestamp = DateTime.UtcNow,
                Uptime = DateTime.UtcNow - _startTime
            };
        }
    }

    /// <summary>
    /// Gets a quick health summary without running all checks.
    /// </summary>
    public HealthSummary GetQuickSummary()
    {
        var process = Process.GetCurrentProcess();
        var uptime = DateTime.UtcNow - _startTime;

        return new HealthSummary
        {
            Uptime = uptime,
            MemoryUsageMB = Math.Round(process.WorkingSet64 / 1024.0 / 1024.0, 0),
            DiskUsagePercent = GetDiskUsagePercent(),
            ProcessorTime = process.TotalProcessorTime,
            ThreadCount = process.Threads.Count,
            HandleCount = process.HandleCount
        };
    }

    /// <summary>
    /// Gets the application start time.
    /// </summary>
    public DateTime GetStartTime() => _startTime;

    /// <summary>
    /// Gets the application uptime.
    /// </summary>
    public TimeSpan GetUptime() => DateTime.UtcNow - _startTime;

    private static double GetMemoryUsageMB()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            return Math.Round(process.WorkingSet64 / 1024.0 / 1024.0, 2);
        }
        catch
        {
            return 0;
        }
    }

    private static double GetDiskUsagePercent()
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(Environment.CurrentDirectory) ?? "C:");
            if (!drive.IsReady) return 0;

            var usedBytes = drive.TotalSize - drive.AvailableFreeSpace;
            return Math.Round((double)usedBytes / drive.TotalSize * 100, 1);
        }
        catch
        {
            return 0;
        }
    }
}

/// <summary>
/// Quick health summary without running health checks.
/// </summary>
public record HealthSummary
{
    public TimeSpan Uptime { get; init; }
    public string UptimeFormatted => Uptime.TotalDays >= 1
        ? $"{(int)Uptime.TotalDays}d {Uptime.Hours}h {Uptime.Minutes}m"
        : $"{Uptime.Hours}h {Uptime.Minutes}m {Uptime.Seconds}s";
    public double MemoryUsageMB { get; init; }
    public double DiskUsagePercent { get; init; }
    public TimeSpan ProcessorTime { get; init; }
    public int ThreadCount { get; init; }
    public int HandleCount { get; init; }
}
