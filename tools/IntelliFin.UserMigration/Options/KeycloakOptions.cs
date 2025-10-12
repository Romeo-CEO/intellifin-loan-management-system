using System.ComponentModel.DataAnnotations;

namespace IntelliFin.UserMigration.Options;

public sealed class KeycloakOptions
{
    private const string DefaultAdminRealm = "master";

    [Required]
    public string BaseUrl { get; set; } = string.Empty;

    [Required]
    public string Realm { get; set; } = string.Empty;

    /// <summary>
    /// Realm used for requesting access tokens. Defaults to the master realm when not specified.
    /// </summary>
    public string TokenRealm { get; set; } = DefaultAdminRealm;

    [Required]
    public string ClientId { get; set; } = string.Empty;

    public string? ClientSecret { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    /// <summary>
    /// Optional rate limiting delay, expressed in milliseconds, between consecutive admin API calls.
    /// </summary>
    [Range(0, 10_000)]
    public int ApiDelayMs { get; set; }

    public bool UseClientCredentials => !string.IsNullOrWhiteSpace(ClientSecret);
}
