# Event Handlers

## MassTransit Event Consumers

This directory contains event consumers that react to messages published on the RabbitMQ message bus.

### Configured Consumers

#### KycStatusEventHandler
**Queue**: `credit-assessment-kyc-events`  
**Events Consumed**:
- `KycExpiredEvent` - When a client's KYC verification expires
- `KycRevokedEvent` - When a client's KYC verification is revoked
- `KycUpdatedEvent` - When a client's KYC verification is updated

**Action**: Automatically invalidates all active credit assessments for the affected client.

### Configuration

MassTransit is configured in `Program.cs` with:
- RabbitMQ connection from `appsettings.json`
- Retry policy: 3 attempts (1s, 5s, 10s intervals)
- InMemory outbox for transactional messaging
- Automatic endpoint configuration

### Testing Events

Use the `EventTestController` endpoints to test event publishing:

```bash
# Publish KycExpiredEvent
POST /api/v1/test/events/kyc-expired?clientId={guid}

# Publish KycRevokedEvent
POST /api/v1/test/events/kyc-revoked?clientId={guid}&reason=Testing

# Publish KycUpdatedEvent
POST /api/v1/test/events/kyc-updated?clientId={guid}&updateType=Renewal
```

### Event Flow

1. **Event Published** (from Client Management Service or test endpoint)
2. **RabbitMQ** routes event to `credit-assessment-kyc-events` queue
3. **Consumer** receives event and processes it
4. **Database Updated** - Affected assessments marked as invalid
5. **Retry** - If processing fails, automatic retry with backoff

### Monitoring

Check consumer status:
- RabbitMQ Management UI: http://localhost:15672
- Prometheus metrics: `/metrics`
- Logs: Search for `KycStatusEventHandler`

### Production Considerations

1. **Remove EventTestController** - Only for testing
2. **Configure Dead Letter Queue** - For failed messages
3. **Set up Alerts** - Monitor consumer lag and failures
4. **Scale Consumers** - Increase concurrency if needed
5. **Audit Events** - Log all event processing to AdminService

---

**Created**: Story 1.12 - KYC Event Subscription  
**Status**: Fully wired and ready for testing
