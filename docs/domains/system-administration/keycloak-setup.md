# Keycloak Deployment and IntelliFin Realm Runbook

This runbook describes the production deployment of Keycloak 24.0.4 for the IntelliFin Loan Management System. It covers infrastructure automation, realm configuration, security controls, monitoring, and disaster-recovery procedures required for the Phase 1 identity migration.

## 1. Prerequisites

- **Kubernetes**: v1.27+ cluster with at least 6 vCPU and 8 GiB RAM dedicated to Keycloak and PostgreSQL.
- **Ingress**: NGINX Ingress Controller with TLS termination and HTTP2 enabled.
- **cert-manager**: ClusterIssuer `letsencrypt-prod` configured for `intellifin.local` certificates.
- **Vault**: HashiCorp Vault with `kv/data/prod/keycloak/*` secrets populated:
  - `postgresql.username`, `postgresql.password`, `postgresql.replicationPassword`, `postgresql.metricsPassword`
  - `admin.username`, `admin.password`, `admin.email`
  - `platform/smtp/intellifin.username`, `platform/smtp/intellifin.password`
  - `platform/minio/backups.accessKey`, `platform/minio/backups.secretKey`, `platform/minio/backups.drAccessKey`, `platform/minio/backups.drSecretKey`
- **External Secrets Operator**: ClusterSecretStore named `vault-production` pointing at Vault.
- **Monitoring Stack**: Prometheus Operator and Grafana with dashboard config maps auto-import enabled (`grafana_dashboard: "1"`).
- **MinIO**: Primary endpoint `https://minio.intellifin.local` and DR endpoint `https://minio-dr.intellifin.local` with bucket `intellifin-keycloak-backups`.

## 2. Deploy Keycloak Infrastructure

```bash
# From repository root
./tools/scripts/keycloak/deploy.sh
```

The script applies the `infra/keycloak` kustomization which provisions:

- `keycloak` namespace with `monitoring=enabled` label
- External Secrets resolving Vault credentials
- PostgreSQL 16 StatefulSet with WAL archiving and metrics sidecar
- Keycloak 24 Deployment (3 replicas) with HA cache stack and custom IntelliFin theme
- Ingress with TLS at `https://keycloak.intellifin.local`
- ServiceMonitor and Grafana dashboard config map
- NetworkPolicies isolating Keycloak and PostgreSQL traffic
- Daily pg_dump CronJob that writes encrypted archives to MinIO primary and DR buckets
- Bootstrap Job that enforces MFA and provisions the `keycloak-admin` user

> **Note**: The namespace manifest is applied first to ensure External Secrets are reconciled prior to workload startup.

### 2.1 PostgreSQL Cluster

- Image: `postgres:16.3`
- Persistent Volume: 200 GiB, `ReadWriteOnce`
- Custom configuration (`postgresql.conf`) sets `max_connections=200`, enables WAL archiving, and prepares replication user (`keycloak_repl`) and metrics user (`keycloak_metrics`).
- Sidecar: `postgres-exporter` exposes metrics on port `9187`.
- Health checks rely on `pg_isready` using the application credentials delivered via Vault.

### 2.2 Keycloak Deployment

- Image: `quay.io/keycloak/keycloak:24.0.4`
- Replicas: 3 with PodDisruptionBudget `minAvailable: 2`
- Enabled features: token exchange, admin fine-grained authorization, metrics, health endpoints
- Database connection pool tuned for 20–100 connections (`KC_DB_POOL_*`)
- Custom IntelliFin login/email themes mounted from ConfigMaps
- Realm JSON import executed on boot (`IntelliFin-realm.json`)
- SMTP configuration resolved via Vault secrets; TLS enforced for outbound mail
- Liveness/Readiness probes target `/health/live` and `/health/ready`

## 3. IntelliFin Realm Configuration

The realm import (`infra/keycloak/realm/IntelliFin-realm.json`) ensures:

