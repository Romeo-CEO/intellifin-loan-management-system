using IntelliFin.Shared.Audit;
using IntelliFin.TreasuryService.Clients;
using IntelliFin.TreasuryService.Models;
using Microsoft.Extensions.Logging;

namespace IntelliFin.TreasuryService.Services;

/// <summary>
/// Service for generating comprehensive audit trails for disbursement activities
/// </summary>
public class DisbursementAuditService : IDisbursementAuditService
{
    private readonly IAdminAuditClient _auditClient;
    private readonly ILogger<DisbursementAuditService> _logger;

    public DisbursementAuditService(
        IAdminAuditClient auditClient,
        ILogger<DisbursementAuditService> logger)
    {
        _auditClient = auditClient;
        _logger = logger;
    }

    /// <summary>
    /// Audit disbursement request received
    /// </summary>
    public async Task AuditDisbursementRequestedAsync(
        string disbursementId,
        string loanId,
        string clientId,
        decimal amount,
        string requestedBy,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var auditEvent = new AuditEventPayload
        {
            Timestamp = DateTime.UtcNow,
            Actor = requestedBy,
            Action = "DisbursementRequested",
            EntityType = "LoanDisbursement",
            EntityId = disbursementId,
            CorrelationId = correlationId,
            EventData = new
            {
                LoanId = loanId,
                ClientId = clientId,
                Amount = amount,
                Currency = "MWK",
                Source = "LoanOrigination",
                Status = "Received"
            }
        };

        await _auditClient.ForwardAuditEventAsync(auditEvent, cancellationToken);

        _logger.LogInformation(
            "Audited disbursement request: DisbursementId={DisbursementId}, LoanId={LoanId}, Amount={Amount}",
            disbursementId, loanId, amount);
    }

    /// <summary>
    /// Audit disbursement validation results
    /// </summary>
    public async Task AuditDisbursementValidatedAsync(
        string disbursementId,
        string validationResult,
        string validationMessage,
        string validatedBy,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var auditEvent = new AuditEventPayload
        {
            Timestamp = DateTime.UtcNow,
            Actor = validatedBy,
            Action = "DisbursementValidated",
            EntityType = "LoanDisbursement",
            EntityId = disbursementId,
            CorrelationId = correlationId,
            EventData = new
            {
                ValidationResult = validationResult,
                ValidationMessage = validationMessage,
                ValidationTimestamp = DateTime.UtcNow,
                SystemValidated = true
            }
        };

        await _auditClient.ForwardAuditEventAsync(auditEvent, cancellationToken);

        _logger.LogDebug(
            "Audited disbursement validation: DisbursementId={DisbursementId}, Result={ValidationResult}",
            disbursementId, validationResult);
    }

    /// <summary>
    /// Audit funding source validation
    /// </summary>
    public async Task AuditFundingValidatedAsync(
        string disbursementId,
        string fundingSource,
        decimal availableAmount,
        decimal requiredAmount,
        string validatedBy,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var auditEvent = new AuditEventPayload
        {
            Timestamp = DateTime.UtcNow,
            Actor = validatedBy,
            Action = "FundingValidated",
            EntityType = "LoanDisbursement",
            EntityId = disbursementId,
            CorrelationId = correlationId,
            EventData = new
            {
                FundingSource = fundingSource,
                AvailableAmount = availableAmount,
                RequiredAmount = requiredAmount,
                SufficientFunds = availableAmount >= requiredAmount,
                ValidationTimestamp = DateTime.UtcNow
            }
        };

        await _auditClient.ForwardAuditEventAsync(auditEvent, cancellationToken);

        _logger.LogDebug(
            "Audited funding validation: DisbursementId={DisbursementId}, Source={FundingSource}, Available={AvailableAmount}",
            disbursementId, fundingSource, availableAmount);
    }

