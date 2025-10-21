using System.Diagnostics;
using IntelliFin.Shared.Audit;
using System.Text.Json;

namespace IntelliFin.ClientManagement.Services;

/// <summary>
/// Service implementation for audit logging with fire-and-forget pattern
/// Wraps the shared IAuditClient for simplified usage in ClientManagement
/// </summary>
public class AuditService : IAuditService
{
    private readonly IAuditClient _auditClient;
    private readonly ILogger<AuditService> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditService(
        IAuditClient auditClient,
        ILogger<AuditService> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _auditClient = auditClient;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Logs an audit event asynchronously with fire-and-forget pattern
    /// Failures are logged but do not throw to prevent disrupting business operations
    /// </summary>
    public async Task LogAuditEventAsync(
        string action,
        string entityType,
        string entityId,
        string actor,
        object? eventData = null)
    {
        try
        {
            var correlationId = GetCorrelationId();
            var ipAddress = GetClientIpAddress();

            var payload = new AuditEventPayload
            {
                Timestamp = DateTime.UtcNow,
                Actor = string.IsNullOrWhiteSpace(actor) ? "system" : actor,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                CorrelationId = correlationId,
                IpAddress = ipAddress,
                UserAgent = _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].FirstOrDefault(),
                EventData = eventData
            };

            // Fire-and-forget: don't await to avoid blocking business operations
            _ = Task.Run(async () =>
            {
                try
                {
                    await _auditClient.LogEventAsync(payload);
                    _logger.LogDebug(
                        "Audit event logged: {Action} on {EntityType}:{EntityId} by {Actor}",
                        action, entityType, entityId, actor);
                }
                catch (Exception ex)
                {
                    // Log failure but don't propagate - audit failures shouldn't break business operations
                    _logger.LogError(ex,
                        "Failed to log audit event: {Action} on {EntityType}:{EntityId}",
                        action, entityType, entityId);
                }
            });

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            // Catch any exceptions in the main path to ensure business operations continue
            _logger.LogError(ex, "Error preparing audit event for {Action} on {EntityType}:{EntityId}",
                action, entityType, entityId);
        }
    }

    /// <summary>
    /// Flushes any pending audit events immediately
    /// This implementation uses fire-and-forget, so this is a no-op
    /// Future implementations with batching would flush the queue here
    /// </summary>
    public Task FlushAsync()
    {
        // No-op for current fire-and-forget implementation
        // Future: When batching is implemented, this would flush the batch queue
        _logger.LogDebug("FlushAsync called - no-op for fire-and-forget implementation");
        return Task.CompletedTask;
    }

    private string? GetCorrelationId()
    {
        // Try to get correlation ID from Activity (OpenTelemetry)
        var activity = Activity.Current;
        if (activity != null && !string.IsNullOrWhiteSpace(activity.TraceId.ToString()))
        {
            return activity.TraceId.ToString();
        }

        // Try to get from HttpContext
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            // Check X-Correlation-Id header
            if (httpContext.Request.Headers.TryGetValue("X-Correlation-Id", out var correlationId))
            {
                return correlationId.FirstOrDefault();
            }

            // Fall back to TraceIdentifier
            return httpContext.TraceIdentifier;
        }

        return null;
    }

    private string? GetClientIpAddress()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return null;
        }

        // Check for forwarded IP (for proxies/load balancers)
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            // X-Forwarded-For can contain multiple IPs, take the first one
            return forwardedFor.Split(',')[0].Trim();
        }

        // Fall back to remote IP
        return httpContext.Connection.RemoteIpAddress?.ToString();
    }
}
