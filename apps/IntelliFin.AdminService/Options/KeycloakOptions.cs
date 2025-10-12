namespace IntelliFin.AdminService.Options;

public class KeycloakOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Realm { get; set; } = "IntelliFin";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}
