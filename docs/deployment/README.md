# Deployment Guide

## Prerequisites

- .NET 9 SDK
- Docker Desktop
- Node.js 20+ (for frontend)
- PowerShell 7+ (Windows) or Bash (Linux/macOS)

## Quick Start

### 1. Clone and Setup

```bash
git clone <repository-url>
cd "IntelliFin Loan Management System"
```

### 2. Start Infrastructure Services

```bash
# Start Docker services
docker-compose up -d

# Verify services are running
docker-compose ps
```

Expected services:
- SQL Server (port 31433)
- RabbitMQ (ports 35672, 15672)
- Redis (port 36379)
- MinIO (ports 39000, 39001)
- Vault (port 38200)

### 3. Database Setup

```bash
# Apply migrations
dotnet ef database update -p libs/IntelliFin.Shared.DomainModels -s libs/IntelliFin.Shared.DomainModels
```

### 4. Build and Run Services

```bash
# Build solution
dotnet build IntelliFin.sln -c Release

# Start services (in separate terminals)
dotnet run --project apps/IntelliFin.IdentityService -c Release
dotnet run --project apps/IntelliFin.ApiGateway -c Release
dotnet run --project apps/IntelliFin.ClientManagement -c Release
dotnet run --project apps/IntelliFin.LoanOrigination -c Release
dotnet run --project apps/IntelliFin.Communications -c Release
```

### 5. Verify Deployment

```bash
# Check health endpoints
curl http://localhost:5235/health  # IdentityService
curl http://localhost:5033/health  # API Gateway
curl http://localhost:5224/health  # ClientManagement
curl http://localhost:5193/health  # LoanOrigination
curl http://localhost:5218/health  # Communications
```

## Vault & Runtime Secrets

Both the Admin Service and API Gateway load credentials at runtime. Configure the
following Vault paths and environment variables before deploying:

### Vault KV / Database Roles

| Component | Vault Engine | Path / Role | Notes |
|-----------|--------------|-------------|-------|
| Admin Service SQL (control plane) | `database` | `database/creds/admin-service` | Dynamic MSSQL credentials used by `AdminDbContext` |
| Financial service SQL bridge | `database` | `database/creds/financial-service` | Dynamic MSSQL credentials used by `FinancialDbContext` |
| Identity SQL bridge | `database` | `database/creds/identity-service` | Dynamic MSSQL credentials for identity projections |
| Audit RabbitMQ | `kv` | `messaging/audit` | Must contain `username` and `password` keys |
| MinIO control plane access | `kv` | `object-storage/admin-service` | Must contain `accessKey` and `secretKey` keys |

Recommended Vault policies grant read on the paths above and the
`database/creds/*` roles, with 5-minute periodic tokens to satisfy FR18. Ensure
tokens are injected via environment variable `VAULT_ADMIN_TOKEN` in production
deployments.

### Required Environment Variables

| Service | Variable | Purpose |
|---------|----------|---------|
| Admin Service | `ADMIN_DB_USERNAME` / `ADMIN_DB_PASSWORD` | Local development fallback when Vault is disabled |
| Admin Service | `FINANCIAL_DB_USERNAME` / `FINANCIAL_DB_PASSWORD` | Financial bridge fallback |
| Admin Service | `IDENTITY_DB_USERNAME` / `IDENTITY_DB_PASSWORD` | Identity bridge fallback |
| Admin Service | `AUDIT_RABBITMQ_USERNAME` / `AUDIT_RABBITMQ_PASSWORD` | RabbitMQ credentials fallback |
| Admin Service | `MINIO_ACCESS_KEY` / `MINIO_SECRET_KEY` | MinIO fallback |
| Admin Service | `CAMUNDA__CLIENTID` / `CAMUNDA__CLIENTSECRET` | OAuth2 client credentials for Camunda workflow engine |
| Admin Service | `CAMUNDA__TOKENENDPOINT` | Keycloak token endpoint used for Camunda client credential flow |
| Admin Service | `CAMUNDA__SCOPE` | Optional OAuth2 scope when Camunda realm requires it |
| Admin Service | `CAMUNDA__FAILOPEN` | Emergency override (`false` by default); set to `true` only during coordinated rollback |
| API Gateway | `APIGATEWAY_DB_CONNECTION_STRING` | SQL connection string when Vault sidecar not used |
| API Gateway | `AUTHENTICATION__KEYCLOAKJWT__AUTHORITY` | Keycloak realm base URL used for metadata |
| API Gateway | `AUTHENTICATION__KEYCLOAKJWT__ISSUER` | Expected issuer for Keycloak access tokens |
| API Gateway | `AUTHENTICATION__KEYCLOAKJWT__AUDIENCE` | OAuth2 audience that the gateway validates |
| API Gateway | `AUTHENTICATION__KEYCLOAKJWT__REQUIREHTTPS` | Must remain `true` in non-development environments |

