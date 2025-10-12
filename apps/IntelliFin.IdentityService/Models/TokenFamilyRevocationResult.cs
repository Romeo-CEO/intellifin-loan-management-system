namespace IntelliFin.IdentityService.Models;

public class TokenFamilyRevocationResult
{
    public string FamilyId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public IReadOnlyList<string> RevokedTokens { get; set; } = Array.Empty<string>();
}
