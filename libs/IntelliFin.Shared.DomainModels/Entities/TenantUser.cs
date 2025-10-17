namespace IntelliFin.Shared.DomainModels.Entities;

public class TenantUser
{
    public Guid TenantId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? Role { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public string? AssignedBy { get; set; }

    // Navigation
    public virtual Tenant? Tenant { get; set; }
    public virtual User? User { get; set; }
}