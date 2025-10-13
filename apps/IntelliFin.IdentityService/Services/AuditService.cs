using System.Diagnostics;
using IntelliFin.IdentityService.Models;
using IntelliFin.Shared.Audit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace IntelliFin.IdentityService.Services;

public class AuditService : IAuditService
{
    private readonly ILogger<AuditService> _logger;
    private readonly IAuditClient _auditClient;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditService(ILogger<AuditService> logger, IAuditClient auditClient, IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger;
        _auditClient = auditClient;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        var activity = Activity.Current;
        var correlationId = !string.IsNullOrWhiteSpace(auditEvent.SessionId)
            ? auditEvent.SessionId
            : activity?.TraceId.ToString() ?? _httpContextAccessor.HttpContext?.TraceIdentifier;

        var payload = new AuditEventPayload
        {
            Timestamp = auditEvent.Timestamp,
            Actor = string.IsNullOrWhiteSpace(auditEvent.ActorId) ? "system" : auditEvent.ActorId,
            Action = auditEvent.Action,
            EntityType = auditEvent.Entity,
            EntityId = auditEvent.EntityId,
            CorrelationId = correlationId,
            IpAddress = auditEvent.IpAddress,
            UserAgent = auditEvent.UserAgent,
            EventData = auditEvent.Details.Count > 0 ? auditEvent.Details : null
        };

        try
        {
            await _auditClient.LogEventAsync(payload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to forward audit event {Action} for {Entity} ({EntityId})", auditEvent.Action, auditEvent.Entity, auditEvent.EntityId);
        }
    }
}
