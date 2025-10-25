using IntelliFin.Shared.Audit;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;
using Microsoft.Extensions.Logging;

namespace IntelliFin.Collections.Workflows.Workers;

/// <summary>
/// Camunda worker that creates a collections call task for the Collections Officer.
/// </summary>
public class CreateCallTaskWorker
{
    private readonly IAuditClient _auditClient;
    private readonly ILogger<CreateCallTaskWorker> _logger;

    public CreateCallTaskWorker(
        IAuditClient auditClient,
        ILogger<CreateCallTaskWorker> logger)
    {
        _auditClient = auditClient;
        _logger = logger;
    }

    public async Task HandleJob(IJobClient jobClient, IJob job)
    {
        _logger.LogInformation(
            "Creating collections call task for workflow instance {WorkflowInstanceKey}",
            job.ProcessInstanceKey);

        try
        {
            var loanId = Guid.Parse(job.Variables.GetProperty("loanId").GetString() ?? throw new InvalidOperationException("LoanId is required"));
            var clientId = Guid.Parse(job.Variables.GetProperty("clientId").GetString() ?? throw new InvalidOperationException("ClientId is required"));
            var daysPastDue = job.Variables.GetProperty("daysPastDue").GetInt32();

            // TODO: Create actual call task in Collections Workbench (Story 1.6)
            _logger.LogInformation(
                "Created call task for client {ClientId}, loan {LoanId} ({DPD} DPD)",
                clientId, loanId, daysPastDue);

            // Audit event
            await _auditClient.LogEventAsync(new AuditEventPayload
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

            await jobClient.NewCompleteJobCommand(job.Key)
                .Variables("{\"callTaskCreated\": true}")
                .Send();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create call task for job {JobKey}", job.Key);
            
            await jobClient.NewFailCommand(job.Key)
                .Retries(job.Retries - 1)
                .ErrorMessage(ex.Message)
                .Send();
        }
    }
}
