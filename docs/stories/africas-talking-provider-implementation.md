# Africa's Talking SMS Provider Implementation

**Story ID:** SMS-2.2
**Epic:** Epic 2 - SMS Provider Migration to Africa's Talking
**Status:** Draft
**Story Points:** 8
**Priority:** High

## User Story
**As a** system administrator
**I want** SMS delivery through Africa's Talking API integration
**So that** we have unified SMS delivery with simplified provider management

## Background
Implement the Africa's Talking SMS provider that integrates with their unified API to replace multiple Zambian carrier integrations. This provider will handle authentication, message formatting, delivery tracking, and cost management.

## Acceptance Criteria

### ✅ Provider Implementation
- [ ] Implement `AfricasTalkingSmsProvider` class conforming to `ISmsProvider`
- [ ] Handle Africa's Talking API authentication with API key
- [ ] Support single SMS and bulk SMS operations
- [ ] Format Zambian phone numbers correctly (+260XXXXXXXXX)
- [ ] Parse API responses and map to standard `SmsResult` format

### ✅ Configuration Management
- [ ] Create `AfricasTalkingConfig` class for provider settings
- [ ] Support sandbox and production environment configurations
- [ ] Configure HTTP client with appropriate timeouts and base URLs
- [ ] Include sender ID configuration for branded messages
- [ ] Support delivery report webhook configuration

### ✅ HTTP Client Integration
- [ ] Implement proper HTTP client usage with connection pooling
- [ ] Add request/response logging for debugging
- [ ] Handle API rate limiting gracefully
- [ ] Implement timeout handling and cancellation token support
- [ ] Add proper error handling for various API error responses

### ✅ Message Formatting
- [ ] Normalize Zambian phone numbers to Africa's Talking format
- [ ] Handle message encoding and character limits
- [ ] Support Unicode messages for special characters
- [ ] Validate message content and recipient numbers
- [ ] Support custom sender ID when configured

### ✅ Cost Tracking
- [ ] Parse cost information from API responses
- [ ] Store cost data in notification logs for reporting
- [ ] Support cost tracking per message and bulk operations
- [ ] Handle cost calculation for failed messages
- [ ] Provide cost estimation capabilities

## Technical Implementation

### Provider Class Structure
```csharp
public class AfricasTalkingSmsProvider : ISmsProvider
{
    private readonly HttpClient _httpClient;
    private readonly AfricasTalkingConfig _config;
    private readonly ILogger<AfricasTalkingSmsProvider> _logger;

    public string ProviderName => "AfricasTalking";

    public async Task<SmsResult> SendAsync(SmsRequest request, CancellationToken cancellationToken = default)
    {
        // Format phone number
        // Create API request payload
        // Send HTTP request to Africa's Talking
        // Parse response and extract message ID, cost, status
        // Return mapped SmsResult
    }

    public async Task<List<SmsResult>> SendBulkAsync(List<SmsRequest> requests, CancellationToken cancellationToken = default)
    {
        // Batch requests for bulk sending
        // Handle bulk API response format
        // Map individual results for each recipient
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
    public string BaseUrl { get; set; } = "https://api.africastalking.com/version1";
    public string SenderId { get; set; } = "IntelliFin";
    public bool EnableDeliveryReports { get; set; } = true;
    public string WebhookUrl { get; set; } = "/api/sms/delivery-webhook";
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxRetryAttempts { get; set; } = 3;
    public int[] RetryDelaySeconds { get; set; } = { 30, 300, 1800 };
}
```

### Request/Response Models
```csharp
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

## Files to Create/Modify

### New Files
- `apps/IntelliFin.Communications/Providers/AfricasTalkingSmsProvider.cs`
- `apps/IntelliFin.Communications/Models/AfricasTalkingModels.cs`
- `apps/IntelliFin.Communications/Configuration/AfricasTalkingConfig.cs`
- `apps/IntelliFin.Communications/Services/PhoneNumberFormatter.cs`

### Modified Files
- `apps/IntelliFin.Communications/Extensions/ServiceCollectionExtensions.cs` - Register provider
- `appsettings.json` - Add Africa's Talking configuration section

## Configuration Example
```json
{
  "AfricasTalking": {
    "ApiKey": "atsk_17ff1222a840ca0d8e4ba32df08ee8ad76f258213a3456d1e2649006f43b2bcc0b8fe5e5",
    "Username": "sandbox",
    "BaseUrl": "https://api.sandbox.africastalking.com/version1",
    "SenderId": "IntelliFin",
    "EnableDeliveryReports": true,
    "WebhookUrl": "/api/sms/delivery-webhook",
    "TimeoutSeconds": 30,
    "MaxRetryAttempts": 3,
    "RetryDelaySeconds": [30, 300, 1800]
  }
}
```

## Testing Requirements

### Unit Tests
- [ ] Phone number formatting for various Zambian formats
- [ ] API request payload creation and validation
- [ ] Response parsing and error handling
- [ ] Cost calculation and extraction
- [ ] HTTP client error scenarios

### Integration Tests
- [ ] End-to-end SMS sending via Africa's Talking sandbox
- [ ] Bulk SMS operations with multiple recipients
- [ ] Error handling for various API error responses
- [ ] Timeout and cancellation behavior
- [ ] Cost tracking accuracy

### Load Testing
- [ ] Concurrent SMS sending performance
- [ ] Bulk SMS operation efficiency
- [ ] HTTP client connection pooling effectiveness
- [ ] Rate limiting compliance

## Dependencies
- HTTP client factory configuration
- Provider abstraction layer (Story SMS-2.1)
- Notification repository for cost tracking
- Configuration management system

## Success Criteria
- Successfully send SMS through Africa's Talking sandbox
- Phone number formatting works for all Zambian number formats
- Cost tracking captures accurate billing information
- API response handling covers all documented scenarios
- Performance meets sub-2-second response time requirement

## Risk Mitigation
- **API Changes**: Version API endpoints and handle backward compatibility
- **Rate Limiting**: Implement proper rate limiting and retry logic
- **Authentication Failures**: Handle API key rotation and refresh
- **Network Issues**: Implement comprehensive timeout and retry strategies
- **Cost Overruns**: Include cost validation and budget threshold checks

## Definition of Done
- [ ] All acceptance criteria implemented and tested
- [ ] Unit test coverage ≥90% for provider implementation
- [ ] Integration tests pass with Africa's Talking sandbox
- [ ] Documentation includes configuration and troubleshooting guides
- [ ] Code review completed and approved
- [ ] Performance benchmarks meet requirements
- [ ] Security review completed for API key handling