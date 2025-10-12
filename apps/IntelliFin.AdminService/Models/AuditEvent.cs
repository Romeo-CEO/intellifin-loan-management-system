namespace IntelliFin.AdminService.Models;

public class AuditEvent
{
    public long Id { get; set; }
    public Guid EventId { get; set; }
    public DateTime Timestamp { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? CorrelationId { get; set; }
    public string? PreviousEventHash { get; set; }
    public string? CurrentEventHash { get; set; }
    public string? EventData { get; set; }
}
