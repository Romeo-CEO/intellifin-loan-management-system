# SMS Provider Architecture

## Overview
The SMS provider architecture supports migration from multiple Zambian carrier integrations (Airtel, MTN, Zamtel) to unified Africa's Talking API while maintaining 100% backward compatibility and providing enhanced delivery tracking capabilities.

## Current SMS Infrastructure Analysis

### Existing Implementation (To Be Enhanced)
```csharp
// Current provider interfaces to maintain
public interface ISmsService
{
    Task<SmsResult> SendAsync(SmsRequest request);
    Task<SmsStatusResult> GetStatusAsync(string messageId);
    Task<List<SmsResult>> SendBulkAsync(List<SmsRequest> requests);
}

// Existing models to preserve
public class SmsRequest
{
    public string To { get; set; }
    public string Message { get; set; }
    public string? From { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

public class SmsResult
{
    public bool Success { get; set; }
    public string MessageId { get; set; }
    public string? ErrorMessage { get; set; }
    public decimal? Cost { get; set; }
}
```

### Enhancement Strategy
- **Preserve Existing Interfaces**: Maintain all current SMS service contracts
- **Provider Abstraction**: Implement provider pattern for easy switching
- **Gradual Migration**: Phase migration with feature flags
- **Enhanced Tracking**: Add comprehensive delivery status tracking

## Africa's Talking Integration Architecture

### Provider Implementation
```csharp
public class AfricasTalkingSmsProvider : ISmsProvider
{
    private readonly HttpClient _httpClient;
    private readonly AfricasTalkingConfig _config;
    private readonly ILogger<AfricasTalkingSmsProvider> _logger;

    public AfricasTalkingSmsProvider(
        HttpClient httpClient,
        IOptions<AfricasTalkingConfig> config,
        ILogger<AfricasTalkingSmsProvider> logger)
    {
        _httpClient = httpClient;
        _config = config.Value;
        _logger = logger;
    }

    public async Task<SmsResult> SendAsync(SmsRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new AfricasTalkingSendRequest
            {
                Username = _config.Username,
                To = FormatPhoneNumber(request.To),
                Message = request.Message,
                From = _config.SenderId
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_config.BaseUrl}/messaging",
                payload,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<AfricasTalkingSendResponse>(cancellationToken);
                return MapToSmsResult(result);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                return new SmsResult
                {
                    Success = false,
                    ErrorMessage = $"HTTP {response.StatusCode}: {error}"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS via Africa's Talking");
            return new SmsResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private string FormatPhoneNumber(string phoneNumber)
    {
        // Normalize Zambian phone numbers to +260XXXXXXXXX format
        if (phoneNumber.StartsWith("260"))
            return $"+{phoneNumber}";
        if (phoneNumber.StartsWith("0"))
            return $"+260{phoneNumber[1..]}";
        if (!phoneNumber.StartsWith("+"))
            return $"+260{phoneNumber}";
        return phoneNumber;
    }
}
```

### Configuration Model
```csharp
public class AfricasTalkingConfig
{
    public const string SectionName = "AfricasTalking";

    public string ApiKey { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string SenderId { get; set; } = string.Empty;
    public bool EnableDeliveryReports { get; set; } = true;
    public string WebhookUrl { get; set; } = string.Empty;
    public int MaxRetryAttempts { get; set; } = 3;
    public int[] RetryDelaySeconds { get; set; } = { 30, 300, 1800 };
    public int TimeoutSeconds { get; set; } = 30;
}
```

### Request/Response Models
```csharp
// Africa's Talking API models
public class AfricasTalkingSendRequest
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("from")]
    public string? From { get; set; }

    [JsonPropertyName("bulkSMSMode")]
    public int BulkSMSMode { get; set; } = 1;
}

public class AfricasTalkingSendResponse
{
    [JsonPropertyName("SMSMessageData")]
    public SmsMessageData SMSMessageData { get; set; } = new();
}

public class SmsMessageData
{
    [JsonPropertyName("Message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("Recipients")]
    public List<SmsRecipient> Recipients { get; set; } = new();
}

public class SmsRecipient
{
    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }

    [JsonPropertyName("number")]
    public string Number { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("cost")]
    public string Cost { get; set; } = string.Empty;

    [JsonPropertyName("messageId")]
    public string MessageId { get; set; } = string.Empty;
}
```

## Webhook Handling Architecture

