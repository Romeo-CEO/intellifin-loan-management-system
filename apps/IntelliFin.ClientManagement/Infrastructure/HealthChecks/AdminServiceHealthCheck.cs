using IntelliFin.Shared.Audit;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace IntelliFin.ClientManagement.Infrastructure.HealthChecks;

public sealed class AdminServiceHealthCheck(IOptionsMonitor<AuditClientOptions> auditOptions) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var options = auditOptions.CurrentValue;
        if (options.BaseAddress is null)
        {
            return HealthCheckResult.Degraded("AuditService BaseAddress not configured");
        }

        try
        {
            using var httpClient = new HttpClient { BaseAddress = options.BaseAddress, Timeout = TimeSpan.FromSeconds(5) };
            using var response = await httpClient.GetAsync("/health/ready", cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy();
            }
            return HealthCheckResult.Unhealthy($"AdminService returned status {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Unable to reach AdminService", ex);
        }
    }
}
