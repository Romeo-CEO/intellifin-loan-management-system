using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using IntelliFin.LoanOriginationService.Exceptions;
using IntelliFin.LoanOriginationService.Models;
using Microsoft.Extensions.Logging;

namespace IntelliFin.LoanOriginationService.Services;

/// <summary>
/// HTTP client implementation for interacting with the Client Management Service to retrieve KYC verification status.
/// Includes circuit breaker, retry policies, and comprehensive error handling.
/// </summary>
public class ClientManagementClient : IClientManagementClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ClientManagementClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientManagementClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client configured with Polly policies.</param>
    /// <param name="logger">The logger for structured logging.</param>
    public ClientManagementClient(HttpClient httpClient, ILogger<ClientManagementClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<ClientVerificationResponse> GetClientVerificationAsync(
        Guid clientId, 
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger.LogInformation(
                "Retrieving KYC verification status for client {ClientId}", 
                clientId);

            var response = await _httpClient.GetAsync(
                $"/api/clients/{clientId}/verification", 
                cancellationToken);

            var duration = DateTime.UtcNow - startTime;

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning(
                    "Client {ClientId} not found in Client Management Service (Duration: {Duration}ms)", 
                    clientId, 
                    duration.TotalMilliseconds);
                
                throw new KycNotVerifiedException(clientId, "NotFound");
            }

            response.EnsureSuccessStatusCode();

            var verification = await response.Content
                .ReadFromJsonAsync<ClientVerificationResponse>(cancellationToken);

            if (verification == null)
            {
                _logger.LogError(
                    "Received null response from Client Management Service for client {ClientId}", 
                    clientId);
                
                throw new ClientManagementServiceException(
                    $"Unable to deserialize verification response for client {clientId}");
            }

            _logger.LogInformation(
                "Successfully retrieved KYC verification for client {ClientId}: Status={KycStatus}, " +
                "AmlStatus={AmlStatus}, ApprovedAt={KycApprovedAt} (Duration: {Duration}ms)", 
                clientId, 
                verification.KycStatus, 
                verification.AmlStatus, 
                verification.KycApprovedAt, 
                duration.TotalMilliseconds);

            return verification;
        }
        catch (KycNotVerifiedException)
        {
            // Re-throw domain exceptions
            throw;
        }
        catch (HttpRequestException ex)
        {
            var duration = DateTime.UtcNow - startTime;
            
            _logger.LogError(
                ex, 
                "HTTP request failed when retrieving client verification for {ClientId} (Duration: {Duration}ms)", 
                clientId, 
                duration.TotalMilliseconds);
            
            throw new ClientManagementServiceException(
                $"Unable to verify KYC status for client {clientId}. The Client Management Service is unreachable.", 
                ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            var duration = DateTime.UtcNow - startTime;
            
            _logger.LogError(
                ex, 
                "Timeout occurred when retrieving client verification for {ClientId} (Duration: {Duration}ms)", 
                clientId, 
                duration.TotalMilliseconds);
            
            throw new ClientManagementServiceException(
                $"Timeout when verifying KYC status for client {clientId}. The Client Management Service did not respond in time.", 
                ex);
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            
            _logger.LogError(
                ex, 
                "Unexpected error when retrieving client verification for {ClientId} (Duration: {Duration}ms)", 
                clientId, 
                duration.TotalMilliseconds);
            
            throw new ClientManagementServiceException(
                $"Unexpected error when verifying KYC status for client {clientId}", 
                ex);
        }
    }
}
