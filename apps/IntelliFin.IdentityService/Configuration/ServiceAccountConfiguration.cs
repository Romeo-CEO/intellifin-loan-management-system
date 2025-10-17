namespace IntelliFin.IdentityService.Configuration;

public class ServiceAccountConfiguration
{
    public bool EnableKeycloakProvisioning { get; set; }
    public int DefaultSecretLength { get; set; } = 48;
    public int ClientIdSuffixLength { get; set; } = 6;
    public int? CredentialExpiryDays { get; set; }
    public string? KeycloakRealm { get; set; }
    public string? KeycloakClientTemplate { get; set; }
    public string? VaultSecretMountPath { get; set; }
    public string? KeycloakBaseUrl { get; set; }
}