    /// <summary>
    /// Audit disbursement approval
    /// </summary>
    public async Task AuditDisbursementApprovedAsync(
        string disbursementId,
        string approverId,
        string approverName,
        int approvalLevel,
        string comments,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var auditEvent = new AuditEventPayload
        {
            Timestamp = DateTime.UtcNow,
            Actor = approverId,
            Action = "DisbursementApproved",
            EntityType = "LoanDisbursement",
            EntityId = disbursementId,
            CorrelationId = correlationId,
            EventData = new
            {
                ApproverId = approverId,
                ApproverName = approverName,
                ApprovalLevel = approvalLevel,
                Comments = comments,
                ApprovalTimestamp = DateTime.UtcNow,
                ApprovalType = "ManagerApproval"
            }
        };

        await _auditClient.ForwardAuditEventAsync(auditEvent, cancellationToken);

        _logger.LogInformation(
            "Audited disbursement approval: DisbursementId={DisbursementId}, Approver={ApproverName}, Level={ApprovalLevel}",
            disbursementId, approverName, approvalLevel);
    }

    /// <summary>
    /// Audit disbursement rejection
    /// </summary>
    public async Task AuditDisbursementRejectedAsync(
        string disbursementId,
        string approverId,
        string approverName,
        int approvalLevel,
        string rejectionReason,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var auditEvent = new AuditEventPayload
        {
            Timestamp = DateTime.UtcNow,
            Actor = approverId,
            Action = "DisbursementRejected",
            EntityType = "LoanDisbursement",
            EntityId = disbursementId,
            CorrelationId = correlationId,
            EventData = new
            {
                ApproverId = approverId,
                ApproverName = approverName,
                ApprovalLevel = approvalLevel,
                RejectionReason = rejectionReason,
                RejectionTimestamp = DateTime.UtcNow,
                ApprovalType = "ManagerApproval"
            }
        };

        await _auditClient.ForwardAuditEventAsync(auditEvent, cancellationToken);

        _logger.LogWarning(
            "Audited disbursement rejection: DisbursementId={DisbursementId}, Approver={ApproverName}, Reason={RejectionReason}",
            disbursementId, approverName, rejectionReason);
    }

    /// <summary>
    /// Audit disbursement execution
    /// </summary>
    public async Task AuditDisbursementExecutedAsync(
        string disbursementId,
        string transactionId,
        string bankReference,
        string executedBy,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var auditEvent = new AuditEventPayload
        {
            Timestamp = DateTime.UtcNow,
            Actor = executedBy,
            Action = "DisbursementExecuted",
            EntityType = "LoanDisbursement",
            EntityId = disbursementId,
            CorrelationId = correlationId,
            EventData = new
            {
                TreasuryTransactionId = transactionId,
                BankReference = bankReference,
                ExecutionTimestamp = DateTime.UtcNow,
                ExecutionStatus = "Success",
                SystemExecuted = true
            }
        };

        await _auditClient.ForwardAuditEventAsync(auditEvent, cancellationToken);

        _logger.LogInformation(
            "Audited disbursement execution: DisbursementId={DisbursementId}, TransactionId={TransactionId}, BankReference={BankReference}",
            disbursementId, transactionId, bankReference);
    }

    /// <summary>
    /// Audit disbursement execution failure
    /// </summary>
    public async Task AuditDisbursementExecutionFailedAsync(
        string disbursementId,
        string errorMessage,
        string failedBy,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var auditEvent = new AuditEventPayload
        {
            Timestamp = DateTime.UtcNow,
            Actor = failedBy,
            Action = "DisbursementExecutionFailed",
            EntityType = "LoanDisbursement",
            EntityId = disbursementId,
            CorrelationId = correlationId,
            EventData = new
            {
                ErrorMessage = errorMessage,
                FailureTimestamp = DateTime.UtcNow,
                FailureType = "ExecutionError",
                SystemFailed = true
            }
        };

        await _auditClient.ForwardAuditEventAsync(auditEvent, cancellationToken);

        _logger.LogError(
            "Audited disbursement execution failure: DisbursementId={DisbursementId}, Error={ErrorMessage}",
            disbursementId, errorMessage);
    }

