namespace IntelliFin.IdentityService.Models;

public class RefreshTokenResult
{
    public string Token { get; set; } = string.Empty;
    public string FamilyId { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public long Sequence { get; set; }
}
