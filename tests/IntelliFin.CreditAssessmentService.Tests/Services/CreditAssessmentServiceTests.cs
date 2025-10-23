using IntelliFin.CreditAssessmentService.Domain.Enums;
using IntelliFin.CreditAssessmentService.Domain.ValueObjects;
using IntelliFin.CreditAssessmentService.Infrastructure.Persistence;
using IntelliFin.CreditAssessmentService.Models;
using IntelliFin.CreditAssessmentService.Options;
using IntelliFin.CreditAssessmentService.Services;
using IntelliFin.CreditAssessmentService.Services.Interfaces;
using IntelliFin.CreditAssessmentService.Services.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace IntelliFin.CreditAssessmentService.Tests.Services;

public class CreditAssessmentServiceTests : IDisposable
{
    private readonly CreditAssessmentDbContext _dbContext;
    private readonly Mock<IRuleEngine> _ruleEngine = new();
    private readonly Mock<ITransUnionClient> _transUnion = new();
    private readonly Mock<IPmecClient> _pmec = new();
    private readonly Mock<IClientManagementClient> _clientManagement = new();
    private readonly Mock<IExplainabilityService> _explainability = new();
    private readonly Mock<IAuditTrailPublisher> _auditPublisher = new();
    private readonly Mock<IPublishEndpoint> _publishEndpoint = new();
    private readonly CreditAssessmentService _sut;

    public CreditAssessmentServiceTests()
    {
        var options = new DbContextOptionsBuilder<CreditAssessmentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new CreditAssessmentDbContext(options);

        _ruleEngine.Setup(r => r.EvaluateAsync(It.IsAny<RuleEvaluationContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RuleEvaluationResult
            {
                RiskGrade = RiskGrade.B,
                Decision = AssessmentDecision.Approved,
                CreditScore = 720,
                DebtToIncomeRatio = 0.35m,
                PaymentCapacity = 3500,
                Factors = new List<AssessmentFactor>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Name = "debt_to_income",
                        Impact = "Positive",
                        Weight = 0.4m,
                        Contribution = 0.32m,
                        Explanation = "Within threshold"
                    }
                },
                AuditMessages = new[] { "Derived risk grade B" }
            });

        _ruleEngine.Setup(r => r.GetCurrentConfigVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("rules:v1|thresholds:v1");

        _transUnion.Setup(t => t.GetReportAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TransUnionReport { IsAvailable = true, CreditScore = 720, MonthlyObligations = 1500 });

        _pmec.Setup(p => p.GetEmploymentProfileAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PmecEmploymentProfile
            {
                IsEmployed = true,
                GrossSalary = 15000,
                NetSalary = 12000,
                LastPayrollDate = DateTime.UtcNow.AddMonths(-1)
            });

        _clientManagement.Setup(c => c.GetClientProfileAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClientProfile { ClientId = Guid.NewGuid(), IsKycComplete = true });
        _clientManagement.Setup(c => c.GetFinancialProfileAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClientFinancialProfile
            {
                MonthlyIncome = 15000,
                MonthlyExpenses = 6000,
                ExistingDebtPayments = 1500
            });

        _explainability.Setup(e => e.BuildExplanation(It.IsAny<Domain.Entities.CreditAssessment>()))
            .Returns("Approved with grade B");

        _sut = new CreditAssessmentService(
            _dbContext,
            _ruleEngine.Object,
            _transUnion.Object,
            _pmec.Object,
            _clientManagement.Object,
            _explainability.Object,
            _auditPublisher.Object,
            _publishEndpoint.Object,
            Options.Create(new FeatureFlagOptions
            {
                UseStandaloneService = true,
                EnableExplainability = true,
                EnableEventPublishing = true,
                EnableManualOverrideWorkflow = true
            }),
            Mock.Of<ILogger<CreditAssessmentService>>());
    }

    [Fact]
    public async Task AssessAsync_PersistsAssessmentAndPublishesEvents()
    {
        var request = new CreditAssessmentRequestDto
        {
            LoanApplicationId = Guid.NewGuid(),
            ClientId = Guid.NewGuid(),
            RequestedAmount = 50000,
            TermMonths = 60,
            InterestRate = 0.18m
        };

        var assessment = await _sut.AssessAsync(request);

        Assert.Equal(AssessmentDecision.Approved, assessment.Decision);
        Assert.Single(_dbContext.CreditAssessments);
        _auditPublisher.Verify(p => p.PublishAsync(It.IsAny<Domain.Entities.CreditAssessment>(), "AssessmentCompleted", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _publishEndpoint.Verify(p => p.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordManualOverrideAsync_UpdatesAssessment()
    {
        var assessment = await _sut.AssessAsync(new CreditAssessmentRequestDto
        {
            LoanApplicationId = Guid.NewGuid(),
            ClientId = Guid.NewGuid(),
            RequestedAmount = 25000,
            TermMonths = 24,
            InterestRate = 0.16m
        });

        var updated = await _sut.RecordManualOverrideAsync(assessment.Id, new ManualOverrideRequestDto
        {
            Officer = "Officer",
            Reason = "Policy exception",
            Outcome = AssessmentDecision.ManualReview.ToString()
        });

        Assert.Equal(AssessmentDecision.ManualReview, updated.Decision);
        Assert.Equal(Domain.Enums.AssessmentStatus.ManualOverride, updated.Status);
        Assert.Single(updated.Overrides);
    }

    [Fact]
    public async Task InvalidateAsync_MarksAssessmentInvalid()
    {
        var assessment = await _sut.AssessAsync(new CreditAssessmentRequestDto
        {
            LoanApplicationId = Guid.NewGuid(),
            ClientId = Guid.NewGuid(),
            RequestedAmount = 10000,
            TermMonths = 12,
            InterestRate = 0.12m
        });

        await _sut.InvalidateAsync(assessment.Id, "KYC expired");

        var stored = await _dbContext.CreditAssessments.Include(a => a.AuditTrail).FirstAsync();
        Assert.Equal(Domain.Enums.AssessmentStatus.Invalidated, stored.Status);
        Assert.Contains(stored.AuditTrail, a => a.Action == "Invalidated");
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
