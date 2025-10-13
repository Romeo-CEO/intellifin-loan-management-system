# MinIO WORM Audit Storage Runbook

This document captures the operational procedures and configuration details for the IntelliFin audit archive that relies on MinIO object lock in COMPLIANCE mode.

## Buckets
- `audit-logs`: primary immutable archive (10-year retention + 1-day grace)
- `audit-access-logs`: immutable bucket capturing MinIO server access events

Both buckets must be created with object lock enabled and versioning turned on. Use `scripts/minio/minio-setup.sh` to enforce retention policies.

## Daily Export Flow
1. `AuditArchiveExportWorker` runs at midnight UTC.
2. Previous day's events are read in timestamp order, serialized to JSONL, compressed, and uploaded with COMPLIANCE retention.
3. Metadata (`chain-metadata.json`) and `verify.py` companion script are stored alongside the archive.
4. SQL Server table `AuditArchiveMetadata` is updated with event counts, hashes, replication status, and retention expiry.
5. Audit events older than 90 days are marked `MigrationSource = 'ARCHIVED'` for cleanup visibility.

## Replication Monitoring
- `AuditArchiveReplicationMonitor` polls MinIO on a configurable interval (default 30 minutes) and updates the replication status column.
- Grafana dashboards should visualise `ReplicationStatus`, `LastReplicationCheckUtc`, and derived lag metrics.

## API Surface
- `GET /api/admin/audit/archive/search`: filter by date range, returns archive metadata including retention expiry and replication status.
- `GET /api/admin/audit/archive/download/{archiveId}`: returns a presigned URL; access is audited and `LastAccessedAtUtc` is updated.

## Access Logging
Configure MinIO webhook logging to `https://admin-service.intellifin.local/api/admin/audit/minio-access` (see `infra/minio/README.md`). Access logs are stored in the `audit-access-logs` bucket for forensic review.

## Disaster Recovery
- Site replication must be configured from the primary MinIO deployment to the DR site.
- `AuditArchiveMetadata.StorageLocation` tracks where the authoritative copy resides (`PRIMARY` or `DR`).
- After failover, update the Admin Service configuration to point at the promoted endpoint and rerun `scripts/minio/minio-setup.sh` to verify retention state.

## Manual Verification
To validate an exported archive offline:
```bash
python verify.py audit-events-2025-10-15.jsonl.gz
```
The script exits non-zero on any hash mismatch or chain break.

## Alerting
- Alert when the export worker fails (no archive for previous day by 02:00 UTC).
- Alert when replication status remains `PENDING` for more than 60 minutes.
- Alert on MinIO access log entries that originate outside the compliance team service accounts.
