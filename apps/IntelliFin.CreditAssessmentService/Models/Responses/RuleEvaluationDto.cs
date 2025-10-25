namespace IntelliFin.CreditAssessmentService.Models.Responses;

/// <summary>
/// Individual rule evaluation result.
/// </summary>
public class RuleEvaluationDto
{
    /// <summary>
    /// Rule identifier (e.g., "PR-001", "BU-002").
    /// </summary>
    public string RuleId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable rule name.
    /// </summary>
    public string RuleName { get; set; } = string.Empty;

    /// <summary>
    /// Whether the rule passed or failed.
    /// </summary>
    public bool Passed { get; set; }

    /// <summary>
    /// Score assigned by this rule.
    /// </summary>
    public decimal Score { get; set; }

    /// <summary>
    /// Weight of this rule in overall assessment.
    /// </summary>
    public decimal Weight { get; set; }

    /// <summary>
    /// Weighted contribution to final score.
    /// </summary>
    public decimal WeightedScore { get; set; }

    /// <summary>
    /// Brief explanation of the rule evaluation.
    /// </summary>
    public string? Explanation { get; set; }

    /// <summary>
    /// Impact category: Positive, Negative, Neutral.
    /// </summary>
    public string Impact { get; set; } = "Neutral";
}
