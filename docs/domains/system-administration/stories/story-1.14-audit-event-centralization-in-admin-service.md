# Story 1.14 – Audit Event Centralization in Admin Service

## Summary
- Added an extensible audit ingestion pipeline to the Admin Service including buffered inserts, SQL Server table-valued batching, RabbitMQ ingestion, and export/query APIs.
- Published a reusable shared audit client library consumed by Identity Service to forward audit events to the Admin Service without bespoke persistence code.
- Documented operational guidance, new configuration knobs, and the FinancialService migration path for consolidating historical audit records.

## Key Endpoints
| Endpoint | Description |
| --- | --- |
| `POST /api/admin/audit/events` | Accepts a single audit event payload. |
| `POST /api/admin/audit/events/batch` | Accepts up to 1,000 events for buffered ingestion. |
| `GET /api/admin/audit/events` | Paged query with filtering for compliance teams. |
| `GET /api/admin/audit/events/export` | CSV export of filtered audit events. |

## Configuration
```json
"AuditIngestion": {
  "BatchSize": 1000,
  "MaxBufferSize": 100000,
  "FlushIntervalSeconds": 5,
  "EnableRabbitMqConsumer": true
},
"AuditRabbitMq": {
  "HostName": "rabbitmq",
  "Port": 5672,
  "UserName": "audit",
  "Password": "ChangeMe123!",
  "QueueName": "admin-service.audit.events",
  "DeadLetterQueue": "admin-service.audit.events.dlq"
}
```

## Migration Checklist
1. Back up `FinancialService.AuditLogs`.
2. Deploy Admin Service schema migration (`20251015094500_UpdateAuditSchema`).
3. Execute `AuditMigrationService` or run `scripts/audit/migrate-financial-audit.ps1`.
4. Validate counts using the migration report output.

## RabbitMQ Flow
- Services publish to `audit.events` exchange.
- Admin Service consumer persists batches and acknowledges after success.
- Dead-letter queue captures failures for replay.

## Performance
- Buffered inserts leverage SQL Server TVPs for <20ms p95 ingestion.
- Background flush worker commits every 5 seconds or when 1,000 events accumulate.
- Buffer overflow protection trips at 100k enqueued events and emits Prometheus metrics (`audit_buffer_size`).

