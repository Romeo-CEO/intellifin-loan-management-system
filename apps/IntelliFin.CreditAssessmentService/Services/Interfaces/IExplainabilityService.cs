using IntelliFin.CreditAssessmentService.Domain.Entities;

namespace IntelliFin.CreditAssessmentService.Services.Interfaces;

/// <summary>
/// Builds human-readable explanations for credit assessments.
/// </summary>
public interface IExplainabilityService
{
    string BuildExplanation(CreditAssessment assessment);
}
