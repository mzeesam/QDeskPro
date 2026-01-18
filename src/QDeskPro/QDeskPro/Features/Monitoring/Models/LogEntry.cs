namespace QDeskPro.Features.Monitoring.Models;

/// <summary>
/// Represents a log entry for the observability dashboard.
/// </summary>
public record LogEntry
{
    /// <summary>
    /// Unique identifier for the log entry.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Timestamp when the log was created.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Log level (Verbose, Debug, Information, Warning, Error, Fatal).
    /// </summary>
    public string Level { get; init; } = string.Empty;

    /// <summary>
    /// The rendered log message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Exception details if present.
    /// </summary>
    public string? Exception { get; init; }

    /// <summary>
    /// Source context (logger name/category).
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// User ID if available from log context.
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// Request ID for correlation.
    /// </summary>
    public string? RequestId { get; init; }

    /// <summary>
    /// Additional properties from the log event.
    /// </summary>
    public Dictionary<string, object?>? Properties { get; init; }

    /// <summary>
    /// Gets the CSS class for styling based on log level.
    /// </summary>
    public string LevelCssClass => Level switch
    {
        "Verbose" or "VRB" => "log-verbose",
        "Debug" or "DBG" => "log-debug",
        "Information" or "INF" => "log-info",
        "Warning" or "WRN" => "log-warning",
        "Error" or "ERR" => "log-error",
        "Fatal" or "FTL" => "log-fatal",
        _ => "log-default"
    };

    /// <summary>
    /// Gets the MudBlazor color for the log level.
    /// </summary>
    public string LevelColor => Level switch
    {
        "Verbose" or "VRB" => "Grey",
        "Debug" or "DBG" => "Default",
        "Information" or "INF" => "Info",
        "Warning" or "WRN" => "Warning",
        "Error" or "ERR" => "Error",
        "Fatal" or "FTL" => "Error",
        _ => "Default"
    };

    /// <summary>
    /// Gets the icon for the log level.
    /// </summary>
    public string LevelIcon => Level switch
    {
        "Verbose" or "VRB" => "Icons.Material.Filled.Code",
        "Debug" or "DBG" => "Icons.Material.Filled.BugReport",
        "Information" or "INF" => "Icons.Material.Filled.Info",
        "Warning" or "WRN" => "Icons.Material.Filled.Warning",
        "Error" or "ERR" => "Icons.Material.Filled.Error",
        "Fatal" or "FTL" => "Icons.Material.Filled.Dangerous",
        _ => "Icons.Material.Filled.Circle"
    };

    /// <summary>
    /// Indicates if this is an error or fatal level log.
    /// </summary>
    public bool IsError => Level is "Error" or "ERR" or "Fatal" or "FTL";

    /// <summary>
    /// Indicates if this is a warning level log.
    /// </summary>
    public bool IsWarning => Level is "Warning" or "WRN";
}
