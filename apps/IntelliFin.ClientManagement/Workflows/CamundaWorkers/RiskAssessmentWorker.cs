using IntelliFin.ClientManagement.Infrastructure.Persistence;
using IntelliFin.ClientManagement.Services;
using Microsoft.EntityFrameworkCore;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;

namespace IntelliFin.ClientManagement.Workflows.CamundaWorkers;

/// <summary>
/// Camunda worker for performing Vault-based risk assessment
/// Uses Vault-managed business rules for dynamic risk scoring
/// </summary>
public class RiskAssessmentWorker : ICamundaJobHandler
{
    private readonly ILogger<RiskAssessmentWorker> _logger;
    private readonly ClientManagementDbContext _context;
    private readonly IRiskScoringService _riskScoringService;

    public RiskAssessmentWorker(
        ILogger<RiskAssessmentWorker> logger,
        ClientManagementDbContext context,
        IRiskScoringService riskScoringService)
    {
        _logger = logger;
        _context = context;
        _riskScoringService = riskScoringService;
    }

    public string GetTopicName() => "client.kyc.risk-assessment";

    public string GetJobType() => "io.intellifin.kyc.risk-assessment";

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
            // Extract variables from job
            var clientIdStr = job.Variables.GetValueOrDefault("clientId")?.ToString();
            if (string.IsNullOrWhiteSpace(clientIdStr) || !Guid.TryParse(clientIdStr, out var clientId))
            {
                throw new ArgumentException($"Invalid or missing clientId in job variables: {clientIdStr}");
            }

            var documentComplete = job.Variables.GetValueOrDefault("documentComplete")?.ToString()
                ?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;

            var amlRiskLevel = job.Variables.GetValueOrDefault("amlRiskLevel")?.ToString() ?? "Clear";

            _logger.LogInformation(
                "Starting Vault-based risk assessment for client {ClientId}",
                clientId);

            // Compute risk using Vault-managed rules
            var riskResult = await _riskScoringService.ComputeRiskAsync(
                clientId,
                "system-workflow",
                correlationId);

            if (riskResult.IsFailure)
            {
                throw new Exception($"Risk computation failed: {riskResult.Error}");
            }

            var riskProfile = riskResult.Value!;

            _logger.LogInformation(
                "Risk assessment completed for client {ClientId}: Score={RiskScore}, Rating={RiskRating}, Rules={Version}",
                clientId, riskProfile.RiskScore, riskProfile.RiskRating, riskProfile.RiskRulesVersion);

            // Complete job with risk assessment results
            await jobClient.NewCompleteJobCommand(job.Key)
                .Variables(new Dictionary<string, object>
                {
                    ["riskScore"] = riskProfile.RiskScore,
                    ["riskRating"] = riskProfile.RiskRating,
                    ["riskRulesVersion"] = riskProfile.RiskRulesVersion,
                    ["riskProfileId"] = riskProfile.Id.ToString()
                })
                .Send();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error performing risk assessment for job {JobKey}",
                job.Key);

            // Fail job with retry
            await jobClient.NewFailCommand(job.Key)
                .Retries(job.Retries - 1)
                .ErrorMessage($"Risk assessment failed: {ex.Message}")
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