### Delivery Status Controller
```csharp
[ApiController]
[Route("api/sms")]
public class SmsDeliveryController : ControllerBase
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IWebhookSecurityService _webhookSecurity;
    private readonly ILogger<SmsDeliveryController> _logger;

    [HttpPost("delivery-webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> HandleDeliveryStatusAsync(
        [FromBody] AfricasTalkingDeliveryReport report)
    {
        try
        {
            // Verify webhook authenticity
            if (!await _webhookSecurity.VerifyWebhookAsync(Request))
            {
                _logger.LogWarning("Webhook verification failed for delivery report");
                return Unauthorized();
            }

            // Process each delivery status
            foreach (var status in report.DeliveryReports)
            {
                await ProcessDeliveryStatusAsync(status);
            }

            return Ok(new { Status = "Success", ProcessedCount = report.DeliveryReports.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process delivery webhook");
            return StatusCode(500, new { Error = "Internal server error" });
        }
    }

    private async Task ProcessDeliveryStatusAsync(DeliveryReportItem status)
    {
        var notification = await _notificationRepository.GetByExternalIdAsync(status.Id);
        if (notification != null)
        {
            var notificationStatus = MapDeliveryStatus(status.Status);
            var gatewayResponse = JsonSerializer.Serialize(status);

            await _notificationRepository.UpdateStatusAsync(
                notification.Id,
                notificationStatus,
                gatewayResponse,
                status.FailureReason);

            _logger.LogInformation(
                "Updated notification {NotificationId} status to {Status}",
                notification.Id, notificationStatus);
        }
    }

    private NotificationStatus MapDeliveryStatus(string status)
    {
        return status.ToUpper() switch
        {
            "SUCCESS" => NotificationStatus.Delivered,
            "SENT" => NotificationStatus.Sent,
            "FAILED" => NotificationStatus.Failed,
            "REJECTED" => NotificationStatus.Failed,
            _ => NotificationStatus.Sent
        };
    }
}
```

### Webhook Security Service
```csharp
public interface IWebhookSecurityService
{
    Task<bool> VerifyWebhookAsync(HttpRequest request);
}

public class WebhookSecurityService : IWebhookSecurityService
{
    private readonly AfricasTalkingConfig _config;
    private readonly ILogger<WebhookSecurityService> _logger;

    public async Task<bool> VerifyWebhookAsync(HttpRequest request)
    {
        // Verify request signature if configured
        if (!string.IsNullOrEmpty(_config.WebhookSecret))
        {
            var signature = request.Headers["X-AfricasTalking-Signature"].FirstOrDefault();
            if (string.IsNullOrEmpty(signature))
                return false;

            var body = await ReadRequestBodyAsync(request);
            var expectedSignature = ComputeSignature(body, _config.WebhookSecret);

            return signature.Equals(expectedSignature, StringComparison.OrdinalIgnoreCase);
        }

        // Verify source IP if configured
        if (_config.AllowedWebhookIPs?.Any() == true)
        {
            var clientIP = GetClientIPAddress(request);
            return _config.AllowedWebhookIPs.Contains(clientIP);
        }

        return true; // No security configured - allow all
    }

    private string ComputeSignature(string body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        return Convert.ToBase64String(hash);
    }
}
```

## Provider Abstraction Layer

### SMS Service Enhancement
```csharp
public class EnhancedSmsService : ISmsService
{
    private readonly ISmsProvider _provider;
    private readonly INotificationRepository _repository;
    private readonly ITemplateRenderingEngine _templateEngine;
    private readonly ILogger<EnhancedSmsService> _logger;

    public async Task<SmsResult> SendAsync(SmsRequest request)
    {
        // Create notification log entry
        var notificationLog = new NotificationLog
        {
            EventId = Guid.NewGuid(),
            RecipientId = request.To,
            RecipientType = "Customer",
            Channel = "SMS",
            Content = request.Message,
            Status = NotificationStatus.Pending,
            BranchId = GetBranchId(request),
            CreatedBy = GetCurrentUser()
        };

        await _repository.CreateAsync(notificationLog);

        try
        {
            // Send via provider
            var result = await _provider.SendAsync(request);

            // Update status based on result
            var status = result.Success ? NotificationStatus.Sent : NotificationStatus.Failed;
            await _repository.UpdateStatusAsync(
                notificationLog.Id,
                status,
                JsonSerializer.Serialize(result),
                result.ErrorMessage);

            // Update external ID for tracking
            if (result.Success && !string.IsNullOrEmpty(result.MessageId))
            {
                notificationLog.ExternalId = result.MessageId;
                notificationLog.Cost = result.Cost;
                await _repository.CreateAsync(notificationLog); // Update
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS to {Recipient}", request.To);

            await _repository.UpdateStatusAsync(
                notificationLog.Id,
                NotificationStatus.Failed,
                null,
                ex.Message);

            return new SmsResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
}
```

