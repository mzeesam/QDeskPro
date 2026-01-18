namespace QDeskPro.Features.Monitoring.Models;

/// <summary>
/// Represents a security event for the observability dashboard.
/// </summary>
public record SecurityEvent
{
    /// <summary>
    /// Unique identifier for the event.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// When the event occurred.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Type of security event.
    /// </summary>
    public string EventType { get; init; } = string.Empty;

    /// <summary>
    /// User ID if available.
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// User email if available.
    /// </summary>
    public string? UserEmail { get; init; }

    /// <summary>
    /// IP address of the request.
    /// </summary>
    public string? IpAddress { get; init; }

    /// <summary>
    /// Additional details about the event.
    /// </summary>
    public string? Details { get; init; }

    /// <summary>
    /// Whether the action was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// The original log message.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Log level of the event.
    /// </summary>
    public string Level { get; init; } = "Information";

    /// <summary>
    /// Gets the display name for the event type.
    /// </summary>
    public string EventTypeDisplayName => EventType switch
    {
        "Login" => "User Login",
        "Logout" => "User Logout",
        "FailedLogin" => "Failed Login Attempt",
        "PasswordReset" => "Password Reset",
        "PasswordChange" => "Password Changed",
        "AccountLocked" => "Account Locked",
        "PermissionDenied" => "Permission Denied",
        "RateLimited" => "Rate Limit Exceeded",
        "TokenValidated" => "Token Validated",
        "TokenFailed" => "Token Validation Failed",
        _ => EventType
    };

    /// <summary>
    /// Gets the icon for the event type.
    /// </summary>
    public string EventIcon => EventType switch
    {
        "Login" => "Icons.Material.Filled.Login",
        "Logout" => "Icons.Material.Filled.Logout",
        "FailedLogin" => "Icons.Material.Filled.ErrorOutline",
        "PasswordReset" => "Icons.Material.Filled.LockReset",
        "PasswordChange" => "Icons.Material.Filled.Password",
        "AccountLocked" => "Icons.Material.Filled.Lock",
        "PermissionDenied" => "Icons.Material.Filled.Block",
        "RateLimited" => "Icons.Material.Filled.Speed",
        "TokenValidated" => "Icons.Material.Filled.VerifiedUser",
        "TokenFailed" => "Icons.Material.Filled.GppBad",
        _ => "Icons.Material.Filled.Security"
    };

    /// <summary>
    /// Gets the color for the event based on success/failure.
    /// </summary>
    public string EventColor => IsSuccess ? "Success" : "Error";

    /// <summary>
    /// Indicates if this is a critical security event.
    /// </summary>
    public bool IsCritical => EventType is "FailedLogin" or "AccountLocked" or "PermissionDenied" or "TokenFailed";
}

/// <summary>
/// Summary of security events.
/// </summary>
public record SecurityEventSummary
{
    /// <summary>
    /// Total events in the period.
    /// </summary>
    public int TotalEvents { get; init; }

    /// <summary>
    /// Number of successful logins.
    /// </summary>
    public int SuccessfulLogins { get; init; }

    /// <summary>
    /// Number of failed login attempts.
    /// </summary>
    public int FailedLogins { get; init; }

    /// <summary>
    /// Number of logouts.
    /// </summary>
    public int Logouts { get; init; }

    /// <summary>
    /// Number of permission denied events.
    /// </summary>
    public int PermissionDenied { get; init; }

    /// <summary>
    /// Number of rate limit events.
    /// </summary>
    public int RateLimited { get; init; }

    /// <summary>
    /// Number of unique users with security events.
    /// </summary>
    public int UniqueUsers { get; init; }

    /// <summary>
    /// Number of unique IP addresses.
    /// </summary>
    public int UniqueIPs { get; init; }

    /// <summary>
    /// Events by type.
    /// </summary>
    public Dictionary<string, int> ByEventType { get; init; } = new();

    /// <summary>
    /// When the summary was generated.
    /// </summary>
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
}
