using System.Net;
using System.Text.Json;

namespace IntelliFin.ClientManagement.Middleware;

/// <summary>
/// Global exception handler middleware for catching and formatting unhandled exceptions
/// </summary>
public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next, 
        ILogger<GlobalExceptionHandlerMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = CorrelationIdMiddleware.GetCorrelationId(context) ?? Guid.NewGuid().ToString();
        var userId = context.User?.Identity?.Name ?? "anonymous";
        var requestPath = context.Request.Path;

        // Log the exception with context
        _logger.LogError(exception,
            "Unhandled exception occurred. CorrelationId: {CorrelationId}, User: {UserId}, Path: {Path}",
            correlationId, userId, requestPath);

        // Determine status code
        var statusCode = exception switch
        {
            ArgumentException => HttpStatusCode.BadRequest,
            UnauthorizedAccessException => HttpStatusCode.Unauthorized,
            KeyNotFoundException => HttpStatusCode.NotFound,
            _ => HttpStatusCode.InternalServerError
        };

        // Build error response
        var response = new ErrorResponse
        {
            Error = _environment.IsDevelopment() 
                ? exception.Message 
                : "An error occurred processing your request.",
            CorrelationId = correlationId,
            Timestamp = DateTime.UtcNow,
            Path = requestPath
        };

        // Include stack trace in development
        if (_environment.IsDevelopment())
        {
            response.Details = exception.ToString();
        }

        // Set response properties
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        // Write response
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = _environment.IsDevelopment()
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, jsonOptions));
    }

    /// <summary>
    /// Error response model
    /// </summary>
    private class ErrorResponse
    {
        public string Error { get; set; } = string.Empty;
        public string CorrelationId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Path { get; set; } = string.Empty;
        public string? Details { get; set; }
    }
}

/// <summary>
/// Extension methods for GlobalExceptionHandlerMiddleware
/// </summary>
public static class GlobalExceptionHandlerMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GlobalExceptionHandlerMiddleware>();
    }
}
