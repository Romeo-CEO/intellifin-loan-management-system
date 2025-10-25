# IntelliFin Credit Assessment Service

## Overview

The Credit Assessment Service is IntelliFin's intelligent credit scoring and risk assessment engine. It provides automated, configurable, and auditable credit decisioning for loan applications with support for multiple product types (payroll loans and business loans).

### Key Features

- **Vault-Based Rule Engine**: Dynamic credit scoring rules configurable via HashiCorp Vault without code deployment
- **Multi-Product Support**: Separate assessment logic for government payroll loans and SME business loans
- **External Integration**: TransUnion credit bureau, PMEC government payroll, and Client Management KYC verification
- **Event-Driven Architecture**: Automatic assessment invalidation when KYC status changes
- **Comprehensive Audit Trail**: All decisions logged to AdminService for regulatory compliance
- **Decision Explainability**: Human-readable explanations for credit decisions
- **Camunda Integration**: Workflow orchestration with fallback to manual review
- **High Performance**: Sub-5-second assessment completion with Redis caching

## Architecture

### Technology Stack

- **.NET 9.0** - Runtime and framework
- **ASP.NET Core** - Web API framework
- **PostgreSQL 15** - Database (shared `LmsDbContext`)
- **Redis** - Caching layer for assessment results and bureau data
- **RabbitMQ** - Event bus for KYC status monitoring
- **HashiCorp Vault** - Configuration management for scoring rules
- **Camunda 8 (Zeebe)** - Workflow orchestration
- **Prometheus** - Metrics and monitoring
- **Serilog** - Structured logging

### Service Dependencies

| Service | Purpose | Required |
|---------|---------|----------|
| PostgreSQL | Database persistence | Yes |
| Redis | Caching and performance | Recommended |
| RabbitMQ | Event-driven KYC monitoring | Recommended |
| Vault | Rule configuration management | Yes |
| Client Management | KYC and employment data | Yes |
| AdminService | Audit trail logging | Yes |
| TransUnion API | Credit bureau data | Optional |
| PMEC API | Government payroll data | Optional |
| Camunda Zeebe | Workflow orchestration | Recommended |

## Getting Started

### Prerequisites

- .NET 9.0 SDK
- Docker and Docker Compose (for local infrastructure)
- PostgreSQL 15
- Redis 7.x
- RabbitMQ (optional for local dev)
- HashiCorp Vault (optional for local dev)

### Local Development Setup

#### 1. Clone the Repository

```bash
cd /workspace
```

#### 2. Start Infrastructure Dependencies

Using Docker Compose (recommended):

```bash
docker compose up -d postgres redis vault
```

Or manually configure:
- PostgreSQL on `localhost:5432`
- Redis on `localhost:6379`
- Vault on `localhost:8200`

#### 3. Configure Database

```bash
# Run migrations
cd libs/IntelliFin.Shared.DomainModels
dotnet ef database update --context LmsDbContext

# Or let the service auto-migrate on startup (development only)
```

#### 4. Configure Vault (Optional for Local Dev)

```bash
# Initialize Vault dev server
docker exec -it vault sh

# Enable secrets engine
vault secrets enable -path=secret kv-v2

# Load sample rules configuration
vault kv put secret/intellifin/credit-assessment/config @sample-rules.json
```

#### 5. Configure Application Settings

Update `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "LmsDatabase": "Host=localhost;Port=5432;Database=intellifin_lms_dev;Username=postgres;Password=postgres",
    "Redis": "localhost:6379"
  },
  "Vault": {
    "Address": "http://localhost:8200",
    "Token": "dev-token"
  }
}
```

#### 6. Build and Run

```bash
cd apps/IntelliFin.CreditAssessmentService
dotnet restore
dotnet build
dotnet run
```

The service will start on `http://localhost:5000` (or `https://localhost:5001`).

### Verify Service is Running

```bash
# Check liveness
curl http://localhost:5000/health/live

# Check readiness (includes database connectivity)
curl http://localhost:5000/health/ready

# View Prometheus metrics
curl http://localhost:5000/metrics

# Service info
curl http://localhost:5000/
```

Expected response:
```json
{
  "name": "IntelliFin.CreditAssessmentService",
  "status": "OK",
  "description": "Intelligent Credit Assessment and Risk Scoring Engine",
  "version": "1.0.0",
  "endpoints": {
    "health_live": "/health/live",
    "health_ready": "/health/ready",
    "metrics": "/metrics",
    "api_docs": "/openapi/v1.json"
  }
}
```

## API Documentation

### Health Check Endpoints

#### GET /health/live
Liveness probe - returns 200 OK if service is running

```bash
curl http://localhost:5000/health/live
```

#### GET /health/ready
Readiness probe - checks database connectivity and critical dependencies

```bash
curl http://localhost:5000/health/ready
```

