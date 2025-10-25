using System.Text.Json;

namespace IntelliFin.CreditAssessmentService.Services.Configuration;

public class VaultConfigService : IVaultConfigService
{
    private readonly ILogger<VaultConfigService> _logger;
    private RuleConfiguration? _cachedConfig;
    private DateTime _lastRefresh = DateTime.MinValue;
    private readonly TimeSpan _refreshInterval = TimeSpan.FromMinutes(5);

    public VaultConfigService(ILogger<VaultConfigService> logger)
    {
        _logger = logger;
    }

    public async Task<RuleConfiguration> GetRulesConfigAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedConfig == null || DateTime.UtcNow - _lastRefresh > _refreshInterval)
        {
            await RefreshConfigurationAsync(cancellationToken);
        }

        return _cachedConfig ?? GetDefaultConfiguration();
    }

    public async Task RefreshConfigurationAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Refreshing Vault configuration");

        try
        {
            // TODO: Implement actual Vault API call
            // For now, use default configuration
            _cachedConfig = GetDefaultConfiguration();
            _lastRefresh = DateTime.UtcNow;

            _logger.LogInformation("Configuration refreshed successfully, version {Version}", _cachedConfig.Version);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing Vault configuration, using cached or default");
            _cachedConfig ??= GetDefaultConfiguration();
        }

        await Task.CompletedTask;
    }

    public string GetCurrentVersion()
    {
        return _cachedConfig?.Version ?? "default-v1.0.0";
    }

    private static RuleConfiguration GetDefaultConfiguration()
    {
        return new RuleConfiguration
        {
            Version = "default-v1.0.0",
            EffectiveDate = DateTime.UtcNow,
            Rules = new Dictionary<string, List<ScoringRule>>
            {
                ["payroll"] = new List<ScoringRule>
                {
                    new()
                    {
                        RuleId = "PR-001",
                        Name = "Debt-to-Income Ratio",
                        Description = "Total debt payments should not exceed 40% of gross income",
                        Weight = 0.30m,
                        Condition = new RuleCondition
                        {
                            Type = "comparison",
                            Expression = "(existingDebt + proposedPayment) / monthlyIncome",
                            Operator = "<=",
                            Threshold = 0.40m
                        },
                        IsActive = true,
                        PassScore = 100,
                        FailScore = -150
                    }
                },
                ["business"] = new List<ScoringRule>()
            },
            Thresholds = new ThresholdConfiguration
            {
                MaxLoanToIncomeRatio = 10.0m,
                MaxDebtToIncomeRatio = 0.40m,
                MaxClientExposure = 500000,
                MinCreditScore = new Dictionary<string, decimal>
                {
                    ["payroll"] = 550,
                    ["business"] = 600
                }
            },
            GradeThresholds = new Dictionary<string, GradeThreshold>
            {
                ["A"] = new() { MinScore = 750, MaxScore = 1000 },
                ["B"] = new() { MinScore = 650, MaxScore = 749 },
                ["C"] = new() { MinScore = 550, MaxScore = 649 },
                ["D"] = new() { MinScore = 450, MaxScore = 549 },
                ["F"] = new() { MinScore = 0, MaxScore = 449 }
            },
            DecisionMatrix = new Dictionary<string, string>
            {
                ["A"] = "Approved",
                ["B"] = "Approved",
                ["C"] = "ManualReview",
                ["D"] = "ManualReview",
                ["F"] = "Rejected"
            }
        };
    }
}
