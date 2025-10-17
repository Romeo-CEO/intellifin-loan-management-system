namespace IntelliFin.IdentityService.Configuration;

public class AuthorizationConfiguration
{
    public string[] TrustedIssuers { get; set; } = Array.Empty<string>();

    public Dictionary<string, string> IssuerMetadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string[] AllowedAudiences { get; set; } = Array.Empty<string>();

    public bool RequireHttpsMetadata { get; set; } = true;

    public int MetadataCacheMinutes { get; set; } = 60;
}
