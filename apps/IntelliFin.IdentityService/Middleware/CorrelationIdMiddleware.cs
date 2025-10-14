using Serilog.Context;

namespace IntelliFin.IdentityService.Middleware;

public class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-ID";
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            context.Response.Headers[CorrelationIdHeader] = correlationId;
            await _next(context);
        }
    }

    private string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var headerValue)
            && !string.IsNullOrWhiteSpace(headerValue))
        {
            var correlationId = headerValue.ToString();
            _logger.LogDebug("Using incoming correlation ID {CorrelationId}", correlationId);
            return correlationId;
        }

        var generated = Guid.NewGuid().ToString();
        _logger.LogDebug("Generated new correlation ID {CorrelationId}", generated);
        return generated;
    }
}
