namespace IntelliFin.CreditAssessmentService.Services.Integration;

public interface IAdminServiceClient
{
    Task LogAuditEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
}

public class AuditEvent
{
    public string EventType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public object? Details { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? CorrelationId { get; set; }
}
