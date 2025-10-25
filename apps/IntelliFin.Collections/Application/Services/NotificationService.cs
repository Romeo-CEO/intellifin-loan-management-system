using IntelliFin.Shared.Audit;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace IntelliFin.Collections.Application.Services;

/// <summary>
/// Service for sending customer notifications via CommunicationService.
/// Respects customer communication preferences and consent.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly HttpClient _httpClient;
    private readonly IAuditClient _auditClient;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        HttpClient httpClient,
        IAuditClient auditClient,
        ILogger<NotificationService> logger)
    {
        _httpClient = httpClient;
        _auditClient = auditClient;
        _logger = logger;
    }

    public async Task SendPaymentReminderAsync(
        Guid loanId,
        Guid clientId,
        decimal amountDue,
        DateTime dueDate,
        int daysPastDue,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Sending payment reminder for loan {LoanId}, client {ClientId}, {DPD} days past due",
            loanId, clientId, daysPastDue);

        var notification = new
        {
            TemplateCode = daysPastDue == 0 ? "PAYMENT_REMINDER" : "OVERDUE_REMINDER",
            RecipientId = clientId.ToString(),
            RecipientType = "Client",
            Channel = "SMS",
            Parameters = new Dictionary<string, object>
            {
                { "loanId", loanId },
                { "amountDue", amountDue },
                { "dueDate", dueDate.ToString("yyyy-MM-dd") },
                { "daysPastDue", daysPastDue }
            },
            CorrelationId = correlationId
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/notifications/send",
                notification,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            await _auditClient.LogEventAsync(new AuditEventPayload
            {
                Timestamp = DateTime.UtcNow,
                Actor = "System",
                Action = "PaymentReminderSent",
                EntityType = "Notification",
                EntityId = loanId.ToString(),
                CorrelationId = correlationId,
                EventData = new
                {
                    LoanId = loanId,
                    ClientId = clientId,
                    AmountDue = amountDue,
                    DaysPastDue = daysPastDue
                }
            }, cancellationToken);

            _logger.LogInformation(
                "Successfully sent payment reminder for loan {LoanId}",
                loanId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send payment reminder for loan {LoanId}",
                loanId);
            // Don't throw - notification failure shouldn't break the workflow
        }
    }

    public async Task SendPaymentConfirmationAsync(
        Guid loanId,
        Guid clientId,
        decimal amountPaid,
        DateTime paymentDate,
        decimal remainingBalance,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Sending payment confirmation for loan {LoanId}, client {ClientId}",
            loanId, clientId);

        var notification = new
        {
            TemplateCode = "PAYMENT_CONFIRMATION",
            RecipientId = clientId.ToString(),
            RecipientType = "Client",
            Channel = "SMS",
            Parameters = new Dictionary<string, object>
            {
                { "loanId", loanId },
                { "amountPaid", amountPaid },
                { "paymentDate", paymentDate.ToString("yyyy-MM-dd") },
                { "remainingBalance", remainingBalance }
            },
            CorrelationId = correlationId
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/notifications/send",
                notification,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            await _auditClient.LogEventAsync(new AuditEventPayload
            {
                Timestamp = DateTime.UtcNow,
                Actor = "System",
                Action = "PaymentConfirmationSent",
                EntityType = "Notification",
                EntityId = loanId.ToString(),
                CorrelationId = correlationId,
                EventData = new
                {
                    LoanId = loanId,
                    ClientId = clientId,
                    AmountPaid = amountPaid
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send payment confirmation for loan {LoanId}",
                loanId);
        }
    }

    public async Task SendClassificationNotificationAsync(
        Guid loanId,
        Guid clientId,
        string newClassification,
        int daysPastDue,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        // Only send notifications for significant classifications
        if (newClassification is not ("Substandard" or "Doubtful" or "Loss"))
        {
            return;
        }

        _logger.LogInformation(
            "Sending classification notification for loan {LoanId}, new classification {Classification}",
            loanId, newClassification);

        var notification = new
        {
            TemplateCode = "LOAN_CLASSIFICATION_CHANGED",
            RecipientId = clientId.ToString(),
            RecipientType = "Client",
            Channel = "SMS",
            Parameters = new Dictionary<string, object>
            {
                { "loanId", loanId },
                { "classification", newClassification },
                { "daysPastDue", daysPastDue }
            },
            CorrelationId = correlationId
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/notifications/send",
                notification,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            await _auditClient.LogEventAsync(new AuditEventPayload
            {
                Timestamp = DateTime.UtcNow,
                Actor = "System",
                Action = "ClassificationNotificationSent",
                EntityType = "Notification",
                EntityId = loanId.ToString(),
                CorrelationId = correlationId,
                EventData = new
                {
                    LoanId = loanId,
                    ClientId = clientId,
                    NewClassification = newClassification,
                    DaysPastDue = daysPastDue
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send classification notification for loan {LoanId}",
                loanId);
        }
    }
}
