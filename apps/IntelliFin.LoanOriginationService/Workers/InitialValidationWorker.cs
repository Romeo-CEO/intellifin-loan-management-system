using IntelliFin.LoanOriginationService.Models;
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

        await _zeebeClient
            .NewWorker()
            .JobType("initial-validation")
            .Handler(HandleInitialValidationJob)
            .MaxJobsActive(5)
            .Name("initial-validation-worker")
            .PollInterval(TimeSpan.FromSeconds(1))
            .Timeout(TimeSpan.FromMinutes(5))
            .Open();

        _logger.LogInformation("InitialValidationWorker started successfully");

        // Keep the worker running until cancellation is requested
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }

        _logger.LogInformation("InitialValidationWorker is stopping");
    }

    private async Task HandleInitialValidationJob(IJobClient jobClient, IJob job)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<InitialValidationWorker>>();

        try
        {
            logger.LogInformation("Received initial validation job with key: {JobKey}", job.Key);

            // Deserialize job variables
            var variables = JsonSerializer.Deserialize<LoanApplicationVariables>(job.Variables);
            if (variables == null)
            {
                logger.LogError("Failed to deserialize job variables for job key: {JobKey}", job.Key);
                await jobClient.NewThrowErrorCommand(job.Key)
                    .ErrorCode("deserialization-error")
                    .ErrorMessage("Failed to deserialize job variables")
                    .Send();
                return;
            }

            logger.LogInformation("Processing loan application validation for amount: {LoanAmount}, product: {ProductType}", 
                variables.LoanAmount, variables.ProductType);

            // Perform validation
            var validationResult = ValidateLoanApplication(variables);

            if (validationResult.IsValid)
            {
                logger.LogInformation("Validation successful for job key: {JobKey}", job.Key);

                // Create variables to send back to the workflow
                var resultVariables = new Dictionary<string, object>
                {
                    ["validationResult"] = validationResult,
                    ["isValid"] = true
                };

                await jobClient.NewCompleteJobCommand(job.Key)
                    .Variables(resultVariables)
                    .Send();
            }
            else
            {
                logger.LogWarning("Validation failed for job key: {JobKey}. Error: {ErrorMessage}", 
                    job.Key, validationResult.ErrorMessage);

                await jobClient.NewThrowErrorCommand(job.Key)
                    .ErrorCode("validation-error")
                    .ErrorMessage(validationResult.ErrorMessage ?? "Validation failed")
                    .Send();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing initial validation job with key: {JobKey}", job.Key);

            await jobClient.NewThrowErrorCommand(job.Key)
                .ErrorCode("processing-error")
                .ErrorMessage($"Error processing validation: {ex.Message}")
                .Send();
        }
    }

    private ValidationResult ValidateLoanApplication(LoanApplicationVariables variables)
    {
        var errors = new List<string>();

        // Validate loan amount
        if (variables.LoanAmount <= 0)
        {
            errors.Add("Loan amount must be greater than zero");
        }

        // Validate product type
        if (string.IsNullOrWhiteSpace(variables.ProductType))
        {
            errors.Add("Product type is required");
        }
        else if (!IsValidProductType(variables.ProductType))
        {
            errors.Add("Product type must be either 'PAYROLL' or 'BUSINESS'");
        }

        // Validate applicant NRC
        if (string.IsNullOrWhiteSpace(variables.ApplicantNrc))
        {
            errors.Add("Applicant NRC is required");
        }

        // Validate branch ID
        if (string.IsNullOrWhiteSpace(variables.BranchId))
        {
            errors.Add("Branch ID is required");
        }

        if (errors.Any())
        {
            return new ValidationResult
            {
                IsValid = false,
                ErrorMessage = string.Join("; ", errors)
            };
        }

        return new ValidationResult
        {
            IsValid = true,
            ErrorMessage = null
        };
    }

    private static bool IsValidProductType(string productType)
    {
        return productType.Equals("PAYROLL", StringComparison.OrdinalIgnoreCase) ||
               productType.Equals("BUSINESS", StringComparison.OrdinalIgnoreCase);
    }
}