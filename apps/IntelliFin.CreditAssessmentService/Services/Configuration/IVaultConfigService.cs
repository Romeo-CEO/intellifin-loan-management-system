namespace IntelliFin.CreditAssessmentService.Services.Configuration;

public interface IVaultConfigService
{
    Task<RuleConfiguration> GetRulesConfigAsync(CancellationToken cancellationToken = default);
    Task RefreshConfigurationAsync(CancellationToken cancellationToken = default);
    string GetCurrentVersion();
}

public class RuleConfiguration
{
    public string Version { get; set; } = string.Empty;
    public DateTime EffectiveDate { get; set; }
    public Dictionary<string, List<ScoringRule>> Rules { get; set; } = new();
    public ThresholdConfiguration Thresholds { get; set; } = new();
    public Dictionary<string, GradeThreshold> GradeThresholds { get; set; } = new();
    public Dictionary<string, string> DecisionMatrix { get; set; } = new();
}

public class ScoringRule
{
    public string RuleId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public RuleCondition Condition { get; set; } = new();
    public bool IsActive { get; set; }
    public decimal PassScore { get; set; }
    public decimal FailScore { get; set; }
}

public class RuleCondition
{
    public string Type { get; set; } = string.Empty;
    public string Expression { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public decimal Threshold { get; set; }
}

public class ThresholdConfiguration
{
    public decimal MaxLoanToIncomeRatio { get; set; }
    public decimal MaxDebtToIncomeRatio { get; set; }
    public decimal MaxClientExposure { get; set; }
    public Dictionary<string, decimal> MinCreditScore { get; set; } = new();
}

public class GradeThreshold
{
    public decimal MinScore { get; set; }
    public decimal MaxScore { get; set; }
}