- Realm name **IntelliFin** with display name *IntelliFin Loan Management System*
- Login and email themes set to `intellifin`
- MFA enforced: `CONFIGURE_TOTP` required action enabled by default and OTP policy set to 6-digit TOTP with 30s window
- SMTP defaults pointing to `smtp.intellifin.local`
- Realm roles:
  - `system-administrator` (composite over realm-management privileges)
  - `audit-viewer` (read-only events + realm visibility)
- Group `System Administrators` automatically mapped to the `system-administrator` role
- OIDC clients:
  - `admin-service` (confidential, service accounts enabled, redirect URIs under `https://admin.intellifin.local/*`)
  - `api-gateway` (bearer-only resource server with standard flow enabled for interactive token exchange)
- Client scope `audit-events` surfaces `audit:accessLevel` claim from user attribute `audit_access_level`

### 3.1 Admin Bootstrap

`keycloak-admin-bootstrap` Job waits for Keycloak readiness, authenticates using the master realm super user (stored in Vault), and:

1. Creates or updates `keycloak-admin` account inside IntelliFin realm.
2. Forces a non-temporary password (from Vault) and marks `CONFIGURE_TOTP` as required.
3. Grants realm-management roles and realm role `system-administrator`.
4. Ensures the user belongs to `System Administrators` group.
5. Verifies OTP policy remains strict (code reuse disabled, 6 digit, 30 second period).

The job is idempotent and can be re-run with:

```bash
kubectl delete job -n keycloak keycloak-admin-bootstrap --ignore-not-found
kubectl apply -f infra/keycloak/keycloak-admin-bootstrap-job.yaml
```

## 4. Access and Security Controls

- **Admin Console URL**: `https://keycloak.intellifin.local/admin`
- **Admin Account**: Username from Vault secret `kv/data/prod/keycloak/admin.username` (default `keycloak-admin`). Passwords must be rotated via Vault and re-applied using the bootstrap job.
- **MFA**: Admin user is forced to enroll in OTP before console access. OTP codes follow RFC 6238 with 6 digits and 30 second period.
- **NetworkPolicies**: Only ingress controller, monitoring namespace, and backup pods can reach Keycloak/DB.
- **Secrets Handling**: All credentials pulled from Vault using External Secrets Operator; no secrets exist in Git.
- **TLS**: cert-manager issues `keycloak-intellifin-tls`. Certificates renew automatically.

## 5. Monitoring & Alerting

- **Prometheus** scrapes Keycloak (`/metrics`) and PostgreSQL exporter (`:9187`).
- **Grafana Dashboard**: `IntelliFin Keycloak Overview` automatically imported (latency, login throughput, failures, session counts).
- **Health Checks**:
  - Liveness probe: `/health/live`
  - Readiness probe: `/health/ready`
- **Alerting Rules** (configure in Prometheus):
  - `up{job="keycloak"} == 0` for more than 3 minutes
  - `rate(keycloak_org_keycloak_events_total{type="LOGIN_ERROR"}[5m]) > 5` for suspicious login failures
  - `histogram_quantile(0.95, keycloak_org_keycloak_requests_duration_seconds_bucket) > 1` (latency SLA breach)

## 6. Backups and Disaster Recovery

### 6.1 Automated Backups

- CronJob `keycloak-postgresql-backup` runs daily at 01:00 CAT.
- Workflow:
  1. Init container `pgdump` performs `pg_dump` (custom format) and stores `keycloak-YYYYMMDDHHMMSS.dump.gz` in an in-memory volume.
  2. Main container uploads archive to MinIO primary and DR endpoints.
  3. Retention enforced via `mc find --older-than 720h` (30 days) on both buckets.

To verify backup success:

```bash
kubectl logs -n keycloak job/<job-name>
mc ls primary/intellifin-keycloak-backups/postgresql | tail
```

### 6.2 Restore Procedure

1. Scale Keycloak Deployment to zero:
   ```bash
   kubectl scale deploy/keycloak -n keycloak --replicas=0
   ```
2. Identify desired backup in MinIO (`keycloak-YYYYMMDDHHMMSS.dump.gz`).
3. Copy archive locally:
   ```bash
   mc cp primary/intellifin-keycloak-backups/postgresql/keycloak-YYYYMMDDHHMMSS.dump.gz ./restore.dump.gz
   ```
