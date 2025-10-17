using System.Text.Json.Serialization;

namespace IntelliFin.IdentityService.Models;

public record IntrospectionResponse
{
    public bool Active { get; init; }
    [JsonPropertyName("sub")]
    public string? Subject { get; init; }
    public string? Username { get; init; }
    public string? Email { get; init; }
    public string[] Roles { get; init; } = Array.Empty<string>();
    public string[] Permissions { get; init; } = Array.Empty<string>();
    public Guid? BranchId { get; init; }
    public Guid? TenantId { get; init; }
    [JsonPropertyName("exp")]
    public long? ExpiresAt { get; init; }
    [JsonPropertyName("iat")]
    public long? IssuedAt { get; init; }
    [JsonPropertyName("iss")]
    public string? Issuer { get; init; }
}
