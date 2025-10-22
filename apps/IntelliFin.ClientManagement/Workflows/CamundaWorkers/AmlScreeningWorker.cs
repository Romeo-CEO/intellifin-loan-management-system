using IntelliFin.ClientManagement.Domain.Events;
using IntelliFin.ClientManagement.Infrastructure.Persistence;
using IntelliFin.ClientManagement.Services;
using Microsoft.EntityFrameworkCore;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;

namespace IntelliFin.ClientManagement.Workflows.CamundaWorkers;

/// <summary>
/// Camunda worker for performing AML (Anti-Money Laundering) screening
/// Checks client against sanctions lists, PEP databases, and watchlists
/// Publishes EddEscalatedEvent when EDD is required (Story 1.14)
/// </summary>
public class AmlScreeningWorker : ICamundaJobHandler
{
    private readonly ILogger<AmlScreeningWorker> _logger;
    private readonly ClientManagementDbContext _context;
    private readonly IAmlScreeningService _amlService;
    private readonly IEventPublisher _eventPublisher;

    public AmlScreeningWorker(
        ILogger<AmlScreeningWorker> logger,
        ClientManagementDbContext context,
        IAmlScreeningService amlService,
        IEventPublisher eventPublisher)
    {
        _logger = logger;
        _context = context;
        _amlService = amlService;
        _eventPublisher = eventPublisher;
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

            // Determine if EDD escalation is required
            var eddEscalation = DetermineEddEscalation(result.Value!, kycStatus);

            if (eddEscalation.EscalateToEdd)
            {
                kycStatus.RequiresEdd = true;
                kycStatus.EddReason = eddEscalation.Reason;
                kycStatus.EddEscalatedAt = DateTime.UtcNow;

                _logger.LogWarning(
                    "EDD escalation triggered for client {ClientId}: {Reason}",
                    clientId, eddEscalation.Reason);
            }

            await _context.SaveChangesAsync();

            // Publish EDD escalation event if triggered
            if (eddEscalation.EscalateToEdd)
            {
                var client = await _context.Clients.FindAsync(clientId);
                if (client != null)
                {
                    var eddEvent = new EddEscalatedEvent
                    {
                        ClientId = clientId,
                        KycStatusId = kycStatus.Id,
                        ClientName = $"{client.FirstName} {client.LastName}",
                        EscalatedAt = DateTime.UtcNow,
                        EddReason = eddEscalation.Reason ?? "High Risk",
                        RiskLevel = result.Value.OverallRiskLevel,
                        HasSanctionsHit = result.Value.SanctionsHit,
                        IsPep = result.Value.PepMatch,
                        ExpectedTimeframe = "5-7 business days",
                        CorrelationId = correlationId,
                        ProcessInstanceId = job.ProcessInstanceKey.ToString()
                    };

                    // Publish event (will use MassTransit if enabled, otherwise in-memory)
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _eventPublisher.PublishAsync(
                                eddEvent,
                                "client.kyc.edd-escalated",
                                correlationId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error publishing EDD escalation event for client {ClientId}", clientId);
                        }
                    });
                }
            }

            // Add EDD escalation to workflow variables
            var workflowVariables = new Dictionary<string, object>(result.Value.Variables)
            {
                ["escalateToEdd"] = eddEscalation.EscalateToEdd,
                ["eddReason"] = eddEscalation.Reason ?? string.Empty
            };

            // Complete job with screening results
            await jobClient.NewCompleteJobCommand(job.Key)
                .Variables(workflowVariables)
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

    /// <summary>
    /// Determines if EDD escalation is required based on screening results
    /// </summary>
    private EddEscalationDecision DetermineEddEscalation(AmlScreeningResult screeningResult, Domain.Entities.KycStatus kycStatus)
    {
        var reasons = new List<string>();
        var escalate = false;

        // Rule 1: Sanctions hit always triggers EDD (highest priority)
        if (screeningResult.SanctionsHit)
        {
            escalate = true;
            reasons.Add("Sanctions list match detected");
            
            _logger.LogWarning("EDD trigger: Sanctions hit for KycStatus {KycStatusId}", kycStatus.Id);
        }

        // Rule 2: High-risk PEP match triggers EDD
        if (screeningResult.PepMatch && screeningResult.OverallRiskLevel == "High")
        {
            escalate = true;
            reasons.Add("High-risk PEP match");
            
            _logger.LogWarning("EDD trigger: High-risk PEP for KycStatus {KycStatusId}", kycStatus.Id);
        }

        // Rule 3: Multiple medium-risk findings (≥2) trigger EDD
        var mediumRiskCount = screeningResult.Screenings.Count(s => s.RiskLevel == "Medium");
        if (mediumRiskCount >= 2)
        {
            escalate = true;
            reasons.Add($"Multiple medium-risk findings ({mediumRiskCount})");
            
            _logger.LogWarning("EDD trigger: Multiple medium risks ({Count}) for KycStatus {KycStatusId}", 
                mediumRiskCount, kycStatus.Id);
        }

        // Rule 4: Overall High risk level
        if (screeningResult.OverallRiskLevel == "High")
        {
            escalate = true;
            if (!reasons.Any()) // Only add if not already captured by other rules
                reasons.Add("High overall AML risk level");
        }

        // Compile final reason
        var finalReason = escalate 
            ? string.Join("; ", reasons) 
            : null;

        return new EddEscalationDecision
        {
            EscalateToEdd = escalate,
            Reason = finalReason
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

/// <summary>
/// EDD escalation decision result
/// </summary>
internal class EddEscalationDecision
{
    public bool EscalateToEdd { get; set; }
    public string? Reason { get; set; }
}
