using IntelliFin.ClientManagement.Common;
using IntelliFin.ClientManagement.Domain.Entities;

namespace IntelliFin.ClientManagement.Services;

/// <summary>
/// Service for performing AML (Anti-Money Laundering) screening
/// </summary>
public interface IAmlScreeningService
{
    /// <summary>
    /// Performs complete AML screening for a client
    /// Includes sanctions, PEP, and watchlist checks
    /// </summary>
    /// <param name="clientId">Client unique identifier</param>
    /// <param name="kycStatusId">KYC status identifier</param>
    /// <param name="screenedBy">User performing screening</param>
    /// <param name="correlationId">Correlation ID for tracking</param>
    /// <returns>Screening results with overall risk level</returns>
    Task<Result<AmlScreeningResult>> PerformScreeningAsync(
        Guid clientId,
        Guid kycStatusId,
        string screenedBy,
        string? correlationId = null);

    /// <summary>
    /// Performs sanctions list screening
    /// </summary>
    Task<AmlScreening> PerformSanctionsScreeningAsync(
        Guid kycStatusId,
        string clientName,
        string screenedBy,
        string? correlationId = null);

    /// <summary>
    /// Performs PEP (Politically Exposed Person) screening
    /// </summary>
    Task<AmlScreening> PerformPepScreeningAsync(
        Guid kycStatusId,
        string clientName,
        string screenedBy,
        string? correlationId = null);
}

/// <summary>
/// Result of AML screening with workflow variables
/// </summary>
public class AmlScreeningResult
{
    public string OverallRiskLevel { get; set; } = "Clear";
    public bool SanctionsHit { get; set; }
    public bool PepMatch { get; set; }
    public List<AmlScreening> Screenings { get; set; } = new();
    public Dictionary<string, object> Variables { get; set; } = new();
}
