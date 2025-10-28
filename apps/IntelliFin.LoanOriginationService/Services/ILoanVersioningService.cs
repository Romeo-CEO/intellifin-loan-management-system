using IntelliFin.Shared.DomainModels.Entities;

namespace IntelliFin.LoanOriginationService.Services;

/// <summary>
/// Service for managing loan number generation and versioning.
/// Ensures thread-safe sequential loan numbers and maintains immutable audit history.
/// </summary>
public interface ILoanVersioningService
{
    /// <summary>
    /// Generates a unique loan number for a new application.
    /// Format: {BranchCode}-{Year}-{Sequence} (e.g., "LUS-2025-00123")
    /// </summary>
    /// <param name="branchCode">Branch code identifier (e.g., "LUS", "CHD")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Unique loan number</returns>
    Task<string> GenerateLoanNumberAsync(string branchCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new version of an existing loan application.
    /// Marks the previous version as non-current and stores a JSON snapshot.
    /// </summary>
    /// <param name="loanId">Current loan application ID</param>
    /// <param name="reason">Reason for creating new version</param>
    /// <param name="changes">Dictionary of changed fields</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>New loan application version</returns>
    Task<LoanApplication> CreateNewVersionAsync(
        Guid loanId,
        string reason,
        Dictionary<string, object> changes,
        CancellationToken cancellationToken = default);
}
