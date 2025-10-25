using IntelliFin.CreditAssessmentService.Models.Requests;
using IntelliFin.CreditAssessmentService.Models.Responses;
using IntelliFin.CreditAssessmentService.Services.Integration;
using IntelliFin.Shared.DomainModels.Data;
using Microsoft.EntityFrameworkCore;

namespace IntelliFin.CreditAssessmentService.Services.Core;

/// <summary>
/// Core credit assessment service implementation.
/// </summary>
public class CreditAssessmentService : ICreditAssessmentService
{
    private readonly LmsDbContext _dbContext;
    private readonly IRiskCalculationEngine _riskEngine;
    private readonly IAdminServiceClient _auditClient;
    private readonly ILogger<CreditAssessmentService> _logger;

    public CreditAssessmentService(
        LmsDbContext dbContext,
        IRiskCalculationEngine riskEngine,
        IAdminServiceClient auditClient,
        ILogger<CreditAssessmentService> logger)
    {
        _dbContext = dbContext;
        _riskEngine = riskEngine;
        _auditClient = auditClient;
        _logger = logger;
    }

    public async Task<AssessmentResponse> PerformAssessmentAsync(
        AssessmentRequest request,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid().ToString();
        
        _logger.LogInformation(
            "Starting credit assessment for loan application {LoanApplicationId}, client {ClientId}, correlation {CorrelationId}",
            request.LoanApplicationId, request.ClientId, correlationId);

        // Log assessment initiation to audit trail
        await _auditClient.LogAuditEventAsync(new Integration.AuditEvent
        {
            EventType = "CreditAssessmentInitiated",
            EntityType = "CreditAssessment",
            EntityId = Guid.NewGuid(), // Will be updated with actual ID
            UserId = userId,
            Action = "InitiateAssessment",
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId,
            Details = new Dictionary<string, object>
            {
                ["LoanApplicationId"] = request.LoanApplicationId,
                ["ClientId"] = request.ClientId,
                ["RequestedAmount"] = request.RequestedAmount,
                ["TermMonths"] = request.TermMonths,
                ["ProductType"] = request.ProductType
            }
        });

        // Gather assessment data
        var assessmentData = new AssessmentData
        {
            RequestedAmount = request.RequestedAmount,
            TermMonths = request.TermMonths,
            ProductType = request.ProductType,
            MonthlyIncome = 15000, // TODO: Get from Client Management (Story 1.5)
            ExistingDebt = 2000,   // TODO: Get from TransUnion (Story 1.6)
            CreditScore = 680,     // TODO: Get from TransUnion (Story 1.6)
            EmploymentMonths = 24, // TODO: Get from PMEC (Story 1.7)
            AdditionalData = request.AdditionalData ?? new()
        };

        // Calculate risk using the engine
        var riskResult = await _riskEngine.CalculateRiskAsync(assessmentData, cancellationToken);

        // Create assessment entity and save to database
        var assessmentEntity = new Shared.DomainModels.Entities.CreditAssessment
        {
            Id = Guid.NewGuid(),
            LoanApplicationId = request.LoanApplicationId,
            RiskGrade = riskResult.Grade,
            CreditScore = riskResult.Score,
            DebtToIncomeRatio = riskResult.DebtToIncomeRatio,
            PaymentCapacity = riskResult.AffordablePayment,
            HasCreditBureauData = assessmentData.CreditScore.HasValue,
            ScoreExplanation = riskResult.Explanation,
            AssessedAt = DateTime.UtcNow,
            AssessedBy = "CreditAssessmentService",
            AssessedByUserId = userId,
            DecisionCategory = riskResult.Decision,
            IsValid = true,
            VaultConfigVersion = "basic-v1.0.0"
        };

        _dbContext.CreditAssessments.Add(assessmentEntity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Map to response
        var assessment = new AssessmentResponse
        {
            AssessmentId = assessmentEntity.Id,
            LoanApplicationId = request.LoanApplicationId,
            ClientId = request.ClientId,
            Decision = riskResult.Decision,
            RiskGrade = riskResult.Grade,
            CompositeScore = riskResult.Score,
            CreditScore = assessmentData.CreditScore,
            DebtToIncomeRatio = riskResult.DebtToIncomeRatio,
            AffordableAmount = riskResult.AffordableAmount,
            AffordablePayment = riskResult.AffordablePayment,
            RulesFired = riskResult.RulesFired,
            Explanation = riskResult.Explanation,
            AssessedAt = assessmentEntity.AssessedAt,
            AssessedByUserId = userId,
            VaultConfigVersion = assessmentEntity.VaultConfigVersion,
            IsValid = true
        };

        _logger.LogInformation(
            "Completed assessment {AssessmentId} with decision {Decision}, correlation {CorrelationId}",
            assessment.AssessmentId, assessment.Decision, correlationId);

        // Log assessment completion to audit trail
        await _auditClient.LogAuditEventAsync(new Integration.AuditEvent
        {
            EventType = "CreditAssessmentCompleted",
            EntityType = "CreditAssessment",
            EntityId = assessment.AssessmentId,
            UserId = userId,
            Action = "CompleteAssessment",
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId,
            Details = new Dictionary<string, object>
            {
                ["Decision"] = assessment.Decision,
                ["RiskGrade"] = assessment.RiskGrade,
                ["CompositeScore"] = assessment.CompositeScore,
                ["DebtToIncomeRatio"] = assessment.DebtToIncomeRatio,
                ["AffordableAmount"] = assessment.AffordableAmount
            }
        });

        return await Task.FromResult(assessment);
    }

    public async Task<AssessmentResponse?> GetAssessmentByIdAsync(
        Guid assessmentId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving assessment {AssessmentId}", assessmentId);

        var entity = await _dbContext.CreditAssessments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == assessmentId, cancellationToken);

        if (entity == null)
        {
            _logger.LogWarning("Assessment {AssessmentId} not found", assessmentId);
            return null;
        }

        // Map entity to response DTO
        var response = new AssessmentResponse
        {
            AssessmentId = entity.Id,
            LoanApplicationId = entity.LoanApplicationId,
            Decision = entity.DecisionCategory ?? "Unknown",
            RiskGrade = entity.RiskGrade,
            CompositeScore = entity.CreditScore,
            CreditScore = entity.HasCreditBureauData ? entity.CreditScore : null,
            DebtToIncomeRatio = entity.DebtToIncomeRatio,
            AffordableAmount = entity.PaymentCapacity,
            AffordablePayment = entity.PaymentCapacity,
            RulesFired = new List<RuleEvaluationDto>(),
            Explanation = entity.ScoreExplanation,
            AssessedAt = entity.AssessedAt,
            AssessedByUserId = entity.AssessedByUserId,
            VaultConfigVersion = entity.VaultConfigVersion,
            IsValid = entity.IsValid,
            InvalidReason = entity.InvalidReason
        };

        return response;
    }

