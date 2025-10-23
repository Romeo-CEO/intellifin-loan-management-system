using System.Text.Json.Serialization;

namespace IntelliFin.ClientManagement.Domain.Models;

/// <summary>
/// Risk scoring configuration retrieved from Vault
/// Contains rules, thresholds, and options for computing client risk scores
/// </summary>
public class RiskScoringConfig
{
    /// <summary>
    /// Semantic version of the configuration (e.g., "1.2.0")
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// SHA256 checksum of the configuration content
    /// Used to detect changes
    /// </summary>
    [JsonPropertyName("checksum")]
    public string Checksum { get; set; } = string.Empty;

    /// <summary>
    /// When this configuration was last modified
    /// </summary>
    [JsonPropertyName("lastModified")]
    public DateTime LastModified { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Collection of risk scoring rules
    /// Key: Rule identifier, Value: Rule definition
    /// </summary>
    [JsonPropertyName("rules")]
    public Dictionary<string, RiskRule> Rules { get; set; } = new();

    /// <summary>
    /// Risk rating thresholds for score mapping
    /// Key: Rating name (low, medium, high)
    /// </summary>
    [JsonPropertyName("thresholds")]
    public Dictionary<string, RiskThreshold> Thresholds { get; set; } = new();

    /// <summary>
    /// Global scoring options
    /// </summary>
    [JsonPropertyName("options")]
    public RiskScoringOptions Options { get; set; } = new();

    /// <summary>
    /// Validates the configuration
    /// </summary>
    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(Version))
            return false;

        if (Rules.Count == 0)
            return false;

        if (Thresholds.Count == 0)
            return false;

        // Validate all rules
        foreach (var rule in Rules.Values)
        {
            if (!rule.IsValid())
                return false;
        }

        return true;
    }
}

/// <summary>
/// Individual risk scoring rule
/// </summary>
public class RiskRule
{
    /// <summary>
    /// Rule identifier (unique within configuration)
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of what the rule checks
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Score points awarded if rule condition is met
    /// </summary>
    [JsonPropertyName("points")]
    public int Points { get; set; }

    /// <summary>
    /// JSONLogic expression for rule condition
    /// Example: "kycComplete == false"
    /// </summary>
    [JsonPropertyName("condition")]
    public string Condition { get; set; } = string.Empty;

    /// <summary>
    /// Whether this rule is enabled
    /// Allows disabling rules without removing them
    /// </summary>
    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Execution priority (lower = higher priority)
    /// Rules execute in priority order
    /// </summary>
    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 100;

    /// <summary>
    /// Optional category for grouping rules
    /// </summary>
    [JsonPropertyName("category")]
    public string? Category { get; set; }

    /// <summary>
    /// Validates the rule
    /// </summary>
    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(Name))
            return false;

        if (string.IsNullOrWhiteSpace(Condition))
            return false;

        if (Points < 0)
            return false;

        return true;
    }
}

/// <summary>
/// Risk threshold for mapping scores to ratings
/// </summary>
public class RiskThreshold
{
    /// <summary>
    /// Rating name (Low, Medium, High)
    /// </summary>
    [JsonPropertyName("rating")]
    public string Rating { get; set; } = string.Empty;

    /// <summary>
    /// Minimum score for this rating (inclusive)
    /// </summary>
    [JsonPropertyName("minScore")]
    public int MinScore { get; set; }

    /// <summary>
    /// Maximum score for this rating (inclusive)
    /// </summary>
    [JsonPropertyName("maxScore")]
    public int MaxScore { get; set; }

    /// <summary>
    /// Description of what this rating means
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Checks if a score falls within this threshold
    /// </summary>
    public bool ContainsScore(int score)
    {
        return score >= MinScore && score <= MaxScore;
    }
}

/// <summary>
/// Global options for risk scoring
/// </summary>
public class RiskScoringOptions
{
    /// <summary>
    /// Maximum possible risk score
    /// </summary>
    [JsonPropertyName("maxScore")]
    public int MaxScore { get; set; } = 100;

    /// <summary>
    /// Default rating if scoring fails
    /// </summary>
    [JsonPropertyName("defaultRating")]
    public string DefaultRating { get; set; } = "Medium";

    /// <summary>
    /// Whether to enable detailed audit logging
    /// </summary>
    [JsonPropertyName("enableAuditLogging")]
    public bool EnableAuditLogging { get; set; } = true;

    /// <summary>
    /// Whether to stop on first error or continue
    /// </summary>
    [JsonPropertyName("stopOnError")]
    public bool StopOnError { get; set; } = false;
}
