# Epic 2: SMS Provider Migration

## Overview
Migrate SMS infrastructure from individual Zambian carrier integrations (Airtel, MTN, Zamtel) to unified Africa's Talking API while maintaining 100% backward compatibility.

## Current State Analysis
**Existing SMS Infrastructure:**
- Complete implementation with Airtel and MTN providers
- Sophisticated routing logic with rate limiting
- Cost tracking and delivery status handling
- Polly resilience patterns implemented
- HTTP client configurations operational

**Migration Target:**
- Unified Africa's Talking API integration
- Single point of integration for multi-carrier delivery
- Enhanced delivery tracking and reporting
- Simplified cost management

## User Stories

### Story 2.1: Unified SMS Provider Integration
**As a** system administrator
**I want** SMS delivery through Africa's Talking instead of multiple carriers
**So that** we have unified delivery reporting and reduced complexity

**Acceptance Criteria:**
- ✅ Route all SMS through Africa's Talking API successfully
- ✅ Handle delivery status webhooks and update logs
- ✅ Support Zambian phone number formats (+260XXXXXXXXX)
- ✅ Maintain existing SMS API contracts (no breaking changes)
- ✅ Provide comprehensive delivery reporting

### Story 2.2: Cost Management Enhancement
**As a** finance manager
**I want** consolidated SMS cost tracking through single provider
**So that** I can better manage communication budgets

**Acceptance Criteria:**
- ✅ Unified cost tracking through Africa's Talking billing
- ✅ Cost per SMS reporting with detailed analytics
- ✅ Budget threshold alerts and monitoring
- ✅ Historical cost comparison reports

### Story 2.3: Delivery Status Enhancement
**As a** customer service representative
**I want** reliable delivery status for all SMS
**So that** I can confirm customers received important communications

**Acceptance Criteria:**
- ✅ Real-time delivery status via webhooks
- ✅ Detailed failure reason reporting
- ✅ Automated retry logic for failed deliveries
- ✅ Delivery analytics and success rate tracking

## Technical Implementation

### Provider Implementation
```csharp
// New unified provider replacing existing carriers
- Providers/AfricasTalkingSmsProvider.cs    // Core provider implementation
- Models/AfricasTalkingModels.cs           // Provider-specific DTOs
- Configuration/AfricasTalkingConfig.cs     // Provider settings
- Webhooks/DeliveryStatusController.cs     // Webhook handling
```

### Configuration
```json
{
  "AfricasTalking": {
    "ApiKey": "atsk_17ff1222a840ca0d8e4ba32df08ee8ad76f258213a3456d1e2649006f43b2bcc0b8fe5e5",
    "Username": "sandbox",
    "BaseUrl": "https://api.sandbox.africastalking.com/version1/messaging",
    "SenderId": "IntelliFin",
    "EnableDeliveryReports": true,
    "WebhookUrl": "/api/sms/delivery-webhook",
    "MaxRetryAttempts": 3,
    "RetryDelaySeconds": [30, 300, 1800]
  }
}
```

### Migration Strategy (Phased Approach)

#### Phase 1: Parallel Provider Setup
- Add Africa's Talking provider alongside existing providers
- Deploy with feature flag disabled
- Test sandbox integration thoroughly
- **Safety**: No impact on existing SMS delivery

#### Phase 2: Gradual Traffic Migration
- Route new traffic through Africa's Talking
- Maintain existing provider routes as fallback
- Monitor delivery success rates
- **Safety**: Immediate fallback to existing providers

#### Phase 3: Legacy Provider Deprecation
- Migrate all traffic to Africa's Talking
- Keep legacy providers as emergency fallback
- **Safety**: Quick rollback capability maintained

#### Phase 4: Complete Migration
- Remove legacy provider code after validation
- Full Africa's Talking integration
- **Safety**: Proven stable operation before cleanup

### Webhook Implementation
```csharp
[HttpPost("delivery-webhook")]
[AllowAnonymous] // Africa's Talking callback
public async Task<IActionResult> HandleDeliveryStatusAsync(
    [FromBody] AfricasTalkingDeliveryReport report)
{
    // Verify webhook signature
    // Update notification log with delivery status
    // Trigger retry logic if failed
    // Update analytics and monitoring
}
```

## Backward Compatibility Requirements

### API Compatibility
- All existing SMS API endpoints remain functional
- Service contracts and response formats preserved
- No breaking changes to notification workflows
- Existing client integrations unaffected

### Service Integration
- SmsService interface unchanged
- Dependency injection patterns maintained
- Configuration structure backward compatible
- Error handling patterns preserved

## Success Metrics
- **SMS Delivery Rate**: ≥95% successful delivery via Africa's Talking
- **API Response Time**: ≤2 seconds for SMS send requests
- **Migration Success**: 100% traffic migrated without service disruption
- **Cost Efficiency**: Measurable cost reduction vs. multiple carrier contracts

## Dependencies
- Epic 3: Database persistence for enhanced delivery tracking
- Existing SMS infrastructure and API contracts
- Production credentials for Africa's Talking

## Risk Mitigation
- **Provider Downtime**: Fallback to legacy providers during migration
- **Delivery Failures**: Enhanced retry logic with exponential backoff
- **Configuration Errors**: Feature flags for immediate rollback
- **Performance Impact**: Connection pooling and rate limiting
- **Webhook Security**: Signature verification and rate limiting

## Testing Strategy
- **Sandbox Testing**: Complete integration testing with Africa's Talking sandbox
- **Load Testing**: Concurrent SMS delivery performance validation
- **Failover Testing**: Legacy provider fallback validation
- **End-to-End Testing**: Full workflow testing with delivery confirmation