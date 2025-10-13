# MinIO WORM Audit Storage

This chart-free manifest set provides guidance for provisioning MinIO with immutable audit buckets that satisfy the Bank of Zambia ten-year retention requirement.

## Prerequisites
- MinIO cluster deployed in the primary data centre and a secondary disaster recovery site
- MinIO client (`mc`) installed on the operator workstation
- TLS certificates for the MinIO API and console endpoints

## Bootstrap Steps
1. Deploy the base MinIO cluster (for local smoke tests you can reuse the `docker-compose` sample below).
2. Run `scripts/minio/minio-setup.sh` with the appropriate `MINIO_ENDPOINT`, `MINIO_ACCESS_KEY`, and `MINIO_SECRET_KEY` environment variables to create the immutable buckets.
3. Configure site replication from the primary to the DR cluster:
   ```bash
   mc admin replicate add primary minio-replication --path audit-logs --arn REPLICATION_ARN --priority 1
   ```
4. Enable access logging to the Admin Service webhook (optional but recommended):
   ```bash
   mc admin config set primary logger_webhook:audit_access endpoint="https://admin-service.intellifin.local/api/admin/audit/minio-access"
   mc admin service restart primary
   ```

## Local Development (docker-compose)
```yaml
docker-compose:
  version: '3.8'
  services:
    minio:
      image: minio/minio:RELEASE.2024-01-01T00-00-00Z
      command: server /data --console-address ":9001"
      ports:
        - "9000:9000"
        - "9001:9001"
      environment:
        MINIO_ROOT_USER: minioadmin
        MINIO_ROOT_PASSWORD: minioadmin
      volumes:
        - ./data:/data
```

## Monitoring
- Prometheus scrape the MinIO `/minio/prometheus/metrics` endpoint.
- Alert when `minio_cluster_replication_lag_seconds` exceeds 3600 seconds.
- Forward MinIO audit access logs to the Admin Service API for additional correlation.

## Disaster Recovery
- Replication lag and status are tracked through the Admin Service `AuditArchiveMetadata` table.
- Quarterly failover tests must validate that archived objects remain protected by object lock after promotion of the DR cluster.
