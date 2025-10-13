# Correlation ID Propagation Playbook

The IntelliFin platform standardises on W3C Trace Context so every HTTP request, background job, and RabbitMQ message can be correlated across logs, traces, and audit events. This document summarises the key runtime behaviours introduced by Story 1.17.

## HTTP Pipeline

- **API Gateway** injects `traceparent`/`tracestate` when external callers do not supply them and returns the identifiers in the response headers so clients can retain the correlation ID.
- Every microservice calls `AddOpenTelemetryInstrumentation`, which now configures `LoggerFactoryOptions.ActivityTrackingOptions` to emit `TraceId`, `SpanId`, and `ParentId` metadata to the logger pipeline and OTLP exporters.
- Serilog configurations for Identity and KYC services enrich console/file output with `TraceId`/`SpanId`, allowing Grafana Loki labels to be derived without regex parsing.

## RabbitMQ Messaging

- `AuditRabbitMqConsumer` uses `Propagators.DefaultTextMapPropagator` to extract incoming context from message headers, starts a consumer span, and ensures dead-lettered messages retain the same trace metadata.
- When audit events lack a caller-supplied `CorrelationId`, the consumer and HTTP ingestion path fall back to the active `Activity.TraceId`, guaranteeing a Jaeger link for every stored record.

## Audit & Logging

- Admin Service’s ingestion normalisation now derives correlation IDs from the active trace and caches the value for bulk inserts, preserving cross-day hash chains while keeping audit lookups trace-aware.
- Shared audit client helpers automatically populate the `CorrelationId` when services emit audit events over HTTP or batches.
- Loki dashboards can filter logs via the `TraceId` property, and Jaeger deep links follow the pattern `https://jaeger.intellifin.local/trace/<trace-id>`.

## Operational Notes

1. **Edge Observability** – API Gateway logs every generated trace ID at `Debug` to assist in triaging missing context from upstream systems.
2. **Sampling** – Adaptive sampler still honours parent sampling decisions so traces initiated by external systems remain intact.
3. **Backwards Compatibility** – Legacy `X-Correlation-ID` headers are preserved, but W3C headers take precedence for downstream propagation.
4. **Alerting** – Missing trace context in audit ingestion is surfaced via existing ingestion metrics; a sustained absence should trigger investigation of upstream instrumentation.

For troubleshooting guidance, see the Observability runbook in `docs/runbooks/observability.md` (to be updated with the next sprint).
