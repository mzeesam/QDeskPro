using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using QDeskPro.Features.Monitoring.Models;
using QDeskPro.Shared.Serilog;

namespace QDeskPro.Features.Monitoring.Hubs;

/// <summary>
/// SignalR hub for real-time log streaming to admin dashboard.
/// </summary>
[Authorize(Policy = "RequireAdministrator")]
public class LogStreamHub : Hub
{
    private readonly InMemorySink _logSink;
    private readonly ILogger<LogStreamHub> _logger;

    public LogStreamHub(InMemorySink logSink, ILogger<LogStreamHub> logger)
    {
        _logSink = logSink;
        _logger = logger;
    }

    /// <summary>
    /// Called when a client connects to the hub.
    /// Sends recent logs to the newly connected client.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogDebug("Admin client connected to LogStreamHub: {ConnectionId}", Context.ConnectionId);

        // Send recent logs to the connected client
        var recentLogs = _logSink.GetRecentLogs(50).ToList();
        await Clients.Caller.SendAsync("ReceiveRecentLogs", recentLogs);

        // Send current statistics
        var stats = _logSink.GetStatistics();
        await Clients.Caller.SendAsync("ReceiveStatistics", stats);

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub.
    /// </summary>
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("Admin client disconnected from LogStreamHub: {ConnectionId}, Exception: {Exception}",
            Context.ConnectionId, exception?.Message);
        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Gets recent logs with optional filtering.
    /// </summary>
    public async Task GetRecentLogs(int count = 100)
    {
        var logs = _logSink.GetRecentLogs(count).ToList();
        await Clients.Caller.SendAsync("ReceiveRecentLogs", logs);
    }

    /// <summary>
    /// Searches logs with the specified filters.
    /// </summary>
    public async Task SearchLogs(string? searchText, string? level, string? source,
        DateTime? from, DateTime? to, int maxResults = 500)
    {
        var logs = _logSink.SearchLogs(searchText, level, source, from, to, maxResults).ToList();
        await Clients.Caller.SendAsync("ReceiveSearchResults", logs);
    }

    /// <summary>
    /// Gets current log statistics.
    /// </summary>
    public async Task GetStatistics()
    {
        var stats = _logSink.GetStatistics();
        await Clients.Caller.SendAsync("ReceiveStatistics", stats);
    }

    /// <summary>
    /// Sets log level filter for this connection.
    /// Stored in connection context for use by LogStreamingService.
    /// </summary>
    public Task SetFilter(string? level, string? source)
    {
        Context.Items["LevelFilter"] = level;
        Context.Items["SourceFilter"] = source;
        _logger.LogDebug("Filter set for {ConnectionId}: Level={Level}, Source={Source}",
            Context.ConnectionId, level, source);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Clears the filter for this connection.
    /// </summary>
    public Task ClearFilter()
    {
        Context.Items.Remove("LevelFilter");
        Context.Items.Remove("SourceFilter");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Pauses log streaming for this connection.
    /// </summary>
    public Task Pause()
    {
        Context.Items["Paused"] = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Resumes log streaming for this connection.
    /// </summary>
    public Task Resume()
    {
        Context.Items.Remove("Paused");
        return Task.CompletedTask;
    }
}
