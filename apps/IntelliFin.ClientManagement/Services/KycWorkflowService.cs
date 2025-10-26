using IntelliFin.ClientManagement.Common;
using IntelliFin.ClientManagement.Controllers.DTOs;
using IntelliFin.ClientManagement.Domain.BusinessRules;
using IntelliFin.ClientManagement.Domain.Entities;
using IntelliFin.ClientManagement.Domain.Enums;
using IntelliFin.ClientManagement.Domain.Exceptions;
using IntelliFin.ClientManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IntelliFin.ClientManagement.Services;

/// <summary>
/// Implementation of KYC workflow service
/// Manages KYC state transitions and business rules
/// </summary>
public class KycWorkflowService : IKycWorkflowService
{
    private readonly ClientManagementDbContext _context;
    private readonly IAuditService _auditService;
    private readonly ILogger<KycWorkflowService> _logger;

    public KycWorkflowService(
        ClientManagementDbContext context,
        IAuditService auditService,
        ILogger<KycWorkflowService> logger)
    {
        _context = context;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<Result<KycStatusResponse>> InitiateKycAsync(
        Guid clientId, 
        string initiatedBy, 
        string? notes = null)
    {
        try
        {
            // Verify client exists
            var clientExists = await _context.Clients.AnyAsync(c => c.Id == clientId);
            if (!clientExists)
            {
                _logger.LogWarning("Cannot initiate KYC: Client not found: {ClientId}", clientId);
                return Result<KycStatusResponse>.Failure($"Client with ID {clientId} not found");
            }

            // Check if KYC already exists
            var existingKyc = await _context.KycStatuses
                .FirstOrDefaultAsync(k => k.ClientId == clientId);

            if (existingKyc != null && !KycStateMachine.IsTerminalState(existingKyc.CurrentState))
            {
                _logger.LogWarning(
                    "Cannot initiate KYC: KYC already exists in {State} state for client {ClientId}",
                    existingKyc.CurrentState, clientId);
                
                return Result<KycStatusResponse>.Failure(
                    $"KYC already exists in {existingKyc.CurrentState} state. " +
                    "Complete or reject existing KYC before re-initiating.");
            }

            // Create new KYC status
            var kycStatus = new KycStatus
            {
                Id = Guid.NewGuid(),
                ClientId = clientId,
                CurrentState = KycState.Pending,
                KycStartedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.KycStatuses.Add(kycStatus);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "KYC initiated for client {ClientId} by {InitiatedBy}",
                clientId, initiatedBy);

            // Log audit event
            await _auditService.LogAuditEventAsync(
                action: "KycInitiated",
                entityType: "KycStatus",
                entityId: kycStatus.Id.ToString(),
                actor: initiatedBy,
                eventData: new
                {
                    ClientId = clientId,
                    InitialState = KycState.Pending.ToString(),
                    Notes = notes
                });

            return Result<KycStatusResponse>.Success(MapToResponse(kycStatus));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating KYC for client {ClientId}", clientId);
            return Result<KycStatusResponse>.Failure($"Error initiating KYC: {ex.Message}");
        }
    }

    public async Task<Result<KycStatusResponse>> UpdateKycStateAsync(
        Guid clientId,
        KycState newState,
        UpdateKycStateRequest request,
        string updatedBy)
    {
        try
        {
            // Load KYC status
            var kycStatus = await _context.KycStatuses
                .Include(k => k.Client)
                .FirstOrDefaultAsync(k => k.ClientId == clientId);

            if (kycStatus == null)
            {
                _logger.LogWarning(
                    "Cannot update KYC state: KYC status not found for client {ClientId}",
                    clientId);
                return Result<KycStatusResponse>.Failure($"KYC status not found for client {clientId}");
            }

            var previousState = kycStatus.CurrentState;

            // Validate state transition
            if (!KycStateMachine.IsValidTransition(previousState, newState))
            {
                var reason = KycStateMachine.GetInvalidTransitionReason(previousState, newState);
                _logger.LogWarning(
                    "Invalid KYC state transition for client {ClientId}: {FromState} -> {ToState}",
                    clientId, previousState, newState);
                
                throw new InvalidKycStateTransitionException(previousState, newState, reason);
            }

            // Apply business rules before state change
            ValidateBusinessRules(kycStatus, newState, request);

            // Update state
            kycStatus.CurrentState = newState;
            kycStatus.UpdatedAt = DateTime.UtcNow;

            // Update state-specific fields
            UpdateStateSpecificFields(kycStatus, newState, request, updatedBy);

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "KYC state updated for client {ClientId}: {FromState} -> {ToState} by {UpdatedBy}",
                clientId, previousState, newState, updatedBy);

            // Log audit event
            await _auditService.LogAuditEventAsync(
                action: "KycStateChanged",
                entityType: "KycStatus",
                entityId: kycStatus.Id.ToString(),
                actor: updatedBy,
                eventData: new
                {
                    ClientId = clientId,
                    PreviousState = previousState.ToString(),
                    NewState = newState.ToString(),
                    Notes = request.Notes,
                    IsDocumentComplete = kycStatus.IsDocumentComplete,
                    AmlScreeningComplete = kycStatus.AmlScreeningComplete,
                    RequiresEdd = kycStatus.RequiresEdd
                });

            return Result<KycStatusResponse>.Success(MapToResponse(kycStatus));
        }
        catch (InvalidKycStateTransitionException)
        {
            // Re-throw to be handled by controller
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating KYC state for client {ClientId}", clientId);
            return Result<KycStatusResponse>.Failure($"Error updating KYC state: {ex.Message}");
        }
    }

