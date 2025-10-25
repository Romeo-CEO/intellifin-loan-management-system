#!/bin/bash
set -e

echo "====================================="
echo "EF Core Migration - Story 1.2"
echo "Credit Assessment Audit Enhancements"
echo "====================================="
echo ""

cd "$(dirname "$0")/.."

echo "Creating migration: CreditAssessmentAuditEnhancements"
echo ""

# Create the migration
dotnet ef migrations add CreditAssessmentAuditEnhancements \
    --context LmsDbContext \
    --output-dir Migrations \
    --verbose

echo ""
echo "✅ Migration created successfully!"
echo ""
echo "To apply the migration:"
echo "  dotnet ef database update --context LmsDbContext"
echo ""
echo "To generate SQL script:"
echo "  dotnet ef migrations script --context LmsDbContext --output migration.sql"
echo ""
echo "To rollback:"
echo "  dotnet ef database update <PreviousMigrationName> --context LmsDbContext"
echo ""
