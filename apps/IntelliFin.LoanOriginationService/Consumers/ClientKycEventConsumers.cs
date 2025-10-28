using IntelliFin.LoanOriginationService.Events;
using IntelliFin.LoanOriginationService.Services;
using IntelliFin.Shared.DomainModels.Repositories;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace IntelliFin.LoanOriginationService.Consumers;

/// <summary>
/// Consumes ClientKycApprovedEvent from ClientManagement service.
/// Allows pending loan applications for the client to proceed.
/// </summary>
public class ClientKycApprovedEventConsumer : IConsumer<ClientKycApprovedEvent>
{
    private readonly ILogger<ClientKycApprovedEventConsumer> _logger;
    private readonly ILoanApplicationRepository _loanApplicationRepository;
    private readonly ILoanApplicationService _loanApplicationService;

    public ClientKycApprovedEventConsumer(
        ILogger<ClientKycApprovedEventConsumer> logger,
        ILoanApplicationRepository loanApplicationRepository,
        ILoanApplicationService loanApplicationService)
    {
        _logger = logger;
        _loanApplicationRepository = loanApplicationRepository;
        _loanApplicationService = loanApplicationService;
    }

    public async Task Consume(ConsumeContext<ClientKycApprovedEvent> context)
    {
        var message = context.Message;
        
        _logger.LogInformation(
            "Processing ClientKycApprovedEvent for Client {ClientId}, CorrelationId: {CorrelationId}",
            message.ClientId, message.CorrelationId);

        try
        {
            // Find all pending loan applications for this client
            var pendingApplications = await _loanApplicationRepository
                .GetByClientIdAsync(message.ClientId, context.CancellationToken);

            var applicationsAwaitingKyc = pendingApplications
                .Where(app => app.Status == "PendingKYC" || app.Status == "Draft")
                .ToList();

            _logger.LogInformation(
                "Found {Count} pending loan applications for Client {ClientId}",
                applicationsAwaitingKyc.Count, message.ClientId);

            // Update applications to allow processing
            foreach (var application in applicationsAwaitingKyc)
            {
                _logger.LogInformation(
                    "Updating loan application {ApplicationId} from {OldStatus} to Submitted",
                    application.Id, application.Status);

                // Update status to allow processing
                application.Status = "Submitted";
                application.SubmittedAt = DateTime.UtcNow;
                application.LastModifiedAtUtc = DateTime.UtcNow;
                application.LastModifiedBy = $"System:KYC_Approved_{message.ApprovedBy}";

                await _loanApplicationRepository.UpdateAsync(application, context.CancellationToken);

                _logger.LogInformation(
                    "Loan application {ApplicationId} updated successfully and ready for credit assessment",
                    application.Id);
            }

            _logger.LogInformation(
                "Successfully processed ClientKycApprovedEvent for Client {ClientId}",
                message.ClientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing ClientKycApprovedEvent for Client {ClientId}",
                message.ClientId);
            throw; // Re-throw to trigger MassTransit retry
        }
    }
}

/// <summary>
/// Consumes ClientKycRevokedEvent from ClientManagement service.
/// Pauses or declines active loan applications for the client.
/// </summary>
public class ClientKycRevokedEventConsumer : IConsumer<ClientKycRevokedEvent>
{
    private readonly ILogger<ClientKycRevokedEventConsumer> _logger;
    private readonly ILoanApplicationRepository _loanApplicationRepository;

    public ClientKycRevokedEventConsumer(
        ILogger<ClientKycRevokedEventConsumer> logger,
        ILoanApplicationRepository loanApplicationRepository)
    {
        _logger = logger;
        _loanApplicationRepository = loanApplicationRepository;
    }

    public async Task Consume(ConsumeContext<ClientKycRevokedEvent> context)
    {
        var message = context.Message;
        
        _logger.LogWarning(
            "Processing ClientKycRevokedEvent for Client {ClientId}, Reason: {Reason}, CorrelationId: {CorrelationId}",
            message.ClientId, message.Reason, message.CorrelationId);

        try
        {
            // Find all active loan applications for this client
            var clientApplications = await _loanApplicationRepository
                .GetByClientIdAsync(message.ClientId, context.CancellationToken);

            var activeApplications = clientApplications
                .Where(app => app.Status != "Approved" && 
                             app.Status != "Rejected" && 
                             app.Status != "Withdrawn" &&
                             app.Status != "Disbursed")
                .ToList();

            _logger.LogWarning(
                "Found {Count} active loan applications to decline for Client {ClientId}",
                activeApplications.Count, message.ClientId);

            // Decline all active applications
            foreach (var application in activeApplications)
            {
                _logger.LogWarning(
                    "Declining loan application {ApplicationId} due to KYC revocation",
                    application.Id);

                application.Status = "Rejected";
                application.DeclineReason = $"KYC Revoked: {message.Reason}";
                application.LastModifiedAtUtc = DateTime.UtcNow;
                application.LastModifiedBy = $"System:KYC_Revoked_{message.RevokedBy}";

                await _loanApplicationRepository.UpdateAsync(application, context.CancellationToken);

                _logger.LogWarning(
                    "Loan application {ApplicationId} declined due to KYC revocation",
                    application.Id);
            }

            _logger.LogWarning(
                "Successfully processed ClientKycRevokedEvent for Client {ClientId}",
                message.ClientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing ClientKycRevokedEvent for Client {ClientId}",
                message.ClientId);
            throw; // Re-throw to trigger MassTransit retry
        }
    }
}

/// <summary>
/// Consumes ClientProfileUpdatedEvent from ClientManagement service.
/// Updates cached client data for active loan applications.
/// </summary>
public class ClientProfileUpdatedEventConsumer : IConsumer<ClientProfileUpdatedEvent>
{
    private readonly ILogger<ClientProfileUpdatedEventConsumer> _logger;
    private readonly ILoanApplicationRepository _loanApplicationRepository;

    public ClientProfileUpdatedEventConsumer(
        ILogger<ClientProfileUpdatedEventConsumer> logger,
        ILoanApplicationRepository loanApplicationRepository)
    {
        _logger = logger;
        _loanApplicationRepository = loanApplicationRepository;
    }

    public async Task Consume(ConsumeContext<ClientProfileUpdatedEvent> context)
    {
        var message = context.Message;
        
        _logger.LogInformation(
            "Processing ClientProfileUpdatedEvent for Client {ClientId}, CorrelationId: {CorrelationId}",
            message.ClientId, message.CorrelationId);

        try
        {
            // Find draft/pending applications that might need the updated data
            var clientApplications = await _loanApplicationRepository
                .GetByClientIdAsync(message.ClientId, context.CancellationToken);

            var pendingApplications = clientApplications
                .Where(app => app.Status == "Draft" || app.Status == "PendingKYC")
                .ToList();

            if (pendingApplications.Any())
            {
                _logger.LogInformation(
                    "Found {Count} pending applications for Client {ClientId} that may need profile updates",
                    pendingApplications.Count, message.ClientId);
                
                // Note: In a full implementation, we'd update application data here
                // For now, we just log that the profile was updated
                // The application will fetch fresh data when it's processed
            }

            _logger.LogInformation(
                "Successfully processed ClientProfileUpdatedEvent for Client {ClientId}",
                message.ClientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing ClientProfileUpdatedEvent for Client {ClientId}",
                message.ClientId);
            // Don't throw - profile updates are informational and shouldn't block
        }
    }
}
