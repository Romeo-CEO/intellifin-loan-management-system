using IntelliFin.LoanOriginationService.Models;
using IntelliFin.LoanOriginationService.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Zeebe.Client;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;

namespace IntelliFin.LoanOriginationService.Workers;

public class InitialValidationWorker : BackgroundService
{
    private readonly IZeebeClient _zeebeClient;
    private readonly ILogger<InitialValidationWorker> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public InitialValidationWorker(
        IZeebeClient zeebeClient,
        ILogger<InitialValidationWorker> logger,
        IServiceScopeFactory serviceScopeFactory)
    {
        _zeebeClient = zeebeClient;
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting InitialValidationWorker for job type: initial-validation");

        _zeebeClient
            .NewWorker()
            .JobType("initial-validation")
            .Handler(HandleInitialValidationJob)
            .MaxJobsActive(5)
            .Name(nameof(InitialValidationWorker))
            .PollInterval(TimeSpan.FromSeconds(1))
            .Timeout(TimeSpan.FromMinutes(2))
            .Open();

        _logger.LogInformation("InitialValidationWorker started.");

        // Keep the worker running
        await Task.Delay(Timeout.Infinite, stoppingToken);

        _logger.LogInformation("InitialValidationWorker is stopping.");
    }

    private async Task HandleInitialValidationJob(IJobClient jobClient, IJob job)
    {
        // The job handler should be quick to execute and not block the worker.
        // We create a new scope to resolve scoped services like our application service.
        using var scope = _serviceScopeFactory.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<InitialValidationWorker>>();
        var loanApplicationService = scope.ServiceProvider.GetRequiredService<ILoanApplicationService>();

        try
        {
            logger.LogInformation("Received job with key: {JobKey}", job.Key);

            var variables = JsonSerializer.Deserialize<LoanApplicationVariables>(job.Variables);
            if (variables == null)
            {
                logger.LogError("Failed to deserialize job variables for job key: {JobKey}", job.Key);
                await jobClient.NewThrowErrorCommand(job.Key)
                    .ErrorCode("deserialization-error")
                    .ErrorMessage("Could not read loan application variables.")
                    .Send();
                return;
            }

            // Call the business logic in the service layer
            var validationResult = await loanApplicationService.ValidateInitialApplicationAsync(variables);

            if (validationResult.IsValid)
            {
                logger.LogInformation("Validation successful for job key: {JobKey}", job.Key);
                await jobClient.NewCompleteJobCommand(job.Key)
                    .Variables(JsonSerializer.Serialize(new { validationSuccessful = true }))
                    .Send();
            }
            else
            {
                logger.LogWarning("Validation failed for job key: {JobKey}. Reason: {Reason}", job.Key, validationResult.ErrorMessage);
                // Throw a business error back to Camunda
                await jobClient.NewThrowErrorCommand(job.Key)
                    .ErrorCode("validation-error")
                    .ErrorMessage(validationResult.ErrorMessage ?? "Validation failed")
                    .Send();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unexpected error occurred while processing job with key: {JobKey}", job.Key);
            // Throw a technical error back to Camunda
            await jobClient.NewThrowErrorCommand(job.Key)
                .ErrorCode("technical-error")
                .ErrorMessage($"An unexpected technical error occurred: {ex.Message}")
                .Send();
        }
    }
}
