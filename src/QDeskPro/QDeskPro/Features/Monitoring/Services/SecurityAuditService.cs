using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using QDeskPro.Features.Monitoring.Models;
using QDeskPro.Shared.Serilog;

namespace QDeskPro.Features.Monitoring.Services;

/// <summary>
/// Service that collects and provides security audit events from logs.
/// </summary>
public partial class SecurityAuditService
{
    private readonly InMemorySink _logSink;
    private readonly ILogger<SecurityAuditService> _logger;
    private readonly ConcurrentQueue<SecurityEvent> _securityEvents = new();
    private const int MaxEventBuffer = 500;

    // Regex patterns for parsing security events
    [GeneratedRegex(@"User\s+(\S+)\s+signed\s+in", RegexOptions.IgnoreCase)]
    private static partial Regex SignedInPattern();

    [GeneratedRegex(@"User\s+(\S+)\s+signed\s+out", RegexOptions.IgnoreCase)]
    private static partial Regex SignedOutPattern();

    [GeneratedRegex(@"from\s+([\d\.]+|[\da-fA-F:]+)", RegexOptions.IgnoreCase)]
    private static partial Regex IpAddressPattern();

    [GeneratedRegex(@"failed\s+login|login\s+failed|authentication\s+failed", RegexOptions.IgnoreCase)]
    private static partial Regex FailedLoginPattern();

    [GeneratedRegex(@"rate\s+limit", RegexOptions.IgnoreCase)]
    private static partial Regex RateLimitPattern();

    [GeneratedRegex(@"password", RegexOptions.IgnoreCase)]
    private static partial Regex PasswordPattern();

    [GeneratedRegex(@"access\s+denied|permission\s+denied|unauthorized", RegexOptions.IgnoreCase)]
    private static partial Regex AccessDeniedPattern();

    public SecurityAuditService(InMemorySink logSink, ILogger<SecurityAuditService> logger)
    {
        _logSink = logSink;
        _logger = logger;

        // Subscribe to log events for security filtering
        _logSink.LogReceived += OnLogReceived;
    }

    private void OnLogReceived(LogEntry entry)
    {
        // Check if this is a security-related log entry
        var securityEvent = TryParseSecurityEvent(entry);
        if (securityEvent != null)
        {
            _securityEvents.Enqueue(securityEvent);

            // Trim buffer if exceeded
            while (_securityEvents.Count > MaxEventBuffer && _securityEvents.TryDequeue(out _)) { }
        }
    }

