using IntelliFin.Communications.Models;
using IntelliFin.Communications.Providers;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Polly;

namespace IntelliFin.Communications.Services;

public class SmsService : ISmsService
{
    private readonly ILogger<SmsService> _logger;
    private readonly IDistributedCache _cache;
    private readonly IAirtelSmsProvider _airtelProvider;
    private readonly IMtnSmsProvider _mtnProvider;
    private readonly SmsRateLimitConfig _rateLimitConfig;
    private readonly Dictionary<SmsProvider, SmsProviderSettings> _providerSettings;
    private readonly IAsyncPolicy _retryPolicy;

    public SmsService(
        ILogger<SmsService> logger,
        IDistributedCache cache,
        IAirtelSmsProvider airtelProvider,
        IMtnSmsProvider mtnProvider,
        IOptions<SmsRateLimitConfig> rateLimitOptions,
        IOptions<Dictionary<SmsProvider, SmsProviderSettings>> providerSettingsOptions)
    {
        _logger = logger;
        _cache = cache;
        _airtelProvider = airtelProvider;
        _mtnProvider = mtnProvider;
        _rateLimitConfig = rateLimitOptions.Value;
        _providerSettings = providerSettingsOptions.Value;

        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("SMS send retry {RetryCount} in {Delay}ms. Context: {Context}",
                        retryCount, timespan.TotalMilliseconds, context);
                });
    }

    public async Task<SmsNotificationResponse> SendSmsAsync(SmsNotificationRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate rate limits
            if (await IsRateLimitExceededAsync(cancellationToken))
            {
                _logger.LogWarning("Rate limit exceeded for SMS sending");
                return new SmsNotificationResponse
                {
                    NotificationId = Guid.NewGuid().ToString(),
                    Status = SmsDeliveryStatus.Failed,
                    ErrorMessage = "Rate limit exceeded",
                    SentAt = DateTime.UtcNow
                };
            }

            // Check opt-out status
            var optOutStatus = await GetOptOutStatusAsync(request.PhoneNumber, cancellationToken);
            if (optOutStatus.IsOptedOut && optOutStatus.OptedOutTypes.Contains(request.NotificationType))
            {
                _logger.LogInformation("SMS blocked - user opted out. Phone: {Phone}, Type: {Type}",
                    MaskPhoneNumber(request.PhoneNumber), request.NotificationType);
                
                return new SmsNotificationResponse
                {
                    NotificationId = Guid.NewGuid().ToString(),
                    Status = SmsDeliveryStatus.OptedOut,
                    ErrorMessage = "User has opted out of this notification type",
                    SentAt = DateTime.UtcNow
                };
            }

            // Validate phone number
            if (!await ValidatePhoneNumberAsync(request.PhoneNumber, cancellationToken))
            {
                return new SmsNotificationResponse
                {
                    NotificationId = Guid.NewGuid().ToString(),
                    Status = SmsDeliveryStatus.Failed,
                    ErrorMessage = "Invalid phone number format",
                    SentAt = DateTime.UtcNow
                };
            }

            // Get optimal provider
            var provider = await GetOptimalProviderAsync(request.PhoneNumber, cancellationToken);
            request.PreferredProvider = provider;

            // Send SMS with retry policy
            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                return await SendSmsWithProviderAsync(request, cancellationToken);
            });

            // Update rate limit counters
            await UpdateRateLimitCountersAsync(cancellationToken);

            // Cache delivery status for tracking
            await CacheDeliveryStatusAsync(response, cancellationToken);

            _logger.LogInformation("SMS sent successfully. NotificationId: {NotificationId}, Provider: {Provider}",
                response.NotificationId, response.UsedProvider);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending SMS to {Phone}", MaskPhoneNumber(request.PhoneNumber));
            
            return new SmsNotificationResponse
            {
                NotificationId = Guid.NewGuid().ToString(),
                Status = SmsDeliveryStatus.Failed,
                ErrorMessage = ex.Message,
                SentAt = DateTime.UtcNow
            };
        }
    }

    public async Task<BulkSmsResponse> SendBulkSmsAsync(BulkSmsRequest request, CancellationToken cancellationToken = default)
    {
        var batchId = Guid.NewGuid().ToString();
        var response = new BulkSmsResponse
        {
            BatchId = batchId,
            TotalRecipients = request.Recipients.Count,
            AcceptedRecipients = 0,
            RejectedRecipients = 0,
            MessageIds = new List<string>(),
            Errors = new List<BulkSmsError>(),
            TotalEstimatedCost = 0,
            ProcessedAt = DateTime.UtcNow
        };

        try
        {
            var tasks = new List<Task<SmsNotificationResponse>>();
            var semaphore = new SemaphoreSlim(request.BatchSize, request.BatchSize);

            foreach (var recipient in request.Recipients)
            {
                var smsRequest = new SmsNotificationRequest
                {
                    PhoneNumber = recipient.To,
                    Message = $"Template:{request.TemplateId}",
                    NotificationType = SmsNotificationType.GeneralNotification
                };
                tasks.Add(ProcessBulkSmsAsync(smsRequest, semaphore, cancellationToken));
            }

            var results = await Task.WhenAll(tasks);
            response.AcceptedRecipients = results.Count(r => r.Status == SmsDeliveryStatus.Sent || r.Status == SmsDeliveryStatus.Pending || r.Status == SmsDeliveryStatus.Delivered);
            response.RejectedRecipients = results.Count(r => r.Status == SmsDeliveryStatus.Failed);
            response.TotalEstimatedCost = results.Sum(r => r.Cost);
            response.MessageIds = results.Select(r => r.NotificationId).ToList();
            response.ProcessedAt = DateTime.UtcNow;

            // Cache bulk status for tracking
            await CacheBulkStatusAsync(response, cancellationToken);

            _logger.LogInformation("Bulk SMS completed. BatchId: {BatchId}, Accepted: {Accepted}, Rejected: {Rejected}",
                batchId, response.AcceptedRecipients, response.RejectedRecipients);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending bulk SMS. BatchId: {BatchId}", batchId);
            response.ProcessedAt = DateTime.UtcNow;
            return response;
        }
    }

    public async Task<SmsNotificationResponse> SendTemplatedSmsAsync(string templateId, string phoneNumber,
        Dictionary<string, object> templateData, SmsNotificationType notificationType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // This would integrate with ISmsTemplateService to render the template
            // For now, we'll create a basic implementation
            var message = $"IntelliFin: Your {notificationType} notification"; // Placeholder

            var request = new SmsNotificationRequest
            {
                PhoneNumber = phoneNumber,
                Message = message,
                NotificationType = notificationType,
                TemplateData = templateData
            };

            return await SendSmsAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending templated SMS. Template: {TemplateId}, Phone: {Phone}",
                templateId, MaskPhoneNumber(phoneNumber));
            
            return new SmsNotificationResponse
            {
                NotificationId = Guid.NewGuid().ToString(),
                Status = SmsDeliveryStatus.Failed,
                ErrorMessage = ex.Message,
                SentAt = DateTime.UtcNow
            };
        }
    }

    public async Task<SmsDeliveryReport?> GetDeliveryStatusAsync(string notificationId, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = $"sms:delivery:{notificationId}";
            var cachedStatus = await _cache.GetStringAsync(cacheKey, cancellationToken);
            
            if (!string.IsNullOrEmpty(cachedStatus))
            {
                return JsonSerializer.Deserialize<SmsDeliveryReport>(cachedStatus);
            }

            // If not in cache, query providers for status
            // This would typically involve calling provider APIs to get delivery status
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting delivery status for {NotificationId}", notificationId);
            return null;
        }
    }

    public async Task<List<SmsDeliveryReport>> GetBulkDeliveryStatusAsync(string batchId, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = $"sms:bulk:status:{batchId}";
            var cachedStatus = await _cache.GetStringAsync(cacheKey, cancellationToken);
            
            if (!string.IsNullOrEmpty(cachedStatus))
            {
                // In this simplified model, we don't store per-recipient delivery reports in bulk response
                // Return empty list or implement provider-specific retrieval if available
                return new List<SmsDeliveryReport>();
            }

            return new List<SmsDeliveryReport>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bulk delivery status for {BatchId}", batchId);
            return new List<SmsDeliveryReport>();
        }
    }

    public async Task<SmsAnalytics> GetSmsAnalyticsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = $"sms:analytics:{startDate:yyyyMMdd}:{endDate:yyyyMMdd}";
            var cachedAnalytics = await _cache.GetStringAsync(cacheKey, cancellationToken);
            
            if (!string.IsNullOrEmpty(cachedAnalytics))
            {
                return JsonSerializer.Deserialize<SmsAnalytics>(cachedAnalytics) ?? new SmsAnalytics();
            }

            // Generate analytics from cached delivery reports
            var analytics = new SmsAnalytics
            {
                StartDate = startDate,
                EndDate = endDate
            };

            // Cache for 1 hour
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(analytics),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) },
                cancellationToken);

            return analytics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating SMS analytics for period {StartDate} to {EndDate}", startDate, endDate);
            return new SmsAnalytics { StartDate = startDate, EndDate = endDate };
        }
    }

    public async Task<bool> ValidatePhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return false;

        // Zambian phone number validation
        var cleanNumber = phoneNumber.Replace("+", "").Replace("-", "").Replace(" ", "");
        
        // Zambian numbers: +260XXXXXXXXX or 0XXXXXXXXX (where X is 9 digits)
        if (cleanNumber.StartsWith("260") && cleanNumber.Length == 12)
            return true;
        
        if (cleanNumber.StartsWith("0") && cleanNumber.Length == 10)
            return true;

        return false;
    }

    public async Task<decimal> EstimateCostAsync(SmsNotificationRequest request, CancellationToken cancellationToken = default)
    {
        var provider = await GetOptimalProviderAsync(request.PhoneNumber, cancellationToken);
        
        if (_providerSettings.TryGetValue(provider, out var settings))
        {
            return settings.CostPerSms;
        }

        return 0.05m; // Default cost in ZMW
    }

    public async Task<decimal> EstimateBulkCostAsync(BulkSmsRequest request, CancellationToken cancellationToken = default)
    {
        var totalCost = 0m;
        
        foreach (var recipient in request.Recipients)
        {
            var req = new SmsNotificationRequest { PhoneNumber = recipient.To, Message = $"Template:{request.TemplateId}" };
            totalCost += await EstimateCostAsync(req, cancellationToken);
        }

        return totalCost;
    }

    public async Task RetryFailedMessagesAsync(DateTime cutoffDate, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting retry of failed SMS messages from {CutoffDate}", cutoffDate);
            
            // This would typically query a database for failed messages
            // For now, we'll implement a placeholder
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrying failed SMS messages");
        }
    }

    public async Task<bool> IsRateLimitExceededAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var now = DateTime.UtcNow;
            var minuteKey = $"sms:rate:minute:{now:yyyyMMddHHmm}";
            var hourKey = $"sms:rate:hour:{now:yyyyMMddHH}";
            var dayKey = $"sms:rate:day:{now:yyyyMMdd}";
            var costKey = $"sms:cost:day:{now:yyyyMMdd}";

            var minuteCount = await GetRateCountAsync(minuteKey, cancellationToken);
            var hourCount = await GetRateCountAsync(hourKey, cancellationToken);
            var dayCount = await GetRateCountAsync(dayKey, cancellationToken);
            var dayCost = await GetRateCostAsync(costKey, cancellationToken);

            return minuteCount >= _rateLimitConfig.MaxSmsPerMinute ||
                   hourCount >= _rateLimitConfig.MaxSmsPerHour ||
                   dayCount >= _rateLimitConfig.MaxSmsPerDay ||
                   dayCost >= _rateLimitConfig.DailyCostLimit;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking rate limits");
            return false; // Allow sending on error to avoid blocking
        }
    }

    public async Task<SmsProvider> GetOptimalProviderAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            var cleanNumber = phoneNumber.Replace("+", "").Replace("-", "").Replace(" ", "");
            
            // Zambian network prefixes
            if (cleanNumber.StartsWith("260"))
            {
                var prefix = cleanNumber.Substring(3, 2);
                return prefix switch
                {
                    "76" or "77" => SmsProvider.Airtel, // Airtel prefixes
                    "95" or "96" or "97" => SmsProvider.MTN, // MTN prefixes
                    "21" or "22" => SmsProvider.Zamtel, // Zamtel prefixes
                    _ => SmsProvider.Airtel // Default to Airtel
                };
            }
            
            if (cleanNumber.StartsWith("0"))
            {
                var prefix = cleanNumber.Substring(1, 2);
                return prefix switch
                {
                    "76" or "77" => SmsProvider.Airtel,
                    "95" or "96" or "97" => SmsProvider.MTN,
                    "21" or "22" => SmsProvider.Zamtel,
                    _ => SmsProvider.Airtel
                };
            }

            return SmsProvider.Airtel; // Default provider
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error determining optimal provider for {Phone}", MaskPhoneNumber(phoneNumber));
            return SmsProvider.Airtel; // Default on error
        }
    }

    private async Task<SmsNotificationResponse> SendSmsWithProviderAsync(SmsNotificationRequest request, CancellationToken cancellationToken)
    {
        var provider = GetSmsProvider(request.PreferredProvider);
        return await provider.SendSmsAsync(request, cancellationToken);
    }

    private async Task<SmsNotificationResponse> ProcessBulkSmsAsync(SmsNotificationRequest notification, 
        SemaphoreSlim semaphore, CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            return await SendSmsAsync(notification, cancellationToken);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private ISmsProvider GetSmsProvider(SmsProvider provider)
    {
        return provider switch
        {
            SmsProvider.Airtel => _airtelProvider,
            SmsProvider.MTN => _mtnProvider,
            SmsProvider.Zamtel => _airtelProvider, // Fallback to Airtel for Zamtel
            _ => _airtelProvider
        };
    }

    private async Task<SmsOptOutStatus> GetOptOutStatusAsync(string phoneNumber, CancellationToken cancellationToken)
    {
        try
        {
            var cacheKey = $"sms:optout:{phoneNumber}";
            var cachedStatus = await _cache.GetStringAsync(cacheKey, cancellationToken);
            
            if (!string.IsNullOrEmpty(cachedStatus))
            {
                return JsonSerializer.Deserialize<SmsOptOutStatus>(cachedStatus) ?? new SmsOptOutStatus { PhoneNumber = phoneNumber };
            }

            // Default: not opted out
            return new SmsOptOutStatus { PhoneNumber = phoneNumber, IsOptedOut = false };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking opt-out status for {Phone}", MaskPhoneNumber(phoneNumber));
            return new SmsOptOutStatus { PhoneNumber = phoneNumber, IsOptedOut = false };
        }
    }

    private async Task UpdateRateLimitCountersAsync(CancellationToken cancellationToken)
    {
        try
        {
            var now = DateTime.UtcNow;
            var minuteKey = $"sms:rate:minute:{now:yyyyMMddHHmm}";
            var hourKey = $"sms:rate:hour:{now:yyyyMMddHH}";
            var dayKey = $"sms:rate:day:{now:yyyyMMdd}";

            await IncrementRateCountAsync(minuteKey, TimeSpan.FromMinutes(1), cancellationToken);
            await IncrementRateCountAsync(hourKey, TimeSpan.FromHours(1), cancellationToken);
            await IncrementRateCountAsync(dayKey, TimeSpan.FromDays(1), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating rate limit counters");
        }
    }

    private async Task<int> GetRateCountAsync(string key, CancellationToken cancellationToken)
    {
        var value = await _cache.GetStringAsync(key, cancellationToken);
        return int.TryParse(value, out var count) ? count : 0;
    }

    private async Task<decimal> GetRateCostAsync(string key, CancellationToken cancellationToken)
    {
        var value = await _cache.GetStringAsync(key, cancellationToken);
        return decimal.TryParse(value, out var cost) ? cost : 0m;
    }

    private async Task IncrementRateCountAsync(string key, TimeSpan expiry, CancellationToken cancellationToken)
    {
        var currentValue = await GetRateCountAsync(key, cancellationToken);
        var newValue = (currentValue + 1).ToString();
        
        await _cache.SetStringAsync(key, newValue, 
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = expiry }, 
            cancellationToken);
    }

    private async Task CacheDeliveryStatusAsync(SmsNotificationResponse response, CancellationToken cancellationToken)
    {
        try
        {
            var deliveryReport = new SmsDeliveryReport
            {
                MessageId = response.NotificationId,
                ExternalId = response.ProviderMessageId ?? string.Empty,
                To = string.Empty,
                Status = response.Status,
                Gateway = response.UsedProvider.ToString(),
                StatusUpdatedAt = DateTime.UtcNow,
                ActualCost = response.Cost
            };

            var cacheKey = $"sms:delivery:{response.NotificationId}";
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(deliveryReport),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching delivery status for {NotificationId}", response.NotificationId);
        }
    }

    private async Task CacheBulkStatusAsync(BulkSmsResponse response, CancellationToken cancellationToken)
    {
        try
        {
            var cacheKey = $"sms:bulk:status:{response.BatchId}";
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(response),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching bulk status for {BatchId}", response.BatchId);
        }
    }

    private static string MaskPhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrEmpty(phoneNumber) || phoneNumber.Length < 4)
            return "****";
        
        return phoneNumber[..2] + "****" + phoneNumber[^2..];
    }
}

public interface ISmsProvider
{
    Task<SmsNotificationResponse> SendSmsAsync(SmsNotificationRequest request, CancellationToken cancellationToken = default);
}