For local development you may instead create `appsettings.Development.json`
files from the provided templates and populate the `SecretFallbacks`/`Secrets`
sections. These files are gitignored to avoid committing credentials.

## OpenTelemetry Log Streaming to Loki (FR23, NFR18)

- **Collector endpoint**: Set `OpenTelemetry:Logs:OtlpEndpoint` to the HTTP/GRPC
  listener exposed by the Loki OpenTelemetry Collector (default:
  `http://otel-collector:4317`). When running locally, point the endpoint to the
  dev collector shipped with `infra/observability` or to `http://localhost:4317`.
- **Tenant headers**: Provide any Loki tenant headers under
  `OpenTelemetry:Logs:Headers`. Example:

  ```json
  "OpenTelemetry": {
    "OtlpEndpoint": "http://otel-collector:4317",
    "Logs": {
      "OtlpEndpoint": "http://otel-collector:4317",
      "Headers": {
        "X-Scope-OrgID": "intellifin-prod"
      }
    }
  }
  ```

- **PII redaction**: Maintain `OpenTelemetry:Logs:SensitiveKeys` with any JSON
  property names that should always be masked (e.g. `nrcNumber`,
  `phoneNumber`). Additional regex patterns can be added via
  `OpenTelemetry:Logs:RedactionPatterns` without redeploying code.
- **Exporter health**: Monitor the `otelcol_logs_exporter_failures_total`
  Prometheus metric. If the rate remains non-zero for 10 minutes, trigger the
  `loki-exporter-outage` alert and follow the fallback procedure below.
- **Fallback plan**:
  1. Update environment configuration to point
     `OpenTelemetry:Logs:OtlpEndpoint` at the regional standby collector or to a
     local file sink (`http://otel-proxy.dr:4317`).
  2. Capture the time window of missing logs in the incident ticket.
  3. Once Loki is restored, replay buffered logs from the standby collector and
     revert the endpoint.

## Keycloak Authentication Cutover (FR11)

- **Cutover date**: 2025-11-15 02:00 UTC during the weekly maintenance window.
- **Pre-cutover checklist**:
  1. Verify Keycloak realm `IntelliFin` exposes JWKS over HTTPS and that client
     credentials are provisioned for all downstream services.
  2. Confirm environment variables above are populated in the deployment
     manifests or Helm values.
  3. Notify dependent teams that legacy HMAC tokens will be rejected after the
     window.
- **Deployment steps**:
  1. Roll out container image `registry.intellifin.local/api-gateway:2025.11-keycloak`
     (built from this change set) to staging, execute smoke tests listed below,
     then promote to production.
  2. Monitor the `api-gateway-auth-failures` alert for 30 minutes. The alert
     fires when 401 responses exceed 5% of traffic in a 5-minute window.
  3. Coordinate with the IdentityService team to disable legacy token issuance
     after production gateways report healthy metrics.
- **Rollback plan**:
  - Re-deploy container tag `registry.intellifin.local/api-gateway:2025.10-legacy`.
  - Restore the previous Kubernetes `ConfigMap` or Helm values snapshot containing
    `Authentication:LegacyJwt` (see `release/2025.10-legacy-jwt` branch).
  - Notify consumers before rollback to avoid inconsistent authentication modes.
- **Smoke test**: `curl -H "Authorization: Bearer <keycloak-token>" -H "X-Correlation-ID: $(uuidgen)" https://gateway.intellifin.local/api/admin/ping`
  should return `200 OK` with `traceparent` header present.
- **Monitoring guidance**:
  - Prometheus: `sum(rate(http_requests_total{service="api-gateway",status="401"}[5m])) by (route)` with alert threshold of 5%.
  - Loki/App Insights: filter by `resource.service.name="IntelliFin.ApiGateway"`
    and `attributes.tokenType="Keycloak"` to isolate authentication failures.
  - Grafana dashboard `API Gateway / Auth` includes the `Keycloak metadata fetch`
    panel; repeated failures indicate HTTPS misconfiguration.

## Camunda OAuth2 Enforcement (FR8–FR11)

- **Configuration checklist**:
  1. Provision a confidential client in Keycloak with access to the Camunda REST API and grant it the `client_credentials` flow.
  2. Store the client secret in Vault (`kv/workflows/camunda`) and inject via `CAMUNDA__CLIENTSECRET` during deployment; never commit the secret to source control.
  3. Populate `CAMUNDA__TOKENENDPOINT`, `CAMUNDA__CLIENTID`, and optional `CAMUNDA__SCOPE` for each environment. `CAMUNDA__FAILOPEN` must remain `false` in production.
