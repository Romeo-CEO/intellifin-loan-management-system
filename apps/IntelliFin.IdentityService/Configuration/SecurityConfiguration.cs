namespace IntelliFin.IdentityService.Configuration;

public class SecurityConfiguration
{
    public int MaxLoginAttemptsPerMinute { get; set; } = 10;
    public int RateLimitWindowMinutes { get; set; } = 15;
    public bool RequireHttps { get; set; } = true;
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
    public bool EnableCors { get; set; } = true;
    public bool EnableSecurityHeaders { get; set; } = true;
    public string ContentSecurityPolicy { get; set; } = "default-src 'self'";
    public bool StrictTransportSecurity { get; set; } = true;
    public int HstsMaxAge { get; set; } = 31536000; // 1 year
    public bool XContentTypeOptions { get; set; } = true;
    public bool XFrameOptions { get; set; } = true;
    public bool ReferrerPolicy { get; set; } = true;
    public string[] TrustedProxies { get; set; } = Array.Empty<string>();
}