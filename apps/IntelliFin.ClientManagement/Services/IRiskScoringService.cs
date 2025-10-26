using IntelliFin.ClientManagement.Common;
using IntelliFin.ClientManagement.Domain.Entities;
using IntelliFin.ClientManagement.Domain.Models;

namespace IntelliFin.ClientManagement.Services;

/// <summary>
/// Service for computing client risk scores using Vault-managed rules
/// </summary>
public interface IRiskScoringService
{
    /// <summary>
    /// Computes risk score for a client using current Vault rules
    /// Creates new RiskProfile and supersedes previous if exists
    /// </summary>
    /// <param name="clientId">Client unique identifier</param>
    /// <param name="computedBy">User or system computing the risk</param>
    /// <param name="correlationId">Correlation ID for tracking</param>
    /// <returns>New risk profile with score and rating</returns>
    Task<Result<RiskProfile>> ComputeRiskAsync(
        Guid clientId,
        string computedBy,
        string? correlationId = null);

    /// <summary>
    /// Recomputes risk score for a client (manual recalculation)
    /// </summary>
    /// <param name="clientId">Client unique identifier</param>
    /// <param name="reason">Reason for recalculation</param>
    /// <param name="computedBy">User triggering recalculation</param>
    /// <returns>New risk profile</returns>
    Task<Result<RiskProfile>> RecomputeRiskAsync(
        Guid clientId,
        string reason,
        string computedBy);

    /// <summary>
    /// Builds input factors for risk scoring from client data
    /// </summary>
    /// <param name="clientId">Client unique identifier</param>
    /// <returns>Input factors for rules engine</returns>
    Task<Result<InputFactors>> BuildInputFactorsAsync(Guid clientId);

    /// <summary>
    /// Gets risk history for a client (all profiles)
    /// </summary>
    /// <param name="clientId">Client unique identifier</param>
    /// <returns>List of historical risk profiles</returns>
    Task<List<RiskProfile>> GetRiskHistoryAsync(Guid clientId);

    /// <summary>
    /// Gets the current (active) risk profile for a client
    /// </summary>
    /// <param name="clientId">Client unique identifier</param>
    /// <returns>Current risk profile or null if none exists</returns>
    Task<RiskProfile?> GetCurrentRiskProfileAsync(Guid clientId);
}
