using System.Collections.Concurrent;
using QDeskPro.Features.Monitoring.Models;
using Serilog.Core;
using Serilog.Events;

namespace QDeskPro.Shared.Serilog;

/// <summary>
/// A custom Serilog sink that stores log entries in memory for the observability dashboard.
/// Uses a circular buffer to prevent unbounded memory growth.
/// </summary>
public class InMemorySink : ILogEventSink
{
    private readonly ConcurrentQueue<LogEntry> _logBuffer = new();
    private readonly int _maxBufferSize;
    private int _currentCount;

    /// <summary>
    /// Event raised when a new log entry is received.
    /// Used by SignalR hub to broadcast to connected clients.
    /// </summary>
    public event Action<LogEntry>? LogReceived;

    /// <summary>
    /// Creates a new InMemorySink with the specified buffer size.
    /// </summary>
    /// <param name="maxBufferSize">Maximum number of log entries to retain (default 1000).</param>
    public InMemorySink(int maxBufferSize = 1000)
    {
        _maxBufferSize = maxBufferSize;
    }

    /// <summary>
    /// Emit a log event to the sink.
    /// </summary>
    public void Emit(LogEvent logEvent)
    {
        var entry = MapToLogEntry(logEvent);

        _logBuffer.Enqueue(entry);
        Interlocked.Increment(ref _currentCount);

        // Trim buffer if exceeded max size
        while (_currentCount > _maxBufferSize && _logBuffer.TryDequeue(out _))
        {
            Interlocked.Decrement(ref _currentCount);
        }

        // Notify subscribers
        try
        {
            LogReceived?.Invoke(entry);
        }
        catch
        {
            // Swallow exceptions from event handlers to prevent logging failures
        }
    }

    /// <summary>
    /// Gets recent log entries.
    /// </summary>
    /// <param name="count">Number of entries to retrieve.</param>
    /// <returns>Recent log entries, newest first.</returns>
    public IEnumerable<LogEntry> GetRecentLogs(int count = 100)
    {
        return _logBuffer
            .TakeLast(Math.Min(count, _maxBufferSize))
            .Reverse();
    }

    /// <summary>
    /// Gets all log entries in the buffer.
    /// </summary>
    /// <returns>All log entries, oldest first.</returns>
    public IEnumerable<LogEntry> GetAllLogs()
    {
        return _logBuffer.ToArray();
    }

    /// <summary>
    /// Searches log entries with optional filters.
    /// </summary>
    public IEnumerable<LogEntry> SearchLogs(
        string? searchText = null,
        string? level = null,
        string? source = null,
        DateTime? from = null,
        DateTime? to = null,
        int maxResults = 500)
    {
        var query = _logBuffer.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var search = searchText.ToLowerInvariant();
            query = query.Where(e =>
                e.Message.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (e.Source?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (e.Exception?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (!string.IsNullOrWhiteSpace(level) && level != "All")
        {
            query = query.Where(e => e.Level.Equals(level, StringComparison.OrdinalIgnoreCase) ||
                                     e.Level.StartsWith(level[..3], StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(source))
        {
            query = query.Where(e => e.Source?.Contains(source, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        if (from.HasValue)
        {
            query = query.Where(e => e.Timestamp >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(e => e.Timestamp <= to.Value);
        }

        return query
            .TakeLast(maxResults)
            .Reverse();
    }

    /// <summary>
    /// Gets log statistics for the dashboard.
    /// </summary>
    public LogStatistics GetStatistics()
    {
        var logs = _logBuffer.ToArray();
        var now = DateTime.UtcNow;
        var oneHourAgo = now.AddHours(-1);
        var recentLogs = logs.Where(l => l.Timestamp >= oneHourAgo).ToList();

        return new LogStatistics
        {
            TotalCount = logs.Length,
            ErrorCount = logs.Count(l => l.IsError),
            WarningCount = logs.Count(l => l.IsWarning),
            RecentErrorCount = recentLogs.Count(l => l.IsError),
            RecentWarningCount = recentLogs.Count(l => l.IsWarning),
            LogsPerMinute = recentLogs.Count / 60.0,
            OldestTimestamp = logs.FirstOrDefault()?.Timestamp,
            NewestTimestamp = logs.LastOrDefault()?.Timestamp,
            ByLevel = logs.GroupBy(l => l.Level)
                .ToDictionary(g => g.Key, g => g.Count()),
            TopSources = logs
                .Where(l => !string.IsNullOrEmpty(l.Source))
                .GroupBy(l => l.Source!)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .ToDictionary(g => g.Key, g => g.Count())
        };
    }

    /// <summary>
    /// Clears all log entries from the buffer.
    /// </summary>
    public void Clear()
    {
        while (_logBuffer.TryDequeue(out _))
        {
            Interlocked.Decrement(ref _currentCount);
        }
    }

    /// <summary>
    /// Gets the current buffer count.
    /// </summary>
    public int Count => _currentCount;

    /// <summary>
    /// Gets the maximum buffer size.
    /// </summary>
    public int MaxSize => _maxBufferSize;

    private static LogEntry MapToLogEntry(LogEvent logEvent)
    {
        var properties = new Dictionary<string, object?>();
        string? userId = null;
        string? requestId = null;
        string? source = null;

        foreach (var property in logEvent.Properties)
        {
            var value = RenderPropertyValue(property.Value);

            switch (property.Key)
            {
                case "UserId":
                    userId = value?.ToString();
                    break;
                case "RequestId":
                    requestId = value?.ToString();
                    break;
                case "SourceContext":
                    source = value?.ToString();
                    // Simplify source context (e.g., "QDeskPro.Features.Sales.Services.SaleService" -> "SaleService")
                    if (source?.Contains('.') == true)
                    {
                        source = source.Split('.').LastOrDefault() ?? source;
                    }
                    break;
                default:
                    // Skip some internal properties
                    if (!property.Key.StartsWith("@") && property.Key != "Application")
                    {
                        properties[property.Key] = value;
                    }
                    break;
            }
        }

        return new LogEntry
        {
            Timestamp = logEvent.Timestamp.UtcDateTime,
            Level = logEvent.Level.ToString(),
            Message = logEvent.RenderMessage(),
            Exception = logEvent.Exception?.ToString(),
            Source = source,
            UserId = userId,
            RequestId = requestId,
            Properties = properties.Count > 0 ? properties : null
        };
    }

    private static object? RenderPropertyValue(LogEventPropertyValue value)
    {
        return value switch
        {
            ScalarValue sv => sv.Value,
            SequenceValue seqv => seqv.Elements.Select(RenderPropertyValue).ToList(),
            StructureValue stv => stv.Properties.ToDictionary(p => p.Name, p => RenderPropertyValue(p.Value)),
            DictionaryValue dv => dv.Elements.ToDictionary(
                kvp => RenderPropertyValue(kvp.Key)?.ToString() ?? "",
                kvp => RenderPropertyValue(kvp.Value)),
            _ => value.ToString()
        };
    }
}

/// <summary>
/// Statistics about logged entries.
/// </summary>
public record LogStatistics
{
    public int TotalCount { get; init; }
    public int ErrorCount { get; init; }
    public int WarningCount { get; init; }
    public int RecentErrorCount { get; init; }
    public int RecentWarningCount { get; init; }
    public double LogsPerMinute { get; init; }
    public DateTime? OldestTimestamp { get; init; }
    public DateTime? NewestTimestamp { get; init; }
    public Dictionary<string, int> ByLevel { get; init; } = new();
    public Dictionary<string, int> TopSources { get; init; } = new();
}
