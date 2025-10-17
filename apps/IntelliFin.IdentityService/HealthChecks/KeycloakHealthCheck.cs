using IntelliFin.IdentityService.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IntelliFin.IdentityService.HealthChecks;

/// <summary>
/// Health check for Keycloak server connectivity and availability
/// </summary>
public class KeycloakHealthCheck : IHealthCheck
{
    private readonly IKeycloakHttpClient _keycloakClient;
    private readonly ILogger<KeycloakHealthCheck> _logger;

    public KeycloakHealthCheck(
        IKeycloakHttpClient keycloakClient,
        ILogger<KeycloakHealthCheck> logger)
    {
        _keycloakClient = keycloakClient;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Executing Keycloak health check");

            var isHealthy = await _keycloakClient.HealthCheckAsync(cancellationToken);

            if (isHealthy)
            {
                _logger.LogDebug("Keycloak health check passed");
                return HealthCheckResult.Healthy(
                    "Keycloak server is reachable and responding",
                    new Dictionary<string, object>
                    {
                        ["timestamp"] = DateTime.UtcNow,
                        ["service"] = "Keycloak"
                    });
            }

            _logger.LogWarning("Keycloak health check failed - server not responding");
            return HealthCheckResult.Unhealthy(
                "Keycloak server is not responding",
                data: new Dictionary<string, object>
                {
                    ["timestamp"] = DateTime.UtcNow,
                    ["service"] = "Keycloak"
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Keycloak health check encountered an exception");
            return HealthCheckResult.Unhealthy(
                "Keycloak health check failed with exception",
                exception: ex,
                data: new Dictionary<string, object>
                {
                    ["timestamp"] = DateTime.UtcNow,
                    ["service"] = "Keycloak",
                    ["error"] = ex.Message
                });
        }
    }
}

/// <summary>
/// Health check for Keycloak OIDC discovery endpoint
/// </summary>
public class KeycloakDiscoveryHealthCheck : IHealthCheck
{
    private readonly IKeycloakHttpClient _keycloakClient;
    private readonly ILogger<KeycloakDiscoveryHealthCheck> _logger;

    public KeycloakDiscoveryHealthCheck(
        IKeycloakHttpClient keycloakClient,
        ILogger<KeycloakDiscoveryHealthCheck> logger)
    {
        _keycloakClient = keycloakClient;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Executing Keycloak OIDC discovery health check");

            var discovery = await _keycloakClient.GetDiscoveryDocumentAsync(cancellationToken);

            if (discovery != null && !string.IsNullOrEmpty(discovery.Issuer))
            {
                _logger.LogDebug("Keycloak OIDC discovery check passed");
                return HealthCheckResult.Healthy(
                    "Keycloak OIDC discovery endpoint is available",
                    new Dictionary<string, object>
                    {
                        ["timestamp"] = DateTime.UtcNow,
                        ["issuer"] = discovery.Issuer,
                        ["tokenEndpoint"] = discovery.TokenEndpoint,
                        ["grantTypes"] = string.Join(", ", discovery.GrantTypesSupported)
                    });
            }

            _logger.LogWarning("Keycloak OIDC discovery check failed - invalid or missing discovery document");
            return HealthCheckResult.Degraded(
                "Keycloak OIDC discovery endpoint returned invalid data",
                data: new Dictionary<string, object>
                {
                    ["timestamp"] = DateTime.UtcNow
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Keycloak OIDC discovery health check failed with exception");
            return HealthCheckResult.Unhealthy(
                "Keycloak OIDC discovery check failed",
                exception: ex,
                data: new Dictionary<string, object>
                {
                    ["timestamp"] = DateTime.UtcNow,
                    ["error"] = ex.Message
                });
        }
    }
}
