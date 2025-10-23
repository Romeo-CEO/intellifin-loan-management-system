using System.Text;
using IntelliFin.CreditAssessmentService.Domain.Entities;
using IntelliFin.CreditAssessmentService.Services.Interfaces;

namespace IntelliFin.CreditAssessmentService.Services;

/// <summary>
/// Generates human-readable explanations for assessment decisions.
/// </summary>
public sealed class ExplainabilityService : IExplainabilityService
{
    public string BuildExplanation(CreditAssessment assessment)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Decision: {assessment.Decision}");
        builder.AppendLine($"Risk Grade: {assessment.RiskGrade}");
        builder.AppendLine($"Credit Score: {assessment.CreditScore}");
        builder.AppendLine($"Debt-To-Income Ratio: {assessment.DebtToIncomeRatio:P2}");
        builder.AppendLine();
        builder.AppendLine("Key Factors:");

        foreach (var factor in assessment.Factors.OrderByDescending(f => Math.Abs(f.Contribution)))
        {
            builder.AppendLine($"- {factor.Name}: {factor.Impact} impact (weight {factor.Weight:P0}) - {factor.Explanation}");
        }

        if (!assessment.AuditTrail.Any())
        {
            return builder.ToString();
        }

        builder.AppendLine();
        builder.AppendLine("Audit Trail:");
        foreach (var entry in assessment.AuditTrail.OrderBy(a => a.OccurredAt))
        {
            builder.AppendLine($"- [{entry.OccurredAt:O}] {entry.Actor}: {entry.Action} - {entry.Details}");
        }

        return builder.ToString();
    }
}
