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
/// Camunda worker that sends reminder SMS via Communication Service.
/// </summary>
public class SendReminderSmsWorker : BackgroundService
{
    private readonly IZeebeClient _zeebeClient;
    private readonly IAuditClient _auditClient;
    private readonly ILogger<SendReminderSmsWorker> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    // TODO: Add ICommunicationServiceClient when implementing Story 1.5

    public SendReminderSmsWorker(
        IZeebeClient zeebeClient,
        IAuditClient auditClient,
        ILogger<SendReminderSmsWorker> logger,
        IServiceScopeFactory serviceScopeFactory)
    {
        _zeebeClient = zeebeClient;
        _auditClient = auditClient;
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting SendReminderSmsWorker for job type: send-reminder-sms");

        _zeebeClient
            .NewWorker()
            .JobType("send-reminder-sms")
            .Handler(HandleJob)
            .MaxJobsActive(5)
            .Name(nameof(SendReminderSmsWorker))
            .PollInterval(TimeSpan.FromSeconds(1))
            .Timeout(TimeSpan.FromMinutes(2))
            .Open();

        _logger.LogInformation("SendReminderSmsWorker started.");

        // Keep the worker running
        await Task.Delay(Timeout.Infinite, stoppingToken);

        _logger.LogInformation("SendReminderSmsWorker is stopping.");
    }

    private async Task HandleJob(IJobClient jobClient, IJob job)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<SendReminderSmsWorker>>();
        var auditClient = scope.ServiceProvider.GetRequiredService<IAuditClient>();

        logger.LogInformation(
            "Sending reminder SMS for workflow instance {WorkflowInstanceKey}",
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

            // TODO: Call Communication Service to send SMS reminder
            // For now, just log and audit
            logger.LogInformation(
                "Would send SMS reminder to client {ClientId} for loan {LoanId} ({DPD} DPD)",
                clientId, loanId, daysPastDue);

            // Audit event
            await auditClient.LogEventAsync(new AuditEventPayload
            {
                Timestamp = DateTime.UtcNow,
                Actor = "System",
                Action = "ReminderSmsSent",
                EntityType = "CollectionsWorkflow",
                EntityId = loanId.ToString(),
                CorrelationId = job.ProcessInstanceKey.ToString(),
                EventData = new
                {
                    LoanId = loanId,
                    ClientId = clientId,
                    DaysPastDue = daysPastDue,
                    WorkflowInstanceKey = job.ProcessInstanceKey
                }
            });

            logger.LogInformation("Successfully completed reminder SMS job {JobKey}", job.Key);

            await jobClient.NewCompleteJobCommand(job.Key)
                .Variables("{\"smsReminderSent\": true}")
                .Send();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send reminder SMS for job {JobKey}", job.Key);

            await jobClient.NewThrowErrorCommand(job.Key)
                .ErrorCode("reminder-sms-error")
                .ErrorMessage($"Failed to send reminder SMS: {ex.Message}")
                .Send();
        }
    }
}
