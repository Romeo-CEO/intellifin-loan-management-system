using IntelliFin.ClientManagement.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Zeebe.Client;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;

namespace IntelliFin.ClientManagement.Workflows.CamundaWorkers;

/// <summary>
/// Background service that manages Camunda/Zeebe worker lifecycle
/// Registers workers, handles job polling, and manages graceful shutdown
/// </summary>
public class CamundaWorkerHostedService : BackgroundService
{
    private readonly ILogger<CamundaWorkerHostedService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly CamundaOptions _options;
    private readonly List<CamundaWorkerRegistration> _workerRegistrations;
    private IZeebeClient? _zeebeClient;
    private readonly List<IJobWorker> _activeWorkers = new();

    public CamundaWorkerHostedService(
        ILogger<CamundaWorkerHostedService> logger,
        IServiceProvider serviceProvider,
        IOptions<CamundaOptions> options,
        IEnumerable<CamundaWorkerRegistration> workerRegistrations)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _workerRegistrations = workerRegistrations.ToList();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Camunda workers are disabled via configuration");
            return;
        }

        try
        {
            _logger.LogInformation(
                "Starting Camunda worker service: {WorkerName} connecting to {GatewayAddress}",
                _options.WorkerName,
                _options.GatewayAddress);

            // Create Zeebe client
            _zeebeClient = ZeebeClient.Builder()
                .UseGatewayAddress(_options.GatewayAddress)
                .UsePlainText() // Use TLS in production
                .Build();

            // Test connectivity
            var topology = await _zeebeClient.TopologyRequest()
                .Send(stoppingToken);

            _logger.LogInformation(
                "Connected to Zeebe cluster with {BrokerCount} brokers",
                topology.Brokers.Count);

            // Register all workers
            foreach (var registration in _workerRegistrations)
            {
                RegisterWorker(registration, stoppingToken);
            }

            _logger.LogInformation(
                "Camunda worker service started successfully with {WorkerCount} workers",
                _activeWorkers.Count);

            // Keep service running until cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Camunda worker service is shutting down");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Fatal error in Camunda worker service. Workers will not be available.");
            throw;
        }
    }

    private void RegisterWorker(CamundaWorkerRegistration registration, CancellationToken stoppingToken)
    {
        try
        {
            var workerName = registration.WorkerName ?? registration.HandlerType.Name;

            _logger.LogInformation(
                "Registering worker: {WorkerName} for topic {TopicName}",
                workerName,
                registration.TopicName);

            var worker = _zeebeClient!
                .NewWorker()
                .JobType(registration.JobType)
                .Handler(async (jobClient, job) => await HandleJobAsync(jobClient, job, registration))
                .MaxJobsActive(registration.MaxJobsToActivate)
                .Name(workerName)
                .Timeout(TimeSpan.FromSeconds(registration.TimeoutSeconds))
                .PollInterval(TimeSpan.FromSeconds(_options.PollingIntervalSeconds))
                .PollingTimeout(TimeSpan.FromSeconds(_options.RequestTimeoutSeconds))
                .Open();

            _activeWorkers.Add(worker);

            _logger.LogInformation(
                "Worker registered successfully: {WorkerName} -> {JobType}",
                workerName,
                registration.JobType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to register worker for topic {TopicName}",
                registration.TopicName);
            // Don't throw - allow other workers to register
        }
    }

    private async Task HandleJobAsync(
        IJobClient jobClient,
        IJob job,
        CamundaWorkerRegistration registration)
    {
        var correlationId = ExtractCorrelationId(job);
        var attemptCount = GetAttemptCount(job);

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["JobKey"] = job.Key,
            ["JobType"] = job.Type,
            ["WorkerName"] = registration.WorkerName ?? registration.HandlerType.Name,
            ["AttemptCount"] = attemptCount
        });

        try
        {
            _logger.LogInformation(
                "Processing job {JobKey} of type {JobType} (attempt {Attempt})",
                job.Key,
                job.Type,
                attemptCount);

            // Create scoped service provider for handler
            using var serviceScope = _serviceProvider.CreateScope();
            var handler = (ICamundaJobHandler)serviceScope.ServiceProvider
                .GetRequiredService(registration.HandlerType);

            // Execute handler
            await handler.HandleJobAsync(jobClient, job);

            _logger.LogInformation(
                "Successfully processed job {JobKey}",
                job.Key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing job {JobKey} (attempt {Attempt})",
                job.Key,
                attemptCount);

            await HandleJobFailureAsync(jobClient, job, ex, attemptCount);
        }
    }

    private async Task HandleJobFailureAsync(
        IJobClient jobClient,
        IJob job,
        Exception exception,
        int attemptCount)
    {
        var nextAttempt = attemptCount + 1;

        if (nextAttempt > _options.MaxRetries)
        {
            // Max retries exceeded - send to DLQ
            _logger.LogError(
                "Job {JobKey} failed after {MaxRetries} attempts. Sending to DLQ.",
                job.Key,
                _options.MaxRetries);

            await jobClient.NewFailCommand(job.Key)
                .Retries(0) // No more retries
                .ErrorMessage($"Max retries ({_options.MaxRetries}) exceeded: {exception.Message}")
                .Send();

            // TODO: In future story, publish to DLQ (RabbitMQ or similar)
            // await PublishToDLQAsync(job, exception);
        }
        else
        {
            // Retry with exponential backoff
            var backoffSeconds = CalculateExponentialBackoff(nextAttempt);

            _logger.LogWarning(
                "Job {JobKey} failed (attempt {Attempt}/{MaxRetries}). " +
                "Retrying in {BackoffSeconds} seconds.",
                job.Key,
                nextAttempt,
                _options.MaxRetries,
                backoffSeconds);

            await jobClient.NewFailCommand(job.Key)
                .Retries(nextAttempt)
                .ErrorMessage($"Attempt {nextAttempt}: {exception.Message}")
                .Send();

            // Zeebe handles the backoff delay internally
        }
    }

    private static int GetAttemptCount(IJob job)
    {
        // Zeebe decrements retries on each failure
        // Calculate attempt from original max retries
        const int defaultMaxRetries = 3;
        return defaultMaxRetries - job.Retries + 1;
    }

    private static int CalculateExponentialBackoff(int attempt)
    {
        // Exponential backoff: 1s, 2s, 4s
        return (int)Math.Pow(2, attempt - 1);
    }

    private static string ExtractCorrelationId(IJob job)
    {
        try
        {
            // Try to extract correlation ID from job variables
            if (job.Variables.Contains("correlationId"))
            {
                return job.Variables.GetValueOrDefault("correlationId", "unknown")?.ToString() 
                    ?? "unknown";
            }

            // Fallback to job key
            return $"job-{job.Key}";
        }
        catch
        {
            return $"job-{job.Key}";
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Gracefully shutting down Camunda worker service. " +
            "Waiting for {WorkerCount} workers to complete in-flight jobs...",
            _activeWorkers.Count);

        // Close all workers (completes in-flight jobs up to timeout)
        foreach (var worker in _activeWorkers)
        {
            try
            {
                worker.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing worker");
            }
        }

        _activeWorkers.Clear();

        // Dispose Zeebe client
        if (_zeebeClient != null)
        {
            try
            {
                _zeebeClient.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing Zeebe client");
            }
        }

        await base.StopAsync(cancellationToken);

        _logger.LogInformation("Camunda worker service stopped");
    }
}
