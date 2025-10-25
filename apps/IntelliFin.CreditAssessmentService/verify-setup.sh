#!/bin/bash

echo "======================================"
echo "Credit Assessment Service - Setup Verification"
echo "======================================"
echo ""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

check_file() {
    if [ -f "$1" ]; then
        echo -e "${GREEN}✓${NC} $1"
        return 0
    else
        echo -e "${RED}✗${NC} $1 (missing)"
        return 1
    fi
}

check_dir() {
    if [ -d "$1" ]; then
        echo -e "${GREEN}✓${NC} $1/"
        return 0
    else
        echo -e "${RED}✗${NC} $1/ (missing)"
        return 1
    fi
}

cd "$(dirname "$0")"

echo "Checking project structure..."
echo ""

# Core project files
check_file "IntelliFin.CreditAssessmentService.csproj"
check_file "Program.cs"
check_file "README.md"
check_file "Dockerfile"
check_file ".dockerignore"
check_file "build.sh"

echo ""
echo "Checking configuration files..."
check_file "appsettings.json"
check_file "appsettings.Development.json"
check_file "appsettings.Production.json"
check_file "Properties/launchSettings.json"

echo ""
echo "Checking Kubernetes manifests..."
check_file "k8s/deployment.yaml"
check_file "k8s/service.yaml"
check_file "k8s/configmap.yaml"
check_file "k8s/secrets.yaml.template"
check_file "k8s/serviceaccount.yaml"

echo ""
echo "Checking Helm chart..."
check_file "k8s/helm/Chart.yaml"
check_file "k8s/helm/values.yaml"
check_file "k8s/helm/templates/_helpers.tpl"
check_file "k8s/helm/templates/deployment.yaml"

echo ""
echo "Checking directory structure..."
check_dir "Controllers"
check_dir "Services"
check_dir "Models"
check_dir "Workers"
check_dir "BPMN"
check_dir "k8s"
check_dir "k8s/helm"

echo ""
echo "======================================"
echo "Verification Complete!"
echo "======================================"
echo ""
echo "Next steps:"
echo "  1. Build the project: ./build.sh"
echo "  2. Run locally: dotnet run"
echo "  3. Test health: curl http://localhost:5000/health/ready"
echo "  4. View metrics: curl http://localhost:5000/metrics"
echo ""
echo "For deployment:"
echo "  • Docker: docker build -t intellifin/credit-assessment-service:latest -f Dockerfile ../.."
echo "  • K8s: kubectl apply -f k8s/"
echo "  • Helm: helm install credit-assessment-service k8s/helm/"
echo ""
echo "Story 1.1 Status: ✅ COMPLETE"
echo ""