Response:
```json
{
  "status": "Healthy",
  "checks": [
    {
      "name": "database",
      "status": "Healthy",
      "description": null,
      "duration": "00:00:00.0123456"
    }
  ]
}
```

### Monitoring Endpoints

#### GET /metrics
Prometheus metrics endpoint

```bash
curl http://localhost:5000/metrics
```

Returns Prometheus-formatted metrics including:
- HTTP request count and duration
- Database connection pool status
- Custom credit assessment metrics (to be added)

### Assessment API Endpoints

**Note**: API endpoints will be added in **Story 1.3** (Core Assessment Service API)

Planned endpoints:
- `POST /api/v1/credit-assessment/assess` - Perform credit assessment
- `GET /api/v1/credit-assessment/{assessmentId}` - Retrieve assessment details
- `GET /api/v1/credit-assessment/client/{clientId}/latest` - Get latest assessment for client
- `POST /api/v1/credit-assessment/{assessmentId}/manual-override` - Manual decision override

## Configuration

### Environment Variables

| Variable | Description | Required | Default |
|----------|-------------|----------|---------|
| `ASPNETCORE_ENVIRONMENT` | Environment (Development/Staging/Production) | No | Production |
| `ASPNETCORE_URLS` | HTTP listening URLs | No | http://+:8080 |
| `ConnectionStrings__LmsDatabase` | PostgreSQL connection string | Yes | - |
| `ConnectionStrings__Redis` | Redis connection string | No | localhost:6379 |
| `Vault__Address` | Vault server address | Yes | - |
| `Vault__Token` | Vault authentication token | Yes | - |

### Configuration Files

- `appsettings.json` - Base configuration
- `appsettings.Development.json` - Development overrides
- `appsettings.Production.json` - Production overrides

See [appsettings.json](./appsettings.json) for full configuration structure.

## Docker

### Build Docker Image

```bash
# From repository root
docker build -t intellifin/credit-assessment-service:latest \
  -f apps/IntelliFin.CreditAssessmentService/Dockerfile .
```

### Run Docker Container

```bash
docker run -d \
  --name credit-assessment-service \
  -p 8080:8080 \
  -e ConnectionStrings__LmsDatabase="Host=postgres;Database=intellifin_lms;Username=postgres;Password=postgres" \
  -e Vault__Address="http://vault:8200" \
  -e Vault__Token="your-vault-token" \
  intellifin/credit-assessment-service:latest
```

### Docker Compose

```yaml
version: '3.8'
services:
  credit-assessment-service:
    image: intellifin/credit-assessment-service:latest
    ports:
      - "8080:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__LmsDatabase=Host=postgres;Database=intellifin_lms;Username=postgres;Password=postgres
      - ConnectionStrings__Redis=redis:6379
      - Vault__Address=http://vault:8200
    depends_on:
      - postgres
      - redis
      - vault
```

## Kubernetes Deployment

### Using kubectl

```bash
# Apply all manifests
kubectl apply -f k8s/

# Or apply individually
kubectl apply -f k8s/serviceaccount.yaml
kubectl apply -f k8s/configmap.yaml
kubectl apply -f k8s/secrets.yaml  # Create from template first
kubectl apply -f k8s/deployment.yaml
kubectl apply -f k8s/service.yaml
```

### Using Helm

```bash
# Install
helm install credit-assessment-service ./k8s/helm \
  --namespace intellifin \
  --create-namespace \
  --set secrets.databaseConnectionString="Host=postgres;Database=intellifin_lms;Username=<USER>;Password=<PASS>" \
  --set secrets.vaultToken="<VAULT_TOKEN>"

# Upgrade
helm upgrade credit-assessment-service ./k8s/helm \
  --namespace intellifin

# Uninstall
helm uninstall credit-assessment-service --namespace intellifin
```

### Verify Deployment

```bash
# Check pod status
kubectl get pods -n intellifin -l app=credit-assessment-service

# Check logs
kubectl logs -n intellifin -l app=credit-assessment-service --tail=100 -f

# Port forward for local testing
kubectl port-forward -n intellifin svc/credit-assessment-service 8080:80

# Test health checks
curl http://localhost:8080/health/ready
```

## Development Roadmap

### ✅ Story 1.1: Service Scaffolding (Current)
- [x] Project structure and configuration
- [x] Health check endpoints
- [x] Prometheus metrics
- [x] Docker and Kubernetes manifests
- [x] README documentation

### ⏳ Story 1.2: Database Schema Enhancement
- [ ] Extend credit_assessments table with audit fields
- [ ] Add rule_evaluations table
- [ ] Add assessment_config_versions table
- [ ] EF Core migrations

### ⏳ Story 1.3: Core Assessment API
- [ ] REST API endpoints
- [ ] Request/response DTOs
- [ ] JWT authentication
- [ ] OpenAPI/Swagger documentation

