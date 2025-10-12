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
