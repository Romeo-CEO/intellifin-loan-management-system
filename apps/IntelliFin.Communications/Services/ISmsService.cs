using IntelliFin.Communications.Models;

namespace IntelliFin.Communications.Services;

public interface ISmsService
{
    Task<SmsNotificationResponse> SendSmsAsync(SmsNotificationRequest request, CancellationToken cancellationToken = default);
    
    Task<BulkSmsResponse> SendBulkSmsAsync(BulkSmsRequest request, CancellationToken cancellationToken = default);
    
    Task<SmsNotificationResponse> SendTemplatedSmsAsync(string templateId, string phoneNumber, 
        Dictionary<string, object> templateData, SmsNotificationType notificationType, 
        CancellationToken cancellationToken = default);
    
    Task<SmsDeliveryReport?> GetDeliveryStatusAsync(string notificationId, CancellationToken cancellationToken = default);
    
    Task<List<SmsDeliveryReport>> GetBulkDeliveryStatusAsync(string batchId, CancellationToken cancellationToken = default);
    
    Task<SmsAnalytics> GetSmsAnalyticsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    
    Task<bool> ValidatePhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default);
    
    Task<decimal> EstimateCostAsync(SmsNotificationRequest request, CancellationToken cancellationToken = default);
    
    Task<decimal> EstimateBulkCostAsync(BulkSmsRequest request, CancellationToken cancellationToken = default);
    
    Task RetryFailedMessagesAsync(DateTime cutoffDate, CancellationToken cancellationToken = default);
    
    Task<bool> IsRateLimitExceededAsync(CancellationToken cancellationToken = default);
    
    Task<SmsProvider> GetOptimalProviderAsync(string phoneNumber, CancellationToken cancellationToken = default);
}