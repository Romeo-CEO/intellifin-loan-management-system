using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IntelliFin.Shared.DomainModels.Entities;

/// <summary>
/// Stores individual rule evaluation results for a credit assessment.
/// Enables detailed analysis of which rules passed/failed and their impact on the final score.
/// Created in Story 1.2 for Credit Assessment microservice.
/// </summary>
public class RuleEvaluation
{
    /// <summary>
    /// Unique identifier for the rule evaluation record.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Reference to the credit assessment this rule evaluation belongs to.
    /// </summary>
    [Required]
    public Guid AssessmentId { get; set; }

    /// <summary>
    /// Rule identifier from Vault configuration (e.g., "PR-001", "BU-002").
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string RuleId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable rule name (e.g., "Maximum Loan-to-Income Ratio").
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string RuleName { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether the rule evaluation passed (true) or failed (false).
    /// </summary>
    [Required]
    public bool Passed { get; set; }

    /// <summary>
    /// Score assigned by the rule (positive for pass, negative for fail).
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(10,2)")]
    public decimal Score { get; set; }

    /// <summary>
    /// Weight of this rule in the overall assessment (0.0 to 1.0).
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(5,4)")]
    public decimal Weight { get; set; }

    /// <summary>
    /// Weighted score contribution (Score * Weight).
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(10,2)")]
    public decimal WeightedScore { get; set; }

    /// <summary>
    /// JSON containing input values used for rule evaluation.
    /// </summary>
    [Required]
    public string InputValues { get; set; } = "{}";

    /// <summary>
    /// Human-readable explanation of the rule evaluation result.
    /// </summary>
    [MaxLength(2000)]
    public string? Explanation { get; set; }

    /// <summary>
    /// Timestamp when the rule was evaluated.
    /// </summary>
    [Required]
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    
    /// <summary>
    /// Navigation property to the associated credit assessment.
    /// </summary>
    public CreditAssessment? Assessment { get; set; }
}
