using System.Text.Json;
using IntelliFin.ClientManagement.Middleware;
using IntelliFin.Shared.Audit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace IntelliFin.ClientManagement.Services;

public sealed class AuditService : IAuditService
{
    private readonly ILogger<AuditService> _logger;
    private readonly IAuditQueue _auditQueue;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditService(
        ILogger<AuditService> logger,
        IAuditQueue auditQueue,
        IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger;
        _auditQueue = auditQueue;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAuditEventAsync(
        string action,
        string entityType,
        string entityId,
        string actor,
        object? eventData = null)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var correlationId = httpContext != null ? CorrelationIdMiddleware.GetCorrelationId(httpContext) : null;
            var ipAddress = httpContext?.Connection.RemoteIpAddress?.ToString();
            string? eventDataJson = eventData is null ? null : JsonSerializer.Serialize(eventData);

            var payload = new AuditEventPayload
            {
                Timestamp = DateTime.UtcNow,
                Actor = string.IsNullOrWhiteSpace(actor) ? "system" : actor,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                CorrelationId = correlationId,
                IpAddress = ipAddress,
                EventData = eventDataJson is null ? null : JsonSerializer.Deserialize<object>(eventDataJson)
            };

            await _auditQueue.EnqueueAsync(payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue audit event {Action} for {EntityType}:{EntityId}", action, entityType, entityId);
        }
    }

    public Task FlushAsync()
    {
        // No-op; batching service flushes on interval or size. Could be extended to signal flush.
        return Task.CompletedTask;
    }
}
