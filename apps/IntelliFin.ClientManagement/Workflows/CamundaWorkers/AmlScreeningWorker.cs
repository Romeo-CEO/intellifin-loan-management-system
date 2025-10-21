using IntelliFin.ClientManagement.Infrastructure.Persistence;
using IntelliFin.ClientManagement.Services;
using Microsoft.EntityFrameworkCore;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;

namespace IntelliFin.ClientManagement.Workflows.CamundaWorkers;

/// <summary>
/// Camunda worker for performing AML (Anti-Money Laundering) screening
/// Checks client against sanctions lists, PEP databases, and watchlists
/// </summary>
public class AmlScreeningWorker : ICamundaJobHandler
{
    private readonly ILogger<AmlScreeningWorker> _logger;
    private readonly ClientManagementDbContext _context;
    private readonly IAmlScreeningService _amlService;

    public AmlScreeningWorker(
        ILogger<AmlScreeningWorker> logger,
        ClientManagementDbContext context,
        IAmlScreeningService amlService)
    {
        _logger = logger;
        _context = context;
        _amlService = amlService;
    }

    public string GetTopicName() => "client.kyc.aml-screening";

    public string GetJobType() => "io.intellifin.kyc.aml-screening";

    public async Task HandleJobAsync(IJobClient jobClient, IJob job)
    {
        var correlationId = ExtractCorrelationId(job);

        using var logScope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["JobKey"] = job.Key,
            ["ProcessInstanceKey"] = job.ProcessInstanceKey
        });

        try
        {
            // Extract client ID from job variables
            var clientIdStr = job.Variables.GetValueOrDefault("clientId")?.ToString();
            if (string.IsNullOrWhiteSpace(clientIdStr) || !Guid.TryParse(clientIdStr, out var clientId))
            {
                throw new ArgumentException($"Invalid or missing clientId in job variables: {clientIdStr}");
            }

            _logger.LogInformation(
                "Starting AML screening for client {ClientId}",
                clientId);

            // Get KYC status
            var kycStatus = await _context.KycStatuses
                .FirstOrDefaultAsync(k => k.ClientId == clientId);

            if (kycStatus == null)
            {
                throw new InvalidOperationException($"KYC status not found for client {clientId}");
            }

            // Perform AML screening
            var result = await _amlService.PerformScreeningAsync(
                clientId,
                kycStatus.Id,
                "system-workflow",
                correlationId);

            if (result.IsFailure)
            {
                throw new Exception($"AML screening failed: {result.Error}");
            }

            // Update KYC status
            kycStatus.AmlScreeningComplete = true;
            kycStatus.AmlScreenedAt = DateTime.UtcNow;
            kycStatus.AmlScreenedBy = "system-workflow";
            kycStatus.UpdatedAt = DateTime.UtcNow;

            // If high risk, flag for EDD
            if (result.Value!.OverallRiskLevel == "High")
            {
                kycStatus.RequiresEdd = true;
                kycStatus.EddReason = result.Value.SanctionsHit ? "Sanctions" : "PEP";
                kycStatus.EddEscalatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            // Complete job with screening results
            await jobClient.NewCompleteJobCommand(job.Key)
                .Variables(result.Value.Variables)
                .Send();

            _logger.LogInformation(
                "AML screening completed for client {ClientId}: Risk={RiskLevel}, " +
                "Sanctions={SanctionsHit}, PEP={PepMatch}",
                clientId,
                result.Value.OverallRiskLevel,
                result.Value.SanctionsHit,
                result.Value.PepMatch);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error performing AML screening for job {JobKey}",
                job.Key);

            // Fail job with retry
            await jobClient.NewFailCommand(job.Key)
                .Retries(job.Retries - 1)
                .ErrorMessage($"AML screening failed: {ex.Message}")
                .Send();
        }
    }

    private static string ExtractCorrelationId(IJob job)
    {
        try
        {
            var corrId = job.Variables.GetValueOrDefault("correlationId")?.ToString();
            return !string.IsNullOrWhiteSpace(corrId) ? corrId : $"job-{job.Key}";
        }
        catch
        {
            return $"job-{job.Key}";
        }
    }
}
