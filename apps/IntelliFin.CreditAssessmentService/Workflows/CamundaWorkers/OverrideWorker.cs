using IntelliFin.CreditAssessmentService.Models;
using IntelliFin.CreditAssessmentService.Services.Interfaces;
using Zeebe.Client;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;

namespace IntelliFin.CreditAssessmentService.Workflows.CamundaWorkers;

/// <summary>
/// Handles manual override workflow steps from Camunda.
/// </summary>
public sealed class OverrideWorker : IHostedService
{
    private readonly IZeebeClient _zeebeClient;
    private readonly ICreditAssessmentService _creditAssessmentService;
    private readonly ILogger<OverrideWorker> _logger;
    private IJobWorker? _worker;

    public OverrideWorker(IZeebeClient zeebeClient, ICreditAssessmentService creditAssessmentService, ILogger<OverrideWorker> logger)
    {
        _zeebeClient = zeebeClient;
        _creditAssessmentService = creditAssessmentService;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _worker = _zeebeClient.NewWorker()
            .JobType("manual-override")
            .Handler(HandleJobAsync)
            .Name("manual-override-worker")
            .Open();

        _logger.LogInformation("Override worker started");
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
            var variables = job.VariablesAsType<ManualOverrideVariables>();
            await _creditAssessmentService.RecordManualOverrideAsync(
                variables.AssessmentId,
                new ManualOverrideRequestDto
                {
                    Officer = variables.Officer,
                    Reason = variables.Reason,
                    Outcome = variables.Outcome
                });

            await client.NewCompleteJobCommand(job.Key).Send();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Override worker failed for job {JobKey}", job.Key);
            await client.NewFailCommand(job.Key)
                .Retries(job.Retries - 1)
                .ErrorMessage(ex.Message)
                .Send();
        }
    }

    private sealed class ManualOverrideVariables
    {
        public Guid AssessmentId { get; init; }
        public string Officer { get; init; } = string.Empty;
        public string Reason { get; init; } = string.Empty;
        public string Outcome { get; init; } = string.Empty;
    }
}
