using IntelliFin.AdminService.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntelliFin.AdminService.HealthChecks;

public class KeycloakHealthCheck(IHttpClientFactory httpClientFactory, IOptions<KeycloakOptions> options, ILogger<KeycloakHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var httpClient = httpClientFactory.CreateClient(nameof(KeycloakHealthCheck));
        var keycloakOptions = options.Value;

        if (string.IsNullOrWhiteSpace(keycloakOptions.BaseUrl) || string.IsNullOrWhiteSpace(keycloakOptions.Realm))
        {
            return HealthCheckResult.Degraded("Keycloak configuration is incomplete");
        }

        try
        {
            var wellKnownPath = $"realms/{keycloakOptions.Realm}/.well-known/openid-configuration";
            var response = await httpClient.GetAsync(wellKnownPath, cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy("Keycloak realm is reachable");
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            logger.LogWarning("Keycloak health check failed with status {StatusCode}: {Body}", response.StatusCode, content);
            return HealthCheckResult.Unhealthy($"Keycloak responded with {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Keycloak health check failed");
            return HealthCheckResult.Unhealthy("Unable to reach Keycloak", ex);
        }
    }
}
