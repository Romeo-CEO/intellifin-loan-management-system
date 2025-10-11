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

### Dashboards Included
- **Kubernetes Cluster Overview**: Node metrics, pod status, resource utilization
- **Service Health Dashboard**: Request rate, error rate, latency (p50/p95/p99)
- **Database Performance**: Query latency, connection pool, deadlocks
- **RabbitMQ Monitoring**: Queue depth, message rate, consumer lag
- **Keycloak Metrics**: Authentication rate, token issuance, active sessions

### Integration Verification
- **IV1**: Prometheus successfully scrapes all instrumented services
- **IV2**: Grafana dashboards display real-time metrics <15-second refresh
- **IV3**: Alert test confirms notification delivery within 1 minute
