namespace IntelliFin.IdentityService.Configuration;

public class SessionConfiguration
{
    public int TimeoutMinutes { get; set; } = 30;
    public int MaxConcurrentSessions { get; set; } = 3;
    public bool RequireUniqueSession { get; set; } = false;
    public bool TrackUserActivity { get; set; } = true;
    public int ActivityUpdateIntervalMinutes { get; set; } = 5;
    public int CleanupIntervalMinutes { get; set; } = 15;
    public bool LogSessionEvents { get; set; } = true;
    public string[] TrustedProxies { get; set; } = Array.Empty<string>();
}