    public async Task<AssessmentResponse?> GetLatestAssessmentForClientAsync(
        Guid clientId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Retrieving latest assessment for client {ClientId}", clientId);

        var entity = await _dbContext.CreditAssessments
            .AsNoTracking()
            .Include(a => a.LoanApplication)
            .Where(a => a.LoanApplication != null && a.LoanApplication.ClientId == clientId)
            .OrderByDescending(a => a.AssessedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity == null)
        {
            _logger.LogInformation("No assessments found for client {ClientId}", clientId);
            return null;
        }

        // Map entity to response DTO
        var response = new AssessmentResponse
        {
            AssessmentId = entity.Id,
            LoanApplicationId = entity.LoanApplicationId,
            Decision = entity.DecisionCategory ?? "Unknown",
            RiskGrade = entity.RiskGrade,
            CompositeScore = entity.CreditScore,
            CreditScore = entity.HasCreditBureauData ? entity.CreditScore : null,
            DebtToIncomeRatio = entity.DebtToIncomeRatio,
            AffordableAmount = entity.PaymentCapacity,
            AffordablePayment = entity.PaymentCapacity,
            RulesFired = new List<RuleEvaluationDto>(),
            Explanation = entity.ScoreExplanation,
            AssessedAt = entity.AssessedAt,
            AssessedByUserId = entity.AssessedByUserId,
            VaultConfigVersion = entity.VaultConfigVersion,
            IsValid = entity.IsValid,
            InvalidReason = entity.InvalidReason
        };

        return response;
    }
}
