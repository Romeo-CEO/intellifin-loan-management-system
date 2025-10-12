using IntelliFin.IdentityService.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace IntelliFin.IdentityService.Services;

public class AuditService : IAuditService
{
    private readonly ILogger<AuditService> _logger;

    public AuditService(ILogger<AuditService> logger)
    {
        _logger = logger;
    }

    public Task LogAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        var detailsJson = auditEvent.Details.Count > 0
            ? JsonSerializer.Serialize(auditEvent.Details)
            : string.Empty;

        _logger.LogInformation("Audit event {Action} by {ActorId} on {Entity} ({EntityId}) success={Success} result={Result} details={Details}",
            auditEvent.Action,
            auditEvent.ActorId,
            auditEvent.Entity,
            auditEvent.EntityId,
            auditEvent.Success,
            auditEvent.Result,
            detailsJson);

        return Task.CompletedTask;
    }
}
