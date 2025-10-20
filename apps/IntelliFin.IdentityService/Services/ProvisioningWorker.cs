using IntelliFin.IdentityService.Configuration;
using Polly;

namespace IntelliFin.IdentityService.Services;

/// <summary>
/// Background worker that consumes provisioning commands from the queue
/// and executes them with retry/circuit breaker policies
/// </summary>
public class ProvisioningWorker : BackgroundService
{
    private readonly IBackgroundQueue<ProvisionCommand> _queue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ProvisioningWorker> _logger;
    private readonly IAsyncPolicy _resiliencePolicy;

    // Dead-letter queue for failed commands after retries exhausted
    private readonly List<ProvisionCommand> _deadLetterQueue = new();
    private readonly SemaphoreSlim _deadLetterLock = new(1, 1);

    public ProvisioningWorker(
        IBackgroundQueue<ProvisionCommand> queue,
        IServiceProvider serviceProvider,
        ILogger<ProvisioningWorker> logger)
    {
        _queue = queue;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _resiliencePolicy = ProvisioningResiliencePolicies.CreateCombinedPolicy(logger);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Provisioning Worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Dequeue next command (blocks until message available)
                var command = await _queue.DequeueAsync(stoppingToken);

                // Process command with resilience policies
                await ProcessCommandAsync(command, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
                _logger.LogInformation("Provisioning Worker stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in Provisioning Worker");
                // Wait before continuing to prevent tight loop on persistent errors
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("Provisioning Worker stopped");
    }

    private async Task ProcessCommandAsync(ProvisionCommand command, CancellationToken cancellationToken)
    {
        var correlationId = command.CorrelationId;
        var userId = command.UserId;
        
        _logger.LogInformation(
            "Processing provisioning command for user {UserId}. Reason: {Reason}, CorrelationId: {CorrelationId}",
            userId,
            command.Reason,
            correlationId);

        try
        {
            // Execute provisioning with retry and circuit breaker policies
            await _resiliencePolicy.ExecuteAsync(async (ct) =>
            {
                using var scope = _serviceProvider.CreateScope();
var provisioningService = scope.ServiceProvider.GetRequiredService<IUserProvisioningService>();
                var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();

                try
                {
                    // Log start
                    await auditService.LogAsync(new Models.AuditEvent
                    {
                        Action = "ProvisionStarted",
                        Entity = "User",
                        EntityId = userId,
                        Details = new Dictionary<string, object>
                        {
                            ["Reason"] = command.Reason,
                            ["CorrelationId"] = correlationId
                        },
                        ActorId = "System"
                    }, ct);

                    // Execute provisioning
                    var result = await provisioningService.ProvisionUserAsync(userId, ct);

                    if (result.Success)
                    {
                        // Log success
                        await auditService.LogAsync(new Models.AuditEvent
                        {
                            Action = "ProvisionSucceeded",
                            Entity = "User",
                            EntityId = userId,
                            Details = new Dictionary<string, object>
                            {
                                ["KeycloakUserId"] = result.KeycloakUserId ?? string.Empty,
                                ["Action"] = result.Action.ToString(),
                                ["CorrelationId"] = correlationId
                            },
                            ActorId = "System"
                        }, ct);

                        _logger.LogInformation(
                            "Successfully provisioned user {UserId}. Keycloak ID: {KeycloakUserId}, Action: {Action}, CorrelationId: {CorrelationId}",
                            userId,
                            result.KeycloakUserId,
                            result.Action,
                            correlationId);
                    }
                    else
                    {
                        // Log failure
                        await auditService.LogAsync(new Models.AuditEvent
                        {
                            Action = "ProvisionFailed",
                            Entity = "User",
                            EntityId = userId,
                            Details = new Dictionary<string, object>
                            {
                                ["ErrorMessage"] = result.ErrorMessage ?? string.Empty,
                                ["CorrelationId"] = correlationId
                            },
                            ActorId = "System"
                        }, ct);

                        _logger.LogWarning(
                            "Provisioning failed for user {UserId}. Error: {Error}, CorrelationId: {CorrelationId}",
                            userId,
                            result.ErrorMessage,
                            correlationId);

                        // Throw to trigger retry
                        throw new InvalidOperationException($"Provisioning failed: {result.ErrorMessage}");
                    }
                }
                catch (Exception ex)
                {
                    // Log failure and rethrow for retry policy
                    await auditService.LogAsync(new Models.AuditEvent
                    {
                        Action = "ProvisionFailed",
                        Entity = "User",
                        EntityId = userId,
                        Details = new Dictionary<string, object>
                        {
                            ["Error"] = ex.Message,
                            ["ExceptionType"] = ex.GetType().Name,
                            ["CorrelationId"] = correlationId
                        },
                        ActorId = "System"
                    }, ct);

                    throw;
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            // All retries exhausted, move to dead-letter queue
            _logger.LogError(
                ex,
                "All retries exhausted for provisioning user {UserId}. Moving to dead-letter queue. CorrelationId: {CorrelationId}",
                userId,
                correlationId);

            await _deadLetterLock.WaitAsync(cancellationToken);
            try
            {
                _deadLetterQueue.Add(command);
                
                // Alert on dead-letter (in production, this would trigger monitoring alerts)
                _logger.LogCritical(
                    "ALERT: Provisioning command for user {UserId} moved to dead-letter queue after {QueuedTime}. Reason: {Reason}, CorrelationId: {CorrelationId}, Error: {Error}",
                    userId,
                    DateTime.UtcNow - command.QueuedAt,
                    command.Reason,
                    correlationId,
                    ex.Message);
            }
            finally
            {
                _deadLetterLock.Release();
            }
        }
    }

    /// <summary>
    /// Get dead-letter queue items (for monitoring/admin purposes)
    /// </summary>
    public async Task<IReadOnlyList<ProvisionCommand>> GetDeadLetterQueueAsync()
    {
        await _deadLetterLock.WaitAsync();
        try
        {
            return _deadLetterQueue.ToList();
        }
        finally
        {
            _deadLetterLock.Release();
        }
    }

    /// <summary>
    /// Retry dead-letter queue items (for admin recovery)
    /// </summary>
    public async Task<int> RetryDeadLetterQueueAsync(CancellationToken cancellationToken = default)
    {
        await _deadLetterLock.WaitAsync(cancellationToken);
        List<ProvisionCommand> commandsToRetry;
        try
        {
            commandsToRetry = _deadLetterQueue.ToList();
            _deadLetterQueue.Clear();
        }
        finally
        {
            _deadLetterLock.Release();
        }

        foreach (var command in commandsToRetry)
        {
            await _queue.QueueAsync(command, cancellationToken);
        }

        _logger.LogInformation("Re-queued {Count} commands from dead-letter queue", commandsToRetry.Count);
        return commandsToRetry.Count;
    }
}
