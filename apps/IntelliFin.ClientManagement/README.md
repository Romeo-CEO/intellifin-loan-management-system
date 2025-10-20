# IntelliFin Client Management Service

**Status:** Foundation Complete (Story 1.1) ✅  
**Version:** 1.0.0  
**Framework:** .NET 9.0

---

## Overview

The Client Management service is the single source of truth for all customer data in the IntelliFin system. It provides comprehensive KYC/AML compliance workflows, document lifecycle management, risk scoring, and regulatory reporting capabilities.

## Current Implementation Status

### ✅ Story 1.1: Database Foundation & EF Core Setup (COMPLETED)

**What's Working:**
- Database infrastructure with EF Core 9.0
- HashiCorp Vault integration for connection strings
- Health check endpoints (`/health`, `/health/db`)
- Initial migration infrastructure
- Integration tests with TestContainers (7 tests)

**Database:** `IntelliFin.ClientManagement`  
**Service Account:** `client_svc` (to be configured)

### ✅ Story 1.2: Shared Libraries & Dependency Injection (COMPLETED)

**What's Working:**
- Correlation ID middleware with auto-generation
- Global exception handler with consistent error responses
- JWT authentication (secret key validation for development)
- Serilog structured logging with correlation ID enricher
- FluentValidation infrastructure
- Result<T> pattern for operation outcomes
- Integration tests (12 tests)

**Middleware:** Correlation ID → Exception Handler → Auth → Authorization  
**Logging:** Structured with correlation IDs in all entries  
**Authentication:** JWT bearer tokens with claims-based authorization

### 🚧 In Progress / Upcoming

- **Story 1.3:** Client CRUD Operations (Next)
- **Story 1.4:** Client Versioning (SCD-2)
- **Story 1.5:** AdminService Audit Integration
- **Story 1.6:** KycDocument Integration
- **Story 1.7:** Communications Integration
- ... (17 stories total, 2 complete)

---

## Architecture

### Clean Architecture / DDD

```
IntelliFin.ClientManagement/
├── Controllers/              # API endpoints (thin layer)
├── Domain/
│   ├── Entities/            # Core domain models (Story 1.3+)
│   ├── Events/              # Domain events
│   └── ValueObjects/        # NRC, PayrollNumber
├── Services/                # Business logic
├── Workflows/
│   └── CamundaWorkers/      # Zeebe job workers (Story 1.9+)
├── Infrastructure/
│   ├── Persistence/         # ✅ EF Core DbContext
│   │   ├── ClientManagementDbContext.cs
│   │   └── Migrations/
│   └── Vault/               # ✅ VaultService
└── Extensions/              # ✅ DI configuration
```

---

## Configuration

### Connection Strings

**Development (appsettings.Development.json):**
```json
{
  "ConnectionStrings": {
    "ClientManagement": "Server=localhost,1433;Database=IntelliFin.ClientManagement;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=true;"
  }
}
```

**Production (Vault):**
- **Path:** `intellifin/db-passwords/client-svc`
- **Key:** `connectionString`

### Vault Configuration

```json
{
  "Vault": {
    "Endpoint": "http://localhost:8200",
    "Token": "dev-token",
    "ConnectionStringPath": "intellifin/db-passwords/client-svc"
  }
}
```

### Environment Variables

```bash
# Required for Vault
VAULT_TOKEN=<token>

# Optional overrides
ConnectionStrings__ClientManagement=<connection-string>
Vault__Endpoint=http://vault:8200
```

---

## Running the Service

### Prerequisites

1. .NET 9.0 SDK
2. SQL Server 2022 (or Docker container)
3. HashiCorp Vault (optional for development)

### Local Development

```bash
# Start SQL Server (Docker)
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrong!Passw0rd" \
   -p 1433:1433 --name sqlserver \
   -d mcr.microsoft.com/mssql/server:2022-latest

# Apply migrations
cd apps/IntelliFin.ClientManagement
dotnet ef database update

# Run service
dotnet run
```

