using MassTransit;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IntelliFin.Shared.Infrastructure.Messaging;

public class BusHealthCheck : IHealthCheck
{
    private readonly IBus _bus;
    public BusHealthCheck(IBus bus) => _bus = bus;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        // Simple check - if we can get the bus instance, assume it's healthy
        // In production, you might want to check connection status or send a test message
        return Task.FromResult(_bus != null
            ? HealthCheckResult.Healthy("MassTransit bus is available")
            : HealthCheckResult.Unhealthy("MassTransit bus is not available"));
    }
}
