namespace IntelliFin.ClientManagement.Middleware;

/// <summary>
/// Middleware to handle correlation IDs for distributed tracing
/// </summary>
public class CorrelationIdMiddleware
{
    private const string CorrelationIdHeaderName = "X-Correlation-ID";
    private const string CorrelationIdKey = "CorrelationId";
    
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId;

        // Check if correlation ID exists in request headers
        if (context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out var correlationIdHeader) &&
            !string.IsNullOrWhiteSpace(correlationIdHeader.FirstOrDefault()))
        {
            correlationId = correlationIdHeader.First()!;
            _logger.LogDebug("Using correlation ID from request header: {CorrelationId}", correlationId);
        }
        else
        {
            // Generate new correlation ID
            correlationId = Guid.NewGuid().ToString();
            _logger.LogDebug("Generated new correlation ID: {CorrelationId}", correlationId);
        }

        // Store correlation ID in HttpContext.Items for access by other components
        context.Items[CorrelationIdKey] = correlationId;

        // Add correlation ID to response headers
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(CorrelationIdHeaderName))
            {
                context.Response.Headers.Append(CorrelationIdHeaderName, correlationId);
            }
            return Task.CompletedTask;
        });

        // Continue pipeline
        await _next(context);
    }

    /// <summary>
    /// Gets the correlation ID from HttpContext
    /// </summary>
    public static string? GetCorrelationId(HttpContext context)
    {
        return context.Items.TryGetValue(CorrelationIdKey, out var correlationId) 
            ? correlationId?.ToString() 
            : null;
    }
}

/// <summary>
/// Extension methods for CorrelationIdMiddleware
/// </summary>
public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<CorrelationIdMiddleware>();
    }
}
