using System;
using System.Threading;
using System.Threading.Tasks;
using IntelliFin.LoanOriginationService.Models;

namespace IntelliFin.LoanOriginationService.Services;

/// <summary>
/// HTTP client interface for interacting with the Client Management Service to retrieve KYC verification status.
/// </summary>
public interface IClientManagementClient
{
    /// <summary>
    /// Retrieves the KYC and AML verification status for a client.
    /// </summary>
    /// <param name="clientId">The unique identifier of the client.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>A <see cref="ClientVerificationResponse"/> containing the client's verification status.</returns>
    /// <exception cref="Exceptions.KycNotVerifiedException">Thrown when the client is not found or KYC is not verified.</exception>
    /// <exception cref="Exceptions.ClientManagementServiceException">Thrown when the service is unreachable or returns an error.</exception>
    Task<ClientVerificationResponse> GetClientVerificationAsync(
        Guid clientId, 
        CancellationToken cancellationToken = default);
}
