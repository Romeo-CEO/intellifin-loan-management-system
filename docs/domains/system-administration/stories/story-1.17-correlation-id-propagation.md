# Story 1.17: Global Correlation ID Propagation

## Story Metadata

| Field | Value |
|-------|-------|
| **Story ID** | 1.17 |
| **Epic** | System Administration Control Plane Enhancement |
| **Phase** | Phase 3: Audit & Compliance |
| **Sprint** | Sprint 7 |
| **Story Points** | 5 |
| **Estimated Effort** | 3-5 days |
| **Priority** | P1 (Important for observability) |
| **Status** | ✅ Completed |
| **Assigned To** | Observability Platform Team |
| **Dependencies** | Story 1.6 (OpenTelemetry), Story 1.7 (Jaeger), Story 1.9 (Loki), Story 1.14 (Centralized audit) |
| **Blocks** | None |

---

## User Story

**As a** DevOps engineer,  
**I want** W3C Trace Context correlation IDs propagated across all HTTP and RabbitMQ calls,  
**so that** I can trace requests end-to-end through distributed microservices.

---

## Business Value

Global correlation ID propagation enables complete distributed tracing across the IntelliFin ecosystem:

- **Root Cause Analysis**: Trace errors through entire request flow (API Gateway → Loan Origination → Credit Bureau → Collections)
- **Performance Debugging**: Identify slow services in request chain
- **Audit Trail Correlation**: Link audit events across services for compliance investigations
- **User Experience**: Correlate customer-reported issues with backend traces
- **SLA Monitoring**: Measure end-to-end request latency across service boundaries

This completes the observability triad (traces, metrics, logs) with unified correlation.

---

## Implementation Summary

- API Gateway now applies a dedicated trace context middleware that ensures every inbound request has a W3C `traceparent`, logs generated identifiers, and echoes the correlation headers back to clients.
- OpenTelemetry defaults were tightened so every service emits span, parent, and baggage identifiers through the shared logging pipeline while RabbitMQ consumers inject/extract trace headers for asynchronous workflows.
- Admin Service ingestion derives audit `CorrelationId` values from the active trace, aligns batch normalization, and preserves context through RabbitMQ dead-letter handling so Jaeger links and Loki labels stay consistent.
- Serilog sinks for Identity and KYC services enrich console/file output with `TraceId`/`SpanId`, enabling Grafana Loki to index the correlation metadata without additional parsing rules.

---

## Acceptance Criteria

### AC1: W3C Trace Context Standard Adopted
**Given** OpenTelemetry instrumentation exists (Story 1.6)  
**When** configuring trace propagation  
**Then**:
- W3C Trace Context standard implemented (traceparent, tracestate headers)
- Trace ID format: 32-character hex (128-bit)
- Span ID format: 16-character hex (64-bit)
- Parent span ID included in traces
- Trace flags for sampling decisions propagated

### AC2: API Gateway Correlation ID Generation
**Given** Incoming HTTP request without correlation ID  
**When** request reaches API Gateway  
**Then**:
- API Gateway generates W3C traceparent header if missing
- Format: `00-<trace-id>-<span-id>-<trace-flags>`
- Generated trace ID logged for debugging
- Existing traceparent headers preserved (from external systems)
- Correlation ID added to response headers for client reference

### AC3: HTTP Service-to-Service Propagation
**Given** Service A calls Service B via HttpClient  
**When** making HTTP request  
**Then**:
- OpenTelemetry HttpClient instrumentation automatically injects traceparent header
- Current span context propagated to downstream service
- Service B extracts trace context and creates child span
- Parent-child relationship preserved in Jaeger trace view
- No manual header manipulation required (automatic via OpenTelemetry)

### AC4: RabbitMQ Message Propagation
**Given** Service publishes message to RabbitMQ  
**When** message consumed by another service  
**Then**:
- Trace context injected into RabbitMQ message properties (application headers)
- Consumer extracts trace context from message properties
- Message processing span linked to original request trace
- Asynchronous workflows traceable end-to-end
- Lost message correlation possible via trace ID

### AC5: Audit Event Correlation
**Given** Audit event logged by any service  
**When** event persisted to Admin Service  
**Then**:
- Audit event `CorrelationId` field populated with current trace ID
- Jaeger trace UI link available in audit event details
- Loki log queries filterable by correlation ID
- Cross-service audit trail queryable by single trace ID
- Compliance investigations trace complete user action flow