4. Exec into PostgreSQL pod and restore:
   ```bash
   gunzip -c restore.dump.gz | kubectl exec -i -n keycloak statefulset/keycloak-postgresql -- bash -c \
     "PGPASSWORD=\"$(kubectl get secret keycloak-db-credentials -n keycloak -o jsonpath='{.data.password}' | base64 -d)\" \
      pg_restore --clean --if-exists --no-owner -U $(kubectl get secret keycloak-db-credentials -n keycloak -o jsonpath='{.data.username}' | base64 -d) -d keycloak_db"
   ```
5. Remove backup job artifacts (`kubectl delete jobs -n keycloak --all` optional).
6. Scale Keycloak replicas back to three and monitor readiness.

### 6.3 DR Replication

Backups are uploaded to both primary and DR MinIO endpoints during the CronJob execution, guaranteeing a cold standby copy in the secondary Lusaka data centre. In addition, WAL files are archived under `/var/lib/postgresql/data/wal_archive` enabling point-in-time restore when paired with object storage replication.

## 7. Verification Checklist

| Category | Command / Action | Expected Result |
|----------|------------------|-----------------|
| Pods | `kubectl get pods -n keycloak` | 3 Keycloak pods Ready, 1 PostgreSQL pod Ready |
| Health | `curl -ks https://keycloak.intellifin.local/health/ready` | HTTP 200 JSON payload |
| Metrics | `kubectl port-forward svc/keycloak -n keycloak 8080:8080` then `curl http://localhost:8080/metrics` | Prometheus metrics stream including `keycloak_org_keycloak_events_total` |
| Realm | `kubectl exec deploy/keycloak -n keycloak -- /opt/keycloak/bin/kcadm.sh get realms/IntelliFin` | Realm exists with login/email theme `intellifin` |
| Clients | `kcadm.sh get clients -r IntelliFin -q clientId=admin-service` | `serviceAccountsEnabled: true`, redirect URI matches `admin.intellifin.local` |
| MFA | Attempt admin console login without configured OTP | Blocked until OTP configured |
| Backup | `kubectl get cronjobs -n keycloak keycloak-postgresql-backup` | `SCHEDULE: 0 1 * * *` and last schedule within 24h |

## 8. Operational Notes

- Realm updates should be performed through Git (modify `IntelliFin-realm.json`) followed by rolling restart.
- Theme changes are hot-reloaded on pod restart; ensure `styles=css/login.css` is preserved.
- For emergency admin password rotation, update Vault secret and re-run bootstrap job.
- API Gateway and Admin Service should validate tokens against `https://keycloak.intellifin.local/realms/IntelliFin` discovery endpoint.

## 9. Troubleshooting

| Symptom | Resolution |
|---------|------------|
| Keycloak pods crashloop with `Database is not reachable` | Verify PostgreSQL service `keycloak-postgresql` reachable, check secrets synced via External Secrets |
| Admin console accessible without OTP | Confirm bootstrap job completed successfully and user has `CONFIGURE_TOTP` pending. Re-run job if necessary. |
| Backups failing with MinIO authentication errors | Validate Vault credentials under `kv/data/prod/platform/minio/backups`, re-run CronJob manually using `kubectl create job --from=cronjob/keycloak-postgresql-backup -n keycloak backup-debug` |
| Grafana dashboard empty | Confirm Grafana is configured to watch `grafana_dashboard=1` ConfigMaps and set datasource UID substitution `${PROMETHEUS_UID}` |
| Realm import not applying | Delete Keycloak StatefulSet PVC to force re-import (data loss!) or apply targeted updates using `kcadm.sh` commands documented above. |

## 10. References

- [Keycloak Documentation](https://www.keycloak.org/documentation)
- [Keycloak Metrics Guide](https://www.keycloak.org/server/metrics)
- [PostgreSQL 16 Documentation](https://www.postgresql.org/docs/16/index.html)
- [MinIO Client (mc)](https://min.io/docs/minio/linux/reference/minio-mc.html)
