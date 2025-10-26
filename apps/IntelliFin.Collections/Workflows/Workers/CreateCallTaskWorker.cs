using IntelliFin.Collections.Application.DTOs;
using IntelliFin.Shared.Audit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Zeebe.Client;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;

namespace IntelliFin.Collections.Workflows.Workers;

/// <summary>
/// Camunda worker that creates a collections call task for the Collections Officer.
/// </summary>
public class CreateCallTaskWorker : BackgroundService
{
    private readonly IZeebeClient _zeebeClient;
    private readonly IAuditClient _auditClient;
    private readonly ILogger<CreateCallTaskWorker> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public CreateCallTaskWorker(
        IZeebeClient zeebeClient,
        IAuditClient auditClient,
        ILogger<CreateCallTaskWorker> logger,
        IServiceScopeFactory serviceScopeFactory)
    {
        _zeebeClient = zeebeClient;
        _auditClient = auditClient;
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting CreateCallTaskWorker for job type: create-call-task");

        _zeebeClient
            .NewWorker()
            .JobType("create-call-task")
            .Handler(HandleJob)
            .MaxJobsActive(5)
            .Name(nameof(CreateCallTaskWorker))
            .PollInterval(TimeSpan.FromSeconds(1))
            .Timeout(TimeSpan.FromMinutes(2))
            .Open();

        _logger.LogInformation("CreateCallTaskWorker started.");

        // Keep the worker running
        await Task.Delay(Timeout.Infinite, stoppingToken);

        _logger.LogInformation("CreateCallTaskWorker is stopping.");
    }

    private async Task HandleJob(IJobClient jobClient, IJob job)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<CreateCallTaskWorker>>();
        var auditClient = scope.ServiceProvider.GetRequiredService<IAuditClient>();

        logger.LogInformation(
            "Creating collections call task for workflow instance {WorkflowInstanceKey}",
            job.ProcessInstanceKey);

        try
        {
            var variables = JsonSerializer.Deserialize<CollectionsWorkflowVariables>(job.Variables);
            if (variables == null)
            {
                logger.LogError("Failed to deserialize job variables for job key: {JobKey}", job.Key);
                await jobClient.NewThrowErrorCommand(job.Key)
                    .ErrorCode("deserialization-error")
                    .ErrorMessage("Could not read collections workflow variables.")
                    .Send();
                return;
            }

            var loanId = Guid.Parse(variables.LoanId);
            var clientId = Guid.Parse(variables.ClientId);
            var daysPastDue = variables.DaysPastDue;

            // TODO: Create actual call task in Collections Workbench (Story 1.6)
            logger.LogInformation(
                "Created call task for client {ClientId}, loan {LoanId} ({DPD} DPD)",
                clientId, loanId, daysPastDue);

            // Audit event
            await auditClient.LogEventAsync(new AuditEventPayload
            {
                Timestamp = DateTime.UtcNow,
                Actor = "System",
                Action = "CollectionsCallTaskCreated",
                EntityType = "CollectionsWorkflow",
                EntityId = loanId.ToString(),
                CorrelationId = job.ProcessInstanceKey.ToString(),
                EventData = new
                {
                    LoanId = loanId,
                    ClientId = clientId,
                    DaysPastDue = daysPastDue,
                    TaskType = "CollectionsCall"
                }
            });

            logger.LogInformation("Successfully completed call task creation job {JobKey}", job.Key);

            await jobClient.NewCompleteJobCommand(job.Key)
                .Variables("{\"callTaskCreated\": true}")
                .Send();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create call task for job {JobKey}", job.Key);

            await jobClient.NewThrowErrorCommand(job.Key)
                .ErrorCode("call-task-creation-error")
                .ErrorMessage($"Failed to create call task: {ex.Message}")
                .Send();
        }
    }
}
