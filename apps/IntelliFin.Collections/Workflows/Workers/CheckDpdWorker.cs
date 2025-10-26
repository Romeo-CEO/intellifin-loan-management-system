using IntelliFin.Collections.Application.DTOs;
using IntelliFin.Collections.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Zeebe.Client;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;

namespace IntelliFin.Collections.Workflows.Workers;

/// <summary>
/// Camunda worker that checks Days Past Due for a loan.
/// </summary>
public class CheckDpdWorker : BackgroundService
{
    private readonly IZeebeClient _zeebeClient;
    private readonly ILogger<CheckDpdWorker> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public CheckDpdWorker(
        IZeebeClient zeebeClient,
        ILogger<CheckDpdWorker> logger,
        IServiceScopeFactory serviceScopeFactory)
    {
        _zeebeClient = zeebeClient;
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting CheckDpdWorker for job type: check-dpd");

        _zeebeClient
            .NewWorker()
            .JobType("check-dpd")
            .Handler(HandleJob)
            .MaxJobsActive(5)
            .Name(nameof(CheckDpdWorker))
            .PollInterval(TimeSpan.FromSeconds(1))
            .Timeout(TimeSpan.FromMinutes(2))
            .Open();

        _logger.LogInformation("CheckDpdWorker started.");

        // Keep the worker running
        await Task.Delay(Timeout.Infinite, stoppingToken);

        _logger.LogInformation("CheckDpdWorker is stopping.");
    }

    private async Task HandleJob(IJobClient jobClient, IJob job)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<CheckDpdWorker>>();
        var scheduleService = scope.ServiceProvider.GetRequiredService<IRepaymentScheduleService>();

        logger.LogInformation(
            "Checking DPD for workflow instance {WorkflowInstanceKey}",
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

            // Extract variables from workflow
            var loanId = Guid.Parse(variables.LoanId);

            // Get schedule
            var schedule = await scheduleService.GetScheduleByLoanIdAsync(loanId);

            if (schedule == null)
            {
                logger.LogError("No schedule found for loan {LoanId}", loanId);
                await jobClient.NewThrowErrorCommand(job.Key)
                    .ErrorCode("schedule-not-found")
                    .ErrorMessage($"No repayment schedule found for loan {loanId}")
                    .Send();
                return;
            }

            // Calculate max DPD
            var today = DateTime.UtcNow.Date;
            var maxDpd = 0;

            foreach (var installment in schedule.Installments.Where(i => i.Status != "Paid"))
            {
                if (installment.DueDate < today)
                {
                    var dpd = (today - installment.DueDate.Date).Days;
                    maxDpd = Math.Max(maxDpd, dpd);
                }
            }

            logger.LogInformation(
                "Loan {LoanId} has {DPD} days past due",
                loanId, maxDpd);

            // Complete job with DPD result
            await jobClient.NewCompleteJobCommand(job.Key)
                .Variables(JsonSerializer.Serialize(new { daysPastDue = maxDpd }))
                .Send();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check DPD for job {JobKey}", job.Key);

            await jobClient.NewThrowErrorCommand(job.Key)
                .ErrorCode("dpd-check-error")
                .ErrorMessage($"Failed to check DPD: {ex.Message}")
                .Send();
        }
    }
}
