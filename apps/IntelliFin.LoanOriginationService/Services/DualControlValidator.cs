using IntelliFin.LoanOriginationService.Events;
using IntelliFin.LoanOriginationService.Exceptions;
using IntelliFin.Shared.DomainModels.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace IntelliFin.LoanOriginationService.Services;

/// <summary>
/// Service implementation for validating dual control (segregation of duties) requirements for loan approvals.
/// Enforces that approvers cannot approve loans they created or assessed.
/// </summary>
public class DualControlValidator : IDualControlValidator
{
    private readonly LmsDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<DualControlValidator> _logger;
    
    /// <summary>
    /// Initializes a new instance of the DualControlValidator.
    /// </summary>
    public DualControlValidator(
        LmsDbContext dbContext,
        IPublishEndpoint publishEndpoint,
        IHttpContextAccessor httpContextAccessor,
        ILogger<DualControlValidator> logger)
    {
        _dbContext = dbContext;
        _publishEndpoint = publishEndpoint;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }
    
    /// <inheritdoc />
    public async Task ValidateApprovalAsync(
        Guid applicationId, 
        string approverUserId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Validating dual control for loan {ApplicationId}, Approver: {ApproverUserId}",
            applicationId, approverUserId);
        
        // Fetch loan application with creator and assessor info
        var application = await _dbContext.LoanApplications
            .Include(la => la.CreditAssessments)
            .FirstOrDefaultAsync(la => la.Id == applicationId, cancellationToken);
        
        if (application == null)
        {
            _logger.LogWarning("Loan application {ApplicationId} not found during dual control validation", applicationId);
            throw new KeyNotFoundException($"Loan application {applicationId} not found");
        }
        
        // Extract IP address for audit trail
        var ipAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() 
            ?? "Unknown";
        
        // Check 1: Approver != Creator
        if (!string.IsNullOrEmpty(application.CreatedBy) && 
            application.CreatedBy.Equals(approverUserId, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Dual control violation: Approver {ApproverUserId} is the creator of loan {LoanNumber}",
                approverUserId, application.LoanNumber);
            
            // Publish audit event for violation attempt
            await PublishApprovalAttemptEventAsync(
                application, approverUserId, ipAddress, "BLOCKED_SELF_APPROVAL", cancellationToken);
            
            throw new DualControlViolationException(
                applicationId, 
                approverUserId, 
                "You cannot approve this loan application because you created it. Another user must approve to maintain segregation of duties.");
        }
        
        // Check 2: Approver != Assessor (if credit assessment exists)
        var latestAssessment = application.CreditAssessments?
            .OrderByDescending(ca => ca.AssessedAt)
            .FirstOrDefault();
            
        if (latestAssessment != null)
        {
            // Check against both legacy AssessedBy field and new AssessedByUserId
            var assessorMatch = (!string.IsNullOrEmpty(latestAssessment.AssessedBy) && 
                                 latestAssessment.AssessedBy.Equals(approverUserId, StringComparison.OrdinalIgnoreCase)) ||
                                (latestAssessment.AssessedByUserId.HasValue && 
                                 latestAssessment.AssessedByUserId.ToString().Equals(approverUserId, StringComparison.OrdinalIgnoreCase));
            
            if (assessorMatch)
            {
                _logger.LogWarning(
                    "Dual control violation: Approver {ApproverUserId} is the assessor of loan {LoanNumber}",
                    approverUserId, application.LoanNumber);
                
                // Publish audit event for violation attempt
                await PublishApprovalAttemptEventAsync(
                    application, approverUserId, ipAddress, "BLOCKED_APPROVER_AS_ASSESSOR", cancellationToken);
                
                throw new DualControlViolationException(
                    applicationId, 
                    approverUserId, 
                    "You cannot approve this loan application because you performed the credit assessment. Another user must approve to maintain segregation of duties.");
            }
        }
        
        // Dual control validation passed
        _logger.LogInformation(
            "Dual control validation passed for loan {LoanNumber}, Approver: {ApproverUserId}",
            application.LoanNumber, approverUserId);
        
        // Publish audit event for successful validation
        await PublishApprovalAttemptEventAsync(
            application, approverUserId, ipAddress, "VALIDATION_PASSED", cancellationToken);
    }
    
    /// <summary>
    /// Publishes a LoanApprovalAttempted audit event for tracking all approval attempts.
    /// </summary>
    private async Task PublishApprovalAttemptEventAsync(
        IntelliFin.Shared.DomainModels.Entities.LoanApplication application,
        string approverUserId,
        string ipAddress,
        string outcome,
        CancellationToken cancellationToken)
    {
        var latestAssessment = application.CreditAssessments?
            .OrderByDescending(ca => ca.AssessedAt)
            .FirstOrDefault();
        
        var assessedByUserId = latestAssessment?.AssessedByUserId?.ToString() 
            ?? latestAssessment?.AssessedBy;
        
        var correlationId = Guid.NewGuid();
        
        _logger.LogInformation(
            "Publishing LoanApprovalAttempted event: ApplicationId={ApplicationId}, Outcome={Outcome}, CorrelationId={CorrelationId}",
            application.Id, outcome, correlationId);
        
        await _publishEndpoint.Publish(new LoanApprovalAttempted
        {
            LoanApplicationId = application.Id,
            LoanNumber = application.LoanNumber ?? $"DRAFT-{application.Id}",
            ApproverUserId = approverUserId,
            CreatedByUserId = application.CreatedBy,
            AssessedByUserId = assessedByUserId,
            IpAddress = ipAddress,
            Outcome = outcome,
            AttemptedAt = DateTime.UtcNow,
            CorrelationId = correlationId
        }, cancellationToken);
    }
}
