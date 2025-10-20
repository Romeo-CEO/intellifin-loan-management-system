namespace IntelliFin.Shared.DomainModels.Entities;

public class AuditEvent
{
    public Guid Id { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public string Data { get; set; } = string.Empty; // JSON payload
    public string? IpAddress { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? TenantId { get; set; }
}
