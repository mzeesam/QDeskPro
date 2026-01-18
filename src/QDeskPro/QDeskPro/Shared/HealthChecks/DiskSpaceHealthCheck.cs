using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace QDeskPro.Shared.HealthChecks;

/// <summary>
/// Health check for disk space availability.
/// Warns at 80% usage, fails at 95% usage.
/// </summary>
public class DiskSpaceHealthCheck : IHealthCheck
{
    private readonly ILogger<DiskSpaceHealthCheck> _logger;
    private const double WarningThresholdPercent = 80.0;
    private const double CriticalThresholdPercent = 95.0;

    public DiskSpaceHealthCheck(ILogger<DiskSpaceHealthCheck> logger)
    {
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(Environment.CurrentDirectory) ?? "C:");

            if (!drive.IsReady)
            {
                _logger.LogWarning("Disk health check failed - drive not ready");
                return Task.FromResult(HealthCheckResult.Unhealthy("Drive is not ready"));
            }

            var totalBytes = drive.TotalSize;
            var freeBytes = drive.AvailableFreeSpace;
            var usedBytes = totalBytes - freeBytes;
            var usedPercent = (double)usedBytes / totalBytes * 100;

            var data = new Dictionary<string, object>
            {
                { "DriveName", drive.Name },
                { "TotalGB", Math.Round(totalBytes / 1024.0 / 1024.0 / 1024.0, 2) },
                { "FreeGB", Math.Round(freeBytes / 1024.0 / 1024.0 / 1024.0, 2) },
                { "UsedPercent", Math.Round(usedPercent, 1) }
            };

            if (usedPercent >= CriticalThresholdPercent)
            {
                _logger.LogError("Disk space critical: {UsedPercent}% used on {Drive}",
                    Math.Round(usedPercent, 1), drive.Name);
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"Disk space critical: {usedPercent:F1}% used",
                    data: data));
            }

            if (usedPercent >= WarningThresholdPercent)
            {
                _logger.LogWarning("Disk space warning: {UsedPercent}% used on {Drive}",
                    Math.Round(usedPercent, 1), drive.Name);
                return Task.FromResult(HealthCheckResult.Degraded(
                    $"Disk space warning: {usedPercent:F1}% used",
                    data: data));
            }

            _logger.LogDebug("Disk health check passed: {UsedPercent}% used", Math.Round(usedPercent, 1));
            return Task.FromResult(HealthCheckResult.Healthy(
                $"Disk space healthy: {usedPercent:F1}% used",
                data: data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Disk health check failed with exception");
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Disk health check failed",
                exception: ex));
        }
    }
}
