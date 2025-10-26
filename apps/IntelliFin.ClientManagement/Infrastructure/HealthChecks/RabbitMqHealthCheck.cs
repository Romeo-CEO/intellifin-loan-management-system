using IntelliFin.ClientManagement.Infrastructure.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace IntelliFin.ClientManagement.Infrastructure.HealthChecks;

/// <summary>
/// Health check for RabbitMQ connectivity
/// Verifies message broker is accessible and operational
/// </summary>
public class RabbitMqHealthCheck : IHealthCheck
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqHealthCheck> _logger;

    public RabbitMqHealthCheck(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqHealthCheck> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return HealthCheckResult.Healthy("RabbitMQ integration disabled");
        }

        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _options.Host,
                Port = _options.Port,
                UserName = _options.Username,
                Password = _options.Password,
                VirtualHost = _options.VirtualHost,
                RequestedConnectionTimeout = TimeSpan.FromSeconds(_options.RequestTimeoutSeconds),
                AutomaticRecoveryEnabled = true
            };

            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            // Check if we can declare a test queue (lightweight operation)
            var queueName = $"{_options.QueueName}.health-check";
            channel.QueueDeclare(
                queue: queueName,
                durable: false,
                exclusive: false,
                autoDelete: true,
                arguments: null);

            // Clean up test queue
            channel.QueueDelete(queueName);

            _logger.LogDebug("RabbitMQ health check passed: {Host}:{Port}", _options.Host, _options.Port);

            return HealthCheckResult.Healthy(
                $"RabbitMQ connection successful: {_options.Host}:{_options.Port}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RabbitMQ health check failed");

            return HealthCheckResult.Unhealthy(
                $"RabbitMQ connection failed: {ex.Message}",
                ex);
        }
    }
}