    public async Task<Result<KycStatusResponse>> GetKycStatusAsync(Guid clientId)
    {
        try
        {
            var kycStatus = await _context.KycStatuses
                .Include(k => k.Client)
                .FirstOrDefaultAsync(k => k.ClientId == clientId);

            if (kycStatus == null)
            {
                return Result<KycStatusResponse>.Failure($"KYC status not found for client {clientId}");
            }

            return Result<KycStatusResponse>.Success(MapToResponse(kycStatus));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting KYC status for client {ClientId}", clientId);
            return Result<KycStatusResponse>.Failure($"Error getting KYC status: {ex.Message}");
        }
    }

    public async Task<Result<bool>> ValidateStateTransitionAsync(Guid clientId, KycState newState)
    {
        try
        {
            var kycStatus = await _context.KycStatuses
                .FirstOrDefaultAsync(k => k.ClientId == clientId);

            if (kycStatus == null)
            {
                return Result<bool>.Failure($"KYC status not found for client {clientId}");
            }

            var isValid = KycStateMachine.IsValidTransition(kycStatus.CurrentState, newState);
            return Result<bool>.Success(isValid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating state transition for client {ClientId}", clientId);
            return Result<bool>.Failure($"Error validating state transition: {ex.Message}");
        }
    }

    private void ValidateBusinessRules(KycStatus kycStatus, KycState newState, UpdateKycStateRequest request)
    {
        // Rule: Cannot transition to Completed without document completeness
        if (newState == KycState.Completed)
        {
            // Check if documents will be complete after this update
            var hasNrc = request.HasNrc ?? kycStatus.HasNrc;
            var hasProofOfAddress = request.HasProofOfAddress ?? kycStatus.HasProofOfAddress;
            var hasPayslip = request.HasPayslip ?? kycStatus.HasPayslip;
            var hasEmploymentLetter = request.HasEmploymentLetter ?? kycStatus.HasEmploymentLetter;

            var isDocumentComplete = hasNrc && hasProofOfAddress && (hasPayslip || hasEmploymentLetter);

            if (!isDocumentComplete)
            {
                throw new InvalidOperationException(
                    "Cannot complete KYC: Required documents not uploaded. " +
                    "Required: NRC, Proof of Address, and (Payslip OR Employment Letter)");
            }

            // Rule: Cannot complete without AML screening
            var amlComplete = request.AmlScreeningComplete ?? kycStatus.AmlScreeningComplete;
            if (!amlComplete)
            {
                throw new InvalidOperationException(
                    "Cannot complete KYC: AML screening not completed");
            }
        }

        // Rule: EDD_Required state requires EDD reason
        if (newState == KycState.EDD_Required)
        {
            if (string.IsNullOrWhiteSpace(request.EddReason) && string.IsNullOrWhiteSpace(kycStatus.EddReason))
            {
                throw new InvalidOperationException(
                    "EDD reason is required when escalating to Enhanced Due Diligence");
            }
        }

        // Rule: Cannot transition from InProgress without at least one document
        if (kycStatus.CurrentState == KycState.Pending && newState == KycState.InProgress)
        {
            var hasAnyDocument = kycStatus.HasNrc || kycStatus.HasProofOfAddress || 
                                kycStatus.HasPayslip || kycStatus.HasEmploymentLetter ||
                                request.HasNrc == true || request.HasProofOfAddress == true ||
                                request.HasPayslip == true || request.HasEmploymentLetter == true;

            if (!hasAnyDocument)
            {
                throw new InvalidOperationException(
                    "Cannot transition to InProgress without at least one document uploaded");
            }
        }
    }

    private void UpdateStateSpecificFields(
        KycStatus kycStatus, 
        KycState newState, 
        UpdateKycStateRequest request, 
        string updatedBy)
    {
        // Update document flags if provided
        if (request.HasNrc.HasValue)
            kycStatus.HasNrc = request.HasNrc.Value;
        if (request.HasProofOfAddress.HasValue)
            kycStatus.HasProofOfAddress = request.HasProofOfAddress.Value;
        if (request.HasPayslip.HasValue)
            kycStatus.HasPayslip = request.HasPayslip.Value;
        if (request.HasEmploymentLetter.HasValue)
            kycStatus.HasEmploymentLetter = request.HasEmploymentLetter.Value;

        // Update AML screening if provided
        if (request.AmlScreeningComplete.HasValue)
        {
            kycStatus.AmlScreeningComplete = request.AmlScreeningComplete.Value;
            if (request.AmlScreeningComplete.Value)
            {
                kycStatus.AmlScreenedAt = DateTime.UtcNow;
                kycStatus.AmlScreenedBy = request.AmlScreenedBy ?? updatedBy;
            }
        }

        // Update EDD fields if provided
        if (request.RequiresEdd.HasValue)
            kycStatus.RequiresEdd = request.RequiresEdd.Value;
        if (!string.IsNullOrWhiteSpace(request.EddReason))
            kycStatus.EddReason = request.EddReason;
        if (!string.IsNullOrWhiteSpace(request.EddApprovedBy))
            kycStatus.EddApprovedBy = request.EddApprovedBy;
        if (!string.IsNullOrWhiteSpace(request.EddCeoApprovedBy))
            kycStatus.EddCeoApprovedBy = request.EddCeoApprovedBy;

        // Update Camunda process instance ID if provided
        if (!string.IsNullOrWhiteSpace(request.CamundaProcessInstanceId))
            kycStatus.CamundaProcessInstanceId = request.CamundaProcessInstanceId;

        // State-specific updates
        switch (newState)
        {
            case KycState.Completed:
                kycStatus.KycCompletedAt = DateTime.UtcNow;
                kycStatus.KycCompletedBy = request.CompletedBy ?? updatedBy;
                break;

            case KycState.EDD_Required:
                kycStatus.EddEscalatedAt = DateTime.UtcNow;
                kycStatus.RequiresEdd = true;
                break;

            case KycState.InProgress:
                // No specific fields for InProgress
                break;

            case KycState.Rejected:
                // Rejection handled by state change
                break;
        }

        // If both compliance and CEO approved EDD, set approved timestamp
        if (!string.IsNullOrWhiteSpace(kycStatus.EddApprovedBy) && 
            !string.IsNullOrWhiteSpace(kycStatus.EddCeoApprovedBy) && 
            kycStatus.EddApprovedAt == null)
        {
            kycStatus.EddApprovedAt = DateTime.UtcNow;
        }
    }

    private static KycStatusResponse MapToResponse(KycStatus kycStatus)
    {
        return new KycStatusResponse
        {
            Id = kycStatus.Id,
            ClientId = kycStatus.ClientId,
            ClientName = kycStatus.Client != null
                ? $"{kycStatus.Client.FirstName} {kycStatus.Client.LastName}"
                : null,
            CurrentState = kycStatus.CurrentState.ToString(),
            KycStartedAt = kycStatus.KycStartedAt,
            KycCompletedAt = kycStatus.KycCompletedAt,
            KycCompletedBy = kycStatus.KycCompletedBy,
            CamundaProcessInstanceId = kycStatus.CamundaProcessInstanceId,
            HasNrc = kycStatus.HasNrc,
            HasProofOfAddress = kycStatus.HasProofOfAddress,
            HasPayslip = kycStatus.HasPayslip,
            HasEmploymentLetter = kycStatus.HasEmploymentLetter,
            IsDocumentComplete = kycStatus.IsDocumentComplete,
            AmlScreeningComplete = kycStatus.AmlScreeningComplete,
            AmlScreenedAt = kycStatus.AmlScreenedAt,
            RequiresEdd = kycStatus.RequiresEdd,
            EddReason = kycStatus.EddReason,
            EddEscalatedAt = kycStatus.EddEscalatedAt,
            EddApprovedBy = kycStatus.EddApprovedBy,
            EddCeoApprovedBy = kycStatus.EddCeoApprovedBy,
            EddApprovedAt = kycStatus.EddApprovedAt,
            CreatedAt = kycStatus.CreatedAt,
            UpdatedAt = kycStatus.UpdatedAt
        };
    }
}
