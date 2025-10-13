# Observability Stack (Jaeger + OpenTelemetry + Prometheus/Grafana + Loki)

This chart provisions the baseline observability stack for Stories 1.7–1.9. It deploys a Jaeger
all-in-one instance with persistent Badger storage, an OpenTelemetry Collector DaemonSet that
fan-outs traces and logs to the appropriate backends, the kube-prometheus-stack (Prometheus,
Alertmanager, Grafana) for metrics/alerting, and a Loki + Promtail logging pipeline backed by MinIO
object storage.

## Components

- **Jaeger All-In-One Deployment** – Provides OTLP and native Jaeger collectors plus the query UI
  exposed via an HTTPS ingress at `https://jaeger.intellifin.local`.
- **Persistent Badger Storage** – Retains seven days of traces with daily compaction and
  automated cleanup to satisfy regulatory observability requirements.
- **OpenTelemetry Collector DaemonSet** – Receives OTLP traffic from the services (gRPC/HTTP),
  batches traces, and exports them to Jaeger while publishing OTLP-metrics to Prometheus.
- **Prometheus Operator Stack** – Installs Prometheus with 30-day retention, Alertmanager with
  Notification Gateway webhooks, kube-state-metrics, and node exporters.
- **Grafana** – Exposes dashboards at `https://grafana.intellifin.local` with Keycloak OIDC SSO,
  automatic ConfigMap dashboard discovery, and TLS ingress.
- **Prometheus ServiceMonitors** – Expose Jaeger admin metrics, collector pipeline metrics, and
  core IntelliFin workload metrics on a 15-second cadence.
- **Grafana Dashboards** – Provides curated dashboards for Kubernetes health, service golden
  signals, database performance, RabbitMQ queues, and Keycloak activity.
- **Loki StatefulSet** – Stores 90 days of logs in MinIO using boltdb-shipper + compactor retention
  with automatic cleanup and OTLP ingestion from the collector.
- **Promtail DaemonSet** – Scrapes pod stdout/stderr with NRC/phone-number redaction before
  forwarding to Loki.
- **Grafana Loki Data Source** – Pre-configured LogQL dashboard with sample queries for rapid log
  triage and correlation ID lookups.

## Usage

```bash
helm dependency build infra/observability
helm upgrade --install observability infra/observability \
  --namespace observability --create-namespace
```

Override `values.yaml` as needed (for example to tune resource requests, provide Alertmanager
receivers, or adjust ServiceMonitor selectors).

## Production-grade Jaeger topology (Story 1.29)

Story 1.29 introduces a multi-component Jaeger deployment backed by Elasticsearch storage. To
enable this topology, switch the chart to `production` mode using the provided override file:

```bash
helm dependency build infra/observability
helm upgrade --install observability infra/observability \
  --namespace observability --create-namespace \
  --values infra/observability/values-story-1-29.yaml
```

In production mode the chart provisions:

- **Jaeger Collector Deployment** – Three replicas with autoscaling (3–10) and OTLP/Jaeger receivers
  exposed on dedicated ports. Sampling strategies are configured via ConfigMap with a 10% default and
  100% sampling for the Admin Service.
- **Jaeger Query Deployment** – Two replicas serving the UI/API with TLS-protected ingress at
  `https://jaeger.intellifin.com` and admin metrics on port 16687.
- **Jaeger Agent DaemonSet** – Runs on every node with hostPorts for UDP ingestion and forwards spans
  to the collectors using gRPC.
- **Elasticsearch Storage Backend** – Configured via environment variables and CLI options (5 shards,
  1 replica, 10M bulk size) with a 7-day retention policy indicator.
- **Prometheus Scraping** – Dedicated ServiceMonitors for collector and query metrics.

All defaults can be tuned under `jaeger.production.*` in `values.yaml` if environment-specific
settings are required.

## BoZ cost governance dashboards (Story 1.30)

Story 1.30 expands the bundled Grafana content with a FinOps-focused dashboard titled
**BoZ Compliance - Cloud Cost & Budget Governance**. The dashboard expects Prometheus metrics that
mirror the data model outlined in the story (for example `azure_cost_daily_usd`,
`azure_budget_monthly_usd`, `azure_cost_anomaly_score`, `azure_cost_forecast_usd`, and
`azure_cost_recommendation_savings_usd`). When those metrics are scraped from the Admin Service's
cost management exporters, Grafana will surface:

- Current month spend, budget utilisation gauges, and daily cost trends per environment
- Cost allocation breakdowns by cost centre, service, and individual Azure resources
- Active cost anomalies with score, percentage increase, and root-cause hints
- Forecast versus actual spend with 95% confidence bands across 30/90/365-day horizons
- Optimisation recommendations ranked by projected monthly savings
- AKS namespace chargeback visualisations for Kubernetes workloads

