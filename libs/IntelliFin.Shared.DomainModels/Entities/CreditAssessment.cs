using System.ComponentModel.DataAnnotations.Schema;

namespace IntelliFin.Shared.DomainModels.Entities;

/// <summary>
/// Credit Assessment entity with comprehensive audit and configuration tracking.
/// Enhanced in Story 1.2 for Credit Assessment microservice.
/// </summary>
public class CreditAssessment
{
    public Guid Id { get; set; }
    public Guid LoanApplicationId { get; set; }
    public string RiskGrade { get; set; } = string.Empty;
    public decimal CreditScore { get; set; }
    public decimal DebtToIncomeRatio { get; set; }
    public decimal PaymentCapacity { get; set; }
    public bool HasCreditBureauData { get; set; }
    public string ScoreExplanation { get; set; } = string.Empty;
    public DateTime AssessedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Legacy field - kept for backward compatibility. Use AssessedByUserId instead.
    /// </summary>
    public string AssessedBy { get; set; } = string.Empty;

    // Story 1.2 Enhancements: Audit and Decision Tracking
    
    /// <summary>
    /// User ID who triggered the assessment (replaces legacy AssessedBy string).
    /// </summary>
    public Guid? AssessedByUserId { get; set; }
    
    /// <summary>
    /// Decision category: Approved, Conditional, ManualReview, Rejected.
    /// </summary>
    public string? DecisionCategory { get; set; }
    
    /// <summary>
    /// List of rule IDs that were triggered during evaluation (stored as JSONB).
    /// </summary>
    [Column(TypeName = "jsonb")]
    public List<string>? TriggeredRules { get; set; }

    // Manual Override Tracking
    
    /// <summary>
    /// User ID who performed manual override, if applicable.
    /// </summary>
    public Guid? ManualOverrideByUserId { get; set; }
    
    /// <summary>
    /// Reason for manual override.
    /// </summary>
    public string? ManualOverrideReason { get; set; }
    
    /// <summary>
    /// Timestamp when manual override was applied.
    /// </summary>
    public DateTime? ManualOverrideAt { get; set; }

    // Validity Tracking (for KYC expiry)
    
    /// <summary>
    /// Indicates if the assessment is still valid. Set to false if KYC expires.
    /// </summary>
    public bool IsValid { get; set; } = true;
    
    /// <summary>
    /// Reason why assessment was invalidated (e.g., "KYC Expired").
    /// </summary>
    public string? InvalidReason { get; set; }

    // Configuration Tracking
    
    /// <summary>
    /// Vault configuration version used for this assessment.
    /// </summary>
    public string? VaultConfigVersion { get; set; }

    // Navigation Properties
    public LoanApplication? LoanApplication { get; set; }
    public ICollection<CreditFactor> CreditFactors { get; set; } = new List<CreditFactor>();
    public ICollection<RiskIndicator> RiskIndicators { get; set; } = new List<RiskIndicator>();
}

public class CreditFactor
{
    public Guid Id { get; set; }
    public Guid CreditAssessmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal Score { get; set; }
    public string Impact { get; set; } = string.Empty;

    public CreditAssessment? CreditAssessment { get; set; }
}

public class RiskIndicator
{
    public Guid Id { get; set; }
    public Guid CreditAssessmentId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public decimal Impact { get; set; }

    public CreditAssessment? CreditAssessment { get; set; }
}