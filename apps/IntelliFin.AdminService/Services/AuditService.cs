using System.Diagnostics;
using System.Text.Json;
using IntelliFin.AdminService.Data;
using IntelliFin.AdminService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IntelliFin.AdminService.Services;

public sealed class AuditService(AdminDbContext dbContext, ILogger<AuditService> logger) : IAuditService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public async Task LogEventAsync(
        string actor,
        string action,
        string entityType,
        string? entityId,
        object? eventData,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        try
        {
            var auditEvent = new AuditEvent
            {
                Actor = actor,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                CorrelationId = correlationId ?? Activity.Current?.Id,
                EventData = eventData is null ? null : JsonSerializer.Serialize(eventData, SerializerOptions)
            };

            await dbContext.AuditEvents.AddAsync(auditEvent, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist audit event for {EntityType} {EntityId}", entityType, entityId);
            dbContext.ChangeTracker.Clear();
        }
    }
}