### ⏳ Story 1.4: Core Logic Migration
- [ ] Migrate assessment service from Loan Origination
- [ ] Risk calculation engine
- [ ] Affordability analysis
- [ ] Decision generation

### ⏳ Story 1.5-1.7: External Integrations
- [ ] Client Management API client
- [ ] TransUnion credit bureau integration
- [ ] PMEC government payroll integration

### ⏳ Story 1.8-1.9: Vault-Based Rules
- [ ] Vault configuration service
- [ ] Dynamic rule evaluation engine
- [ ] Composite scoring algorithm

### ⏳ Story 1.10-1.13: Advanced Features
- [ ] Decision explainability
- [ ] AdminService audit integration
- [ ] KYC status event handling
- [ ] Manual override workflow

### ⏳ Story 1.14-1.15: Camunda Integration
- [ ] External task worker
- [ ] BPMN workflow definition

### ⏳ Story 1.16-1.20: Production Readiness
- [ ] Feature flags
- [ ] Performance optimization
- [ ] Comprehensive testing
- [ ] Monitoring and alerting
- [ ] Production deployment

## Testing

### Run Unit Tests

```bash
# From repository root
dotnet test tests/IntelliFin.CreditAssessmentService.Tests
```

### Run Integration Tests

```bash
dotnet test tests/IntelliFin.CreditAssessmentService.Tests --filter "Category=Integration"
```

### Manual Testing with cURL

```bash
# Health checks
curl -X GET http://localhost:5000/health/live
curl -X GET http://localhost:5000/health/ready

# Metrics
curl -X GET http://localhost:5000/metrics

# Assessment API (to be added in Story 1.3)
# curl -X POST http://localhost:5000/api/v1/credit-assessment/assess \
#   -H "Authorization: Bearer <token>" \
#   -H "Content-Type: application/json" \
#   -d '{"loanApplicationId":"uuid","clientId":"uuid","requestedAmount":50000}'
```

## Monitoring and Observability

### Prometheus Metrics

Available at `/metrics` endpoint:

- **HTTP Metrics**:
  - `http_requests_received_total` - Total HTTP requests
  - `http_request_duration_seconds` - HTTP request duration histogram
  
- **Custom Metrics** (to be added):
  - `credit_assessments_total` - Total assessments performed
  - `credit_assessment_duration_seconds` - Assessment processing time
  - `credit_decisions_by_category` - Decisions by category (Approved/Rejected/etc.)

### Structured Logging

Logs are output in JSON format for easy parsing:

```json
{
  "Timestamp": "2025-01-12T10:30:00.123Z",
  "Level": "Information",
  "MessageTemplate": "Credit assessment completed",
  "Properties": {
    "ServiceName": "CreditAssessmentService",
    "AssessmentId": "uuid",
    "Decision": "Approved",
    "Duration": 2.345
  }
}
```

### Health Checks

- **Liveness** (`/health/live`): Service is running
- **Readiness** (`/health/ready`): Service is ready to accept requests (database connected)

## Troubleshooting

### Service Won't Start

**Issue**: Service fails to start with database connection error

**Solution**:
1. Verify PostgreSQL is running: `docker ps | grep postgres`
2. Check connection string in `appsettings.Development.json`
3. Ensure database exists: `psql -h localhost -U postgres -l`
4. Run migrations: `dotnet ef database update`

### Health Check Failing

**Issue**: `/health/ready` returns 503 Unhealthy

**Solution**:
1. Check database connectivity
2. Review logs: `kubectl logs -n intellifin -l app=credit-assessment-service`
3. Verify all required services are running

### High Latency

**Issue**: Assessment requests taking > 5 seconds

**Solution**:
1. Enable Redis caching (`CreditAssessment:CacheEnabled = true`)
2. Check external service latency (TransUnion, PMEC, Client Management)
3. Review Prometheus metrics at `/metrics`
4. Check database query performance

## Contributing

### Code Style

- Follow existing C# coding conventions
- Use `async/await` for all I/O operations
- Add XML documentation comments for public APIs
- Write comprehensive unit tests (80%+ coverage target)

### Pull Request Process

1. Create feature branch from `master`
2. Implement changes following story acceptance criteria
3. Add/update tests
4. Update README if needed
5. Create pull request with clear description
6. Ensure CI/CD pipeline passes

## Support

For issues, questions, or contributions:

- **Documentation**: See `docs/domains/credit-assessment/` in repository
- **Architecture**: See `docs/domains/credit-assessment/brownfield-architecture.md`
- **Stories**: See `docs/domains/credit-assessment/stories/` for detailed implementation stories

## License

Copyright © 2025 IntelliFin. All rights reserved.

---

**Service Status**: 🟢 Story 1.1 Complete - Service Scaffolding Ready  
**Next Step**: Story 1.2 - Database Schema Enhancement  
**Version**: 1.0.0  
**Last Updated**: 2025-01-12
