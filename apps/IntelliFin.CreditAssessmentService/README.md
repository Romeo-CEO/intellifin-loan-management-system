# IntelliFin Credit Assessment Service

The IntelliFin Credit Assessment Service is a standalone microservice that orchestrates credit scoring, affordability assessments, and regulatory compliance workflows for the IntelliFin lending platform. It exposes APIs and event-driven workers that evaluate loan applications, integrates with bureaus and government sources, and publishes rich audit trails for downstream systems.

## Table of Contents
- [Architecture Overview](#architecture-overview)
- [Prerequisites](#prerequisites)
- [Local Development](#local-development)
- [Configuration](#configuration)
- [Observability](#observability)
- [Health Checks](#health-checks)
- [Metrics](#metrics)
- [Docker](#docker)
- [Kubernetes Deployment](#kubernetes-deployment)
- [Next Steps](#next-steps)

## Architecture Overview

The service follows IntelliFin's microservice standards:

- **ASP.NET Core 9** with minimal hosting APIs and JWT authentication
- **Entity Framework Core** with a dedicated `CreditAssessmentDbContext`
- **Serilog** and **OpenTelemetry** for end-to-end observability
- **Prometheus** metrics exposed at `/metrics`
- **HashiCorp Vault** integration for rule and credential management with in-memory caching and graceful fallback
- **RabbitMQ/MassTransit** consumers and publishers for event-driven orchestration
- **Camunda 8 (Zeebe)** external workers for automated and manual review workflows

The repository layout reflects the functional domains implemented in stories 1.1–1.20:

```
apps/IntelliFin.CreditAssessmentService/
├── BPMN/                     # Camunda BPMN definitions
├── Controllers/              # REST controllers for the public API
├── Domain/                   # Domain models and aggregates
├── Extensions/               # Cross-cutting helpers
├── Infrastructure/           # Persistence, messaging, and integrations
├── Models/                   # DTOs (requests/responses)
├── Services/                 # Core services, rule engine, integrations
├── Workflows/                # Camunda external task workers
├── Program.cs                # Application entry point
├── Dockerfile                # Container build definition
├── README.md                 # This document
├── appsettings*.json         # Environment configuration
└── k8s/                      # Kubernetes and Helm resources
```

## Prerequisites

- .NET SDK 9.0.100 (global.json managed)
- Docker Engine 24+
- kubectl 1.29+
- Helm 3.14+
- Access to IntelliFin container registry
- Access to development Kubernetes cluster

## Local Development

1. Navigate to the service directory:
   ```bash
   cd apps/IntelliFin.CreditAssessmentService
   ```

2. Restore dependencies and build:
   ```bash
   dotnet restore
   dotnet build
   ```

3. Provide a PostgreSQL instance and update the `ConnectionStrings__CreditAssessmentDatabase` environment variable if required. For example:
   ```bash
   export ConnectionStrings__CreditAssessmentDatabase="Host=localhost;Port=5432;Database=intellifin_credit_assessment_dev;Username=postgres;Password=postgres"
   ```

4. Run the service locally:
   ```bash
   dotnet run
   ```

5. Test key endpoints:
   ```bash
   curl http://localhost:5000/health/live
   curl http://localhost:5000/health/ready
   curl http://localhost:5000/metrics
   ```

## Configuration

Configuration is sourced from `appsettings.json`, environment-specific overrides, and environment variables. Critical sections include:

- `ConnectionStrings:CreditAssessmentDatabase`: PostgreSQL connection string used by `CreditAssessmentDbContext`.
- `VaultRules`: HashiCorp Vault address and paths for rule, threshold, and credential retrieval.
- `TransUnion`, `Pmec`, `ClientManagement`: Endpoint, timeout, and retry policies for external integrations.
- `RabbitMq`: Messaging settings for publishing decision events and consuming workflow/KYC messages.
- `Zeebe`: Gateway and credential configuration for Camunda 8 workers.
- `FeatureFlags`: Controls gradual rollout of the standalone service, explainability, manual override, and event publishing.

Use environment variables (double underscore `__` separators) or Kubernetes secrets to override sensitive values at runtime.

## Observability

- Logging is configured with Serilog and enriched with machine, process, and thread metadata. Override logging levels in `appsettings.*.json` or via `Serilog__MinimumLevel__Default` environment variables.
- OpenTelemetry instrumentation is wired through `IntelliFin.Shared.Observability`. Configure OTLP exporters under the `OpenTelemetry` section.

## Health Checks

Two health check endpoints support Kubernetes probes:

- `GET /health/live`: Basic liveness response ensuring the process is running.
- `GET /health/ready`: Includes database connectivity verification via PostgreSQL.

Responses are JSON-formatted with status, duration, and check results for easy observability integration.

## Metrics

Prometheus metrics are exposed at `GET /metrics` via `prometheus-net`. Default HTTP request metrics are enabled, and placeholders exist for domain-specific metrics to be added in later stories.

## Docker

Build the Docker image locally:

```bash
docker build -t intellifin/credit-assessment-service:local .
```

Run the container (requires PostgreSQL connection):

```bash
docker run --rm -p 8080:8080 \
  -e ConnectionStrings__LmsDatabase="Host=host.docker.internal;Port=5432;Database=intellifin_lms_dev;Username=postgres;Password=postgres" \
  intellifin/credit-assessment-service:local
```

## Kubernetes Deployment

A baseline Kubernetes manifest and Helm chart are provided under `k8s/`:

- `k8s/configmap.yaml`: Non-sensitive configuration defaults.
- `k8s/secrets.yaml`: Template for secrets managed via Vault/Kubernetes secrets.
- `k8s/deployment.yaml`: Deployment with health probes and resource constraints.
- `k8s/service.yaml`: ClusterIP service exposing port 8080.
- `k8s/helm/Chart.yaml` & `values.yaml`: Helm chart for templated deployments.

### Deploy Steps

1. Update container image references in `values.yaml` or set `--set image.tag` during Helm install.
2. Create/update secrets:
   ```bash
   kubectl apply -f k8s/secrets.yaml
   kubectl apply -f k8s/configmap.yaml
   ```
3. Deploy via Helm:
   ```bash
   helm upgrade --install credit-assessment-service k8s/helm \
     --namespace lending --create-namespace
   ```
4. Verify deployment health:
   ```bash
   kubectl get pods -n lending -l app=credit-assessment-service
   kubectl get svc -n lending credit-assessment-service
   ```
5. Port-forward to test endpoints:
   ```bash
   kubectl port-forward svc/credit-assessment-service 8080:8080 -n lending
   curl http://localhost:8080/health/ready
   curl http://localhost:8080/metrics
   ```

## Next Steps

Subsequent stories will introduce the assessment API surface, rule engine, integrations, and workflow workers. This scaffold establishes consistent infrastructure primitives to support those increments.
