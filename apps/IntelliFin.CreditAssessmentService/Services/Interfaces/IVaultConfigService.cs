using IntelliFin.CreditAssessmentService.Services.Models;

namespace IntelliFin.CreditAssessmentService.Services.Interfaces;

/// <summary>
/// Provides configuration data sourced from Vault with caching and fallback support.
/// </summary>
public interface IVaultConfigService
{
    Task<VaultRuleConfiguration> GetRuleConfigurationAsync(CancellationToken cancellationToken = default);
    Task<VaultThresholdConfiguration> GetThresholdConfigurationAsync(CancellationToken cancellationToken = default);
    Task<VaultTransUnionCredential> GetTransUnionCredentialsAsync(CancellationToken cancellationToken = default);
}
