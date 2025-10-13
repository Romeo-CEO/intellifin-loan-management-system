#!/usr/bin/env bash
set -euo pipefail

if ! command -v mc >/dev/null 2>&1; then
  echo "MinIO client (mc) not found on PATH" >&2
  exit 1
fi

: "${MINIO_ALIAS:=intellifin}"
: "${MINIO_ENDPOINT:?Set MINIO_ENDPOINT (e.g. https://minio.intellifin.local)}"
: "${MINIO_ACCESS_KEY:?Set MINIO_ACCESS_KEY}" 
: "${MINIO_SECRET_KEY:?Set MINIO_SECRET_KEY}" 

mc alias set "$MINIO_ALIAS" "$MINIO_ENDPOINT" "$MINIO_ACCESS_KEY" "$MINIO_SECRET_KEY"

if ! mc ls "$MINIO_ALIAS"/audit-logs >/dev/null 2>&1; then
  mc mb --with-lock "$MINIO_ALIAS"/audit-logs
  mc version enable "$MINIO_ALIAS"/audit-logs
fi

if ! mc ls "$MINIO_ALIAS"/audit-access-logs >/dev/null 2>&1; then
  mc mb --with-lock "$MINIO_ALIAS"/audit-access-logs
fi

mc retention set --default COMPLIANCE "3654d" "$MINIO_ALIAS"/audit-logs
mc retention set --default COMPLIANCE "3654d" "$MINIO_ALIAS"/audit-access-logs

cat <<POLICY | mc ilm import "$MINIO_ALIAS"/audit-logs
{
  "Rules": [
    {
      "ID": "expire-after-11-years",
      "Status": "Enabled",
      "Expiration": { "Days": 4018 },
      "Filter": { "Prefix": "" }
    }
  ]
}
POLICY

echo "MinIO WORM buckets configured successfully"
