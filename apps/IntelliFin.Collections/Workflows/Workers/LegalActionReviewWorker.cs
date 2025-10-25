using IntelliFin.Shared.Audit;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;
using Microsoft.Extensions.Logging;

namespace IntelliFin.Collections.Workflows.Workers;

/// <summary>
/// Camunda worker that initiates legal action review for severely delinquent loans.
/// </summary>
public class LegalActionReviewWorker
{
    private readonly IAuditClient _auditClient;
    private readonly ILogger<LegalActionReviewWorker> _logger;

    public LegalActionReviewWorker(
        IAuditClient auditClient,
        ILogger<LegalActionReviewWorker> logger)
    {
        _auditClient = auditClient;
        _logger = logger;
    }

    public async Task HandleJob(IJobClient jobClient, IJob job)
    {
        _logger.LogInformation(
            "Initiating legal action review for workflow instance {WorkflowInstanceKey}",
            job.ProcessInstanceKey);

        try
        {
            var loanId = Guid.Parse(job.Variables.GetProperty("loanId").GetString() ?? throw new InvalidOperationException("LoanId is required"));
            var clientId = Guid.Parse(job.Variables.GetProperty("clientId").GetString() ?? throw new InvalidOperationException("ClientId is required"));
            var daysPastDue = job.Variables.GetProperty("daysPastDue").GetInt32();

            // TODO: Create legal action review task for Legal/Credit team
            _logger.LogWarning(
                "Initiated legal action review for loan {LoanId} ({DPD} DPD)",
                loanId, daysPastDue);

            // Audit event
            await _auditClient.LogEventAsync(new AuditEventPayload
            {
                Timestamp = DateTime.UtcNow,
                Actor = "System",
                Action = "LegalActionReviewInitiated",
                EntityType = "CollectionsWorkflow",
                EntityId = loanId.ToString(),
                CorrelationId = job.ProcessInstanceKey.ToString(),
                EventData = new
                {
                    LoanId = loanId,
                    ClientId = clientId,
                    DaysPastDue = daysPastDue,
                    ReviewReason = "90+ days past due - Substandard classification"
                }
            });

            await jobClient.NewCompleteJobCommand(job.Key)
                .Variables("{\"legalReviewInitiated\": true}")
                .Send();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initiate legal action review for job {JobKey}", job.Key);
            
            await jobClient.NewFailCommand(job.Key)
                .Retries(job.Retries - 1)
                .ErrorMessage(ex.Message)
                .Send();
        }
    }
}
