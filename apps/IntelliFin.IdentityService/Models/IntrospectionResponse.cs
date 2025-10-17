namespace IntelliFin.IdentityService.Models;

public record IntrospectionResponse
{
    public bool Active { get; init; }
    public string? Subject { get; init; }
    public string? Username { get; init; }
    public string? Email { get; init; }
    public string[] Roles { get; init; } = Array.Empty<string>();
    public string[] Permissions { get; init; } = Array.Empty<string>();
    public Guid? BranchId { get; init; }
    public Guid? TenantId { get; init; }
    public long? ExpiresAt { get; init; }
    public long? IssuedAt { get; init; }
    public string? Issuer { get; init; }
}