### Health Checks

```bash
# General health
curl http://localhost:5000/health

# Database health
curl http://localhost:5000/health/db
```

**Expected Response (Healthy):**
```
HTTP/1.1 200 OK
Healthy
```

---

## Testing

### Running Tests

```bash
# All tests
dotnet test tests/IntelliFin.ClientManagement.IntegrationTests

# Specific test
dotnet test --filter "FullyQualifiedName~DbContextTests"

# With detailed output
dotnet test --logger "console;verbosity=detailed"
```

### Test Coverage

**Story 1.1 Tests (7 tests):**
- ✅ Database connection tests
- ✅ Migration application tests
- ✅ Query execution tests
- ✅ Health check tests (healthy + unhealthy scenarios)

**Test Infrastructure:**
- xUnit test framework
- FluentAssertions for readable assertions
- TestContainers for SQL Server 2022
- Moq for mocking (future use)

---

## API Endpoints

### Current Endpoints

| Endpoint | Method | Description | Status |
|----------|--------|-------------|--------|
| `/` | GET | Service information | ✅ Working |
| `/health` | GET | General health check | ✅ Working |
| `/health/db` | GET | Database health check | ✅ Working |

### Future Endpoints (Story 1.3+)

| Endpoint | Method | Description | Story |
|----------|--------|-------------|-------|
| `POST /api/clients` | POST | Create client | 1.3 |
| `GET /api/clients/{id}` | GET | Get client by ID | 1.3 |
| `PUT /api/clients/{id}` | PUT | Update client | 1.3 |
| `DELETE /api/clients/{id}` | DELETE | Soft delete client | 1.3 |
| `GET /api/clients?nrc=...` | GET | Search by NRC | 1.3 |

---

## Database Schema

### Current State (Story 1.1)

**Migration:** `20251020000000_InitialCreate`  
**Tables:** None (migration infrastructure only)

### Future Tables (Story 1.3+)

| Table | Story | Purpose |
|-------|-------|---------|
| `Clients` | 1.3 | Core client records |
| `ClientVersions` | 1.4 | Temporal versioning (SCD-2) |
| `ClientDocuments` | 1.6 | Document metadata |
| `CommunicationConsents` | 1.7 | Communication preferences |
| `RiskProfiles` | 1.13 | Risk assessment data |
| `AmlScreenings` | 1.13 | AML screening results |
| `ClientEvents` | 1.15 | Event logging |

---

## Dependencies

### NuGet Packages

| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.EntityFrameworkCore.SqlServer | 9.0.0 | SQL Server provider |
| Microsoft.EntityFrameworkCore.Design | 9.0.0 | Migration tooling |
| Microsoft.EntityFrameworkCore.Tools | 9.0.0 | CLI tools |
| AspNetCore.HealthChecks.SqlServer | 8.0.0 | Database health checks |
| VaultSharp | 1.17.5.1 | HashiCorp Vault client |

### Project References

- `IntelliFin.Shared.Observability` - OpenTelemetry instrumentation

---

## Deployment

### Docker Support (Future)

```dockerfile
# Dockerfile will be added in future stories
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
# ... (to be added)
```

### Kubernetes Configuration (Future)

```yaml
# k8s/deployment.yaml will be added in future stories
apiVersion: apps/v1
kind: Deployment
# ... (to be added)
```

### Migration Strategy

**Development:**
- Apply migrations manually: `dotnet ef database update`

**Production:**
- Use EF Core migrations bundle in init container
- Manual approval required for schema changes

---

## Security

### Data Protection

- ✅ TLS 1.3 in transit
- ⏳ SQL Server TDE at rest (Story 1.3+)
- ⏳ MinIO SSE-S3 encryption (Story 1.6+)
- ⏳ JWT bearer token authentication (Story 1.2)
- ⏳ Claims-based authorization (Story 1.2)

