using Serilog.Core;
using Serilog.Events;

namespace IntelliFin.ClientManagement.Infrastructure.Logging;

/// <summary>
/// Serilog enricher to add correlation ID to log events
/// </summary>
public class CorrelationIdEnricher : ILogEventEnricher
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CorrelationIdEnricher(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            return;

        var correlationId = Middleware.CorrelationIdMiddleware.GetCorrelationId(httpContext);
        if (correlationId != null)
        {
            var property = propertyFactory.CreateProperty("CorrelationId", correlationId);
            logEvent.AddPropertyIfAbsent(property);
        }
    }
}
