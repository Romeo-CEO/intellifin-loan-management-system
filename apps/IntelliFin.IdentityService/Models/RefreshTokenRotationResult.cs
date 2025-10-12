namespace IntelliFin.IdentityService.Models;

public class RefreshTokenRotationResult
{
    public string UserId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string FamilyId { get; set; } = string.Empty;
    public DateTime RefreshTokenExpiresAt { get; set; }
}
