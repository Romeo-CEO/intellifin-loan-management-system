using IntelliFin.Shared.Audit;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;
using Microsoft.Extensions.Logging;

namespace IntelliFin.Collections.Workflows.Workers;

/// <summary>
/// Camunda worker that escalates a loan to the Collections Manager.
/// </summary>
public class EscalateToManagerWorker
{
    private readonly IAuditClient _auditClient;
    private readonly ILogger<EscalateToManagerWorker> _logger;

    public EscalateToManagerWorker(
        IAuditClient auditClient,
        ILogger<EscalateToManagerWorker> logger)
    {
        _auditClient = auditClient;
        _logger = logger;
    }

    public async Task HandleJob(IJobClient jobClient, IJob job)
    {
        _logger.LogInformation(
            "Escalating to manager for workflow instance {WorkflowInstanceKey}",
            job.ProcessInstanceKey);

        try
        {
            var loanId = Guid.Parse(job.Variables.GetProperty("loanId").GetString() ?? throw new InvalidOperationException("LoanId is required"));
            var clientId = Guid.Parse(job.Variables.GetProperty("clientId").GetString() ?? throw new InvalidOperationException("ClientId is required"));
            var daysPastDue = job.Variables.GetProperty("daysPastDue").GetInt32();

            // TODO: Create escalation task and notify Collections Manager
            _logger.LogWarning(
                "Escalated loan {LoanId} to Collections Manager ({DPD} DPD)",
                loanId, daysPastDue);

            // Audit event
            await _auditClient.LogEventAsync(new AuditEventPayload
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

            await jobClient.NewCompleteJobCommand(job.Key)
                .Variables("{\"escalatedToManager\": true}")
                .Send();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to escalate to manager for job {JobKey}", job.Key);
            
            await jobClient.NewFailCommand(job.Key)
                .Retries(job.Retries - 1)
                .ErrorMessage(ex.Message)
                .Send();
        }
    }
}
