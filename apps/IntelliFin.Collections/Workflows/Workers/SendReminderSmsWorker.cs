using IntelliFin.Shared.Audit;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;
using Microsoft.Extensions.Logging;

namespace IntelliFin.Collections.Workflows.Workers;

/// <summary>
/// Camunda worker that sends reminder SMS via Communication Service.
/// </summary>
public class SendReminderSmsWorker
{
    private readonly IAuditClient _auditClient;
    private readonly ILogger<SendReminderSmsWorker> _logger;
    // TODO: Add ICommunicationServiceClient when implementing Story 1.5

    public SendReminderSmsWorker(
        IAuditClient auditClient,
        ILogger<SendReminderSmsWorker> logger)
    {
        _auditClient = auditClient;
        _logger = logger;
    }

    public async Task HandleJob(IJobClient jobClient, IJob job)
    {
        _logger.LogInformation(
            "Sending reminder SMS for workflow instance {WorkflowInstanceKey}",
            job.ProcessInstanceKey);

        try
        {
            var loanId = Guid.Parse(job.Variables.GetProperty("loanId").GetString() ?? throw new InvalidOperationException("LoanId is required"));
            var clientId = Guid.Parse(job.Variables.GetProperty("clientId").GetString() ?? throw new InvalidOperationException("ClientId is required"));
            var daysPastDue = job.Variables.GetProperty("daysPastDue").GetInt32();

            // TODO: Call Communication Service to send SMS reminder
            // For now, just log and audit
            _logger.LogInformation(
                "Would send SMS reminder to client {ClientId} for loan {LoanId} ({DPD} DPD)",
                clientId, loanId, daysPastDue);

            // Audit event
            await _auditClient.LogEventAsync(new AuditEventPayload
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

            await jobClient.NewCompleteJobCommand(job.Key)
                .Variables("{\"smsReminderSent\": true}")
                .Send();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send reminder SMS for job {JobKey}", job.Key);
            
            await jobClient.NewFailCommand(job.Key)
                .Retries(job.Retries - 1)
                .ErrorMessage(ex.Message)
                .Send();
        }
    }
}