After deploying the exporters, no additional Helm values are required—Grafana automatically loads the
dashboard from the `observability-grafana-dashboards` ConfigMap. Use the **Environment** drop-down to
filter the visualisations to dev, staging, production, or aggregate views when performing BoZ
compliance reporting.

## BoZ compliance operations dashboard (Story 1.31)

Story 1.31 layers an audit and governance focused dashboard named **BoZ Compliance Overview** into
Grafana. It is distributed from a dedicated `BoZ Compliance` folder so that Grafana RBAC can limit
access to Compliance Officers, Auditors, and executives per the story's role-matrix. The dashboard
refreshes every 30 seconds and is built on three backing data sources:

- **Prometheus compliance exporter** – Publishes KPIs such as `audit_events_total`,
  `audit_chain_integrity_status`, `access_certifications_*`, `security_incidents_*`,
  `security_violations_total`, `loan_classification_*`, `system_uptime_percent`,
  `business_rto_breach_total`, and `business_rpo_breach_total`. These originate from the Admin
  Service compliance metrics exporter described in Story 1.31.
- **PostgreSQL (Admin Service DB)** – Supplies tabular details for overdue access certifications,
  manual loan classification overrides, and compliance calendar deadlines using SQL queries run via
  the pre-provisioned `AdminPostgres` data source.
- **Elasticsearch audit index** – Provides last validation timestamps for audit-chain integrity and
  supports drill-down links into Kibana for detailed evidence gathering.

Once those data feeds exist, the dashboard surfaces:

- Audit coverage gauges with drill-down links to Kibana plus annual/daily/monthly volume analysis
- Access recertification completion gauges, overdue ownership tables, high-risk review counters, and
  countdown timers to the next quarterly deadline
- Real-time access violation counts with per-type trend visualisations sourced from Elasticsearch
- Security incident posture including MTTR, open incident aging, severity mix, and top categories
- Loan classification accuracy, NPL ratio gauges, reclassification trends, and override audit trails
- Platform availability compliance against BoZ RTO/RPO expectations

Use Grafana's **Export → PDF** action (enabled through the image renderer plugin) to create a BoZ
report, then store the generated artefact in the `compliance-reports` MinIO bucket to honour the
seven-year retention objective.

## Cost-performance executive dashboard (Story 1.32)

Story 1.32 introduces the **Cost-Performance Overview** dashboard for the CFO and finance team. The
dashboard resides in the default `IntelliFin` folder so it is available to executives alongside the
existing FinOps material. It pulls together the FinOps metrics emitted by the cost management
exporters (`service_cost_usd`, `service_cost_component_usd`, `service_cost_projection_month_end_usd`,
`service_cost_recommendation_*`, `service_cost_anomaly_percent`,
`service_cost_anomaly_impact_usd`, `service_cost_anomaly_root_cause`,
`cost_budget_monthly_usd`, `service_cost_forecast_*`, `tagged_cost_usd`,
`cost_tag_coverage_percent`, and `cost_alert_*`) together with operational telemetry sourced from
Prometheus (`business_loan_applications_total`, `business_active_users_total`, `api_requests_total`,
`http_request_duration_seconds_bucket`, `http_requests_total`, `http_request_errors_total`,
`service_uptime_percent`, `kube_pod_container_resource_requests_*`,
`container_cpu_usage_seconds_total`, `container_memory_working_set_bytes`, and
`pvc_usage_bytes`). A PostgreSQL query via the pre-defined `AdminPostgres` data source powers the
chargeback detail table.

Key dashboard groupings include:

- **Executive summary**: total month-to-date spend, budget utilisation, efficiency score, and active
  cost alerts with a direct link to the cost governance runbook.
- **Per-service cost breakdown**: top-ten services with CPU, memory, storage, and network cost
  allocations, projected month-end burn, and month-over-month deltas. A panel link pre-populates the
  Grafana CSV export for finance reconciliation.
- **Cost efficiency analytics**: cost-per-loan, cost-per-user, and cost-per-request trends visualised
  against the total processed business volume.
- **Resource efficiency**: CPU, memory, storage, and overall efficiency gauges plus a table of
  underutilised services (<30% CPU request consumption) to prioritise rightsizing.
- **SLA compliance**: per-service P50/P95/P99 latency, error rate, and uptime visualisations with
  target thresholds mirroring the platform SLAs (P95 <2 s, P99 <5 s, errors <1%, uptime 99.9%).
