# AdminService Integration

This directory contains the integration components for communicating with the AdminService for audit logging.

## Overview

The ClientManagement service integrates with AdminService to log audit events for all client operations. This integration follows a **fire-and-forget** pattern to ensure that audit logging failures do not disrupt business operations.

## Components

### 1. IAdminServiceClient (Refit Interface)

**File:** `IAdminServiceClient.cs`

Defines the Refit HTTP client interface for AdminService audit endpoints:

- `POST /api/audit/events` - Log a single audit event
- `POST /api/audit/events/batch` - Log multiple audit events (for future batching)

**Note:** Currently using the shared `IntelliFin.Shared.Audit` library's `IAuditClient` which provides the same functionality with built-in resilience.

### 2. AuditEventDto

**File:** `DTOs/AuditEventDto.cs`

Data transfer object matching AdminService's audit event schema:

```csharp
{
    "actor": "user-id",
    "action": "ClientCreated",
    "entityType": "Client",
    "entityId": "guid",
    "correlationId": "trace-id",
    "ipAddress": "192.168.1.1",
    "eventData": { ... },
    "timestamp": "2025-10-21T10:30:00Z"
}
```

## AuditService

**File:** `../Services/AuditService.cs`

Wraps the shared `IAuditClient` to provide a simplified interface for audit logging within ClientManagement.

### Key Features

1. **Fire-and-Forget Pattern**
   - Audit logging happens asynchronously using `Task.Run`
   - Failures are logged but don't throw exceptions
   - Business operations continue even if audit logging fails

2. **Automatic Correlation ID Propagation**
   - Uses OpenTelemetry Activity.TraceId if available
   - Falls back to X-Correlation-Id header
   - Uses HttpContext.TraceIdentifier as last resort

3. **IP Address Extraction**
   - Checks X-Forwarded-For header (for proxies/load balancers)
   - Falls back to Connection.RemoteIpAddress
   - Handles multiple IPs in X-Forwarded-For (takes first)

4. **User Agent Tracking**
   - Automatically captures User-Agent header from requests

## Usage in ClientService

```csharp
// In ClientService.CreateClientAsync
await _auditService.LogAuditEventAsync(
    action: "ClientCreated",
    entityType: "Client",
    entityId: client.Id.ToString(),
    actor: userId,
    eventData: new
    {
        Nrc = client.Nrc,
        FullName = $"{client.FirstName} {client.LastName}",
        BranchId = client.BranchId
    });
```

## Configuration

### appsettings.json

```json
{
  "AuditService": {
    "BaseAddress": "http://admin-service:5000",
    "HttpTimeout": "00:00:30"
  }
}
```

### appsettings.Development.json

```json
{
  "AuditService": {
    "BaseAddress": "http://localhost:5001",
    "HttpTimeout": "00:00:30"
  }
}
```

## Dependency Injection

The audit client is registered in `ServiceCollectionExtensions.cs`:

```csharp
// Add audit client from shared library
services.AddAuditClient(configuration);

// Add local audit service wrapper
services.AddScoped<IAuditService, AuditService>();
```

## Resilience

The shared `IAuditClient` should be configured with Polly retry policies:

- **Retry:** 3 attempts with exponential backoff
- **Circuit Breaker:** Opens after 5 consecutive failures
- **Timeout:** 30 seconds per request

## Testing

### Unit Tests

**File:** `tests/.../Services/AuditServiceTests.cs`

Tests include:
- ✅ Successful audit event logging
- ✅ Correlation ID propagation from headers
- ✅ Correlation ID from OpenTelemetry Activity
- ✅ IP address extraction (both direct and X-Forwarded-For)
- ✅ Default actor to "system" when empty
- ✅ Fire-and-forget pattern (failures don't throw)
- ✅ User-Agent header capture

### Integration Tests

**File:** `tests/.../Services/ClientServiceAuditIntegrationTests.cs`

Tests include:
- ✅ Audit events logged on client creation
- ✅ Audit events logged on client updates
- ✅ Event data includes relevant details
- ✅ No audit events on failed operations
- ✅ Multiple operations log separate events

## Future Enhancements (Story 1.6+)

1. **Batching Support**
   - Implement event queue with configurable batch size (e.g., 100 events)
   - Flush on timeout (e.g., 5 seconds) or when batch is full
   - Use `System.Threading.Channels` for efficient queue management

2. **Retry with Dead Letter Queue**
   - Store failed events locally for retry
   - Background service to process failed events
   - Alert on persistent failures

3. **Event Deduplication**
   - Track event hashes to prevent duplicate logging
   - Important for idempotency in distributed systems

4. **Performance Metrics**
   - Track audit event success/failure rates
   - Monitor batch sizes and flush intervals
   - Alert on audit service degradation

## Compliance Notes

- **Bank of Zambia Requirements:** All client operations must be audited
- **Data Retention:** Audit events are retained for 7+ years in AdminService
- **Immutability:** Audit events are append-only (no updates or deletes)
- **Chain Integrity:** Future versions may implement event hash chaining

## Troubleshooting

### Audit events not appearing in AdminService

1. Check AdminService is running and accessible
2. Verify `AuditService:BaseAddress` configuration
3. Check network connectivity between services
4. Review ClientManagement logs for audit errors

### High latency in client operations

If audit logging is blocking operations:
1. Verify fire-and-forget pattern is working (check `Task.Run` usage)
2. Check for synchronous waits in audit code
3. Monitor AdminService response times
4. Consider increasing timeout or circuit breaker thresholds

---

**Story:** 1.5 - AdminService Audit Integration  
**Status:** ✅ Complete  
**Last Updated:** 2025-10-21
