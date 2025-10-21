using IntelliFin.ClientManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;

namespace IntelliFin.ClientManagement.Workflows.CamundaWorkers;

/// <summary>
/// Camunda worker for performing basic risk assessment
/// Calculates risk score based on documents, AML results, and client profile
/// NOTE: Will be enhanced in Story 1.13 with Vault-based rules
/// </summary>
public class RiskAssessmentWorker : ICamundaJobHandler
{
    private readonly ILogger<RiskAssessmentWorker> _logger;
    private readonly ClientManagementDbContext _context;

    public RiskAssessmentWorker(
        ILogger<RiskAssessmentWorker> logger,
        ClientManagementDbContext context)
    {
        _logger = logger;
        _context = context;
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
                "Starting risk assessment for client {ClientId}",
                clientId);

            // Load client for additional risk factors
            var client = await _context.Clients.FindAsync(clientId);
            if (client == null)
            {
                throw new InvalidOperationException($"Client not found: {clientId}");
            }

            // Calculate risk score
            var riskScore = CalculateRiskScore(documentComplete, amlRiskLevel, client);
            var riskRating = MapScoreToRating(riskScore);

            _logger.LogInformation(
                "Risk assessment completed for client {ClientId}: Score={RiskScore}, Rating={RiskRating}",
                clientId, riskScore, riskRating);

            // Complete job with risk assessment results
            await jobClient.NewCompleteJobCommand(job.Key)
                .Variables(new Dictionary<string, object>
                {
                    ["riskScore"] = riskScore,
                    ["riskRating"] = riskRating
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

    /// <summary>
    /// Calculates basic risk score (0-100)
    /// NOTE: Simplified version - will be enhanced with Vault-based rules in Story 1.13
    /// </summary>
    private static int CalculateRiskScore(bool documentComplete, string amlRiskLevel, Domain.Entities.Client client)
    {
        int score = 0;

        // Document completeness factor (20% weight)
        if (!documentComplete)
        {
            score += 20;
        }

        // AML risk factor (50% weight)
        score += amlRiskLevel switch
        {
            "Clear" => 0,
            "Low" => 10,
            "Medium" => 25,
            "High" => 50,
            _ => 0
        };

        // Client profile factors (30% weight)
        // Age factor: Younger clients may have less financial history
        var age = DateTime.UtcNow.Year - client.DateOfBirth.Year;
        if (age < 25) score += 10;

        // Simple heuristics (will be replaced with Vault rules)
        // For now, just use basic scoring

        // Cap score at 100
        return Math.Min(score, 100);
    }

    /// <summary>
    /// Maps risk score to rating category
    /// </summary>
    private static string MapScoreToRating(int score)
    {
        return score switch
        {
            <= 25 => "Low",
            <= 50 => "Medium",
            _ => "High"
        };
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