- **Cost anomaly watchlist**: severity-ranked anomalies with root cause hints and monthly impact.
- **Forecast and optimisation**: 90-day forecast bands and a savings table sorted by potential
  monthly reduction across rightsizing, reserved capacity, and spot usage recommendations.
- **Chargeback distribution**: domain/environment/team pie charts, the PostgreSQL-backed chargeback
  ledger with a one-click CSV link, and a tag coverage indicator built from `tagged_cost_usd`.
- **Executive alerts**: surfaced from `cost_alert_detail` to expose budget, SLA, and anomaly alarms
  that need action.

## Automated alerting & incident response (Story 1.33)

Story 1.33 activates critical alerting and the corresponding incident-response automation for the
platform. The Helm values define an Alertmanager configuration that fans CRITICAL alerts to
PagerDuty and Slack while routing WARNINGS to the DevOps email list. A dedicated inhibit rule keeps
WARNING notifications quiet when a higher-severity alert is already firing for the same service.

The `prometheus-rules.yaml` manifest now publishes a new `platform.incident-response` rule group
covering:

- `KeycloakDown` – triggered when no successful authentications are observed for five minutes. Plays
  against the `/playbooks/keycloak-down` runbook link in notifications.
- `AuditChainBreak` – fires as soon as `audit_chain_integrity_status` flips to `0` to protect the
  tamper-evident audit ledger.
- `MutualTlsHandshakeFailures` – catches bursts of Linkerd TLS errors (>10/minute) so certificates and
  trust bundles can be repaired.
- `PlatformHighErrorRate` – global HTTP 5xx ratio alert (>5% for five minutes) that references the
  high-error-rate playbook.
- `VaultUnavailable` – leverages the `up{job="vault"}` probe to detect Vault outages.
- `DatabaseConnectionPoolExhausted` – warns when the custom
  `intellifin_db_pool_available_connections` gauge drops to zero for three minutes, signalling client
  saturation.

Configure PagerDuty and Slack credentials through the new `alerting` section in `values.yaml` (for
example, set `alerting.pagerDuty.routingKey` and `alerting.slack.webhookUrl`). The email receiver
defaults to `devops@intellifin.com` but can be overridden with a list of recipients. The same section
also pins the incident playbook base URL so notifications link directly into the Admin UI.

On the response side, the Admin Service introduces an `IncidentResponseController` and backing
service that expose APIs for playbook management, Alertmanager silence orchestration, and
operational-incident tracking. Camunda workflows described by
`apps/IntelliFin.AdminService/Workflows/incident-response.bpmn` orchestrate auto-remediation steps and
post-incident review scheduling. The controller is reachable at
`/api/admin/incident-response/*` for authorised System Administrators, DevOps, and Security Engineers
and surfaces:

- Playbook CRUD operations so runbooks can be authored with diagnosis, resolution, and escalation
  guidance.
- Recording endpoints to measure playbook effectiveness/MTTR by linking runs to incidents.
- Alertmanager silence helpers that respect the default 120-minute window from configuration and log
  audit entries in SQL.
- Incident ingestion and resolution endpoints that start Camunda automation and optionally trigger
  postmortem workflows for major incidents.

Supply Alertmanager base URLs and Camunda process identifiers via the `IncidentResponse` section of
`appsettings*.json` or by overriding the Helm values. Once applied, the incident automation closes
the loop between Prometheus alerts, PagerDuty notifications, and the Camunda-driven response process
expected by Story 1.33.

All panels respect the `Environment` templating variable, defaulting to production while allowing
`All` aggregate or environment-specific focus. Refresh cadence is set to 60 seconds so the finance
and operations teams can monitor near real-time trends during month-end close.

## Validation Checklist

1. Port-forward the Jaeger query service and confirm traces are visible in the UI.
2. Verify OTLP ingestion by checking collector pod logs for `Exporting` entries.
3. Confirm Prometheus is scraping the generated ServiceMonitors (Jaeger, OTEL Collector, Loki,
   API Gateway, Admin Service, Loan Origination, Credit Bureau).
4. Ensure Grafana login succeeds via Keycloak and dashboards render live metrics with <15 second
   refresh intervals.
5. Trigger a test alert (e.g., scale API Gateway to zero) and verify Alertmanager forwards to the
   Notification Gateway webhook receiver.
6. Run a sample LogQL query from the **Centralized Logging Overview** dashboard (e.g., failed
   authentication attempts) and confirm PII redaction masks NRC/phone fields.
7. Ensure the persisted Badger volume reports data newer than seven days only (trace TTL
   enforcement), Prometheus retention adheres to 30-day policy, and Loki bucket contains 90 days of
   log data only (retention compactor status healthy).
