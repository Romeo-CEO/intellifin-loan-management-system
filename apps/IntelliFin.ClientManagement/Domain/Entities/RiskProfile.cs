namespace IntelliFin.ClientManagement.Domain.Entities;

/// <summary>
/// Risk profile for a client
/// Maintains historical record of all risk assessments with Vault rules versioning
/// Only one profile per client can have IsCurrent = true
/// </summary>
public class RiskProfile
{
    /// <summary>
    /// Unique identifier for this risk profile assessment
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Foreign key to Client entity
    /// </summary>
    public Guid ClientId { get; set; }

    // ========== Risk Assessment Results ==========

    /// <summary>
    /// Risk rating classification (Low, Medium, High)
    /// </summary>
    public string RiskRating { get; set; } = string.Empty;

    /// <summary>
    /// Numeric risk score (0-100)
    /// 0-25: Low, 26-50: Medium, 51-100: High
    /// </summary>
    public int RiskScore { get; set; }

    /// <summary>
    /// When this risk assessment was computed
    /// </summary>
    public DateTime ComputedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User ID or system identifier that computed the risk
    /// Values: "system-workflow", user-{userId}, "batch-recalculation"
    /// </summary>
    public string ComputedBy { get; set; } = string.Empty;

    // ========== Vault Rules Tracking ==========

    /// <summary>
    /// Version of the risk scoring rules used
    /// Format: Semantic versioning (e.g., "1.2.0")
    /// </summary>
    public string RiskRulesVersion { get; set; } = string.Empty;

    /// <summary>
    /// SHA256 checksum of the rules configuration
    /// Used to detect configuration changes
    /// </summary>
    public string RiskRulesChecksum { get; set; } = string.Empty;

    // ========== Audit and Traceability ==========

    /// <summary>
    /// JSON log of rule execution details
    /// Contains: which rules fired, scores awarded, conditions evaluated
    /// </summary>
    public string? RuleExecutionLog { get; set; }

    /// <summary>
    /// JSON of input factors used for scoring
    /// Contains: kycComplete, amlRiskLevel, isPep, hasSanctionsHit, etc.
    /// </summary>
    public string InputFactorsJson { get; set; } = string.Empty;

    // ========== Historical Tracking ==========

    /// <summary>
    /// Whether this is the current (latest) risk profile for the client
    /// Only one profile per client should have IsCurrent = true
    /// </summary>
    public bool IsCurrent { get; set; } = true;

    /// <summary>
    /// When this risk profile was superseded by a newer assessment
    /// Null if this is the current profile
    /// </summary>
    public DateTime? SupersededAt { get; set; }

    /// <summary>
    /// Reason for superseding (if applicable)
    /// Values: "Scheduled", "RulesUpdated", "ManualRecalculation", "ClientDataChanged"
    /// </summary>
    public string? SupersededReason { get; set; }

    // ========== Navigation Properties ==========

    /// <summary>
    /// Navigation property to associated Client
    /// </summary>
    public Client? Client { get; set; }
}

/// <summary>
/// Risk rating constants
/// </summary>
public static class RiskRating
{
    public const string Low = "Low";
    public const string Medium = "Medium";
    public const string High = "High";
}
