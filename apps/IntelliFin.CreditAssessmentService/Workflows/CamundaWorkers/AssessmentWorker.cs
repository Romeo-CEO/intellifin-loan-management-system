using IntelliFin.CreditAssessmentService.Models;
using IntelliFin.CreditAssessmentService.Services.Interfaces;
using Zeebe.Client;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;

namespace IntelliFin.CreditAssessmentService.Workflows.CamundaWorkers;

/// <summary>
/// Camunda 8 external task worker that orchestrates assessments.
/// </summary>
public sealed class AssessmentWorker : IHostedService
{
    private readonly IZeebeClient _zeebeClient;
    private readonly ICreditAssessmentService _creditAssessmentService;
    private readonly ILogger<AssessmentWorker> _logger;
    private IJobWorker? _worker;

    public AssessmentWorker(IZeebeClient zeebeClient, ICreditAssessmentService creditAssessmentService, ILogger<AssessmentWorker> logger)
    {
        _zeebeClient = zeebeClient;
        _creditAssessmentService = creditAssessmentService;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _worker = _zeebeClient.NewWorker()
            .JobType("credit-assessment")
            .Handler(HandleJobAsync)
            .Name("credit-assessment-worker")
            .Open();

        _logger.LogInformation("Assessment worker started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _worker?.Dispose();
        return Task.CompletedTask;
    }

    private async Task HandleJobAsync(IJobClient client, IJob job)
    {
        try
        {
            var variables = job.VariablesAsType<AssessmentRequestVariables>();
            await _creditAssessmentService.AssessAsync(new CreditAssessmentRequestDto
            {
                LoanApplicationId = variables.LoanApplicationId,
                ClientId = variables.ClientId,
                RequestedAmount = variables.RequestedAmount,
                TermMonths = variables.TermMonths,
                InterestRate = variables.InterestRate
            });

            await client.NewCompleteJobCommand(job.Key).Send();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Assessment worker failed for job {JobKey}", job.Key);
            await client.NewFailCommand(job.Key)
                .Retries(job.Retries - 1)
                .ErrorMessage(ex.Message)
                .Send();
        }
    }

    private sealed class AssessmentRequestVariables
    {
        public Guid LoanApplicationId { get; init; }
        public Guid ClientId { get; init; }
        public decimal RequestedAmount { get; init; }
        public int TermMonths { get; init; }
        public decimal InterestRate { get; init; }
    }
}
