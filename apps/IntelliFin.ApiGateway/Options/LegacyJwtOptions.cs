namespace IntelliFin.ApiGateway.Options;

public class LegacyJwtOptions
{
    public string? Authority { get; set; }
    public string? Audience { get; set; }
    public string? ValidIssuer { get; set; }
    public string? SigningKey { get; set; }
    public bool RequireHttps { get; set; } = true;
}
