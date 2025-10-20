namespace IntelliFin.Shared.DomainModels.Entities;

public class TenantBranch
{
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual Tenant? Tenant { get; set; }
}