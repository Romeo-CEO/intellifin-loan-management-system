using IntelliFin.LoanOriginationService.Events;
using IntelliFin.LoanOriginationService.Models;
using IntelliFin.Shared.DomainModels.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Zeebe.Client;

namespace IntelliFin.LoanOriginationService.Consumers;

/// <summary>
/// MassTransit consumer that handles ClientKycRevoked events from Client Management Service.
/// Pauses all active loan workflows for the affected client and publishes audit events.
/// </summary>
public class ClientKycRevokedConsumer : IConsumer<ClientKycRevoked>
{
    private readonly IZeebeClient _zeebeClient;
    private readonly LmsDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<ClientKycRevokedConsumer> _logger;

    public ClientKycRevokedConsumer(
        IZeebeClient zeebeClient,
        LmsDbContext dbContext,
        IPublishEndpoint publishEndpoint,
        ILogger<ClientKycRevokedConsumer> logger)
    {
        _zeebeClient = zeebeClient;
        _dbContext = dbContext;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    /// <summary>
    /// Processes ClientKycRevoked events by pausing all active loan workflows for the affected client.
    /// </summary>
    public async Task Consume(ConsumeContext<ClientKycRevoked> context)
    {
        var @event = context.Message;
        var clientId = @event.ClientId;
        var revokedAt = @event.RevokedAt;
        var reason = @event.Reason;

        _logger.LogInformation(
            "Processing ClientKycRevoked event for client {ClientId}. Reason: {Reason}, RevokedBy: {RevokedBy}, CorrelationId: {CorrelationId}",
            clientId, reason, @event.RevokedBy, @event.CorrelationId);

        try
        {
            // Find all active loan workflows for this client
            // Active means: has workflow instance ID and status is not terminal (Approved, Rejected, Cancelled, Withdrawn)
            var activeLoans = await _dbContext.LoanApplications
                .Where(la => la.ClientId == clientId
                    && !string.IsNullOrEmpty(la.WorkflowInstanceId)
                    && la.Status != LoanApplicationStatus.Approved
                    && la.Status != LoanApplicationStatus.Rejected
                    && la.Status != LoanApplicationStatus.Withdrawn
                    && la.Status != LoanApplicationStatus.Paused) // Don't re-pause already paused workflows
                .ToListAsync();

            if (!activeLoans.Any())
            {
                _logger.LogInformation(
                    "No active loan workflows found for client {ClientId}. Event processing complete.",
                    clientId);
                return;
            }

            _logger.LogInformation(
                "Found {Count} active loan workflows for client {ClientId}. Pausing workflows.",
                activeLoans.Count, clientId);

            var pausedCount = 0;
            var failedCount = 0;

            // Pause each active workflow
            foreach (var loan in activeLoans)
            {
                try
                {
                    // Update workflow variables to mark KYC as revoked
                    var kycRevokedVariables = new
                    {
                        kycRevoked = true,
                        kycRevokedAt = revokedAt.ToString("o"),
                        kycRevokedReason = reason
                    };

                    await _zeebeClient.NewSetVariablesCommand(long.Parse(loan.WorkflowInstanceId))
                        .Variables(JsonSerializer.Serialize(kycRevokedVariables))
                        .Send();

                    // Publish message to workflow (for intermediate catch events if needed)
                    await _zeebeClient.NewPublishMessageCommand()
                        .MessageName("kyc-revoked")
                        .CorrelationKey(loan.Id.ToString())
                        .Variables(JsonSerializer.Serialize(new { action = "pause_workflow" }))
                        .Send();

                    // Update loan status in database
                    loan.Status = LoanApplicationStatus.Paused;
                    loan.PausedReason = $"KYC_REVOKED: {reason}";
                    loan.PausedAt = DateTime.UtcNow;

                    // Publish audit event to AdminService
                    var auditEvent = new LoanApplicationPaused
                    {
                        LoanApplicationId = loan.Id,
                        ClientId = clientId,
                        LoanNumber = loan.LoanNumber,
                        WorkflowInstanceId = loan.WorkflowInstanceId,
                        PausedReason = $"KYC_REVOKED: {reason}",
                        PausedAt = DateTime.UtcNow,
                        KycRevokedAt = revokedAt,
                        CorrelationId = @event.CorrelationId
                    };

                    await _publishEndpoint.Publish(auditEvent);

                    pausedCount++;

                    _logger.LogInformation(
                        "Paused workflow for loan {LoanNumber} (Application ID: {ApplicationId}, Workflow Instance: {WorkflowInstanceId})",
                        loan.LoanNumber, loan.Id, loan.WorkflowInstanceId);
                }
                catch (Exception ex)
                {
                    failedCount++;
                    _logger.LogError(ex,
                        "Failed to pause workflow for loan {LoanNumber} (Application ID: {ApplicationId}). Continuing with other loans.",
                        loan.LoanNumber, loan.Id);
                    // Continue processing other loans even if one fails
                }
            }

            // Save all changes to database
            var savedChanges = await _dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "KYC revocation processing complete for client {ClientId}. Paused: {PausedCount}, Failed: {FailedCount}, DB Changes: {SavedChanges}",
                clientId, pausedCount, failedCount, savedChanges);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Critical error processing ClientKycRevoked event for client {ClientId}. Event will be retried.",
                clientId);
            // Throw to trigger MassTransit retry mechanism
            throw;
        }
    }
}
