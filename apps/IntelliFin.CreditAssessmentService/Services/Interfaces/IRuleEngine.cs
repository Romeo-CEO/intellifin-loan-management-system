using IntelliFin.CreditAssessmentService.Domain.Entities;
using IntelliFin.CreditAssessmentService.Domain.Enums;
using IntelliFin.CreditAssessmentService.Services.Models;

namespace IntelliFin.CreditAssessmentService.Services.Interfaces;

/// <summary>
/// Evaluates credit assessment rules and produces scoring outcomes.
/// </summary>
public interface IRuleEngine
{
    Task<RuleEvaluationResult> EvaluateAsync(RuleEvaluationContext context, CancellationToken cancellationToken = default);
    Task<string> GetCurrentConfigVersionAsync(CancellationToken cancellationToken = default);
}