- **Runtime behaviour**:
  - The Admin Service fetches bearer tokens on demand and caches them for the configured lease duration. Tokens refresh automatically with a 60-second safety buffer.
  - Any non-success Camunda response raises a `CamundaWorkflowException`, returning `502 Bad Gateway` to callers with the upstream status code captured in `problemDetails.camundaStatus`.
  - Structured logs include `workflow_type`, `camunda_status_code`, and `correlationId`; metrics increment the `camunda.workflow.failures` counter for Prometheus scraping.
- **Monitoring guidance**:
  - Prometheus: alert when `rate(camunda_workflow_failures_total[5m]) > 0` for two consecutive intervals or when the rate exceeds 0.1 failures/second.
  - Loki: query `resource.service.name="IntelliFin.AdminService"` with `workflow_type` label to identify failing processes; filter on `camunda_status_code` for upstream root cause.
  - Grafana dashboard `Admin Service / Workflows` visualises token refresh latency and Camunda response codes (add panel using the metrics above).
- **Rollback plan**:
  - Short-term fail-open: set `CAMUNDA__FAILOPEN=true` (or update the Helm value) and restart the Admin Service. This bypasses Camunda orchestration and should only be used under executive approval.
  - Full rollback: redeploy container tag `registry.intellifin.local/admin-service:2025.10-camunda-fallback` and restore the previous configuration snapshot where workflows returned synthetic IDs. Document the incident and revert `FailOpen` to `false` once Camunda is healthy.

## Financial Service Audit Forwarding (FR12–FR14)

1. **Configuration**
   - Financial Service must be configured with the Admin Service base URL via the `AuditService` section:

     ```json
     "AuditService": {
       "BaseAddress": "https://adminservice.production.svc",
       "HttpTimeout": "00:00:30"
     }
     ```

   - Equivalent environment variables: `AUDITSERVICE__BASEADDRESS` and `AUDITSERVICE__HTTPTIMEOUT`.
   - Ensure the deployment manifest grants outbound HTTPS connectivity to the Admin Service cluster IP or ingress hostname.

2. **One-time migration of historical audit events**
   - Stop Financial Service write traffic (put the app into read-only maintenance mode) to freeze the legacy `AuditEvents` table.
   - Run the export script from an operations workstation with SQL access:

     ```powershell
     pwsh ./tools/migrations/financial-service/export-audit-events.ps1 \ 
       -SqlConnectionString "Server=sql-prod;Database=IntelliFin;User Id=...;Password=...;TrustServerCertificate=true;" \ 
       -AdminServiceBaseUrl "https://adminservice.production.svc"
     ```

   - The script sends batches of 500 events to `/api/admin/audit/events/batch` preserving chronological order and includes a `migrationSource` tag of `FinancialService.Sql` for downstream integrity reports.
   - Validate ingestion with:
     ```bash
     curl -s "$ADMIN_SERVICE/api/admin/audit/events?PageSize=1&Action=CollectionsPaymentRecorded" | jq
     ```

3. **Mark legacy tables read-only**
   - After export, execute SQL to revoke write access from the service principal:
     ```sql
     ALTER TABLE AuditEvents ADD CONSTRAINT CK_AuditEvents_ReadOnly CHECK (0 = 0);
     DENY INSERT, UPDATE, DELETE ON dbo.AuditEvents TO [lms_financial_rw];
     ```
   - EF Core now marks the entity as read-only (`SetIsReadOnlyBeforeSave/AfterSave`) so any accidental write attempt will throw before hitting the database.

4. **Operational verification**
   - Exercise core audit emitters (`POST /api/collections/payments`, `POST /api/pmec/deductions/submit`, `POST /api/gl/journal-entries`) and confirm Admin Service receives matching events via `/api/admin/audit/events?EntityType=CollectionsPayment`.
   - For read paths, call the new Financial Service proxy endpoints (`GET /api/audit/events`, `/api/audit/integrity/status`) and verify the payload matches the Admin Service responses.
   - Monitor `camunda.workflow.failures` and `audit.forwarding.failures` metrics in Grafana dashboard `Financial Service / Compliance` to confirm zero failures after cutover.

5. **Rollback**
   - If Admin Service availability issues require a rollback, deploy the previous Financial Service image tag (`registry.intellifin.local/financial-service:2025.10-audit-sql`) which still points to the local EF-backed audit service.
   - Re-enable SQL write permissions on `AuditEvents` and disable the `AuditService` section (set `AUDITSERVICE__BASEADDRESS` empty). Document the exception and plan a follow-up deployment once Admin Service stability is restored.
- **Smoke tests**:
  - `curl -H "Authorization: Bearer <admin-token>" -H "Content-Type: application/json" -d '{"justification":"test","duration":30,"requestedRoles":["Support"]}' https://admin.intellifin.local/api/admin/access/elevate` should return `202 Accepted` with a non-empty `processInstanceId`.
  - Use `vault kv get workflows/camunda` to confirm token credentials exist before deployment.

