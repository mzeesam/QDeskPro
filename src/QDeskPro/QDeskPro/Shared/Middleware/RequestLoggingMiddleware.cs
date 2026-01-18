using System.Diagnostics;
using System.Text;
using QDeskPro.Features.Monitoring.Services;

namespace QDeskPro.Shared.Middleware;

/// <summary>
/// Middleware for logging HTTP requests and responses.
/// Also records metrics for the observability dashboard.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    private readonly MetricsCollectorService _metricsCollector;

    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger,
        MetricsCollectorService metricsCollector)
    {
        _next = next;
        _logger = logger;
        _metricsCollector = metricsCollector;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only log API requests to avoid cluttering logs with static file requests
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var requestId = Guid.NewGuid().ToString();
        var userId = context.User?.Identity?.Name;
        var path = context.Request.Path.Value ?? string.Empty;
        var method = context.Request.Method;

        // Log request
        _logger.LogInformation(
            "HTTP {Method} {Path} started. RequestId: {RequestId}, UserId: {UserId}, QueryString: {QueryString}",
            method,
            path,
            requestId,
            userId ?? "Anonymous",
            context.Request.QueryString.HasValue ? context.Request.QueryString.Value : "none");

        // Capture response
        var originalBodyStream = context.Response.Body;
        var statusCode = 500; // Default to error if not set

        try
        {
            await _next(context);

            stopwatch.Stop();
            statusCode = context.Response.StatusCode;

            // Log response
            var logLevel = statusCode >= 400 ? LogLevel.Warning : LogLevel.Information;

            _logger.Log(logLevel,
                "HTTP {Method} {Path} completed with {StatusCode} in {ElapsedMs}ms. RequestId: {RequestId}",
                method,
                path,
                statusCode,
                stopwatch.ElapsedMilliseconds,
                requestId);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            statusCode = 500;

            _logger.LogError(ex,
                "HTTP {Method} {Path} failed after {ElapsedMs}ms. RequestId: {RequestId}",
                method,
                path,
                stopwatch.ElapsedMilliseconds,
                requestId);

            throw;
        }
        finally
        {
            // Record metrics for observability dashboard
            _metricsCollector.RecordRequest(path, method, statusCode, stopwatch.ElapsedMilliseconds, userId);

            context.Response.Body = originalBodyStream;
        }
    }
}

/// <summary>
/// Extension method for adding RequestLoggingMiddleware to the pipeline
/// </summary>
public static class RequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestLoggingMiddleware>();
    }
}
