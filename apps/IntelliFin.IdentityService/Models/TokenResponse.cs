namespace IntelliFin.IdentityService.Models;

public class TokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string[] Scope { get; set; } = Array.Empty<string>();
    public string RefreshTokenFamilyId { get; set; } = string.Empty;
    public DateTime RefreshTokenExpiresAt { get; set; }
    public UserInfo User { get; set; } = new();
}
