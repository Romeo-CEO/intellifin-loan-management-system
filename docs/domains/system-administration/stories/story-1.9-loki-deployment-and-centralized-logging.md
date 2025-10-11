# Story 1.9: Loki Deployment and Centralized Logging

### Metadata
- **ID**: 1.9 | **Points**: 5 | **Effort**: 3-5 days | **Priority**: P1
- **Dependencies**: 1.6 (OpenTelemetry logs), MinIO
- **Blocks**: 1.17

### User Story
**As a** DevOps engineer,  
**I want** Loki deployed collecting logs from all services via Promtail,  
**so that** I can perform centralized log search and analysis without SSH-ing to pods.

### Acceptance Criteria
1. Loki deployed via Helm chart with MinIO backend
2. Promtail deployed as DaemonSet scraping pod logs (stdout/stderr)
3. OpenTelemetry Collector exports structured logs to Loki via OTLP
4. Log retention 90 days with automated deletion
5. Grafana Loki data source configured with LogQL examples
6. Sample LogQL queries validated (error logs, audit events, correlation IDs)
7. PII redaction configured via Promtail pipeline

### LogQL Examples
```
# Error logs from all services
{namespace="intellifin"} |= "error" | json

# Audit events with specific correlation ID
{namespace="admin"} | json | CorrelationId="abc-123-xyz"

# High-latency API requests
{service_name="api-gateway"} | json | latency_ms > 1000

# Failed authentication attempts
{service_name="keycloak"} |= "authentication failed" | json | line_format "{{.username}} from {{.ip}}"
```

### PII Redaction Pipeline
```yaml
# promtail-config.yaml
scrape_configs:
  - job_name: kubernetes-pods
    pipeline_stages:
      - regex:
          expression: '(?P<nrc>\d{6}/\d{2}/\d{1})'
          replace: '******/**/**'
      - regex:
          expression: '(?P<phone>\+260\d{9})'
          replace: '+260*********'
```

### Integration Verification
- **IV1**: Existing file-based logging functional (dual logging during transition)
- **IV2**: Log search performance <3 seconds for 1-hour queries
- **IV3**: Log volume within NFR8 estimate (2TB for 90-day retention)
