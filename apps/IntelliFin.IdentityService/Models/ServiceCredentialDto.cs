namespace IntelliFin.IdentityService.Models;

public class ServiceCredentialDto
{
    public Guid Id { get; set; }
    public Guid ServiceAccountId { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
}
