using IntelliFin.CreditAssessmentService.Domain.Enums;
using IntelliFin.CreditAssessmentService.Services;
using IntelliFin.CreditAssessmentService.Services.Interfaces;
using IntelliFin.CreditAssessmentService.Services.Models;

namespace IntelliFin.CreditAssessmentService.Tests.RuleEngine;

public class VaultRuleEngineTests
{
    private readonly Mock<IVaultConfigService> _vaultConfigService = new();
    private readonly VaultRuleEngine _sut;

    public VaultRuleEngineTests()
    {
        _vaultConfigService.Setup(s => s.GetRuleConfigurationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VaultRuleConfiguration
            {
                Version = "v-test",
                Rules = new[]
                {
                    new VaultRule { Key = "debt_to_income", Weight = 0.4m },
                    new VaultRule { Key = "credit_score", Weight = 0.4m },
                    new VaultRule { Key = "kyc_completeness", Weight = 0.2m }
                }
            });

        _vaultConfigService.Setup(s => s.GetThresholdConfigurationAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VaultThresholdConfiguration
            {
                Version = "v-threshold",
                DebtToIncomeThreshold = 0.4m,
                GradeThresholds = new Dictionary<string, decimal>
                {
                    ["A"] = 850,
                    ["B"] = 750,
                    ["C"] = 650,
                    ["D"] = 550,
                    ["E"] = 450,
                    ["F"] = 0
                }
            });

        _sut = new VaultRuleEngine(_vaultConfigService.Object, Mock.Of<ILogger<VaultRuleEngine>>());
    }

    [Fact]
    public async Task EvaluateAsync_ComputesScoreAndGrade()
    {
        var context = new RuleEvaluationContext
        {
            DebtToIncomeRatio = 0.35m,
            BureauScore = 780,
            RiskFlags = Array.Empty<string>(),
            FinancialMetrics = new Dictionary<string, decimal> { ["employment_months"] = 48 },
            MonthlyIncome = 12000,
            ExistingDebtPayments = 2000
        };

        var result = await _sut.EvaluateAsync(context);

        Assert.Equal(RiskGrade.B, result.RiskGrade);
        Assert.Equal(AssessmentDecision.Approved, result.Decision);
        Assert.True(result.CreditScore > 700);
        Assert.NotEmpty(result.Factors);
        Assert.Contains(result.AuditMessages, m => m.Contains("Derived risk grade"));
    }

    [Fact]
    public async Task GetCurrentConfigVersionAsync_ReturnsCombinedVersion()
    {
        var version = await _sut.GetCurrentConfigVersionAsync();
        Assert.Equal("rules:v-test|thresholds:v-threshold", version);
    }
}
