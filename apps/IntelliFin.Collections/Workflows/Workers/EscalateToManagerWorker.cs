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
/// Camunda worker that escalates a loan to the Collections Manager.
/// </summary>
public class EscalateToManagerWorker : BackgroundService
{
    private readonly IZeebeClient _zeebeClient;
    private readonly IAuditClient _auditClient;
    private readonly ILogger<EscalateToManagerWorker> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public EscalateToManagerWorker(
        IZeebeClient zeebeClient,
        IAuditClient auditClient,
        ILogger<EscalateToManagerWorker> logger,
        IServiceScopeFactory serviceScopeFactory)
    {
        _zeebeClient = zeebeClient;
        _auditClient = auditClient;
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting EscalateToManagerWorker for job type: escalate-to-manager");

        _zeebeClient
            .NewWorker()
            .JobType("escalate-to-manager")
            .Handler(HandleJob)
            .MaxJobsActive(5)
            .Name(nameof(EscalateToManagerWorker))
            .PollInterval(TimeSpan.FromSeconds(1))
            .Timeout(TimeSpan.FromMinutes(2))
            .Open();

        _logger.LogInformation("EscalateToManagerWorker started.");

        // Keep the worker running
        await Task.Delay(Timeout.Infinite, stoppingToken);

        _logger.LogInformation("EscalateToManagerWorker is stopping.");
    }

    private async Task HandleJob(IJobClient jobClient, IJob job)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<EscalateToManagerWorker>>();
        var auditClient = scope.ServiceProvider.GetRequiredService<IAuditClient>();

        logger.LogInformation(
            "Escalating to manager for workflow instance {WorkflowInstanceKey}",
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

            // TODO: Create escalation task and notify Collections Manager
            logger.LogWarning(
                "Escalated loan {LoanId} to Collections Manager ({DPD} DPD)",
                loanId, daysPastDue);

            // Audit event
            await auditClient.LogEventAsync(new AuditEventPayload
            {
                Timestamp = DateTime.UtcNow,
                Actor = "System",
                Action = "LoanEscalatedToManager",
                EntityType = "CollectionsWorkflow",
                EntityId = loanId.ToString(),
                CorrelationId = job.ProcessInstanceKey.ToString(),
                EventData = new
                {
                    LoanId = loanId,
                    ClientId = clientId,
                    DaysPastDue = daysPastDue,
                    EscalationReason = "60+ days past due"
                }
            });

            logger.LogInformation("Successfully completed escalation job {JobKey}", job.Key);

            await jobClient.NewCompleteJobCommand(job.Key)
                .Variables("{\"escalatedToManager\": true}")
                .Send();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to escalate to manager for job {JobKey}", job.Key);

            await jobClient.NewThrowErrorCommand(job.Key)
                .ErrorCode("escalation-error")
                .ErrorMessage($"Failed to escalate to manager: {ex.Message}")
                .Send();
        }
    }
}
