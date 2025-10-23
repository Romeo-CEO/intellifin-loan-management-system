using IntelliFin.ClientManagement.Domain.Enums;
using IntelliFin.ClientManagement.Domain.Events;
using IntelliFin.ClientManagement.Infrastructure.Persistence;
using IntelliFin.ClientManagement.Services;
using IntelliFin.Shared.Audit;
using Microsoft.EntityFrameworkCore;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;

namespace IntelliFin.ClientManagement.Workflows.CamundaWorkers;

/// <summary>
/// Camunda worker for updating KYC status when EDD is approved or rejected
/// Handles both approval and rejection scenarios
/// Publishes domain events via IEventPublisher (Story 1.14)
/// </summary>
public class EddStatusUpdateWorker : ICamundaJobHandler
{
    private readonly ILogger<EddStatusUpdateWorker> _logger;
    private readonly ClientManagementDbContext _context;
    private readonly IAuditService _auditService;
    private readonly IEventPublisher _eventPublisher;

    public EddStatusUpdateWorker(
        ILogger<EddStatusUpdateWorker> logger,
        ClientManagementDbContext context,
        IAuditService auditService,
        IEventPublisher eventPublisher)
    {
        _logger = logger;
        _context = context;
        _auditService = auditService;
        _eventPublisher = eventPublisher;
    }

    // This worker handles both approved and rejected status updates
    public string GetTopicName() => "client.edd.update-status";

    public string GetJobType() => "io.intellifin.edd.update-status-approved,io.intellifin.edd.update-status-rejected";

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

            var kycStatusIdStr = job.Variables.GetValueOrDefault("kycStatusId")?.ToString();
            if (string.IsNullOrWhiteSpace(kycStatusIdStr) || !Guid.TryParse(kycStatusIdStr, out var kycStatusId))
            {
                throw new ArgumentException($"Invalid or missing kycStatusId in job variables: {kycStatusIdStr}");
            }

            // Determine if this is approval or rejection based on job type
            var jobType = job.Type;
            var isApproval = jobType.Contains("approved", StringComparison.OrdinalIgnoreCase);

            if (isApproval)
            {
                await HandleApprovalAsync(clientId, kycStatusId, job, correlationId);
            }
            else
            {
                await HandleRejectionAsync(clientId, kycStatusId, job, correlationId);
            }

