namespace IntelliFin.CreditAssessmentService.Domain.Entities;

/// <summary>
/// Represents a configurable assessment rule loaded from Vault.
/// </summary>
public class AssessmentRule
{
    public Guid Id { get; set; }
    public string RuleKey { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public string Category { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}
