# Story 2.1: Africa's Talking SMS Provider Integration

**Epic:** Epic 2 - SMS Provider Migration to Africa's Talking
**Story ID:** COMM-021
**Status:** Draft
**Priority:** High
**Effort:** 8 Story Points

## User Story
**As a** system administrator
**I want** SMS delivery through Africa's Talking instead of multiple carriers
**So that** we have unified delivery reporting and reduced complexity

## Business Value
- **Unified SMS Delivery**: Single provider integration reduces operational complexity
- **Enhanced Reliability**: Africa's Talking provides multi-carrier redundancy automatically
- **Improved Tracking**: Better delivery status reporting and webhook support
- **Cost Optimization**: Simplified billing and cost management through single provider
- **Reduced Maintenance**: Less complex provider management and configuration

## Acceptance Criteria

### Primary Functionality
- [ ] **SMS Sending**: Successfully send SMS through Africa's Talking API
  - Support Zambian phone number formats (+260XXXXXXXXX)
  - Handle both single and bulk SMS sending
  - Maintain response times under 2 seconds
- [ ] **Delivery Tracking**: Process delivery status webhooks from Africa's Talking
  - Update notification logs with delivery status
  - Handle success, failed, and pending statuses
  - Process delivery reports in real-time
- [ ] **Error Handling**: Comprehensive error handling and logging
  - Log failed API calls with detailed error information
  - Handle rate limiting and quota exceeded scenarios
  - Implement exponential backoff for retries
- [ ] **Security**: Secure webhook endpoint implementation
  - Verify webhook signatures from Africa's Talking
  - Rate limit webhook endpoints
  - Validate webhook payload structure

### API Compatibility
- [ ] **Backward Compatibility**: Maintain existing SMS service contracts
  - ISmsService interface unchanged
  - SmsRequest/SmsResult models preserved
  - No breaking changes to existing notification workflows
- [ ] **Configuration**: Support new Africa's Talking configuration
  - API key and username configuration
  - Webhook URL configuration
  - Retry and timeout settings

### Performance Requirements
- [ ] **Response Time**: SMS send requests complete within 2 seconds
- [ ] **Throughput**: Support minimum 100 SMS per minute
- [ ] **Reliability**: 99.9% uptime for SMS sending functionality
- [ ] **Delivery Rate**: Achieve ≥95% successful delivery rate

## Technical Implementation

### Components to Implement

#### 1. Africa's Talking Provider
```csharp
// File: apps/IntelliFin.Communications/Providers/AfricasTalkingSmsProvider.cs
public class AfricasTalkingSmsProvider : ISmsProvider
{
    // Core provider implementation
    // HTTP client configuration with resilience patterns
    // Phone number formatting for Zambian numbers
    // Request/response mapping
}
```

#### 2. Configuration Models
```csharp
// File: apps/IntelliFin.Communications/Configuration/AfricasTalkingConfig.cs
public class AfricasTalkingConfig
{
    public string ApiKey { get; set; }
    public string Username { get; set; }
    public string BaseUrl { get; set; }
    public string SenderId { get; set; }
    public bool EnableDeliveryReports { get; set; }
    public string WebhookUrl { get; set; }
    public int MaxRetryAttempts { get; set; }
    public int[] RetryDelaySeconds { get; set; }
}
```

#### 3. Request/Response Models
```csharp
// File: apps/IntelliFin.Communications/Models/AfricasTalkingModels.cs
public class AfricasTalkingSendRequest
public class AfricasTalkingSendResponse
public class AfricasTalkingDeliveryReport
public class DeliveryReportItem
```

#### 4. Webhook Controller
```csharp
// File: apps/IntelliFin.Communications/Controllers/SmsWebhookController.cs
[ApiController]
[Route("api/sms")]
public class SmsWebhookController : ControllerBase
{
    [HttpPost("africastalking/delivery")]
    [AllowAnonymous]
    public async Task<IActionResult> HandleAfricasTalkingDeliveryAsync(
        [FromBody] AfricasTalkingDeliveryReport report)
    {
        // Webhook signature verification
        // Delivery status processing
        // Notification log updates
    }
}
```

#### 5. Webhook Security Service
```csharp
// File: apps/IntelliFin.Communications/Services/WebhookSecurityService.cs
public interface IWebhookSecurityService
{
    Task<bool> VerifyAfricasTalkingWebhookAsync(HttpRequest request);
}
```

