using IntelliFin.CreditAssessmentService.Domain.Entities;
using IntelliFin.CreditAssessmentService.Domain.Enums;
using IntelliFin.CreditAssessmentService.Domain.Events;
using IntelliFin.CreditAssessmentService.Domain.ValueObjects;
using IntelliFin.CreditAssessmentService.Infrastructure.Persistence;
using IntelliFin.CreditAssessmentService.Models;
using IntelliFin.CreditAssessmentService.Options;
using IntelliFin.CreditAssessmentService.Services.Interfaces;
using IntelliFin.CreditAssessmentService.Services.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IntelliFin.CreditAssessmentService.Services;

/// <summary>
/// Implements the core orchestration required to perform credit assessments.
/// </summary>
public sealed class CreditAssessmentService : ICreditAssessmentService
{
    private readonly CreditAssessmentDbContext _dbContext;
    private readonly IRuleEngine _ruleEngine;
    private readonly ITransUnionClient _transUnionClient;
    private readonly IPmecClient _pmecClient;
    private readonly IClientManagementClient _clientManagementClient;
    private readonly IExplainabilityService _explainabilityService;
    private readonly IAuditTrailPublisher _auditTrailPublisher;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly FeatureFlagOptions _featureFlags;
    private readonly ILogger<CreditAssessmentService> _logger;

    public CreditAssessmentService(
        CreditAssessmentDbContext dbContext,
        IRuleEngine ruleEngine,
        ITransUnionClient transUnionClient,
        IPmecClient pmecClient,
        IClientManagementClient clientManagementClient,
        IExplainabilityService explainabilityService,
        IAuditTrailPublisher auditTrailPublisher,
        IPublishEndpoint publishEndpoint,
        IOptions<FeatureFlagOptions> featureFlags,
        ILogger<CreditAssessmentService> logger)
    {
        _dbContext = dbContext;
        _ruleEngine = ruleEngine;
        _transUnionClient = transUnionClient;
        _pmecClient = pmecClient;
        _clientManagementClient = clientManagementClient;
        _explainabilityService = explainabilityService;
        _auditTrailPublisher = auditTrailPublisher;
        _publishEndpoint = publishEndpoint;
        _featureFlags = featureFlags.Value;
        _logger = logger;
    }

