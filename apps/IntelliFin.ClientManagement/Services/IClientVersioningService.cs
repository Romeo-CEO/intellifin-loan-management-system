using IntelliFin.ClientManagement.Common;
using IntelliFin.ClientManagement.Controllers.DTOs;
using IntelliFin.ClientManagement.Domain.Entities;

namespace IntelliFin.ClientManagement.Services;

/// <summary>
/// Service interface for client versioning operations (SCD-2 temporal tracking)
/// </summary>
public interface IClientVersioningService
{
    /// <summary>
    /// Creates a new version snapshot of a client
    /// </summary>
    Task<Result<ClientVersionResponse>> CreateVersionAsync(
        Client client, 
        string changeReason, 
        string userId, 
        string? ipAddress = null, 
        string? correlationId = null);

    /// <summary>
    /// Retrieves all version history for a client
    /// </summary>
    Task<Result<List<ClientVersionResponse>>> GetVersionHistoryAsync(Guid clientId);

    /// <summary>
    /// Retrieves a specific version by version number
    /// </summary>
    Task<Result<ClientVersionResponse>> GetVersionByNumberAsync(Guid clientId, int versionNumber);

    /// <summary>
    /// Retrieves the version that was valid at a specific point in time
    /// </summary>
    Task<Result<ClientVersionResponse>> GetVersionAtTimestampAsync(Guid clientId, DateTime asOfDate);

    /// <summary>
    /// Closes the current version (sets IsCurrent=false and ValidTo=now)
    /// </summary>
    Task<Result> CloseCurrentVersionAsync(Guid clientId);
}
