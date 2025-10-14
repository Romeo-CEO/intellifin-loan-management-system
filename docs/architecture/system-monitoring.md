# System Monitoring Playbook

## Loki Log Streaming (FR23, NFR18)

- **Pipeline**: All services use `IntelliFin.Shared.Observability` to export OpenTelemetry logs to the Loki collector defined by
  `OpenTelemetry:Logs:OtlpEndpoint`. Logs are JSON encoded and include `trace_id`, `span_id`, and `resource.service.name` for
  correlation.
- **Redaction**: The shared `SensitiveDataLogProcessor` masks NRC identifiers (`123456/78/9`), Zambian phone numbers (`+260`),
  and any keys listed in `OpenTelemetry:Logs:SensitiveKeys`. Additional patterns can be appended via configuration without
  code changes.
- **Verification query**: In Loki, run
  ``{resource_service_name="IntelliFin.ApiGateway"} | json | line_format "{{.body}}"``
  and confirm values that match `nrcNumber` or phone fields appear as `***`.
- **Alerts**: Configure `sum by (service) (rate(otelcol_logs_exporter_failures_total[5m])) > 0` to detect exporter failures. A
  warning alert should page SREs when failures persist for more than 10 minutes.
- **Fallback**: When Loki is unavailable, services log a warning and continue operating. Platform engineering can temporarily
  switch `OpenTelemetry:Logs:OtlpEndpoint` to a local collector or file sink while Loki recovers. Document the temporary sink in
  the incident ticket and backfill the missing window once Loki is restored.

## Camunda Workflow Observability

- **Metric**: `camunda.workflow.failures`
  - **Source**: Admin Service (`Meter: IntelliFin.AdminService.Camunda`)
  - **Description**: Counts Camunda workflow invocations that returned non-success status codes. Tagged with `workflow_type` and `status_code`.
  - **Alert**: Fire `P1 - Camunda Workflow Failures` when `rate(camunda_workflow_failures_total[5m]) > 0` for 3 consecutive intervals *or* when the 15-minute rate exceeds `0.05`.
  - **Dashboard**: Grafana panel `Admin Service / Workflows` should visualise the failure rate split by `workflow_type` alongside successful throughput.

- **Log signal**
  - Search `resource.service.name="IntelliFin.AdminService"` and filter for `workflow_type` plus `camunda_status_code`. Each failure log includes the `correlationId` that matches the inbound API request for traceability.
  - Forward logs tagged with `severity >= Warning` and `workflow_type` to the governance SIEM for FR11 compliance evidence.

- **Tracing**
  - Ensure OpenTelemetry spans annotate the Camunda HTTP dependency. Failures should be marked with status `Error` and include `http.status_code`.

## Token Acquisition Monitoring

- **Metric**: `camunda.token.refresh.duration` (histogram emitted by Admin Service when token is refreshed)
  - **Alert**: Warning when 95th percentile refresh duration > 5 seconds over a 15-minute window; critical at > 10 seconds.
  - **Note**: Token refreshes should occur roughly every 5 minutes (matching Vault lease) with the configured 60-second safety buffer.

## Rollback Guardrails

- `CAMUNDA__FAILOPEN` must remain `false`. Add a configuration compliance rule that pages the control-plane team whenever the value deviates in production. Use either Gatekeeper (`ConstraintTemplate` on ConfigMaps/Secrets) or an Argo CD policy check.
- When fail-open is toggled, create an incident ticket automatically and attach the Prometheus snapshot of `camunda.workflow.failures` leading up to the event.