## Migration Strategy

### Phase 1: Parallel Provider Setup
```csharp
public class SmsProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;

    public ISmsProvider CreateProvider()
    {
        var providerType = _configuration["SMS:Provider"];

        return providerType switch
        {
            "AfricasTalking" => _serviceProvider.GetRequiredService<AfricasTalkingSmsProvider>(),
            "Legacy" => _serviceProvider.GetRequiredService<LegacySmsProvider>(),
            "Hybrid" => _serviceProvider.GetRequiredService<HybridSmsProvider>(),
            _ => throw new InvalidOperationException($"Unknown SMS provider: {providerType}")
        };
    }
}

// Hybrid provider for gradual migration
public class HybridSmsProvider : ISmsProvider
{
    private readonly AfricasTalkingSmsProvider _africasTalking;
    private readonly LegacySmsProvider _legacy;
    private readonly IConfiguration _configuration;

    public async Task<SmsResult> SendAsync(SmsRequest request)
    {
        var useAfricasTalking = ShouldUseAfricasTalking(request);

        if (useAfricasTalking)
        {
            try
            {
                return await _africasTalking.SendAsync(request);
            }
            catch (Exception)
            {
                // Fallback to legacy on failure
                return await _legacy.SendAsync(request);
            }
        }
        else
        {
            return await _legacy.SendAsync(request);
        }
    }

    private bool ShouldUseAfricasTalking(SmsRequest request)
    {
        // Gradual rollout logic
        var rolloutPercentage = _configuration.GetValue<int>("SMS:AfricasTalkingRolloutPercentage");
        var hash = request.To.GetHashCode();
        return Math.Abs(hash % 100) < rolloutPercentage;
    }
}
```

### Phase 2: Feature Flag Configuration
```json
{
  "SMS": {
    "Provider": "Hybrid",
    "AfricasTalkingRolloutPercentage": 10,
    "EnableFallback": true,
    "MaxRetryAttempts": 3
  },
  "FeatureFlags": {
    "EnableAfricasTalking": true,
    "EnableLegacyFallback": true,
    "EnableDeliveryTracking": true
  }
}
```

## Performance Optimization

### Connection Pooling
```csharp
services.AddHttpClient<AfricasTalkingSmsProvider>(client =>
{
    client.BaseAddress = new Uri(configuration["AfricasTalking:BaseUrl"]);
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("apikey", configuration["AfricasTalking:ApiKey"]);
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(15),
    MaxConnectionsPerServer = 20
})
.AddPolicyHandler(GetRetryPolicy())
.AddPolicyHandler(GetCircuitBreakerPolicy());
```

### Retry and Circuit Breaker Policies
```csharp
private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return Policy
        .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
        .Or<HttpRequestException>()
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryCount, context) =>
            {
                // Log retry attempt
            });
}

private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
{
    return Policy
        .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 5,
            durationOfBreak: TimeSpan.FromSeconds(30),
            onBreak: (result, timespan) =>
            {
                // Log circuit breaker opened
            },
            onReset: () =>
            {
                // Log circuit breaker closed
            });
}
```

## Monitoring and Analytics

### SMS Metrics Collection
```csharp
public class SmsMetricsCollector
{
    private readonly IMetrics _metrics;

    public void RecordSmsSent(string provider, bool success, decimal? cost = null)
    {
        _metrics.Measure.Counter.Increment("sms.sent", new MetricTags("provider", provider, "success", success.ToString()));

        if (cost.HasValue)
        {
            _metrics.Measure.Gauge.SetValue("sms.cost", cost.Value, new MetricTags("provider", provider));
        }
    }

    public void RecordDeliveryStatus(string status, TimeSpan deliveryTime)
    {
        _metrics.Measure.Counter.Increment("sms.delivery", new MetricTags("status", status));
        _metrics.Measure.Timer.Time("sms.delivery_time", deliveryTime);
    }
}
```

This SMS provider architecture ensures seamless migration to Africa's Talking while maintaining system reliability and providing enhanced tracking capabilities.