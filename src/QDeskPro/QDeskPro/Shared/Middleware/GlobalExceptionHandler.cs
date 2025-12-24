using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;

namespace QDeskPro.Shared.Middleware;

/// <summary>
/// Global exception handler that provides consistent error responses
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var correlationId = Guid.NewGuid().ToString();

        // Log the exception with structured logging
        _logger.LogError(exception,
            "Unhandled exception occurred. CorrelationId: {CorrelationId}, Path: {Path}, Method: {Method}, UserId: {UserId}",
            correlationId,
            httpContext.Request.Path,
            httpContext.Request.Method,
            httpContext.User?.Identity?.Name ?? "Anonymous");

        // Determine status code and error message
        var (statusCode, message) = exception switch
        {
            ArgumentNullException => (HttpStatusCode.BadRequest, "A required value was not provided."),
            ArgumentException => (HttpStatusCode.BadRequest, exception.Message),
            InvalidOperationException => (HttpStatusCode.UnprocessableEntity, exception.Message),
            UnauthorizedAccessException => (HttpStatusCode.Forbidden, "You do not have permission to perform this action."),
            KeyNotFoundException => (HttpStatusCode.NotFound, "The requested resource was not found."),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred. Please try again later.")
        };

        var problemDetails = new ProblemDetailsResponse
        {
            Status = (int)statusCode,
            Title = GetStatusCodeTitle(statusCode),
            Detail = _environment.IsDevelopment() ? exception.Message : message,
            Instance = httpContext.Request.Path,
            CorrelationId = correlationId,
            Timestamp = DateTime.UtcNow
        };

        // Include stack trace in development
        if (_environment.IsDevelopment())
        {
            problemDetails.StackTrace = exception.StackTrace;
        }

        httpContext.Response.StatusCode = (int)statusCode;
        httpContext.Response.ContentType = "application/json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true; // Exception handled
    }

    private static string GetStatusCodeTitle(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.BadRequest => "Bad Request",
        HttpStatusCode.Unauthorized => "Unauthorized",
        HttpStatusCode.Forbidden => "Forbidden",
        HttpStatusCode.NotFound => "Not Found",
        HttpStatusCode.UnprocessableEntity => "Unprocessable Entity",
        HttpStatusCode.InternalServerError => "Internal Server Error",
        _ => "Error"
    };
}

/// <summary>
/// Problem details response following RFC 7807
/// </summary>
public class ProblemDetailsResponse
{
    public int Status { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string Instance { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? StackTrace { get; set; }
}
