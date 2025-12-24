using System.Diagnostics;
using System.Text;

namespace QDeskPro.Shared.Middleware;

/// <summary>
/// Middleware for logging HTTP requests and responses
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
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

        // Log request
        _logger.LogInformation(
            "HTTP {Method} {Path} started. RequestId: {RequestId}, UserId: {UserId}, QueryString: {QueryString}",
            context.Request.Method,
            context.Request.Path,
            requestId,
            context.User?.Identity?.Name ?? "Anonymous",
            context.Request.QueryString.HasValue ? context.Request.QueryString.Value : "none");

        // Capture response
        var originalBodyStream = context.Response.Body;

        try
        {
            await _next(context);

            stopwatch.Stop();

            // Log response
            var logLevel = context.Response.StatusCode >= 400 ? LogLevel.Warning : LogLevel.Information;

            _logger.Log(logLevel,
                "HTTP {Method} {Path} completed with {StatusCode} in {ElapsedMs}ms. RequestId: {RequestId}",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                requestId);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex,
                "HTTP {Method} {Path} failed after {ElapsedMs}ms. RequestId: {RequestId}",
                context.Request.Method,
                context.Request.Path,
                stopwatch.ElapsedMilliseconds,
                requestId);

            throw;
        }
        finally
        {
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