### Secrets Management

- ✅ HashiCorp Vault for connection strings
- ⏳ Vault for risk scoring rules (Story 1.13)
- ✅ Fallback to appsettings.json in Development only

---

## Monitoring & Observability

### Current Instrumentation

- ✅ OpenTelemetry traces
- ✅ Structured logging with Serilog
- ✅ Health check endpoints

### Future Metrics (Story 1.17)

- Request duration histograms
- Database query performance
- Cache hit rates
- Workflow completion rates

---

## Contributing

### Development Workflow

1. Create feature branch: `feature/story-X.X-description`
2. Implement story following acceptance criteria
3. Write integration tests (90% coverage target)
4. Update documentation
5. Create pull request

### Code Standards

- Enable nullable reference types
- Add XML comments on public APIs
- Use FluentValidation for input validation
- Log all operations with correlation IDs
- Follow async/await for all I/O

---

## Documentation

### Story Documentation

- **PRD:** `docs/domains/client-management/prd.md`
- **Brownfield Architecture:** `docs/domains/client-management/brownfield-architecture.md`
- **Stories:** `docs/domains/client-management/stories/`
- **Implementation Summary:** `docs/domains/client-management/stories/1.1.implementation-summary.md`

### Additional Documentation

- **Customer Profile:** `docs/domains/client-management/customer-profile-management.md`
- **KYC/AML:** `docs/domains/client-management/kyc-aml-compliance.md`
- **Communications:** `docs/domains/client-management/customer-communication-management.md`

---

## Troubleshooting

### Database Connection Issues

```bash
# Check SQL Server is running
docker ps | grep sqlserver

# Test connection
sqlcmd -S localhost,1433 -U sa -P "YourStrong!Passw0rd"

# Check health endpoint
curl http://localhost:5000/health/db
```

### Vault Connection Issues

```bash
# Check Vault status
curl http://localhost:8200/v1/sys/health

# Set environment variable for token
export VAULT_TOKEN=dev-token

# Service will fall back to appsettings.json in Development
```

### Migration Issues

```bash
# List migrations
dotnet ef migrations list --project apps/IntelliFin.ClientManagement

# Remove last migration
dotnet ef migrations remove --project apps/IntelliFin.ClientManagement

# Generate SQL script
dotnet ef migrations script --project apps/IntelliFin.ClientManagement
```

---

## Roadmap

### Phase 1: Foundation (Weeks 1-2) - Story 1.1 ✅ DONE
- [x] Story 1.1: Database Foundation & EF Core Setup
- [ ] Story 1.2: Shared Libraries & Dependency Injection
- [ ] Story 1.3: Client CRUD Operations

### Phase 2: Versioning & Integration (Weeks 2-3)
- [ ] Story 1.4: Client Versioning (SCD-2)
- [ ] Story 1.5: AdminService Audit Integration
- [ ] Story 1.6: KycDocument Integration
- [ ] Story 1.7: Communications Integration

### Phase 3: Workflows & Compliance (Weeks 3-4)
- [ ] Story 1.8: Dual-Control Verification
- [ ] Story 1.9: Camunda Worker Infrastructure
- [ ] Story 1.10-1.12: KYC/AML Workflows

### Phase 4: Risk & Analytics (Weeks 4-5)
- [ ] Story 1.13: Vault Risk Scoring
- [ ] Story 1.14-1.17: Monitoring & Compliance

---

## License

Proprietary - IntelliFin Banking System  
© 2025 IntelliFin. All rights reserved.

---

## Support

For technical questions or issues:
- Review story documentation in `docs/domains/client-management/stories/`
- Check brownfield architecture document for design patterns
- Refer to PRD for business requirements

---

**Current Status:** ✅ Story 1.1 Complete  
**Next Milestone:** Story 1.2 - Shared Libraries & Dependency Injection  
**Estimated Completion:** Story 1.7 (End of Phase 2)
