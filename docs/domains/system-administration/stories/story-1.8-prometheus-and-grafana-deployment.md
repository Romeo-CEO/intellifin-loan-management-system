# Story 1.8: Prometheus and Grafana Deployment

### Metadata
- **ID**: 1.8 | **Points**: 5 | **Effort**: 3-5 days | **Priority**: P1
- **Dependencies**: 1.6 (OpenTelemetry metrics), Kubernetes
- **Blocks**: 1.30, 1.31, 1.32

### User Story
**As a** DevOps engineer,
**I want** Prometheus and Grafana deployed collecting metrics from all services,
**so that** I can monitor system health, performance, and compliance KPIs.

### Acceptance Criteria
1. Prometheus deployed via Helm chart (kube-prometheus-stack)
2. ServiceMonitor resources created for automatic scraping
3. Prometheus retention 30 days, 15-second scrape intervals
4. Grafana deployed with Prometheus data source pre-configured
5. Initial dashboards imported (Kubernetes cluster, service health, API latency)
6. Alertmanager configured with basic alert rules
7. Grafana accessible at `https://grafana.intellifin.local` with Keycloak SSO

### Implementation Notes
- Extended the `infra/observability` chart to vendor the `kube-prometheus-stack` Helm dependency,
  configuring Prometheus with 30-day retention, 15-second scraping, and Operator selectors that
  discover ServiceMonitors across namespaces.
- Enabled Alertmanager with webhook receivers targeting the Notification Gateway along with
  platform alert rules (API Gateway 5xx, latency SLO, Keycloak auth failures, database deadlocks).
- Bootstrapped Grafana with HTTPS ingress, Keycloak OIDC authentication, automatic dashboard
  discovery, and an external secret mount for the client credentials.
- Supplied opinionated dashboards for cluster health, service golden signals, database
  performance, RabbitMQ throughput, and Keycloak activity to fast-track observability adoption.
- Generated ServiceMonitors for core IntelliFin services (API Gateway, Admin Service, Loan
  Origination, Credit Bureau) so the stack scrapes workloads immediately after deployment.

### Integration Verification
- **IV1**: Prometheus successfully scrapes all instrumented services (ServiceMonitor interval fixed at
  15 seconds and selectors allow cross-namespace discovery).
- **IV2**: Grafana dashboards display real-time metrics (<15-second refresh) using the pre-configured
  Prometheus data source and foldered dashboard ConfigMaps.
- **IV3**: Alertmanager routes simulated alerts to Notification Gateway webhooks, covering both
  default and critical severities for operations response drills.
