using Microsoft.AspNetCore.SignalR;
using QDeskPro.Features.Monitoring.Hubs;
using QDeskPro.Features.Monitoring.Models;
using QDeskPro.Shared.Serilog;

namespace QDeskPro.Features.Monitoring.Services;

/// <summary>
/// Background service that broadcasts log entries to connected SignalR clients.
/// </summary>
public class LogStreamingService : IHostedService, IDisposable
{
    private readonly InMemorySink _sink;
    private readonly IHubContext<LogStreamHub> _hubContext;
    private readonly ILogger<LogStreamingService> _logger;
    private readonly Timer _statisticsTimer;

    public LogStreamingService(
        InMemorySink sink,
        IHubContext<LogStreamHub> hubContext,
        ILogger<LogStreamingService> logger)
    {
        _sink = sink;
        _hubContext = hubContext;
        _logger = logger;

        // Timer to periodically broadcast statistics (every 30 seconds)
        _statisticsTimer = new Timer(BroadcastStatistics, null, Timeout.Infinite, Timeout.Infinite);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("LogStreamingService starting");

        // Subscribe to log events
        _sink.LogReceived += OnLogReceived;

        // Start statistics broadcast timer
        _statisticsTimer.Change(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("LogStreamingService stopping");

        // Unsubscribe from log events
        _sink.LogReceived -= OnLogReceived;

        // Stop statistics timer
        _statisticsTimer.Change(Timeout.Infinite, Timeout.Infinite);

        return Task.CompletedTask;
    }

    private async void OnLogReceived(LogEntry entry)
    {
        try
        {
            // Broadcast to all connected clients
            // Clients can filter on their end or use SetFilter to register preferences
            await _hubContext.Clients.All.SendAsync("ReceiveLog", entry);
        }
        catch (Exception ex)
        {
            // Don't log here to avoid infinite recursion
            // Just swallow the exception
            System.Diagnostics.Debug.WriteLine($"LogStreamingService broadcast error: {ex.Message}");
        }
    }

    private async void BroadcastStatistics(object? state)
    {
        try
        {
            var stats = _sink.GetStatistics();
            await _hubContext.Clients.All.SendAsync("ReceiveStatistics", stats);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Statistics broadcast error: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _statisticsTimer?.Dispose();
        GC.SuppressFinalize(this);
    }
}
