using IntelliFin.ClientManagement.Infrastructure.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Zeebe.Client;

namespace IntelliFin.ClientManagement.Infrastructure.HealthChecks;

/// <summary>
/// Health check for Camunda/Zeebe gateway connectivity
/// Verifies workers can communicate with workflow orchestration engine
/// </summary>
public class CamundaHealthCheck : IHealthCheck
{
    private readonly ILogger<CamundaHealthCheck> _logger;
    private readonly CamundaOptions _options;

    public CamundaHealthCheck(
        ILogger<CamundaHealthCheck> logger,
        IOptions<CamundaOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return HealthCheckResult.Degraded(
                "Camunda integration is disabled via configuration");
        }

        IZeebeClient? client = null;
        try
        {
            _logger.LogDebug(
                "Checking Camunda health: Gateway={GatewayAddress}",
                _options.GatewayAddress);

            // Create temporary client for health check
            client = ZeebeClient.Builder()
                .UseGatewayAddress(_options.GatewayAddress)
                .UsePlainText() // Use TLS in production
                .Build();

            // Test connection with timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var topology = await client.TopologyRequest()
                .Send(cts.Token);

            var data = new Dictionary<string, object>
            {
                ["gateway_address"] = _options.GatewayAddress,
                ["broker_count"] = topology.Brokers.Count,
                ["cluster_size"] = topology.ClusterSize,
                ["partitions_count"] = topology.PartitionsCount,
                ["replication_factor"] = topology.ReplicationFactor,
                ["worker_name"] = _options.WorkerName
            };

            _logger.LogDebug(
                "Camunda health check passed: {BrokerCount} brokers, {PartitionsCount} partitions",
                topology.Brokers.Count,
                topology.PartitionsCount);

            return HealthCheckResult.Healthy(
                $"Connected to Zeebe cluster with {topology.Brokers.Count} broker(s)",
                data);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Camunda health check timed out connecting to {GatewayAddress}",
                _options.GatewayAddress);

            return HealthCheckResult.Unhealthy(
                $"Timeout connecting to Zeebe gateway at {_options.GatewayAddress}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Camunda health check failed: {ErrorMessage}",
                ex.Message);

            return HealthCheckResult.Unhealthy(
                $"Failed to connect to Zeebe gateway: {ex.Message}",
                ex);
        }
        finally
        {
            // Clean up temporary client
            client?.Dispose();
        }
    }
}
