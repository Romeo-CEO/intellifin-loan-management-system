using IntelliFin.CreditAssessmentService.Services.Core;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace IntelliFin.CreditAssessmentService.Tests.Unit;

/// <summary>
/// Unit tests for Risk Calculation Engine.
/// Story 1.18: Comprehensive Testing Suite
/// </summary>
public class RiskCalculationEngineTests
{
    private readonly Mock<ILogger<RiskCalculationEngine>> _mockLogger;
    private readonly IRiskCalculationEngine _engine;

    public RiskCalculationEngineTests()
    {
        _mockLogger = new Mock<ILogger<RiskCalculationEngine>>();
        _engine = new RiskCalculationEngine(_mockLogger.Object);
    }

    [Fact]
    public async Task CalculateRisk_WithLowDTI_ShouldPassDTIRule()
    {
        // Arrange
        var assessmentData = new AssessmentData
        {
            RequestedAmount = 50000,
            TermMonths = 24,
            ProductType = "PAYROLL",
            MonthlyIncome = 20000,
            ExistingDebt = 2000,
            CreditScore = 700,
            EmploymentMonths = 24
        };

        // Act
        var result = await _engine.CalculateRiskAsync(assessmentData);

        // Assert
        result.Should().NotBeNull();
        result.DebtToIncomeRatio.Should().BeLessThan(0.40m);
        var dtiRule = result.RulesFired.FirstOrDefault(r => r.RuleId == "BASIC-001");
        dtiRule.Should().NotBeNull();
        dtiRule!.Passed.Should().BeTrue();
    }

    [Fact]
    public async Task CalculateRisk_WithHighDTI_ShouldFailDTIRule()
    {
        // Arrange
        var assessmentData = new AssessmentData
        {
            RequestedAmount = 100000,
            TermMonths = 12,
            ProductType = "PAYROLL",
            MonthlyIncome = 10000,
            ExistingDebt = 5000,
            CreditScore = 650,
            EmploymentMonths = 24
        };

        // Act
        var result = await _engine.CalculateRiskAsync(assessmentData);

        // Assert
        result.Should().NotBeNull();
        result.DebtToIncomeRatio.Should().BeGreaterThan(0.40m);
        var dtiRule = result.RulesFired.FirstOrDefault(r => r.RuleId == "BASIC-001");
        dtiRule.Should().NotBeNull();
        dtiRule!.Passed.Should().BeFalse();
        dtiRule.Impact.Should().Be("Negative");
    }

    [Fact]
    public async Task CalculateRisk_WithHighScore_ShouldReturnGradeA()
    {
        // Arrange
        var assessmentData = new AssessmentData
        {
            RequestedAmount = 30000,
            TermMonths = 24,
            ProductType = "PAYROLL",
            MonthlyIncome = 25000,
            ExistingDebt = 1000,
            CreditScore = 800,
            EmploymentMonths = 36
        };

        // Act
        var result = await _engine.CalculateRiskAsync(assessmentData);

        // Assert
        result.Should().NotBeNull();
        result.Grade.Should().Be("A");
        result.Decision.Should().Be("Approved");
        result.Score.Should().BeGreaterThanOrEqualTo(750);
    }

    [Theory]
    [InlineData(800, "A", "Approved")]
    [InlineData(700, "B", "Approved")]
    [InlineData(600, "C", "ManualReview")]
    [InlineData(500, "D", "ManualReview")]
    [InlineData(400, "F", "Rejected")]
    public async Task CalculateRisk_ShouldAssignCorrectGradeForScore(
        decimal expectedScore, string expectedGrade, string expectedDecision)
    {
        // Arrange - Configure data to produce expected score
        var assessmentData = new AssessmentData
        {
            RequestedAmount = 30000,
            TermMonths = 24,
            ProductType = "PAYROLL",
            MonthlyIncome = 20000,
            ExistingDebt = 1000,
            CreditScore = expectedScore,
            EmploymentMonths = 24
        };

        // Act
        var result = await _engine.CalculateRiskAsync(assessmentData);

        // Assert
        result.Should().NotBeNull();
        result.Grade.Should().Be(expectedGrade);
        result.Decision.Should().Be(expectedDecision);
    }
}
