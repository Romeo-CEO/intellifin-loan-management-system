namespace IntelliFin.CreditAssessmentService.Options;

/// <summary>
/// Configuration options for Vault-backed rule evaluation.
/// </summary>
public class VaultRuleOptions
{
    public const string SectionName = "VaultRules";

    /// <summary>
    /// Base address of the Vault server.
    /// </summary>
    public string Address { get; set; } = "http://vault:8200";

    /// <summary>
    /// Path containing credit assessment rule configuration.
    /// </summary>
    public string RulePath { get; set; } = "intellifin/credit-assessment/rules";

    /// <summary>
    /// Path containing risk thresholds.
    /// </summary>
    public string ThresholdPath { get; set; } = "intellifin/credit-assessment/thresholds";

    /// <summary>
    /// API path with TransUnion credentials.
    /// </summary>
    public string TransUnionSecretPath { get; set; } = "intellifin/credit-assessment/transunion";

    /// <summary>
    /// Refresh interval in minutes.
    /// </summary>
    public int RefreshIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// Indicates whether Vault integration is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
