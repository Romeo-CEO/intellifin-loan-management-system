using IntelliFin.LoanOriginationService.Models;

namespace IntelliFin.LoanOriginationService.Services;

/// <summary>
/// Service for loading loan product configurations from HashiCorp Vault.
/// Provides in-memory caching and EAR compliance validation.
/// </summary>
public interface IVaultProductConfigService
{
    /// <summary>
    /// Retrieves product configuration from Vault with caching and EAR compliance validation.
    /// Configuration is cached for 5 minutes to reduce Vault load.
    /// </summary>
    /// <param name="productCode">Product code (e.g., "GEPL-001", "SMEABL-001")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validated product configuration</returns>
    /// <exception cref="ComplianceException">Thrown when calculated EAR exceeds regulatory limit</exception>
    /// <exception cref="InvalidOperationException">Thrown when product configuration not found in Vault</exception>
    Task<LoanProductConfig> GetProductConfigAsync(string productCode, CancellationToken cancellationToken = default);
}
