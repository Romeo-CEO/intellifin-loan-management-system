namespace IntelliFin.Shared.DomainModels.Entities;

public class ServiceAccount
{
    public Guid ServiceAccountId { get; set; } = Guid.NewGuid();
    public string ClientId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }

    // Navigation
    public virtual ICollection<ServiceCredential> Credentials { get; set; } = new List<ServiceCredential>();
}