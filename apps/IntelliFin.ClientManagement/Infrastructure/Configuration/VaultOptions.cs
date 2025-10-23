namespace IntelliFin.ClientManagement.Infrastructure.Configuration;

/// <summary>
/// Configuration options for HashiCorp Vault integration
/// Used for retrieving risk scoring rules and other sensitive configuration
/// </summary>
public class VaultOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "VaultRiskScoring";

    /// <summary>
    /// Vault server address (e.g., http://vault:8200)
    /// </summary>
    public string Address { get; set; } = "http://localhost:8200";

    /// <summary>
    /// Authentication method (Token, JWT, Kubernetes)
    /// </summary>
    public string AuthMethod { get; set; } = "Token";

    /// <summary>
    /// Service role name for JWT authentication
    /// </summary>
    public string? RoleName { get; set; }

    /// <summary>
    /// Direct token for development (Token auth method)
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// Path to risk scoring rules in Vault
    /// Format: intellifin/client-management/risk-scoring-rules
    /// </summary>
    public string RiskScoringConfigPath { get; set; } = "intellifin/data/client-management/risk-scoring-rules";

    /// <summary>
    /// Mount point for KV v2 secrets engine
    /// </summary>
    public string MountPoint { get; set; } = "intellifin";

    /// <summary>
    /// Polling interval for configuration changes (seconds)
    /// </summary>
    public int PollingIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Vault client request timeout (seconds)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Whether Vault integration is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Use fallback (hardcoded) scoring if Vault unavailable
    /// </summary>
    public bool UseFallbackOnError { get; set; } = true;

    /// <summary>
    /// Maximum retry attempts for Vault operations
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Validates configuration
    /// </summary>
    public bool IsValid()
    {
        if (!Enabled)
            return true; // Valid if disabled

        if (string.IsNullOrWhiteSpace(Address))
            return false;

        if (string.IsNullOrWhiteSpace(RiskScoringConfigPath))
            return false;

        if (AuthMethod == "JWT" && string.IsNullOrWhiteSpace(RoleName))
            return false;

        if (AuthMethod == "Token" && string.IsNullOrWhiteSpace(Token))
            return false;

        return true;
    }
}