    /// <summary>
    /// Audit balance updates
    /// </summary>
    public async Task AuditBalanceUpdatedAsync(
        string branchId,
        decimal oldBalance,
        decimal newBalance,
        decimal changeAmount,
        string changeReason,
        string updatedBy,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var auditEvent = new AuditEventPayload
        {
            Timestamp = DateTime.UtcNow,
            Actor = updatedBy,
            Action = "BranchBalanceUpdated",
            EntityType = "BranchFloat",
            EntityId = branchId,
            CorrelationId = correlationId,
            EventData = new
            {
                BranchId = branchId,
                OldBalance = oldBalance,
                NewBalance = newBalance,
                ChangeAmount = changeAmount,
                ChangeReason = changeReason,
                UpdateTimestamp = DateTime.UtcNow,
                BalanceChangeType = changeAmount < 0 ? "Debit" : "Credit"
            }
        };

        await _auditClient.ForwardAuditEventAsync(auditEvent, cancellationToken);

        _logger.LogInformation(
            "Audited balance update: BranchId={BranchId}, Change={ChangeAmount}, NewBalance={NewBalance}",
            branchId, changeAmount, newBalance);
    }

    /// <summary>
    /// Audit duplicate request detection
    /// </summary>
    public async Task AuditDuplicateRequestDetectedAsync(
        string disbursementId,
        string originalRequestId,
        string detectedBy,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var auditEvent = new AuditEventPayload
        {
            Timestamp = DateTime.UtcNow,
            Actor = detectedBy,
            Action = "DuplicateRequestDetected",
            EntityType = "LoanDisbursement",
            EntityId = disbursementId,
            CorrelationId = correlationId,
            EventData = new
            {
                OriginalRequestId = originalRequestId,
                DuplicateDetectionTimestamp = DateTime.UtcNow,
                DetectionMethod = "IdempotencyCheck",
                SystemDetected = true
            }
        };

        await _auditClient.ForwardAuditEventAsync(auditEvent, cancellationToken);

        _logger.LogWarning(
            "Audited duplicate request detection: DisbursementId={DisbursementId}, OriginalRequest={OriginalRequestId}",
            disbursementId, originalRequestId);
    }

    /// <summary>
    /// Audit workflow state changes
    /// </summary>
    public async Task AuditWorkflowStateChangedAsync(
        string disbursementId,
        string oldState,
        string newState,
        string changedBy,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var auditEvent = new AuditEventPayload
        {
            Timestamp = DateTime.UtcNow,
            Actor = changedBy,
            Action = "WorkflowStateChanged",
            EntityType = "LoanDisbursement",
            EntityId = disbursementId,
            CorrelationId = correlationId,
            EventData = new
            {
                OldState = oldState,
                NewState = newState,
                StateChangeTimestamp = DateTime.UtcNow,
                StateChangeReason = "WorkflowProgression",
                WorkflowEngine = "Camunda"
            }
        };

        await _auditClient.ForwardAuditEventAsync(auditEvent, cancellationToken);

        _logger.LogDebug(
            "Audited workflow state change: DisbursementId={DisbursementId}, {OldState} -> {NewState}",
            disbursementId, oldState, newState);
    }
}

/// <summary>
/// Interface for disbursement audit service
/// </summary>
public interface IDisbursementAuditService
{
    Task AuditDisbursementRequestedAsync(
        string disbursementId,
        string loanId,
        string clientId,
        decimal amount,
        string requestedBy,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task AuditDisbursementValidatedAsync(
        string disbursementId,
        string validationResult,
        string validationMessage,
        string validatedBy,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task AuditFundingValidatedAsync(
        string disbursementId,
        string fundingSource,
        decimal availableAmount,
        decimal requiredAmount,
        string validatedBy,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task AuditDisbursementApprovedAsync(
        string disbursementId,
        string approverId,
        string approverName,
        int approvalLevel,
        string comments,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task AuditDisbursementRejectedAsync(
        string disbursementId,
        string approverId,
        string approverName,
        int approvalLevel,
        string rejectionReason,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task AuditDisbursementExecutedAsync(
        string disbursementId,
        string transactionId,
        string bankReference,
        string executedBy,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task AuditDisbursementExecutionFailedAsync(
        string disbursementId,
        string errorMessage,
        string failedBy,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task AuditBalanceUpdatedAsync(
        string branchId,
        decimal oldBalance,
        decimal newBalance,
        decimal changeAmount,
        string changeReason,
        string updatedBy,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task AuditDuplicateRequestDetectedAsync(
        string disbursementId,
        string originalRequestId,
        string detectedBy,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task AuditWorkflowStateChangedAsync(
        string disbursementId,
        string oldState,
        string newState,
        string changedBy,
        string correlationId,
        CancellationToken cancellationToken = default);
}

