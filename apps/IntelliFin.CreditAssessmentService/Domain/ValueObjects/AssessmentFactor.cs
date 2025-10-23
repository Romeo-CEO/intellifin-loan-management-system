namespace IntelliFin.CreditAssessmentService.Domain.ValueObjects;

/// <summary>
/// Represents a factor that contributed to the credit assessment score.
/// </summary>
public class AssessmentFactor
{
    public Guid Id { get; set; }
    public Guid CreditAssessmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal Contribution { get; set; }
    public string Explanation { get; set; } = string.Empty;
}
