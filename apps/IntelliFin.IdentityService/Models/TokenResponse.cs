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

public class UserInfo
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string[] Roles { get; set; } = Array.Empty<string>();
    public string[] Permissions { get; set; } = Array.Empty<string>();
    public string? BranchId { get; set; }
    public string? BranchName { get; set; }
    public string? BranchRegion { get; set; }
    public bool RequiresTwoFactor { get; set; }
    public bool IsActive { get; set; }
}