### AC6: Loki Log Correlation
**Given** Structured logs emitted by services  
**When** logs ingested to Loki  
**Then**:
- Trace ID extracted as Loki label (`trace_id`)
- LogQL queries support: `{service_name="loan-origination"} | json | trace_id="abc123"`
- Grafana Explore shows logs linked to Jaeger traces
- Error logs automatically linked to trace context
- Log-to-trace navigation in Grafana dashboard

### AC7: Jaeger Trace Deep Linking
**Given** Correlation ID exists in audit event or log  
**When** user clicks trace link  
**Then**:
- Deep link format: `https://jaeger.intellifin.local/trace/<trace-id>`
- Jaeger UI opens with complete trace visualization
- Service dependencies visible in trace graph
- Span timing breakdown shows performance bottlenecks
- Error spans highlighted in red

---

## Technical Implementation Details

### Architecture Reference

**PRD Sections**: Section 5.3 (Phase 3 Stories), Lines 1022-1045  
**Architecture Sections**: Section 8.2 (Correlation ID Flow), Lines 1640-1698  
**Requirements**: FR15, NFR18

### Technology Stack

- **Standard**: W3C Trace Context (HTTP traceparent header)
- **Instrumentation**: OpenTelemetry .NET SDK (already in Story 1.6)
- **RabbitMQ**: Activity (System.Diagnostics.Activity) propagation
- **Logging**: Serilog with trace context enrichment

### W3C Trace Context Implementation

```csharp
// IntelliFin.Shared.Observability/OpenTelemetryExtensions.cs
services.AddOptions<LoggerFactoryOptions>()
    .Configure(options =>
    {
        options.ActivityTrackingOptions = ActivityTrackingOptions.TraceId
            | ActivityTrackingOptions.SpanId
            | ActivityTrackingOptions.ParentId
            | ActivityTrackingOptions.Baggage;
    });

services.AddOpenTelemetry()
    .ConfigureResource(resourceBuilder => resourceBuilder.AddService(serviceName, serviceVersion: serviceVersion))
    .WithTracing(tracerProviderBuilder => tracerProviderBuilder
        .AddAspNetCoreInstrumentation(options =>
        {
            options.RecordException = true;
            options.Filter = httpContext => !httpContext.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);
        })
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddSource("IntelliFin.*")
        .SetSampler(new AdaptiveSampler())
        .AddOtlpExporter(exporterOptions => exporterOptions.Endpoint = new Uri(otlpEndpoint)))
    .WithMetrics(...);
```

### RabbitMQ Correlation Propagation

```csharp
// apps/IntelliFin.ApiGateway/Middleware/TraceContextMiddleware.cs
context.Response.OnStarting(() =>
{
    var activity = Activity.Current ?? httpActivityFeature?.Activity;
    var traceParent = incomingTraceParent;

    if (string.IsNullOrWhiteSpace(traceParent))
    {
        traceParent = activity is not null
            ? $"00-{activity.TraceId}-{activity.SpanId}-{(activity.Recorded ? "01" : "00")}"
            : $"00-{ActivityTraceId.CreateRandom()}-{ActivitySpanId.CreateRandom()}-01";

        _logger.LogDebug("Generated traceparent header for request {Method} {Path}: {TraceParent}",
            context.Request.Method,
            context.Request.Path,
            traceParent);
    }

    context.Response.Headers["traceparent"] = traceParent;
    if (activity is not null && !string.IsNullOrEmpty(activity.TraceStateString))
    {
        context.Response.Headers["tracestate"] = activity.TraceStateString;
    }

    return Task.CompletedTask;
});

// apps/IntelliFin.AdminService/Services/AuditRabbitMqConsumer.cs
var propagationContext = Propagators.DefaultTextMapPropagator.Extract(default, args.BasicProperties, ReadHeaderValues);
var previousBaggage = Baggage.Current;
Baggage.Current = propagationContext.Baggage;

using var activity = ActivitySource.StartActivity(
    "audit.events.consume",
    ActivityKind.Consumer,
    propagationContext.ActivityContext);

activity?.SetTag("messaging.system", "rabbitmq");
activity?.SetTag("messaging.destination", _options.QueueName);

if (string.IsNullOrWhiteSpace(payload.CorrelationId) && activity is not null)
{
    payload.CorrelationId = activity.TraceId.ToString();
}

await auditService.LogEventAsync(payload, CancellationToken.None);
await auditService.FlushBufferAsync(CancellationToken.None);

_channel.BasicAck(args.DeliveryTag, false);
activity?.SetStatus(ActivityStatusCode.Ok);
```

