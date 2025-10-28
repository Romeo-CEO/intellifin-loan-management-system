using IntelliFin.LoanOriginationService.Exceptions;
using IntelliFin.LoanOriginationService.Services;
using System.Text.Json;
using Zeebe.Client;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;

namespace IntelliFin.LoanOriginationService.Workers;

/// <summary>
/// Zeebe job worker that handles KYC verification for loan workflows.
/// Verifies client KYC status and expiration before allowing workflow to proceed.
/// </summary>
public class KycVerificationWorker : BackgroundService
{
    private readonly IZeebeClient _zeebeClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<KycVerificationWorker> _logger;
    private readonly IConfiguration _configuration;

    public KycVerificationWorker(
        IZeebeClient zeebeClient,
        IServiceProvider serviceProvider,
        ILogger<KycVerificationWorker> logger,
        IConfiguration configuration)
    {
        _zeebeClient = zeebeClient;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Starts the worker and registers it with Zeebe to process verify-kyc jobs.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("KycVerificationWorker starting");

        // Register job worker for "verify-kyc" task type
        await _zeebeClient.NewWorker()
            .JobType("verify-kyc")
            .Handler(HandleKycVerificationAsync)
            .MaxJobsActive(10) // Process up to 10 KYC checks concurrently
            .Name("kyc-verification-worker")
            .AutoComplete(false) // Manual completion after validation
            .PollInterval(TimeSpan.FromSeconds(1))
            .Timeout(TimeSpan.FromSeconds(10))
            .Open();

        _logger.LogInformation("KycVerificationWorker registered successfully for job type 'verify-kyc'");

        // Keep service running
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    /// <summary>
    /// Handles KYC verification jobs from the workflow.
    /// Verifies client KYC status and expiration, completing job on success or throwing errors on failure.
    /// </summary>
    /// <param name="jobClient">Zeebe job client for completing or failing jobs</param>
    /// <param name="job">The job to process</param>
    private async Task HandleKycVerificationAsync(IJobClient jobClient, IJob job)
    {
        // Create a new scope for scoped services (IClientManagementClient)
        using var scope = _serviceProvider.CreateScope();
        var clientManagementClient = scope.ServiceProvider.GetRequiredService<IClientManagementClient>();

        Guid clientId = Guid.Empty;

        try
        {
            // Extract clientId from job variables
            var variables = JsonDocument.Parse(job.Variables);
            if (!variables.RootElement.TryGetProperty("clientId", out var clientIdElement))
            {
                await jobClient.NewThrowErrorCommand(job.Key)
                    .ErrorCode("MISSING_CLIENT_ID")
                    .ErrorMessage("Job variables missing required 'clientId' field")
                    .Send();
                _logger.LogError("KYC verification job {JobKey} missing clientId in variables", job.Key);
                return;
            }

            clientId = Guid.Parse(clientIdElement.GetString()!);

            _logger.LogInformation(
                "Processing KYC verification for client {ClientId}, Job Key: {JobKey}",
                clientId, job.Key);

            // Call Client Management Service to get verification status
            var verification = await clientManagementClient.GetClientVerificationAsync(
                clientId, CancellationToken.None);

            // Check KYC status - must be "Approved"
            if (verification.KycStatus != "Approved")
            {
                await jobClient.NewThrowErrorCommand(job.Key)
                    .ErrorCode("KYC_NOT_VERIFIED")
                    .ErrorMessage($"Client {clientId} KYC status is '{verification.KycStatus}'. KYC approval required.")
                    .Send();

                _logger.LogWarning(
                    "KYC verification failed for client {ClientId}: Status = {Status}",
                    clientId, verification.KycStatus);
                return;
            }

            // Check KYC expiration (12 months from approval date)
            if (verification.KycApprovedAt.HasValue)
            {
                var expirationDate = verification.KycApprovedAt.Value.AddMonths(12);
                if (DateTime.UtcNow > expirationDate)
                {
                    await jobClient.NewThrowErrorCommand(job.Key)
                        .ErrorCode("KYC_EXPIRED")
                        .ErrorMessage($"Client {clientId} KYC verification expired. Approved on {verification.KycApprovedAt:yyyy-MM-dd}, valid for 12 months.")
                        .Send();

                    _logger.LogWarning(
                        "KYC verification expired for client {ClientId}: Approved = {ApprovedAt}, Expiration = {ExpirationDate}",
                        clientId, verification.KycApprovedAt, expirationDate);
                    return;
                }
            }

            // KYC verified and not expired - complete job with success variables
            var outputVariables = new
            {
                kycVerified = true,
                kycApprovedAt = verification.KycApprovedAt?.ToString("o"),
                kycVerificationLevel = verification.VerificationLevel,
                clientRiskRating = verification.RiskRating
            };

            await jobClient.NewCompleteCommand(job.Key)
                .Variables(JsonSerializer.Serialize(outputVariables))
                .Send();

            _logger.LogInformation(
                "KYC verification successful for client {ClientId}. Verification level: {Level}, Risk rating: {Rating}",
                clientId, verification.VerificationLevel, verification.RiskRating);
        }
        catch (KycNotVerifiedException ex)
        {
            // KYC not verified exception - throw workflow error
            _logger.LogWarning(ex,
                "KYC not verified for client {ClientId}",
                clientId);

            await jobClient.NewThrowErrorCommand(job.Key)
                .ErrorCode("KYC_NOT_VERIFIED")
                .ErrorMessage($"Client {clientId} KYC is not verified: {ex.Message}")
                .Send();
        }
        catch (ClientManagementServiceException ex)
        {
            // Circuit breaker or service unavailable - fail job with retry
            _logger.LogError(ex,
                "Client Management Service unavailable for client {ClientId}. Retries remaining: {Retries}",
                clientId, job.Retries);

            await jobClient.NewFailCommand(job.Key)
                .Retries(job.Retries - 1) // Decrement retries
                .ErrorMessage($"Client Management Service unavailable: {ex.Message}")
                .Send();
        }
        catch (JsonException ex)
        {
            // Invalid job variables
            _logger.LogError(ex,
                "Invalid JSON in job variables for job {JobKey}",
                job.Key);

            await jobClient.NewThrowErrorCommand(job.Key)
                .ErrorCode("INVALID_JOB_VARIABLES")
                .ErrorMessage($"Failed to parse job variables: {ex.Message}")
                .Send();
        }
        catch (Exception ex)
        {
            // Unexpected error - fail job with retry
            _logger.LogError(ex,
                "Unexpected error during KYC verification for client {ClientId}. Retries remaining: {Retries}",
                clientId, job.Retries);

            await jobClient.NewFailCommand(job.Key)
                .Retries(job.Retries - 1)
                .ErrorMessage($"Unexpected error: {ex.Message}")
                .Send();
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("KycVerificationWorker stopping");
        return base.StopAsync(cancellationToken);
    }
}