            // Complete job
            await jobClient.NewCompleteJobCommand(job.Key)
                .Variables(new Dictionary<string, object>
                {
                    ["statusUpdated"] = true,
                    ["updatedAt"] = DateTime.UtcNow.ToString("o")
                })
                .Send();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error updating EDD status for job {JobKey}",
                job.Key);

            // Fail job with retry
            await jobClient.NewFailCommand(job.Key)
                .Retries(job.Retries - 1)
                .ErrorMessage($"EDD status update failed: {ex.Message}")
                .Send();
        }
    }

    private async Task HandleApprovalAsync(
        Guid clientId,
        Guid kycStatusId,
        IJob job,
        string correlationId)
    {
        _logger.LogInformation(
            "Processing EDD approval for client {ClientId}, KycStatus {KycStatusId}",
            clientId, kycStatusId);

        // Load KYC status and client
        var kycStatus = await _context.KycStatuses
            .Include(k => k.Client)
            .FirstOrDefaultAsync(k => k.Id == kycStatusId);

        if (kycStatus == null)
        {
            throw new InvalidOperationException($"KYC status not found: {kycStatusId}");
        }

        if (kycStatus.Client == null)
        {
            throw new InvalidOperationException($"Client not found for KYC status: {kycStatusId}");
        }

        // Extract approval details from job variables
        var complianceApprovedBy = job.Variables.GetValueOrDefault("complianceApprovedBy")?.ToString() ?? "unknown";
        var complianceComments = job.Variables.GetValueOrDefault("complianceComments")?.ToString();
        var ceoApprovedBy = job.Variables.GetValueOrDefault("ceoApprovedBy")?.ToString() ?? "unknown";
        var ceoComments = job.Variables.GetValueOrDefault("ceoComments")?.ToString();
        var riskAcceptanceLevel = job.Variables.GetValueOrDefault("riskAcceptanceLevel")?.ToString() ?? "Standard";

        // Update KYC status to Completed
        kycStatus.CurrentState = KycState.Completed;
        kycStatus.KycCompletedAt = DateTime.UtcNow;
        kycStatus.KycCompletedBy = ceoApprovedBy;
        
        // Set EDD approval details
        kycStatus.EddApprovedBy = complianceApprovedBy;
        kycStatus.EddCeoApprovedBy = ceoApprovedBy;
        kycStatus.EddApprovedAt = DateTime.UtcNow;
        kycStatus.RiskAcceptanceLevel = riskAcceptanceLevel;
        kycStatus.ComplianceComments = complianceComments;
        kycStatus.CeoComments = ceoComments;
        kycStatus.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "EDD approved for client {ClientId}: Compliance={ComplianceOfficer}, CEO={Ceo}, RiskLevel={RiskLevel}",
            clientId, complianceApprovedBy, ceoApprovedBy, riskAcceptanceLevel);

        // Publish domain event (simplified - in production would use MassTransit)
        var approvedEvent = new EddApprovedEvent
        {
            ClientId = clientId,
            KycStatusId = kycStatusId,
            ClientName = $"{kycStatus.Client.FirstName} {kycStatus.Client.LastName}",
            ComplianceApprovedBy = complianceApprovedBy,
            ComplianceComments = complianceComments,
            CeoApprovedBy = ceoApprovedBy,
            CeoComments = ceoComments,
            RiskAcceptanceLevel = riskAcceptanceLevel,
            ApprovedAt = DateTime.UtcNow,
            CorrelationId = correlationId,
            ProcessInstanceId = job.ProcessInstanceKey.ToString()
        };

        _logger.LogInformation(
            "EDD approved event: {@EddApprovedEvent}",
            approvedEvent);

        // Publish event (will use MassTransit if enabled, otherwise in-memory)
        _ = Task.Run(async () =>
        {
            try
            {
                await _eventPublisher.PublishAsync(
                    approvedEvent,
                    "client.edd.approved",
                    correlationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing EDD approved event for client {ClientId}", clientId);
            }
        });

        // Log audit event
        await _auditService.LogEventAsync(
            "EDD.Approved",
            "ClientManagement",
            clientId.ToString(),
            ceoApprovedBy,
            new
            {
                kycStatusId,
                complianceApprovedBy,
                ceoApprovedBy,
                riskAcceptanceLevel,
                complianceComments,
                ceoComments
            },
            correlationId);
    }

    private async Task HandleRejectionAsync(
        Guid clientId,
        Guid kycStatusId,
        IJob job,
        string correlationId)
    {
        _logger.LogInformation(
            "Processing EDD rejection for client {ClientId}, KycStatus {KycStatusId}",
            clientId, kycStatusId);

        // Load KYC status and client
        var kycStatus = await _context.KycStatuses
            .Include(k => k.Client)
            .FirstOrDefaultAsync(k => k.Id == kycStatusId);

        if (kycStatus == null)
        {
            throw new InvalidOperationException($"KYC status not found: {kycStatusId}");
        }

        if (kycStatus.Client == null)
        {
            throw new InvalidOperationException($"Client not found for KYC status: {kycStatusId}");
        }

        // Extract rejection details from job variables
        var rejectedBy = job.Variables.GetValueOrDefault("rejectedBy")?.ToString() ?? "unknown";
        var rejectionStage = job.Variables.GetValueOrDefault("rejectionStage")?.ToString() ?? "Unknown";
        var rejectionReason = job.Variables.GetValueOrDefault("rejectionReason")?.ToString() ?? "Not specified";

        // Update KYC status to Rejected
        kycStatus.CurrentState = KycState.Rejected;
        kycStatus.UpdatedAt = DateTime.UtcNow;

        // Store rejection details in appropriate comments field
        if (rejectionStage.Equals("Compliance", StringComparison.OrdinalIgnoreCase))
        {
            kycStatus.ComplianceComments = $"REJECTED: {rejectionReason}";
        }
        else if (rejectionStage.Equals("CEO", StringComparison.OrdinalIgnoreCase))
        {
            kycStatus.CeoComments = $"REJECTED: {rejectionReason}";
        }

        await _context.SaveChangesAsync();

        _logger.LogWarning(
            "EDD rejected for client {ClientId}: Stage={Stage}, RejectedBy={RejectedBy}, Reason={Reason}",
            clientId, rejectionStage, rejectedBy, rejectionReason);

        // Publish domain event (simplified)
        var rejectedEvent = new EddRejectedEvent
        {
            ClientId = clientId,
            KycStatusId = kycStatusId,
            ClientName = $"{kycStatus.Client.FirstName} {kycStatus.Client.LastName}",
            RejectedBy = rejectedBy,
            RejectionStage = rejectionStage,
            RejectionReason = rejectionReason,
            RejectedAt = DateTime.UtcNow,
            CorrelationId = correlationId,
            ProcessInstanceId = job.ProcessInstanceKey.ToString()
        };

        _logger.LogWarning(
            "EDD rejected event: {@EddRejectedEvent}",
            rejectedEvent);

        // Publish event (will use MassTransit if enabled, otherwise in-memory)
        _ = Task.Run(async () =>
        {
            try
            {
                await _eventPublisher.PublishAsync(
                    rejectedEvent,
                    "client.edd.rejected",
                    correlationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing EDD rejected event for client {ClientId}", clientId);
            }
        });

        // Log audit event
        await _auditService.LogEventAsync(
            "EDD.Rejected",
            "ClientManagement",
            clientId.ToString(),
            rejectedBy,
            new
            {
                kycStatusId,
                rejectionStage,
                rejectionReason
            },
            correlationId);
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
