namespace IntelliFin.CreditAssessmentService.Domain.Entities;

/// <summary>
/// Represents a manual override performed by a credit officer.
/// </summary>
public class ManualOverride
{
    public Guid Id { get; set; }
    public Guid CreditAssessmentId { get; set; }
    public string Officer { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
}
