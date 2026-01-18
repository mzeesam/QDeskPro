using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace QDeskPro.Shared.HealthChecks;

/// <summary>
/// Health check for memory usage.
/// Monitors working set memory of the application process.
/// </summary>
public class MemoryHealthCheck : IHealthCheck
{
    private readonly ILogger<MemoryHealthCheck> _logger;

    // Warning at 1GB, critical at 2GB (adjust based on your deployment)
    private const long WarningThresholdBytes = 1L * 1024 * 1024 * 1024;  // 1 GB
    private const long CriticalThresholdBytes = 2L * 1024 * 1024 * 1024; // 2 GB

    public MemoryHealthCheck(ILogger<MemoryHealthCheck> logger)
    {
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var workingSet = process.WorkingSet64;
            var privateMemory = process.PrivateMemorySize64;
            var gcMemory = GC.GetTotalMemory(false);
            var gen0Collections = GC.CollectionCount(0);
            var gen1Collections = GC.CollectionCount(1);
            var gen2Collections = GC.CollectionCount(2);

            var data = new Dictionary<string, object>
            {
                { "WorkingSetMB", Math.Round(workingSet / 1024.0 / 1024.0, 2) },
                { "PrivateMemoryMB", Math.Round(privateMemory / 1024.0 / 1024.0, 2) },
                { "GCMemoryMB", Math.Round(gcMemory / 1024.0 / 1024.0, 2) },
                { "Gen0Collections", gen0Collections },
                { "Gen1Collections", gen1Collections },
                { "Gen2Collections", gen2Collections },
                { "ProcessUptime", (DateTime.Now - process.StartTime).ToString(@"d\.hh\:mm\:ss") }
            };

            if (workingSet >= CriticalThresholdBytes)
            {
                _logger.LogError("Memory usage critical: {WorkingSetMB}MB working set",
                    Math.Round(workingSet / 1024.0 / 1024.0, 0));
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"Memory critical: {workingSet / 1024.0 / 1024.0:F0}MB working set",
                    data: data));
            }

            if (workingSet >= WarningThresholdBytes)
            {
                _logger.LogWarning("Memory usage elevated: {WorkingSetMB}MB working set",
                    Math.Round(workingSet / 1024.0 / 1024.0, 0));
                return Task.FromResult(HealthCheckResult.Degraded(
                    $"Memory elevated: {workingSet / 1024.0 / 1024.0:F0}MB working set",
                    data: data));
            }

            _logger.LogDebug("Memory health check passed: {WorkingSetMB}MB",
                Math.Round(workingSet / 1024.0 / 1024.0, 0));
            return Task.FromResult(HealthCheckResult.Healthy(
                $"Memory healthy: {workingSet / 1024.0 / 1024.0:F0}MB working set",
                data: data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Memory health check failed with exception");
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Memory health check failed",
                exception: ex));
        }
    }
}
