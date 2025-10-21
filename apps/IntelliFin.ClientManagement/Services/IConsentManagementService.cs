using IntelliFin.ClientManagement.Common;
using IntelliFin.ClientManagement.Controllers.DTOs;

namespace IntelliFin.ClientManagement.Services;

/// <summary>
/// Service interface for managing communication consent preferences
/// </summary>
public interface IConsentManagementService
{
    /// <summary>
    /// Gets consent preferences for a specific consent type
    /// </summary>
    /// <param name="clientId">Client unique identifier</param>
    /// <param name="consentType">Type of consent (Marketing, Operational, Regulatory)</param>
    /// <returns>Consent response or null if not found</returns>
    Task<Result<ConsentResponse?>> GetConsentAsync(Guid clientId, string consentType);

    /// <summary>
    /// Gets all consent preferences for a client
    /// </summary>
    /// <param name="clientId">Client unique identifier</param>
    /// <returns>List of consent preferences</returns>
    Task<Result<List<ConsentResponse>>> GetAllConsentsAsync(Guid clientId);

    /// <summary>
    /// Updates or creates consent preferences
    /// </summary>
    /// <param name="clientId">Client unique identifier</param>
    /// <param name="request">Updated consent preferences</param>
    /// <param name="userId">User performing the update</param>
    /// <param name="correlationId">Correlation ID for tracing</param>
    /// <returns>Updated consent response</returns>
    Task<Result<ConsentResponse>> UpdateConsentAsync(
        Guid clientId,
        UpdateConsentRequest request,
        string userId,
        string? correlationId = null);

    /// <summary>
    /// Checks if a client has consented to a specific channel for a consent type
    /// </summary>
    /// <param name="clientId">Client unique identifier</param>
    /// <param name="consentType">Type of consent (Marketing, Operational, Regulatory)</param>
    /// <param name="channel">Communication channel (SMS, Email, InApp, Call)</param>
    /// <returns>True if consented, false otherwise</returns>
    Task<bool> CheckConsentAsync(Guid clientId, string consentType, string channel);
}
