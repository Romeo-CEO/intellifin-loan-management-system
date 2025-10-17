namespace IntelliFin.Shared.DomainModels.Entities;

public class ServiceCredential
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ServiceAccountId { get; set; }
    public string SecretHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "system";
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? RevokedBy { get; set; }
    public string? RevocationReason { get; set; }

    public virtual ServiceAccount? ServiceAccount { get; set; }
}