    private SecurityEvent? TryParseSecurityEvent(LogEntry entry)
    {
        var message = entry.Message;
        if (string.IsNullOrEmpty(message)) return null;

        // Extract IP address if present
        var ipMatch = IpAddressPattern().Match(message);
        var ipAddress = ipMatch.Success ? ipMatch.Groups[1].Value : null;

        // Check for sign in
        var signInMatch = SignedInPattern().Match(message);
        if (signInMatch.Success)
        {
            return new SecurityEvent
            {
                Timestamp = entry.Timestamp,
                EventType = "Login",
                UserEmail = signInMatch.Groups[1].Value,
                UserId = entry.UserId,
                IpAddress = ipAddress,
                IsSuccess = true,
                Message = message,
                Level = entry.Level
            };
        }

        // Check for sign out
        var signOutMatch = SignedOutPattern().Match(message);
        if (signOutMatch.Success)
        {
            return new SecurityEvent
            {
                Timestamp = entry.Timestamp,
                EventType = "Logout",
                UserEmail = signOutMatch.Groups[1].Value,
                UserId = entry.UserId,
                IpAddress = ipAddress,
                IsSuccess = true,
                Message = message,
                Level = entry.Level
            };
        }

        // Check for failed login
        if (FailedLoginPattern().IsMatch(message))
        {
            return new SecurityEvent
            {
                Timestamp = entry.Timestamp,
                EventType = "FailedLogin",
                UserId = entry.UserId,
                IpAddress = ipAddress,
                IsSuccess = false,
                Message = message,
                Level = entry.Level,
                Details = ExtractDetails(message)
            };
        }

        // Check for rate limiting
        if (RateLimitPattern().IsMatch(message))
        {
            return new SecurityEvent
            {
                Timestamp = entry.Timestamp,
                EventType = "RateLimited",
                UserId = entry.UserId,
                IpAddress = ipAddress,
                IsSuccess = false,
                Message = message,
                Level = entry.Level
            };
        }

        // Check for password events
        if (PasswordPattern().IsMatch(message))
        {
            var isReset = message.Contains("reset", StringComparison.OrdinalIgnoreCase);
            return new SecurityEvent
            {
                Timestamp = entry.Timestamp,
                EventType = isReset ? "PasswordReset" : "PasswordChange",
                UserId = entry.UserId,
                IpAddress = ipAddress,
                IsSuccess = !entry.IsError,
                Message = message,
                Level = entry.Level
            };
        }

        // Check for access denied
        if (AccessDeniedPattern().IsMatch(message))
        {
            return new SecurityEvent
            {
                Timestamp = entry.Timestamp,
                EventType = "PermissionDenied",
                UserId = entry.UserId,
                IpAddress = ipAddress,
                IsSuccess = false,
                Message = message,
                Level = entry.Level
            };
        }

        // Check for JWT/token events
        if (message.Contains("JWT", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("token", StringComparison.OrdinalIgnoreCase))
        {
            var isValidated = message.Contains("validated", StringComparison.OrdinalIgnoreCase);
            var isFailed = message.Contains("failed", StringComparison.OrdinalIgnoreCase);

            if (isValidated || isFailed)
            {
                return new SecurityEvent
                {
                    Timestamp = entry.Timestamp,
                    EventType = isFailed ? "TokenFailed" : "TokenValidated",
                    UserId = entry.UserId,
                    IpAddress = ipAddress,
                    IsSuccess = !isFailed,
                    Message = message,
                    Level = entry.Level
                };
            }
        }

        return null;
    }

    private static string? ExtractDetails(string message)
    {
        // Try to extract email from message
        var emailMatch = Regex.Match(message, @"[\w\.-]+@[\w\.-]+\.\w+");
        return emailMatch.Success ? $"Email: {emailMatch.Value}" : null;
    }

    /// <summary>
    /// Gets recent security events.
    /// </summary>
    public List<SecurityEvent> GetRecentEvents(int count = 50)
    {
        return _securityEvents
            .TakeLast(count)
            .Reverse()
            .ToList();
    }

    /// <summary>
    /// Gets security events with optional filtering.
    /// </summary>
    public List<SecurityEvent> GetEvents(
        string? eventType = null,
        bool? successOnly = null,
        string? userId = null,
        DateTime? from = null,
        DateTime? to = null,
        int maxResults = 100)
    {
        var query = _securityEvents.AsEnumerable();

        if (!string.IsNullOrEmpty(eventType) && eventType != "All")
        {
            query = query.Where(e => e.EventType.Equals(eventType, StringComparison.OrdinalIgnoreCase));
        }

        if (successOnly.HasValue)
        {
            query = query.Where(e => e.IsSuccess == successOnly.Value);
        }

        if (!string.IsNullOrEmpty(userId))
        {
            query = query.Where(e =>
                (e.UserId?.Contains(userId, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (e.UserEmail?.Contains(userId, StringComparison.OrdinalIgnoreCase) ?? false));
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
            .Reverse()
            .ToList();
    }

    /// <summary>
    /// Gets a summary of security events.
    /// </summary>
    public SecurityEventSummary GetSummary(int hoursBack = 24)
    {
        var cutoff = DateTime.UtcNow.AddHours(-hoursBack);
        var events = _securityEvents.Where(e => e.Timestamp >= cutoff).ToList();

        return new SecurityEventSummary
        {
            TotalEvents = events.Count,
            SuccessfulLogins = events.Count(e => e.EventType == "Login" && e.IsSuccess),
            FailedLogins = events.Count(e => e.EventType == "FailedLogin"),
            Logouts = events.Count(e => e.EventType == "Logout"),
            PermissionDenied = events.Count(e => e.EventType == "PermissionDenied"),
            RateLimited = events.Count(e => e.EventType == "RateLimited"),
            UniqueUsers = events
                .Where(e => !string.IsNullOrEmpty(e.UserId) || !string.IsNullOrEmpty(e.UserEmail))
                .Select(e => e.UserId ?? e.UserEmail)
                .Distinct()
                .Count(),
            UniqueIPs = events
                .Where(e => !string.IsNullOrEmpty(e.IpAddress))
                .Select(e => e.IpAddress)
                .Distinct()
                .Count(),
            ByEventType = events
                .GroupBy(e => e.EventType)
                .ToDictionary(g => g.Key, g => g.Count()),
            GeneratedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Gets failed login attempts grouped by IP address.
    /// </summary>
    public List<(string IpAddress, int Count, DateTime LastAttempt)> GetFailedLoginsByIP(int hoursBack = 24)
    {
        var cutoff = DateTime.UtcNow.AddHours(-hoursBack);
        return _securityEvents
            .Where(e => e.EventType == "FailedLogin" && e.Timestamp >= cutoff && !string.IsNullOrEmpty(e.IpAddress))
            .GroupBy(e => e.IpAddress!)
            .Select(g => (g.Key, g.Count(), g.Max(e => e.Timestamp)))
            .OrderByDescending(x => x.Item2)
            .Take(20)
            .ToList();
    }

    /// <summary>
    /// Gets failed login attempts grouped by user.
    /// </summary>
    public List<(string User, int Count, DateTime LastAttempt)> GetFailedLoginsByUser(int hoursBack = 24)
    {
        var cutoff = DateTime.UtcNow.AddHours(-hoursBack);
        return _securityEvents
            .Where(e => e.EventType == "FailedLogin" && e.Timestamp >= cutoff)
            .Where(e => !string.IsNullOrEmpty(e.UserId) || !string.IsNullOrEmpty(e.UserEmail))
            .GroupBy(e => e.UserEmail ?? e.UserId ?? "Unknown")
            .Select(g => (g.Key, g.Count(), g.Max(e => e.Timestamp)))
            .OrderByDescending(x => x.Item2)
            .Take(20)
            .ToList();
    }

    /// <summary>
    /// Clears the security event buffer.
    /// </summary>
    public void Clear()
    {
        while (_securityEvents.TryDequeue(out _)) { }
    }

    /// <summary>
    /// Gets available event types for filtering.
    /// </summary>
    public static IEnumerable<string> GetEventTypes()
    {
        return new[]
        {
            "All",
            "Login",
            "Logout",
            "FailedLogin",
            "PasswordReset",
            "PasswordChange",
            "PermissionDenied",
            "RateLimited",
            "TokenValidated",
            "TokenFailed"
        };
    }
}
