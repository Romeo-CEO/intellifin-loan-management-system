namespace IntelliFin.IdentityService.Models;

public class SessionInfo
{
    public string SessionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? DeviceId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastAccessAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsActive { get; set; }
    public string AuthenticationLevel { get; set; } = "basic";
    public Dictionary<string, string> Metadata { get; set; } = new();
    
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    public bool IsValid => IsActive && !IsExpired;
    public TimeSpan TimeUntilExpiry => ExpiresAt - DateTime.UtcNow;
}