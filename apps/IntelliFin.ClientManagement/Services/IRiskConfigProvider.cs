using IntelliFin.ClientManagement.Common;
using IntelliFin.ClientManagement.Domain.Models;

namespace IntelliFin.ClientManagement.Services;

/// <summary>
/// Provider for risk scoring configuration
/// Retrieves configuration from Vault with caching and hot-reload support
/// </summary>
public interface IRiskConfigProvider
{
    /// <summary>
    /// Gets the current risk scoring configuration
    /// Returns cached configuration if available and valid
    /// </summary>
    Task<Result<RiskScoringConfig>> GetCurrentConfigAsync();

    /// <summary>
    /// Registers a callback to be notified when configuration changes
    /// </summary>
    /// <param name="callback">Action to invoke with new configuration</param>
    void RegisterConfigChangeCallback(Action<RiskScoringConfig> callback);

    /// <summary>
    /// Validates a risk scoring configuration
    /// </summary>
    Task<bool> ValidateConfigAsync(RiskScoringConfig config);

    /// <summary>
    /// Forces a configuration refresh from Vault
    /// Bypasses cache
    /// </summary>
    Task<Result<RiskScoringConfig>> RefreshConfigAsync();

    /// <summary>
    /// Gets the fallback configuration used when Vault is unavailable
    /// </summary>
    RiskScoringConfig GetFallbackConfig();
}
