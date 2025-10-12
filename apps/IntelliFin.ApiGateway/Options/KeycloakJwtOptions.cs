namespace IntelliFin.ApiGateway.Options;

public class KeycloakJwtOptions
{
    public string? Authority { get; set; }
    public string? Issuer { get; set; }
    public string? Audience { get; set; }
    public string? MetadataAddress { get; set; }
    public bool RequireHttps { get; set; } = true;
}