    public async Task<CreditAssessment> AssessAsync(CreditAssessmentRequestDto request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting credit assessment for LoanApplicationId={LoanApplicationId}", request.LoanApplicationId);

        var clientProfile = await _clientManagementClient.GetClientProfileAsync(request.ClientId, cancellationToken);
        if (!clientProfile.IsKycComplete)
        {
            throw new InvalidOperationException("Client KYC must be complete before performing assessment.");
        }

        var financialProfile = await _clientManagementClient.GetFinancialProfileAsync(request.ClientId, cancellationToken);
        var bureauReport = await _transUnionClient.GetReportAsync(request.ClientId, cancellationToken);
        var pmecProfile = await _pmecClient.GetEmploymentProfileAsync(request.ClientId, cancellationToken);

        var debtToIncome = CalculateDebtToIncomeRatio(request, financialProfile, bureauReport);
        var evaluationContext = BuildEvaluationContext(request, financialProfile, bureauReport, pmecProfile, debtToIncome, clientProfile);
        var evaluationResult = await _ruleEngine.EvaluateAsync(evaluationContext, cancellationToken);
        var vaultVersion = await _ruleEngine.GetCurrentConfigVersionAsync(cancellationToken);

        var assessment = new CreditAssessment
        {
            Id = Guid.NewGuid(),
            LoanApplicationId = request.LoanApplicationId,
            ClientId = request.ClientId,
            Status = AssessmentStatus.Completed,
            AssessedAt = DateTime.UtcNow,
            AssessedBy = "CreditAssessmentService",
            RiskGrade = evaluationResult.RiskGrade,
            Decision = evaluationResult.Decision,
            CreditScore = evaluationResult.CreditScore,
            DebtToIncomeRatio = evaluationResult.DebtToIncomeRatio,
            PaymentCapacity = evaluationResult.PaymentCapacity,
            VaultConfigVersion = vaultVersion,
            HasCreditBureauData = bureauReport.IsAvailable,
            Factors = evaluationResult.Factors.ToList(),
            AuditTrail = evaluationResult.AuditMessages.Select(msg => new AssessmentAuditTrail
            {
                Id = Guid.NewGuid(),
                OccurredAt = DateTime.UtcNow,
                Actor = "RuleEngine",
                Action = "Evaluation",
                Details = msg
            }).ToList()
        };

        if (_featureFlags.EnableExplainability)
        {
            assessment.DecisionReason = _explainabilityService.BuildExplanation(assessment);
        }

        _dbContext.CreditAssessments.Add(assessment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditTrailPublisher.PublishAsync(assessment, "AssessmentCompleted", "Credit assessment completed", cancellationToken);

        if (_featureFlags.EnableEventPublishing)
        {
            await _publishEndpoint.Publish(new AssessmentCompletedEvent(
                assessment.Id,
                assessment.LoanApplicationId,
                assessment.ClientId,
                assessment.RiskGrade,
                assessment.Decision,
                assessment.CreditScore,
                assessment.VaultConfigVersion,
                assessment.AssessedAt,
                assessment.Factors.Select(f => f.Name).ToArray()), cancellationToken);
        }

        _logger.LogInformation("Credit assessment {AssessmentId} completed with decision {Decision}", assessment.Id, assessment.Decision);
        return assessment;
    }

    public async Task<CreditAssessment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.CreditAssessments
            .Include(a => a.Factors)
            .Include(a => a.Overrides)
            .Include(a => a.AuditTrail)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<CreditAssessment>> GetByLoanApplicationAsync(Guid loanApplicationId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.CreditAssessments
            .Where(a => a.LoanApplicationId == loanApplicationId)
            .Include(a => a.Factors)
            .Include(a => a.Overrides)
            .Include(a => a.AuditTrail)
            .OrderByDescending(a => a.AssessedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<CreditAssessment> RecordManualOverrideAsync(Guid assessmentId, ManualOverrideRequestDto request, CancellationToken cancellationToken = default)
    {
        var assessment = await _dbContext.CreditAssessments
            .Include(a => a.Factors)
            .Include(a => a.Overrides)
            .Include(a => a.AuditTrail)
            .FirstOrDefaultAsync(a => a.Id == assessmentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Assessment {assessmentId} not found");

        var manualOverride = new ManualOverride
        {
            Id = Guid.NewGuid(),
            CreditAssessmentId = assessment.Id,
            CreatedAt = DateTime.UtcNow,
            Officer = request.Officer,
            Outcome = request.Outcome,
            Reason = request.Reason
        };

        assessment.Overrides.Add(manualOverride);
        assessment.Status = AssessmentStatus.ManualOverride;
        assessment.Decision = Enum.TryParse<AssessmentDecision>(request.Outcome, out var overrideDecision)
            ? overrideDecision
            : assessment.Decision;

        assessment.AuditTrail.Add(new AssessmentAuditTrail
        {
            Id = Guid.NewGuid(),
            CreditAssessmentId = assessment.Id,
            OccurredAt = DateTime.UtcNow,
            Actor = request.Officer,
            Action = "ManualOverride",
            Details = request.Reason
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditTrailPublisher.PublishAsync(assessment, "ManualOverride", request.Reason, cancellationToken);

        return assessment;
    }

    public async Task InvalidateAsync(Guid assessmentId, string reason, CancellationToken cancellationToken = default)
    {
        var assessment = await _dbContext.CreditAssessments
            .Include(a => a.AuditTrail)
            .FirstOrDefaultAsync(a => a.Id == assessmentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Assessment {assessmentId} not found");

        assessment.Status = AssessmentStatus.Invalidated;
        assessment.AuditTrail.Add(new AssessmentAuditTrail
        {
            Id = Guid.NewGuid(),
            CreditAssessmentId = assessment.Id,
            OccurredAt = DateTime.UtcNow,
            Actor = "KycMonitor",
            Action = "Invalidated",
            Details = reason
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditTrailPublisher.PublishAsync(assessment, "Invalidated", reason, cancellationToken);
    }

    private static decimal CalculateDebtToIncomeRatio(CreditAssessmentRequestDto request, ClientFinancialProfile financialProfile, TransUnionReport bureauReport)
    {
        var existingDebtPayments = financialProfile.ExistingDebtPayments + bureauReport.MonthlyObligations;
        var estimatedPayment = CalculateMonthlyPayment(request.RequestedAmount, request.InterestRate, request.TermMonths);
        var totalDebtPayments = existingDebtPayments + estimatedPayment;
        return financialProfile.MonthlyIncome == 0 ? 1 : totalDebtPayments / financialProfile.MonthlyIncome;
    }

    private static decimal CalculateMonthlyPayment(decimal principal, decimal interestRate, int termMonths)
    {
        if (termMonths <= 0)
        {
            return 0;
        }

        var monthlyRate = interestRate / 12m;
        if (monthlyRate == 0)
        {
            return principal / termMonths;
        }

        var factor = (decimal)Math.Pow(1 + (double)monthlyRate, termMonths);
        return principal * monthlyRate * factor / (decimal)(factor - 1);
    }

    private static RuleEvaluationContext BuildEvaluationContext(
        CreditAssessmentRequestDto request,
        ClientFinancialProfile financialProfile,
        TransUnionReport bureauReport,
        PmecEmploymentProfile pmecProfile,
        decimal debtToIncome,
        ClientProfile clientProfile)
    {
        var financialMetrics = new Dictionary<string, decimal>(financialProfile.AdditionalMetrics)
        {
            ["employment_months"] = pmecProfile.IsEmployed ? (decimal)(DateTime.UtcNow - pmecProfile.LastPayrollDate).TotalDays / 30m : 0m,
            ["net_salary"] = pmecProfile.NetSalary,
            ["gross_salary"] = pmecProfile.GrossSalary
        };

        return new RuleEvaluationContext
        {
            LoanApplicationId = request.LoanApplicationId,
            ClientId = request.ClientId,
            RequestedAmount = request.RequestedAmount,
            TermMonths = request.TermMonths,
            InterestRate = request.InterestRate,
            MonthlyIncome = financialProfile.MonthlyIncome,
            MonthlyExpenses = financialProfile.MonthlyExpenses,
            ExistingDebtPayments = financialProfile.ExistingDebtPayments,
            DebtToIncomeRatio = debtToIncome,
            BureauScore = bureauReport.CreditScore,
            HasBureauData = bureauReport.IsAvailable,
            RiskFlags = clientProfile.RiskFlags,
            FinancialMetrics = financialMetrics
        };
    }
}
