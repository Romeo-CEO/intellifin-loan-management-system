using System.Text.Json;
using IntelliFin.LoanOriginationService.Exceptions;
using IntelliFin.LoanOriginationService.Services;
using Zeebe.Client;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;

namespace IntelliFin.LoanOriginationService.Workers;

/// <summary>
/// Zeebe job worker for processing 'generate-agreement' service tasks in the loan origination workflow.
/// Generates loan agreement PDFs using JasperReports and stores them in MinIO.
/// </summary>
public class GenerateAgreementWorker : BackgroundService
{
    private readonly IZeebeClient _zeebeClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<GenerateAgreementWorker> _logger;
    
    /// <summary>
    /// Initializes a new instance of the GenerateAgreementWorker.
    /// </summary>
    public GenerateAgreementWorker(
        IZeebeClient zeebeClient,
        IServiceProvider serviceProvider,
        ILogger<GenerateAgreementWorker> logger)
    {
        _zeebeClient = zeebeClient;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }
    
    /// <summary>
    /// Executes the background service to register the Zeebe job worker.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("GenerateAgreementWorker starting up");
        
        try
        {
            // Register job worker for "generate-agreement" task type
            await _zeebeClient.NewWorker()
                .JobType("generate-agreement")
                .Handler(HandleAgreementGenerationAsync)
                .MaxJobsActive(5) // Process up to 5 agreement generations concurrently
                .Name("agreement-generation-worker")
                .AutoComplete(false) // Manual completion after generation
                .PollInterval(TimeSpan.FromSeconds(1))
                .Timeout(TimeSpan.FromSeconds(10)) // 10-second timeout for JasperReports call
                .Open();
            
            _logger.LogInformation(
                "GenerateAgreementWorker registered successfully for job type 'generate-agreement'");
            
            // Keep service running
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GenerateAgreementWorker");
            throw;
        }
    }
    
    /// <summary>
    /// Handles the agreement generation job from Zeebe.
    /// </summary>
    private async Task HandleAgreementGenerationAsync(
        IJobClient jobClient,
        IJob job)
    {
        Guid applicationId = Guid.Empty;
        
        try
        {
            // Extract applicationId from job variables
            var variables = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(job.Variables);
            
            if (variables == null || !variables.ContainsKey("applicationId"))
            {
                _logger.LogError(
                    "Job {JobKey} missing required variable 'applicationId'",
                    job.Key);
                
                await jobClient.NewThrowErrorCommand(job.Key)
                    .ErrorCode("MISSING_APPLICATION_ID")
                    .ErrorMessage("Required variable 'applicationId' not found in job variables")
                    .Send();
                
                return;
            }
            
            var applicationIdStr = variables["applicationId"].GetString();
            if (!Guid.TryParse(applicationIdStr, out applicationId))
            {
                _logger.LogError(
                    "Job {JobKey} has invalid applicationId format: {ApplicationId}",
                    job.Key, applicationIdStr);
                
                await jobClient.NewThrowErrorCommand(job.Key)
                    .ErrorCode("INVALID_APPLICATION_ID")
                    .ErrorMessage($"Invalid applicationId format: {applicationIdStr}")
                    .Send();
                
                return;
            }
            
            _logger.LogInformation(
                "Processing agreement generation for application {ApplicationId}, Job Key: {JobKey}",
                applicationId, job.Key);
            
            // Create scope for agreement service (scoped service in background worker)
            using var scope = _serviceProvider.CreateScope();
            var agreementService = scope.ServiceProvider.GetRequiredService<IAgreementGenerationService>();
            
            // Generate agreement document
            var agreement = await agreementService.GenerateAgreementAsync(
                applicationId,
                CancellationToken.None);
            
            // Complete job with agreement details as workflow variables
            await jobClient.NewCompleteCommand(job.Key)
                .Variables(JsonSerializer.Serialize(new
                {
                    agreementGenerated = true,
                    agreementHash = agreement.FileHash,
                    agreementMinioPath = agreement.MinioPath,
                    agreementGeneratedAt = agreement.GeneratedAt.ToString("o")
                }))
                .Send();
            
            _logger.LogInformation(
                "Agreement generation completed successfully for application {ApplicationId}, Loan: {LoanNumber}, Job Key: {JobKey}",
                applicationId, agreement.LoanNumber, job.Key);
        }
        catch (AgreementGenerationException ex)
        {
            // JasperReports generation failed - throw workflow error for incident
            _logger.LogError(ex,
                "Agreement generation failed for application {ApplicationId}, Job Key: {JobKey}",
                applicationId, job.Key);
            
            await jobClient.NewThrowErrorCommand(job.Key)
                .ErrorCode("AGREEMENT_GENERATION_FAILED")
                .ErrorMessage($"JasperReports generation failed: {ex.Message}")
                .Send();
        }
        catch (KeyNotFoundException ex)
        {
            // Loan application not found - throw workflow error
            _logger.LogError(ex,
                "Loan application {ApplicationId} not found, Job Key: {JobKey}",
                applicationId, job.Key);
            
            await jobClient.NewThrowErrorCommand(job.Key)
                .ErrorCode("APPLICATION_NOT_FOUND")
                .ErrorMessage($"Loan application {applicationId} not found")
                .Send();
        }
        catch (Exception ex)
        {
            // Unexpected error - fail job with retry
            _logger.LogError(ex,
                "Unexpected error during agreement generation for application {ApplicationId}, Job Key: {JobKey}",
                applicationId, job.Key);
            
            // Calculate remaining retries
            var remainingRetries = job.Retries > 0 ? job.Retries - 1 : 0;
            
            await jobClient.NewFailCommand(job.Key)
                .Retries(remainingRetries)
                .ErrorMessage($"Unexpected error: {ex.Message}")
                .Send();
            
            _logger.LogWarning(
                "Job {JobKey} failed with {RemainingRetries} retries remaining",
                job.Key, remainingRetries);
        }
    }
}
