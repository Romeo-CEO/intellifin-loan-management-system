# SMS Delivery Status Webhook System

**Story ID:** SMS-2.3
**Epic:** Epic 2 - SMS Provider Migration to Africa's Talking
**Status:** Draft
**Story Points:** 5
**Priority:** High

## User Story
**As a** customer service representative
**I want** real-time SMS delivery status updates from Africa's Talking
**So that** I can confirm customers received important communications

## Background
Implement a webhook system to receive and process delivery status reports from Africa's Talking API. This will provide real-time tracking of SMS delivery success, failures, and detailed status information.

## Acceptance Criteria

### ✅ Webhook Controller Implementation
- [ ] Create dedicated controller for Africa's Talking delivery webhooks
- [ ] Handle POST requests with delivery status payloads
- [ ] Process multiple delivery reports in single webhook call
- [ ] Update notification logs with delivery status in real-time
- [ ] Return appropriate HTTP responses to acknowledge webhook receipt

### ✅ Security Implementation
- [ ] Implement webhook signature verification for authenticity
- [ ] Support IP address whitelisting for Africa's Talking servers
- [ ] Rate limiting to prevent webhook abuse
- [ ] Request validation and sanitization
- [ ] Logging of webhook security events

### ✅ Status Processing
- [ ] Map Africa's Talking delivery statuses to internal status enum
- [ ] Handle success, failure, and pending delivery states
- [ ] Process delivery timestamps and calculate delivery times
- [ ] Extract and store failure reasons for failed deliveries
- [ ] Update notification costs with final billing information

### ✅ Retry Logic Enhancement
- [ ] Trigger automatic retry for failed deliveries when appropriate
- [ ] Implement exponential backoff for retry attempts
- [ ] Track retry attempts and prevent infinite retry loops
- [ ] Support manual retry triggers for customer service
- [ ] Log retry decisions and outcomes

### ✅ Analytics and Monitoring
- [ ] Record delivery metrics for reporting and analytics
- [ ] Track delivery success rates by time period
- [ ] Monitor webhook processing performance
- [ ] Alert on unusual delivery failure patterns
- [ ] Generate delivery reports for customer service teams

## Technical Implementation

### Webhook Controller
```csharp
[ApiController]
[Route("api/sms")]
public class SmsDeliveryController : ControllerBase
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IWebhookSecurityService _webhookSecurity;
    private readonly ISmsRetryService _retryService;
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
            return StatusCode(500);
        }
    }
}
```

### Security Service
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
        // Verify request signature
        if (!string.IsNullOrEmpty(_config.WebhookSecret))
        {
            var signature = request.Headers["X-AfricasTalking-Signature"].FirstOrDefault();
            var body = await ReadRequestBodyAsync(request);
            var expectedSignature = ComputeSignature(body, _config.WebhookSecret);

            return signature?.Equals(expectedSignature, StringComparison.OrdinalIgnoreCase) == true;
        }

        // Verify source IP
        if (_config.AllowedWebhookIPs?.Any() == true)
        {
            var clientIP = GetClientIPAddress(request);
            return _config.AllowedWebhookIPs.Contains(clientIP);
        }

        return true;
    }
}
```

### Delivery Report Models
```csharp
public class AfricasTalkingDeliveryReport
{
    [JsonPropertyName("deliveryReports")]
    public List<DeliveryReportItem> DeliveryReports { get; set; } = new();
}

public class DeliveryReportItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("phoneNumber")]
    public string PhoneNumber { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("networkCode")]
    public string NetworkCode { get; set; } = string.Empty;

    [JsonPropertyName("cost")]
    public string Cost { get; set; } = string.Empty;

    [JsonPropertyName("retryCount")]
    public int RetryCount { get; set; }

    [JsonPropertyName("failureReason")]
    public string? FailureReason { get; set; }

    [JsonPropertyName("deliveryTime")]
    public DateTime? DeliveryTime { get; set; }
}
```

### Status Mapping
```csharp
private NotificationStatus MapDeliveryStatus(string status)
{
    return status.ToUpper() switch
    {
        "SUCCESS" => NotificationStatus.Delivered,
        "SENT" => NotificationStatus.Sent,
        "FAILED" => NotificationStatus.Failed,
        "REJECTED" => NotificationStatus.Failed,
        "EXPIRED" => NotificationStatus.Failed,
        "PENDING" => NotificationStatus.Sent,
        _ => NotificationStatus.Sent
    };
}
```

## Files to Create/Modify

### New Files
- `apps/IntelliFin.Communications/Controllers/SmsDeliveryController.cs`
- `apps/IntelliFin.Communications/Services/WebhookSecurityService.cs`
- `apps/IntelliFin.Communications/Services/ISmsRetryService.cs`
- `apps/IntelliFin.Communications/Services/SmsRetryService.cs`
- `apps/IntelliFin.Communications/Models/DeliveryReportModels.cs`

### Modified Files
- `apps/IntelliFin.Communications/Extensions/ServiceCollectionExtensions.cs` - Register services
- `apps/IntelliFin.Communications/Configuration/AfricasTalkingConfig.cs` - Add webhook settings

## Configuration Enhancements
```json
{
  "AfricasTalking": {
    "WebhookSecret": "your-webhook-secret-key",
    "AllowedWebhookIPs": [
      "46.36.203.19",
      "46.36.203.20"
    ],
    "WebhookUrl": "/api/sms/delivery-webhook",
    "RetryFailedDeliveries": true,
    "MaxAutoRetryAttempts": 3,
    "RetryDelayMinutes": [5, 30, 180]
  }
}
```

## Testing Requirements

### Unit Tests
- [ ] Webhook signature verification logic
- [ ] Delivery status mapping for all possible statuses
- [ ] Retry logic decision making
- [ ] Error handling for malformed webhook payloads
- [ ] Security validation scenarios

### Integration Tests
- [ ] End-to-end webhook processing with test payloads
- [ ] Database updates for delivery status changes
- [ ] Retry triggering for failed deliveries
- [ ] Performance under high webhook volume
- [ ] Security enforcement with invalid signatures

### Load Testing
- [ ] Webhook processing under high delivery volume
- [ ] Concurrent webhook handling
- [ ] Database performance with status updates
- [ ] Memory usage during bulk webhook processing

## Dependencies
- Africa's Talking provider implementation (Story SMS-2.2)
- Notification repository and models
- HTTP client for retry operations
- Logging and monitoring infrastructure

## Success Criteria
- Process delivery webhooks with <500ms response time
- 100% delivery status accuracy in notification logs
- Automatic retry triggers work for appropriate failure types
- Webhook security prevents unauthorized access
- Delivery analytics provide actionable insights

## Risk Mitigation
- **Webhook Failures**: Implement idempotency to handle duplicate webhooks
- **Performance Issues**: Add webhook queuing for high-volume scenarios
- **Security Breaches**: Multi-layer security with signatures and IP filtering
- **Data Consistency**: Use database transactions for status updates
- **Retry Loops**: Implement maximum retry limits and exponential backoff

## Definition of Done
- [ ] All acceptance criteria implemented and tested
- [ ] Unit test coverage ≥90% for webhook processing code
- [ ] Integration tests validate end-to-end webhook flow
- [ ] Security testing confirms webhook protection
- [ ] Performance testing meets response time requirements
- [ ] Documentation includes webhook setup and troubleshooting
- [ ] Code review completed and approved