### Audit Event Correlation

```csharp
// IntelliFin.Shared.Audit/AuditClient.cs (extended from Story 1.14)
public class AuditClient : IAuditClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditClient> _logger;
    
    public async Task LogEventAsync(string action, string? entityType = null, string? entityId = null, object? eventData = null)
    {
        try
        {
            var context = _httpContextAccessor.HttpContext;
            var actor = context?.User?.Identity?.Name ?? "System";
            
            // Extract correlation ID from current Activity (OpenTelemetry)
            var correlationId = Activity.Current?.Id ?? context?.TraceIdentifier;
            var traceId = Activity.Current?.TraceId.ToString();
            
            var ipAddress = context?.Connection?.RemoteIpAddress?.ToString();
            var userAgent = context?.Request?.Headers["User-Agent"].ToString();
            
            var auditEvent = new
            {
                Timestamp = DateTime.UtcNow,
                Actor = actor,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                CorrelationId = correlationId,  // W3C trace context ID
                TraceId = traceId,  // For deep linking to Jaeger
                IpAddress = ipAddress,
                UserAgent = userAgent,
                EventData = eventData != null ? JsonSerializer.Serialize(eventData) : null
            };
            
            var response = await _httpClient.PostAsJsonAsync("/api/admin/audit/events", auditEvent);
            response.EnsureSuccessStatusCode();
            
            _logger.LogDebug("Audit event logged with correlation ID: {CorrelationId}", correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log audit event: {Action}", action);
        }
    }
}
```

### Loki Log Correlation

```csharp
// Program.cs - Serilog configuration with trace context enrichment
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ServiceName", context.Configuration["ServiceName"])
    .Enrich.WithProperty("Environment", context.Configuration["Environment"])
    .Enrich.With<TraceIdEnricher>()  // Custom enricher for trace ID
    .WriteTo.Console(outputTemplate: 
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} | TraceId: {TraceId} | SpanId: {SpanId} {NewLine}{Exception}")
    .WriteTo.OpenTelemetry(options =>
    {
        options.Endpoint = context.Configuration["OpenTelemetry:OtlpEndpoint"];
        options.ResourceAttributes = new Dictionary<string, object>
        {
            ["service.name"] = context.Configuration["ServiceName"],
            ["deployment.environment"] = context.Configuration["Environment"]
        };
    }));

// Custom enricher to add trace context to logs
public class TraceIdEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var activity = Activity.Current;
        if (activity != null)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceId", activity.TraceId));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SpanId", activity.SpanId));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ParentId", activity.ParentId));
        }
    }
}
```

### Admin UI Trace Deep Linking

