namespace IntelliFin.IdentityService.Models;

public record PermissionCheckResponse
{
    public bool Allowed { get; init; }
    public string Reason { get; init; } = string.Empty;
}
