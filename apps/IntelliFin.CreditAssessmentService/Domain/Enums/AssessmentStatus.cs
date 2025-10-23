namespace IntelliFin.CreditAssessmentService.Domain.Enums;

/// <summary>
/// Tracks the lifecycle state of a credit assessment.
/// </summary>
public enum AssessmentStatus
{
    Pending,
    InProgress,
    Completed,
    ManualOverride,
    Invalidated
}
