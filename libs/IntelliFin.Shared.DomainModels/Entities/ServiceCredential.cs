namespace IntelliFin.Shared.DomainModels.Entities;

public class ServiceCredential
{
    public Guid CredentialId { get; set; } = Guid.NewGuid();
    public Guid ServiceAccountId { get; set; }
    public string SecretHash { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }

    // Navigation
    public virtual ServiceAccount? ServiceAccount { get; set; }
}