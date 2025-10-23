using IntelliFin.ClientManagement.Common;
using IntelliFin.ClientManagement.Infrastructure.Persistence;
using IntelliFin.ClientManagement.Integration;
using IntelliFin.ClientManagement.Integration.DTOs;
using IntelliFin.ClientManagement.Models;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;

namespace IntelliFin.ClientManagement.Services;

/// <summary>
/// Service for sending KYC-related notifications to clients
/// Handles consent checking, retry logic, and CommunicationsService integration
/// </summary>
public class KycNotificationService : INotificationService
{
    private readonly ICommunicationsClient _communicationsClient;
    private readonly ClientManagementDbContext _context;
    private readonly ILogger<KycNotificationService> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    // Configuration (would come from appsettings in production)
    private const int MaxRetries = 3;
    private const string DefaultBranchContact = "0977-123-456";
    private const string DefaultComplianceContact = "compliance@intellifin.zm";

    public KycNotificationService(
        ICommunicationsClient communicationsClient,
        ClientManagementDbContext context,
        ILogger<KycNotificationService> logger)
    {
        _communicationsClient = communicationsClient;
        _context = context;
        _logger = logger;

        // Configure exponential backoff retry policy
        _retryPolicy = Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: MaxRetries,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        "Notification retry {RetryCount}/{MaxRetries} after {Delay}ms. Error: {Error}",
                        retryCount, MaxRetries, timeSpan.TotalMilliseconds, exception.Message);
                });
    }

    public async Task<Result<NotificationResult>> SendKycStatusNotificationAsync(
        Guid clientId,
        string templateId,
        Dictionary<string, object> personalizations,
        string? correlationId = null)
    {
        correlationId ??= Guid.NewGuid().ToString();

        try
        {
            _logger.LogInformation(
                "Sending KYC notification: ClientId={ClientId}, Template={Template}, CorrelationId={CorrelationId}",
                clientId, templateId, correlationId);

            // Check consent first
            var hasConsent = await CheckNotificationConsentAsync(clientId, NotificationChannel.SMS);
            if (!hasConsent)
            {
                _logger.LogInformation(
                    "Notification blocked due to consent: ClientId={ClientId}, Template={Template}",
                    clientId, templateId);

                return Result<NotificationResult>.Success(new NotificationResult
                {
                    Success = false,
                    BlockedReason = "No consent",
                    AttemptCount = 0
                });
            }

            // Get client information
            var client = await _context.Clients.FindAsync(clientId);
            if (client == null)
            {
                _logger.LogError("Client not found: {ClientId}", clientId);
                return Result<NotificationResult>.Failure($"Client not found: {clientId}");
            }

            // Build notification request
            var request = new NotificationRequest
            {
                TemplateId = templateId,
                RecipientId = clientId.ToString(),
                Channel = "SMS",
                PersonalizationData = personalizations,
                CorrelationId = correlationId
            };

            // Send with retry logic
            return await SendNotificationWithRetryAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error sending KYC notification: ClientId={ClientId}, Template={Template}",
                clientId, templateId);

            return Result<NotificationResult>.Failure($"Notification failed: {ex.Message}");
        }
    }

    public async Task<bool> CheckNotificationConsentAsync(Guid clientId, NotificationChannel channel)
    {
        try
        {
            var consent = await _context.CommunicationConsents
                .Where(c => c.ClientId == clientId)
                .Where(c => c.ConsentType == "Operational") // KYC notifications are operational
                .Where(c => c.ConsentRevokedAt == null) // Not revoked
                .FirstOrDefaultAsync();

            if (consent == null)
            {
                _logger.LogWarning("No operational consent found for client {ClientId}", clientId);
                return false;
            }

            return channel switch
            {
                NotificationChannel.SMS => consent.SmsEnabled,
                NotificationChannel.Email => consent.EmailEnabled,
                NotificationChannel.InApp => consent.InAppEnabled,
                _ => false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking consent for client {ClientId}", clientId);
            // Fail safe: block notification if consent check fails
            return false;
        }
    }

    public async Task<Result<NotificationResult>> SendNotificationWithRetryAsync(NotificationRequest request)
    {
        var attemptCount = 0;
        var startTime = DateTime.UtcNow;

        try
        {
            // Execute with retry policy
            await _retryPolicy.ExecuteAsync(async () =>
            {
                attemptCount++;

                _logger.LogDebug(
                    "Notification attempt {Attempt}/{MaxRetries}: Template={Template}, Recipient={Recipient}",
                    attemptCount, MaxRetries, request.TemplateId, request.RecipientId);

                // Convert personalization data to Dictionary<string, string>
                var personalizations = request.PersonalizationData
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value?.ToString() ?? string.Empty);

                // Build CommunicationsService request
                var commRequest = new SendNotificationRequest
                {
                    TemplateId = request.TemplateId,
                    RecipientId = request.RecipientId,
                    Channel = request.Channel,
                    PersonalizationData = personalizations,
                    CorrelationId = request.CorrelationId
                };

                // Send notification
                var response = await _communicationsClient.SendNotificationAsync(commRequest);

                // Check response status
                if (response.Status == "Failed")
                {
                    throw new Exception($"CommunicationsService returned Failed status: {response.ErrorMessage}");
                }

                _logger.LogInformation(
                    "Notification sent successfully: NotificationId={NotificationId}, Status={Status}",
                    response.NotificationId, response.Status);
            });

            var endTime = DateTime.UtcNow;

            return Result<NotificationResult>.Success(new NotificationResult
            {
                Success = true,
                AttemptCount = attemptCount,
                FinalAttemptAt = endTime
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Notification failed permanently after {Attempts} attempts: Template={Template}",
                attemptCount, request.TemplateId);

            // In production, this would send to DLQ
            // For now, just log and return failure
            _logger.LogWarning(
                "Notification would be sent to DLQ: Template={Template}, Recipient={Recipient}",
                request.TemplateId, request.RecipientId);

            return Result<NotificationResult>.Success(new NotificationResult
            {
                Success = false,
                AttemptCount = attemptCount,
                FinalError = ex.Message,
                SentToDlq = true,
                FinalAttemptAt = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Builds personalization data with common fields
    /// </summary>
    protected Dictionary<string, object> BuildBasePersonalizations(string clientName)
    {
        return new Dictionary<string, object>
        {
            ["ClientName"] = clientName,
            ["BranchContact"] = DefaultBranchContact,
            ["ComplianceContact"] = DefaultComplianceContact,
            ["CompanyName"] = "IntelliFin"
        };
    }
}
