#!/usr/bin/env bash
set -euo pipefail

if [[ -z "${ADMIN_SERVICE_URL:-}" ]]; then
  echo "ADMIN_SERVICE_URL environment variable is required" >&2
  exit 1
fi

if [[ -z "${FINANCIAL_DB_CONNECTION:-}" ]]; then
  echo "FINANCIAL_DB_CONNECTION environment variable is required" >&2
  exit 1
fi

echo "Starting FinancialService audit migration at $(date -u)"

dotnet run --project tools/AuditMigration/AuditMigration.csproj \
  --adminServiceUrl "$ADMIN_SERVICE_URL" \
  --financialConnection "$FINANCIAL_DB_CONNECTION"

echo "Migration completed"
