using Microsoft.Extensions.Diagnostics.HealthChecks;
using QDeskPro.Data;

namespace QDeskPro.Shared.HealthChecks;

/// <summary>
/// Health check for database connectivity
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly AppDbContext _context;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(AppDbContext context, ILogger<DatabaseHealthCheck> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to execute a simple query to verify database connection
            var canConnect = await _context.Database.CanConnectAsync(cancellationToken);

            if (canConnect)
            {
                _logger.LogDebug("Database health check passed");
                return HealthCheckResult.Healthy("Database is accessible");
            }

            _logger.LogWarning("Database health check failed - cannot connect");
            return HealthCheckResult.Unhealthy("Cannot connect to database");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed with exception");
            return HealthCheckResult.Unhealthy(
                "Database health check failed",
                exception: ex);
        }
    }
}
