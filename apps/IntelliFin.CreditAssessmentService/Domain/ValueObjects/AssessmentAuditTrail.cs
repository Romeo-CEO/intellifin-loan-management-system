namespace IntelliFin.CreditAssessmentService.Domain.ValueObjects;

/// <summary>
/// Represents an audit entry for credit assessment decisions.
/// </summary>
public class AssessmentAuditTrail
{
    public Guid Id { get; set; }
    public Guid CreditAssessmentId { get; set; }
    public DateTime OccurredAt { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}
