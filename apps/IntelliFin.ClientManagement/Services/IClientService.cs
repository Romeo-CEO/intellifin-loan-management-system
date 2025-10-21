using IntelliFin.ClientManagement.Common;
using IntelliFin.ClientManagement.Controllers.DTOs;

namespace IntelliFin.ClientManagement.Services;

/// <summary>
/// Service interface for client operations
/// </summary>
public interface IClientService
{
    /// <summary>
    /// Creates a new client
    /// </summary>
    Task<Result<ClientResponse>> CreateClientAsync(CreateClientRequest request, string userId);

    /// <summary>
    /// Retrieves a client by ID
    /// </summary>
    Task<Result<ClientResponse>> GetClientByIdAsync(Guid id);

    /// <summary>
    /// Retrieves a client by NRC (case-insensitive)
    /// </summary>
    Task<Result<ClientResponse>> GetClientByNrcAsync(string nrc);

    /// <summary>
    /// Updates an existing client
    /// </summary>
    Task<Result<ClientResponse>> UpdateClientAsync(Guid id, UpdateClientRequest request, string userId);
}
