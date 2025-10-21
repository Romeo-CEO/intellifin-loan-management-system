using IntelliFin.ClientManagement.Common;
using IntelliFin.ClientManagement.Controllers.DTOs;
using IntelliFin.ClientManagement.Domain.Enums;

namespace IntelliFin.ClientManagement.Services;

/// <summary>
/// Service for managing KYC (Know Your Customer) workflow and state transitions
/// </summary>
public interface IKycWorkflowService
{
    /// <summary>
    /// Initiates KYC process for a client
    /// Creates KycStatus record with Pending state
    /// </summary>
    /// <param name="clientId">Client unique identifier</param>
    /// <param name="initiatedBy">User ID who initiated KYC</param>
    /// <param name="notes">Optional initiation notes</param>
    /// <returns>Created KYC status</returns>
    Task<Result<KycStatusResponse>> InitiateKycAsync(Guid clientId, string initiatedBy, string? notes = null);

    /// <summary>
    /// Updates KYC state with validation of allowed transitions
    /// </summary>
    /// <param name="clientId">Client unique identifier</param>
    /// <param name="newState">Target state</param>
    /// <param name="request">State update request with additional data</param>
    /// <param name="updatedBy">User ID making the update</param>
    /// <returns>Updated KYC status</returns>
    Task<Result<KycStatusResponse>> UpdateKycStateAsync(
        Guid clientId, 
        KycState newState, 
        UpdateKycStateRequest request, 
        string updatedBy);

    /// <summary>
    /// Gets current KYC status for a client
    /// </summary>
    /// <param name="clientId">Client unique identifier</param>
    /// <returns>KYC status or failure if not found</returns>
    Task<Result<KycStatusResponse>> GetKycStatusAsync(Guid clientId);

    /// <summary>
    /// Validates if a state transition is allowed for a client
    /// </summary>
    /// <param name="clientId">Client unique identifier</param>
    /// <param name="newState">Target state</param>
    /// <returns>True if transition is valid</returns>
    Task<Result<bool>> ValidateStateTransitionAsync(Guid clientId, KycState newState);
}
