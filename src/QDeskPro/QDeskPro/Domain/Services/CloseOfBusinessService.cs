using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QDeskPro.Data;
using QDeskPro.Features.Reports.Services;

namespace QDeskPro.Domain.Services;

/// <summary>
/// Background service that runs once daily before midnight to calculate and save
/// closing balances for all quarries. This ensures opening balances are ready
/// for the following day.
/// </summary>
public class CloseOfBusinessService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CloseOfBusinessService> _logger;
    private readonly TimeSpan _runTime = new(23, 55, 0); // 11:55 PM

    public CloseOfBusinessService(
        IServiceScopeFactory scopeFactory,
        ILogger<CloseOfBusinessService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CloseOfBusinessService started. Scheduled to run daily at {Time}", _runTime);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.Now;
                var nextRun = GetNextRunTime(now);
                var delay = nextRun - now;

                _logger.LogDebug("Next COB run scheduled for {NextRun} (in {Delay})", nextRun, delay);

                await Task.Delay(delay, stoppingToken);

                if (!stoppingToken.IsCancellationRequested)
                {
                    await RunCloseOfBusinessAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in CloseOfBusinessService. Will retry at next scheduled time.");
                // Wait 1 minute before checking again to avoid tight loop on persistent errors
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        _logger.LogInformation("CloseOfBusinessService stopped.");
    }

    private DateTime GetNextRunTime(DateTime now)
    {
        var todayRun = now.Date.Add(_runTime);

        // If we haven't passed today's run time, run today
        if (now < todayRun)
        {
            return todayRun;
        }

        // Otherwise, run tomorrow
        return todayRun.AddDays(1);
    }

    private async Task RunCloseOfBusinessAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Close of Business process for {Date}", DateTime.Today.ToString("yyyy-MM-dd"));

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var reportService = scope.ServiceProvider.GetRequiredService<ReportService>();

        try
        {
            // Get all active quarries
            var quarries = await context.Quarries
                .Where(q => q.IsActive)
                .Select(q => new { q.Id, q.QuarryName })
                .ToListAsync(stoppingToken);

            _logger.LogInformation("Processing COB for {Count} quarries", quarries.Count);

            var successCount = 0;
            var errorCount = 0;

            foreach (var quarry in quarries)
            {
                if (stoppingToken.IsCancellationRequested) break;

                try
                {
                    // Call the existing ReportService method to generate report and save closing balance
                    // Using null for userId to process entire quarry (all clerks combined)
                    // This will automatically save the closing balance to DailyNote
                    await reportService.GenerateReportAsync(quarry.Id, DateTime.Today, DateTime.Today, null);

                    successCount++;
                    _logger.LogDebug("COB completed for quarry: {QuarryName}", quarry.QuarryName);
                }
                catch (Exception ex)
                {
                    errorCount++;
                    _logger.LogWarning(ex, "COB failed for quarry: {QuarryName} ({QuarryId})", quarry.QuarryName, quarry.Id);
                }
            }

            _logger.LogInformation(
                "Close of Business completed. Success: {Success}, Errors: {Errors}",
                successCount, errorCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run Close of Business process");
            throw;
        }
    }
}
