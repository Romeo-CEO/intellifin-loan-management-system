namespace IntelliFin.IdentityService.Models;

public class ServiceAccountDto
{
    public Guid Id { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IReadOnlyCollection<string> Scopes { get; set; } = Array.Empty<string>();
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public DateTime? DeactivatedAtUtc { get; set; }
    public ServiceCredentialDto? Credential { get; set; }
}