## Kubernetes Service Mesh (Linkerd)

For Kubernetes environments, install the Linkerd control plane and enable
mutual TLS between services:

```bash
# Bootstrap Linkerd (control plane, viz extension, namespace annotations)
./scripts/linkerd/bootstrap.sh

# Re-run after upgrades or cluster restores to verify TLS coverage
./scripts/linkerd/verify.sh
```

Key verification commands:

```bash
linkerd check
linkerd viz edges deploy -A
linkerd viz stat deploy -A --window 30s
```

Alerts for TLS handshake failures and Linkerd proxy restarts are shipped with
the observability chart once the mesh is active.

## Kubernetes NetworkPolicies

Apply the micro-segmentation policies after namespaces and Linkerd are in
place to enforce a default-deny stance across the platform:

```bash
kubectl apply -k infra/network-policies
```

Validate that required paths remain functional and unauthorized flows are
blocked:

```bash
./scripts/network-policies/test-network-policies.sh
```

The observability stack scrapes Calico metrics and raises the
`NetworkPolicyDeniesDetected` alert when the cluster records denied packets,
surfacing regressions quickly.

## MinIO WORM Audit Storage

Provision immutable audit buckets before enabling the Admin Service export
workers:

```bash
export MINIO_ENDPOINT=https://minio.intellifin.local
export MINIO_ACCESS_KEY=<root-user>
export MINIO_SECRET_KEY=<root-password>

./scripts/minio/minio-setup.sh
```

This script enables object lock, versioning, and a 10-year retention policy for
`audit-logs` and `audit-access-logs`. Replication between the primary and DR
clusters should be configured via `mc admin replicate` and monitored through the
Admin Service archive endpoints.

## Service Ports

| Service | Port | Purpose |
|---------|------|---------|
| IdentityService | 5235 | Authentication |
| API Gateway | 5033 | Unified API access |
| ClientManagement | 5224 | Client operations |
| LoanOrigination | 5193 | Loan processing |
| Communications | 5218 | Notifications |

## Infrastructure Ports

| Service | Port | Purpose |
|---------|------|---------|
| SQL Server | 31433 | Database |
| RabbitMQ | 35672 | Message broker |
| RabbitMQ Management | 15672 | Web UI |
| Redis | 36379 | Caching |
| MinIO | 39000 | Object storage |
| MinIO Console | 39001 | Web UI |
| Vault | 38200 | Secrets management |

## Configuration

### Environment Variables

```bash
# Database
CONNECTIONSTRINGS__DEFAULTCONNECTION="Server=localhost,31433;Database=IntelliFinLms;User Id=sa;Password=Your_password123;TrustServerCertificate=true"

# RabbitMQ
RABBITMQ__HOST="localhost"
RABBITMQ__PORT="35672"
RABBITMQ__USERNAME="guest"
RABBITMQ__PASSWORD="guest"

# JWT
JWT__ISSUER="IntelliFin.Identity"
JWT__AUDIENCE="intellifin-api"
JWT__SIGNINGKEY="dev-super-secret-signing-key-change-me-please-1234567890"
```

### Docker Compose Override

Create `docker-compose.override.yml` for local customizations:

```yaml
version: '3.8'
services:
  sqlserver:
    ports:
      - "1433:1433"  # Use standard port locally
```

## Testing the Deployment

### 1. Get Authentication Token

```bash
curl -X POST http://localhost:5235/auth/dev-token \
  -H "Content-Type: application/json" \
  -d '{"username":"dev","roles":["Admin"]}'
```

### 2. Test API Gateway

```bash
# Use token from step 1
curl -H "Authorization: Bearer <token>" \
  http://localhost:5033/api/clients/
```

### 3. Test Message Flow

```bash
# Create loan application
curl -X POST http://localhost:5033/api/origination/loan-applications \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{
    "clientId": "11111111-1111-1111-1111-111111111111",
    "amount": 50000,
    "termMonths": 12,
    "productCode": "PAYROLL"
  }'
```

Check Communications service logs for message consumption.

## Troubleshooting

### Common Issues

1. **Port conflicts**: Check if ports are already in use
2. **Docker not running**: Ensure Docker Desktop is started
3. **Database connection**: Verify SQL Server container is healthy
4. **Message broker**: Check RabbitMQ management UI

### Logs

```bash
# View service logs
docker-compose logs sqlserver
docker-compose logs rabbitmq

# View application logs
dotnet run --project apps/IntelliFin.ApiGateway --verbosity detailed
```

### Reset Environment

```bash
# Stop all services
docker-compose down -v

# Remove all containers and volumes
docker system prune -a --volumes

# Restart
docker-compose up -d
```
