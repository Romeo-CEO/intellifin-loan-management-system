using IntelliFin.CreditAssessmentService.Domain.Enums;
using IntelliFin.CreditAssessmentService.Domain.ValueObjects;
using IntelliFin.CreditAssessmentService.Services.Interfaces;
using IntelliFin.CreditAssessmentService.Services.Models;

namespace IntelliFin.CreditAssessmentService.Services;

/// <summary>
/// Implements rule evaluation using configuration supplied from Vault.
/// </summary>
public sealed class VaultRuleEngine : IRuleEngine
{
    private readonly IVaultConfigService _vaultConfigService;
    private readonly ILogger<VaultRuleEngine> _logger;

    public VaultRuleEngine(IVaultConfigService vaultConfigService, ILogger<VaultRuleEngine> logger)
    {
        _vaultConfigService = vaultConfigService;
        _logger = logger;
    }

    public async Task<RuleEvaluationResult> EvaluateAsync(RuleEvaluationContext context, CancellationToken cancellationToken = default)
    {
        var ruleConfig = await _vaultConfigService.GetRuleConfigurationAsync(cancellationToken);
        var thresholds = await _vaultConfigService.GetThresholdConfigurationAsync(cancellationToken);

        var factors = new List<AssessmentFactor>();
        decimal weightedScore = 0m;
        decimal totalWeight = 0m;
        decimal debtToIncomeRatio = context.DebtToIncomeRatio;
        decimal paymentCapacity = Math.Max(0, context.MonthlyIncome * thresholds.DebtToIncomeThreshold - context.ExistingDebtPayments);

        foreach (var rule in ruleConfig.Rules)
        {
            var (value, explanation) = EvaluateRule(rule.Key, context, thresholds);
            weightedScore += value * rule.Weight;
            totalWeight += rule.Weight;

            factors.Add(new AssessmentFactor
            {
                Id = Guid.NewGuid(),
                Name = rule.Key,
                Impact = DetermineImpact(value),
                Weight = rule.Weight,
                Contribution = value * rule.Weight,
                Explanation = explanation
            });
        }

        if (totalWeight == 0)
        {
            _logger.LogWarning("Rule configuration contains zero total weight. Falling back to neutral score.");
            totalWeight = 1m;
        }

        var normalizedScore = Math.Clamp(weightedScore / totalWeight, 0m, 1m) * 1000m;
        var grade = DetermineGrade(normalizedScore, thresholds);
        var decision = DetermineDecision(grade, thresholds, debtToIncomeRatio, thresholds.DebtToIncomeThreshold);

        return new RuleEvaluationResult
        {
            CreditScore = decimal.Round(normalizedScore, 0),
            RiskGrade = grade,
            Decision = decision,
            DebtToIncomeRatio = debtToIncomeRatio,
            PaymentCapacity = decimal.Round(paymentCapacity, 2),
            Factors = factors,
            AuditMessages = BuildAuditTrail(factors, grade, decision)
        };
    }

    public async Task<string> GetCurrentConfigVersionAsync(CancellationToken cancellationToken = default)
    {
        var ruleConfig = await _vaultConfigService.GetRuleConfigurationAsync(cancellationToken);
        var thresholds = await _vaultConfigService.GetThresholdConfigurationAsync(cancellationToken);
        return $"rules:{ruleConfig.Version}|thresholds:{thresholds.Version}";
    }

    private static (decimal score, string explanation) EvaluateRule(string key, RuleEvaluationContext context, VaultThresholdConfiguration thresholds)
    {
        return key switch
        {
            "debt_to_income" => EvaluateDebtToIncome(context.DebtToIncomeRatio, thresholds.DebtToIncomeThreshold),
            "credit_score" => EvaluateCreditScore(context.BureauScore),
            "employment_stability" => EvaluateEmployment(context.FinancialMetrics),
            "kyc_completeness" => EvaluateKyc(context.RiskFlags),
            _ => (0.5m, $"No handler for rule '{key}'. Using neutral contribution.")
        };
    }

    private static (decimal score, string explanation) EvaluateDebtToIncome(decimal dti, decimal threshold)
    {
        var score = dti <= threshold ? 1m : Math.Max(0m, 1m - (dti - threshold) * 2m);
        var explanation = $"Debt-to-income ratio {dti:P2} compared to threshold {threshold:P2}.";
        return (score, explanation);
    }

    private static (decimal score, string explanation) EvaluateCreditScore(decimal bureauScore)
    {
        var normalized = Math.Clamp(bureauScore / 1000m, 0m, 1m);
        var explanation = $"Normalized bureau score {bureauScore} resulting in {normalized:P0}.";
        return (normalized, explanation);
    }

    private static (decimal score, string explanation) EvaluateEmployment(IReadOnlyDictionary<string, decimal> metrics)
    {
        metrics.TryGetValue("employment_months", out var months);
        var normalized = Math.Clamp(months / 120m, 0m, 1m);
        var explanation = $"Employment tenure of {months} months normalized to {normalized:P0}.";
        return (normalized, explanation);
    }

    private static (decimal score, string explanation) EvaluateKyc(IReadOnlyCollection<string> riskFlags)
    {
        if (riskFlags.Count == 0)
        {
            return (1m, "No KYC risk flags present.");
        }

        var penalty = Math.Min(0.5m, riskFlags.Count * 0.1m);
        var score = Math.Max(0m, 1m - penalty);
        return (score, $"Detected {riskFlags.Count} KYC risk flags. Penalty {penalty:P0}.");
    }

    private static RiskGrade DetermineGrade(decimal score, VaultThresholdConfiguration thresholds)
    {
        foreach (var grade in thresholds.GradeThresholds.OrderByDescending(kvp => kvp.Value))
        {
            if (score >= grade.Value)
            {
                return Enum.Parse<RiskGrade>(grade.Key);
            }
        }

        return RiskGrade.F;
    }

    private static AssessmentDecision DetermineDecision(RiskGrade grade, VaultThresholdConfiguration thresholds, decimal dti, decimal dtiThreshold)
    {
        if (dti > dtiThreshold * 1.25m)
        {
            return AssessmentDecision.Rejected;
        }

        if (!thresholds.DecisionMatrix.TryGetValue(grade.ToString(), out var decisionText))
        {
            return AssessmentDecision.ManualReview;
        }

        return Enum.TryParse<AssessmentDecision>(decisionText, out var decision)
            ? decision
            : AssessmentDecision.ManualReview;
    }

    private static string DetermineImpact(decimal value)
    {
        if (value >= 0.75m)
        {
            return "Positive";
        }

        if (value >= 0.45m)
        {
            return "Neutral";
        }

        return "Negative";
    }

    private static IReadOnlyCollection<string> BuildAuditTrail(IEnumerable<AssessmentFactor> factors, RiskGrade grade, AssessmentDecision decision)
    {
        var messages = new List<string>
        {
            $"Derived risk grade {grade}.",
            $"Derived decision {decision}."
        };

        messages.AddRange(factors.Select(f => $"Factor {f.Name} contributed {f.Contribution:F2} ({f.Impact})."));
        return messages;
    }
}
