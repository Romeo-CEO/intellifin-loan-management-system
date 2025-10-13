namespace IntelliFin.IdentityService.Configuration;

public class VaultConfiguration
{
    public string CredentialsPath { get; set; } = "/vault/secrets/database-credentials.json";

    public string Role { get; set; } = "identity-service";

    public string DatabaseRole { get; set; } = "identity-service-role";

    /// <summary>
    /// Optional interval (in seconds) for proactive lease renewal monitoring.
    /// </summary>
    public int LeaseRenewalLeadTimeSeconds { get; set; } = 3600;
}
