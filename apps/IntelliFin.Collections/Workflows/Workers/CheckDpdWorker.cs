using IntelliFin.Collections.Application.Services;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;
using Microsoft.Extensions.Logging;

namespace IntelliFin.Collections.Workflows.Workers;

/// <summary>
/// Camunda worker that checks Days Past Due for a loan.
/// </summary>
public class CheckDpdWorker
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CheckDpdWorker> _logger;

    public CheckDpdWorker(
        IServiceProvider serviceProvider,
        ILogger<CheckDpdWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task HandleJob(IJobClient jobClient, IJob job)
    {
        _logger.LogInformation(
            "Checking DPD for workflow instance {WorkflowInstanceKey}",
            job.ProcessInstanceKey);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var scheduleService = scope.ServiceProvider.GetRequiredService<IRepaymentScheduleService>();

            // Extract variables from workflow
            var loanId = Guid.Parse(job.Variables.GetProperty("loanId").GetString() ?? throw new InvalidOperationException("LoanId is required"));

            // Get schedule
            var schedule = await scheduleService.GetScheduleByLoanIdAsync(loanId);
            
            if (schedule == null)
            {
                throw new InvalidOperationException($"No schedule found for loan {loanId}");
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

            // Complete job with DPD result
            await jobClient.NewCompleteJobCommand(job.Key)
                .Variables($"{{\"daysPastDue\": {maxDpd}}}")
                .Send();

            _logger.LogInformation(
                "Loan {LoanId} has {DPD} days past due",
                loanId, maxDpd);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check DPD for job {JobKey}", job.Key);
            
            await jobClient.NewFailCommand(job.Key)
                .Retries(job.Retries - 1)
                .ErrorMessage(ex.Message)
                .Send();
        }
    }
}
