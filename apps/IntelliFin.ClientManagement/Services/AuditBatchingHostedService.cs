using IntelliFin.Shared.Audit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using System.Collections.Concurrent;

namespace IntelliFin.ClientManagement.Services;

public sealed class AuditBatchingHostedService : BackgroundService
{
    private readonly ILogger<AuditBatchingHostedService> _logger;
    private readonly IAuditQueue _auditQueue;
    private readonly IAuditClient _auditClient;
    private readonly IOptionsMonitor<AuditBatchingOptions> _optionsMonitor;

    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly AsyncCircuitBreakerPolicy _circuitBreakerPolicy;

    public AuditBatchingHostedService(
        ILogger<AuditBatchingHostedService> logger,
        IAuditQueue auditQueue,
        IAuditClient auditClient,
        IOptionsMonitor<AuditBatchingOptions> optionsMonitor)
    {
        _logger = logger;
        _auditQueue = auditQueue;
        _auditClient = auditClient;
        _optionsMonitor = optionsMonitor;

        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1)),
                (exception, delay, attempt, _) =>
                {
                    _logger.LogWarning(exception, "Retrying audit batch send, attempt {Attempt} after {Delay}", attempt, delay);
                });

        _circuitBreakerPolicy = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30),
                onBreak: (ex, breakDelay) => _logger.LogError(ex, "Audit batching circuit opened for {Delay}", breakDelay),
                onReset: () => _logger.LogInformation("Audit batching circuit reset"));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Audit batching service started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_optionsMonitor.CurrentValue.BatchIntervalSeconds), stoppingToken);
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Audit batching loop error");
            }
        }
        _logger.LogInformation("Audit batching service stopping; final flush");
        await ProcessBatchAsync(CancellationToken.None);
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var batch = new List<AuditEventPayload>();
        var maxBatchSize = Math.Max(1, _optionsMonitor.CurrentValue.BatchSize);

        while (batch.Count < maxBatchSize && _auditQueue.TryRead(out var payload) && payload is not null)
        {
            batch.Add(payload);
        }

        if (batch.Count == 0)
        {
            return;
        }

        try
        {
            await Policy.WrapAsync(_retryPolicy, _circuitBreakerPolicy)
                .ExecuteAsync(async ct =>
                {
                    await _auditClient.LogEventsBatchAsync(batch, ct);
                }, cancellationToken);

            _logger.LogInformation("Sent audit batch with {Count} events", batch.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send audit batch of {Count} events", batch.Count);
            if (_optionsMonitor.CurrentValue.EnableDeadLetterQueue)
            {
                try
                {
                    await AuditEventDeadLetterQueue.WriteAsync(batch, _optionsMonitor.CurrentValue.DeadLetterQueuePath, ex, cancellationToken);
                }
                catch (Exception dlqEx)
                {
                    _logger.LogError(dlqEx, "Failed to write audit events to DLQ");
                }
            }
        }
    }
}
