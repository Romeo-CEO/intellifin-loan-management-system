using IntelliFin.Communications.Models;

namespace IntelliFin.Communications.Services;

public interface IEmailService
{
    Task<SendEmailResponse> SendEmailAsync(SendEmailRequest request, CancellationToken cancellationToken = default);
    Task<BulkEmailResponse> SendBulkEmailAsync(BulkEmailRequest request, CancellationToken cancellationToken = default);
    Task<EmailDeliveryReport> GetDeliveryReportAsync(string messageId, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmailDeliveryReport>> GetDeliveryReportsAsync(IEnumerable<string> messageIds, CancellationToken cancellationToken = default);
    Task<EmailAnalytics> GetAnalyticsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<bool> ProcessWebhookAsync(string gateway, string payload, CancellationToken cancellationToken = default);
    Task ProcessUnsubscribeAsync(UnsubscribeRequest request, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmailMessage>> GetEmailHistoryAsync(string emailAddress, int pageSize = 50, int pageNumber = 1, CancellationToken cancellationToken = default);
}

public interface IEmailTemplateService
{
    Task<EmailTemplate> CreateTemplateAsync(CreateEmailTemplateRequest request, CancellationToken cancellationToken = default);
    Task<EmailTemplate> UpdateTemplateAsync(string templateId, CreateEmailTemplateRequest request, CancellationToken cancellationToken = default);
    Task<EmailTemplate?> GetTemplateAsync(string templateId, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmailTemplate>> GetTemplatesAsync(EmailCategory? category = null, CancellationToken cancellationToken = default);
    Task DeleteTemplateAsync(string templateId, CancellationToken cancellationToken = default);
    Task<string> RenderTemplateAsync(string templateId, Dictionary<string, string> parameters, CancellationToken cancellationToken = default);
    Task<(string subject, string textContent, string htmlContent)> RenderFullTemplateAsync(string templateId, Dictionary<string, string> parameters, CancellationToken cancellationToken = default);
    Task<List<string>> ValidateTemplateAsync(string templateId, CancellationToken cancellationToken = default);
}

public interface IEmailGatewayService
{
    Task<SendEmailResponse> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
    Task<BulkEmailResponse> SendBulkAsync(IEnumerable<EmailMessage> messages, CancellationToken cancellationToken = default);
    Task<EmailDeliveryReport> GetDeliveryStatusAsync(string externalId, CancellationToken cancellationToken = default);
    Task<bool> ProcessWebhookAsync(string payload, CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
    string GatewayName { get; }
    bool IsEnabled { get; }
}

public interface IEmailSuppressionService
{
    Task<bool> IsSupressedAsync(string emailAddress, CancellationToken cancellationToken = default);
    Task SuppressAsync(string emailAddress, SuppressionReason reason, DateTime? expiresAt = null, string? notes = null, CancellationToken cancellationToken = default);
    Task UnsuppressAsync(string emailAddress, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmailSuppression>> GetSuppressionsAsync(int pageSize = 50, int pageNumber = 1, CancellationToken cancellationToken = default);
    Task ProcessBounceAsync(EmailBounce bounce, CancellationToken cancellationToken = default);
    Task CleanupExpiredSuppressionsAsync(CancellationToken cancellationToken = default);
}

public interface IEmailQueue
{
    Task EnqueueAsync(EmailMessage message, CancellationToken cancellationToken = default);
    Task<EmailMessage?> DequeueAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<EmailMessage>> DequeueBatchAsync(int batchSize, CancellationToken cancellationToken = default);
    Task RequeueAsync(EmailMessage message, TimeSpan delay, CancellationToken cancellationToken = default);
    Task<int> GetQueueLengthAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<EmailMessage>> GetFailedMessagesAsync(int maxAge = 24, CancellationToken cancellationToken = default);
}

public interface IEmailScheduler
{
    Task ScheduleEmailAsync(string messageId, DateTime scheduledAt, CancellationToken cancellationToken = default);
    Task CancelScheduledEmailAsync(string messageId, CancellationToken cancellationToken = default);
    Task<IEnumerable<EmailMessage>> GetScheduledEmailsAsync(DateTime? upTo = null, CancellationToken cancellationToken = default);
    Task ProcessScheduledEmailsAsync(CancellationToken cancellationToken = default);
}