#!/bin/bash
set -e

echo "Building IntelliFin Credit Assessment Service..."
echo ""

# Change to repository root
cd "$(dirname "$0")/../.."

# Add project to solution if not already added
echo "Adding project to solution..."
dotnet sln IntelliFin.sln add apps/IntelliFin.CreditAssessmentService/IntelliFin.CreditAssessmentService.csproj || true

# Restore dependencies
echo "Restoring NuGet packages..."
dotnet restore apps/IntelliFin.CreditAssessmentService/IntelliFin.CreditAssessmentService.csproj

# Build project
echo "Building project..."
dotnet build apps/IntelliFin.CreditAssessmentService/IntelliFin.CreditAssessmentService.csproj -c Release

# Run tests (when available)
# echo "Running tests..."
# dotnet test tests/IntelliFin.CreditAssessmentService.Tests/

echo ""
echo "✅ Build completed successfully!"
echo ""
echo "To run the service locally:"
echo "  cd apps/IntelliFin.CreditAssessmentService"
echo "  dotnet run"
echo ""
echo "To build Docker image:"
echo "  docker build -t intellifin/credit-assessment-service:latest -f apps/IntelliFin.CreditAssessmentService/Dockerfile ."
echo ""