### Configuration Structure
```json
{
  "AfricasTalking": {
    "ApiKey": "${AFRICAS_TALKING_API_KEY}",
    "Username": "${AFRICAS_TALKING_USERNAME}",
    "BaseUrl": "https://api.africastalking.com/version1",
    "SenderId": "IntelliFin",
    "EnableDeliveryReports": true,
    "WebhookUrl": "/api/sms/africastalking/delivery",
    "MaxRetryAttempts": 3,
    "RetryDelaySeconds": [30, 300, 1800],
    "TimeoutSeconds": 30
  }
}
```

### HTTP Client Configuration
```csharp
// Dependency injection setup
services.AddHttpClient<AfricasTalkingSmsProvider>(client =>
{
    client.BaseAddress = new Uri(config["AfricasTalking:BaseUrl"]);
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("apikey", config["AfricasTalking:ApiKey"]);
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(15),
    MaxConnectionsPerServer = 20
})
.AddPolicyHandler(GetRetryPolicy())
.AddPolicyHandler(GetCircuitBreakerPolicy());
```

## Dependencies
- **Epic 1 Completion**: Event processing framework and notification infrastructure
- **Africa's Talking Account**: Production API credentials and webhook configuration
- **SSL Certificate**: HTTPS endpoint for webhook security
- **Database Schema**: Notification log table structure (from Epic 1)

## Risks and Mitigation

### Technical Risks
- **API Rate Limits**: Monitor usage and implement exponential backoff
- **Webhook Reliability**: Implement idempotent webhook processing
- **Phone Number Formatting**: Comprehensive validation for Zambian number formats
- **Network Failures**: Circuit breaker pattern and connection pooling

### Business Risks
- **Provider Downtime**: Preparation for fallback mechanism (implemented in Story 2.4)
- **Cost Overruns**: Implement usage monitoring and alerts
- **Delivery Failures**: Enhanced retry logic and failure analysis

## Testing Strategy

### Unit Tests
- [ ] Africa's Talking provider implementation
- [ ] Phone number formatting logic
- [ ] Request/response model serialization
- [ ] Configuration validation
- [ ] Webhook signature verification

### Integration Tests
- [ ] SMS sending via Africa's Talking sandbox
- [ ] Webhook delivery status processing
- [ ] Error handling scenarios
- [ ] Retry mechanism validation

### End-to-End Tests
- [ ] Complete SMS delivery workflow
- [ ] Delivery status update flow
- [ ] Webhook security validation
- [ ] Performance under load

## Success Metrics
- **SMS Delivery Rate**: ≥95% successful delivery via Africa's Talking
- **API Response Time**: ≤2 seconds for SMS send requests
- **Webhook Processing**: ≤1 second for delivery status updates
- **Error Rate**: <1% for SMS sending operations
- **Uptime**: 99.9% availability for SMS sending functionality

## Definition of Done
- [ ] All acceptance criteria implemented and tested
- [ ] Unit tests achieve >90% code coverage
- [ ] Integration tests pass with Africa's Talking sandbox
- [ ] Performance tests meet response time requirements
- [ ] Security review completed for webhook endpoints
- [ ] Configuration documented and validated
- [ ] Code review completed and approved
- [ ] Feature deployed to staging environment
- [ ] Monitoring and alerting configured

## Related Stories
- **Prerequisite**: Epic 1 stories (notification infrastructure)
- **Successor**: Story 2.2 (Provider abstraction layer)
- **Related**: Story 2.5 (Enhanced delivery tracking)

## Technical Notes

### Phone Number Formatting Logic
```csharp
private string FormatZambianPhoneNumber(string phoneNumber)
{
    // Remove all non-numeric characters except +
    var cleaned = Regex.Replace(phoneNumber, @"[^\d+]", "");

    // Handle different input formats
    if (cleaned.StartsWith("+260"))
        return cleaned;
    if (cleaned.StartsWith("260"))
        return $"+{cleaned}";
    if (cleaned.StartsWith("0") && cleaned.Length == 10)
        return $"+260{cleaned[1..]}";
    if (cleaned.Length == 9)
        return $"+260{cleaned}";

    throw new ArgumentException($"Invalid Zambian phone number format: {phoneNumber}");
}
```

### Webhook Signature Verification
```csharp
private bool VerifyWebhookSignature(string body, string signature, string secret)
{
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
    var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
    var computedSignature = Convert.ToBase64String(computedHash);

    return signature.Equals(computedSignature, StringComparison.OrdinalIgnoreCase);
}
```

This story establishes the foundation for Africa's Talking integration while maintaining backward compatibility with existing SMS infrastructure.