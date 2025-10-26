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
/// Camunda worker that initiates legal action review for severely delinquent loans.
/// </summary>
public class LegalActionReviewWorker : BackgroundService
{
    private readonly IZeebeClient _zeebeClient;
    private readonly IAuditClient _auditClient;
    private readonly ILogger<LegalActionReviewWorker> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public LegalActionReviewWorker(
        IZeebeClient zeebeClient,
        IAuditClient auditClient,
        ILogger<LegalActionReviewWorker> logger,
        IServiceScopeFactory serviceScopeFactory)
    {
        _zeebeClient = zeebeClient;
        _auditClient = auditClient;
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting LegalActionReviewWorker for job type: legal-action-review");

        _zeebeClient
            .NewWorker()
            .JobType("legal-action-review")
            .Handler(HandleJob)
            .MaxJobsActive(5)
            .Name(nameof(LegalActionReviewWorker))
            .PollInterval(TimeSpan.FromSeconds(1))
            .Timeout(TimeSpan.FromMinutes(2))
            .Open();

        _logger.LogInformation("LegalActionReviewWorker started.");

        // Keep the worker running
        await Task.Delay(Timeout.Infinite, stoppingToken);

        _logger.LogInformation("LegalActionReviewWorker is stopping.");
    }

    private async Task HandleJob(IJobClient jobClient, IJob job)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<LegalActionReviewWorker>>();
        var auditClient = scope.ServiceProvider.GetRequiredService<IAuditClient>();

        logger.LogInformation(
            "Initiating legal action review for workflow instance {WorkflowInstanceKey}",
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

            // TODO: Create legal action review task for Legal/Credit team
            logger.LogWarning(
                "Initiated legal action review for loan {LoanId} ({DPD} DPD)",
                loanId, daysPastDue);

            // Audit event
            await auditClient.LogEventAsync(new AuditEventPayload
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

            logger.LogInformation("Successfully completed legal action review job {JobKey}", job.Key);

            await jobClient.NewCompleteJobCommand(job.Key)
                .Variables("{\"legalReviewInitiated\": true}")
                .Send();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initiate legal action review for job {JobKey}", job.Key);

            await jobClient.NewThrowErrorCommand(job.Key)
                .ErrorCode("legal-review-error")
                .ErrorMessage($"Failed to initiate legal action review: {ex.Message}")
                .Send();
        }
    }
}