```typescript
// Admin portal - Audit event detail component
interface AuditEvent {
  eventId: string;
  timestamp: string;
  actor: string;
  action: string;
  correlationId: string;
  traceId: string;
  // ... other fields
}

export function AuditEventDetail({ event }: { event: AuditEvent }) {
  const jaegerUrl = `https://jaeger.intellifin.local/trace/${event.traceId}`;
  const lokiUrl = `https://grafana.intellifin.local/explore?left={"queries":[{"expr":"{trace_id=\\"${event.traceId}\\"}","refId":"A"}],"datasource":"loki"}`;
  
  return (
    <div className="audit-event-detail">
      <h3>Audit Event Details</h3>
      <dl>
        <dt>Event ID:</dt>
        <dd>{event.eventId}</dd>
        
        <dt>Timestamp:</dt>
        <dd>{new Date(event.timestamp).toLocaleString()}</dd>
        
        <dt>Actor:</dt>
        <dd>{event.actor}</dd>
        
        <dt>Action:</dt>
        <dd>{event.action}</dd>
        
        <dt>Correlation ID:</dt>
        <dd>
          {event.correlationId}
          <div className="trace-links">
            <a href={jaegerUrl} target="_blank" rel="noopener noreferrer">
              🔍 View Trace in Jaeger
            </a>
            <a href={lokiUrl} target="_blank" rel="noopener noreferrer">
              📝 View Logs in Grafana
            </a>
          </div>
        </dd>
      </dl>
    </div>
  );
}
```

---

## Integration Verification

### IV1: Existing CorrelationId Field Populated
**Verification Steps**:
1. Query ErrorLog table (existing CorrelationId field from brownfield analysis)
2. Verify field automatically populated by OpenTelemetry Activity
3. Confirm no code changes needed (automatic via Activity.Current)

**Success Criteria**: Existing CorrelationId field populated without manual intervention.

### IV2: End-to-End Trace Validation
**Verification Steps**:
1. Make API request: POST /api/loans (API Gateway)
2. Trace flows: API Gateway → Loan Origination → Credit Bureau → Collections
3. Open Jaeger UI with trace ID
4. Verify all 4 services appear in trace graph with correct parent-child relationships

**Success Criteria**: Complete trace visible in Jaeger showing full request path.

### IV3: Performance Overhead Acceptable
**Verification Steps**:
1. Measure baseline request latency without correlation propagation
2. Enable correlation propagation
3. Measure new request latency
4. Calculate overhead

**Success Criteria**: <1ms overhead per hop (W3C header parsing is lightweight).

---

## Testing Strategy

### Unit Tests
1. **Traceparent Parsing**: Test W3C format parsing
2. **RabbitMQ Header Injection**: Test trace context in message properties
3. **Audit Correlation**: Test CorrelationId extracted from Activity

### Integration Tests
1. **HTTP Propagation**: Service A → Service B → Verify parent-child span relationship
2. **RabbitMQ Propagation**: Publisher → Queue → Consumer → Verify trace continuity
3. **Audit Correlation**: API call → Audit event → Verify matching trace ID

### End-to-End Tests
- **User Journey Trace**: Login → Browse Loans → Apply → Submit → Approve
  - Single trace ID spans entire user journey
  - All services visible in Jaeger trace graph

### Performance Tests
- **Overhead Measurement**: 10,000 requests with correlation vs without
  - Target: <1% performance degradation

---

## Risks and Mitigation

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| Trace context lost across RabbitMQ | Medium | Low | Comprehensive testing, fallback to correlation_id property |
| External system doesn't support W3C Trace Context | Low | Medium | Accept incoming traces, don't force on external systems |
| High cardinality in Loki trace_id labels | Low | Low | Loki handles high cardinality well, monitor storage growth |
| Jaeger storage exhaustion from trace volume | Medium | Medium | Adaptive sampling (10% of normal, 100% errors), 7-day retention |

---

## Definition of Done (DoD)

- [ ] W3C Trace Context standard implemented (already in Story 1.6)
- [ ] API Gateway generates traceparent for incoming requests without header
- [ ] HTTP service-to-service propagation verified (automatic via OpenTelemetry)
- [ ] RabbitMQ message correlation propagation implemented and tested
- [ ] Audit events include CorrelationId from Activity.Current
- [ ] Loki logs labeled with trace_id for correlation
- [ ] Jaeger deep links work from Admin UI audit event details
- [ ] Grafana dashboards show log-to-trace navigation
- [ ] All integration verification criteria passed
- [ ] End-to-end trace demonstrated across 4+ services
- [ ] Performance overhead <1ms per hop verified
- [ ] Documentation updated in `docs/domains/system-administration/correlation-id-propagation.md`

---

## Related Documentation

### PRD References
- **Full PRD**: `../system-administration-control-plane-prd.md` (Lines 1022-1045)
- **Requirements**: FR15, NFR18

### Architecture References
- **Full Architecture**: `../system-administration-control-plane-architecture.md` (Section 8.2, Lines 1640-1698)

### External Standards
- [W3C Trace Context Specification](https://www.w3.org/TR/trace-context/)
- [OpenTelemetry Context Propagation](https://opentelemetry.io/docs/specs/otel/context/)

---

## Notes for Development Team

### Pre-Implementation Checklist
- [ ] Story 1.6 (OpenTelemetry) completed (automatic propagation foundation)
- [ ] Story 1.7 (Jaeger) deployed (trace visualization)
- [ ] Story 1.9 (Loki) deployed (log correlation)
- [ ] Story 1.14 (Audit) has CorrelationId field

### Post-Implementation Handoff
- DevOps team trained on Jaeger trace navigation
- Example traces documented for common user journeys
- Grafana dashboards show log-trace correlation
- Runbook for correlation ID debugging

---

**Story Created**: 2025-10-11  
**Last Updated**: 2025-10-11  
**Next Story**: Story 1.18 - Offline CEO App Audit Merge Implementation
