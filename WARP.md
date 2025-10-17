# WARP.md

This file provides guidance to WARP (warp.dev) when working with code in this repository.

Project type: .NET 9 microservices monorepo with a Next.js frontend and local Docker Compose infrastructure.

Requirements
- .NET SDK 9.0.100 (global.json pins version)
- Node.js 18+
- PowerShell 7+ (pwsh)
- Docker (for local infra)

Common commands
- First-time setup (idempotent):
```bash path=null start=null
pwsh -File tools/scripts/setup-solution.ps1 -Build -ComposeProjectName intf_dev
# macOS/Linux: tools/scripts/setup-solution.sh -Build -ComposeProjectName intf_dev
```
- Restore, build, and test the full solution:
```bash path=null start=null
dotnet restore IntelliFin.sln
dotnet build IntelliFin.sln
dotnet test IntelliFin.sln
```
- Run a single test (xUnit):
```bash path=null start=null
# Example: run one test method in FinancialService tests
# (uses xUnit + --filter by FullyQualifiedName)
dotnet test tests/IntelliFin.FinancialService.Tests --filter "FullyQualifiedName~GeneralLedgerControllerTests.GetAccountBalance_ValidAccountId_ReturnsOkResult"
```
- Run a specific service locally (example):
```bash path=null start=null
dotnet run --project apps/IntelliFin.FinancialService
```
- Frontend (Next.js) development:
```bash path=null start=null
cd apps/frontend
npm install
npm run dev     # start dev server
npm run build   # production build
npm run lint    # ESLint
```
- Local infrastructure (uses .env written by setup script with COMPOSE_PROJECT_NAME and ports):
```bash path=null start=null
# Bring up core dependencies: SQL Server, RabbitMQ, Redis, MinIO, Vault
docker compose up -d
# Or specify a project name explicitly
docker compose --project-name intf_dev up -d
```

Architecture overview
- Monorepo layout: apps/ (microservices and apps), libs/ (shared cross-cutting libraries), tests/ (Unit, Integration, E2E), tools/ (automation), docs/.
- Key apps (non-exhaustive, see IntelliFin.sln): ApiGateway, IdentityService, ClientManagement, LoanOriginationService, FinancialService, Communications, GeneralLedger, PmecService, Reporting, OfflineSync, AdminService.
- Shared libraries (cross-cutting): Shared.DomainModels, Shared.Infrastructure, Shared.Authentication, Shared.Logging, Shared.Validation, Shared.UI, Shared.Observability, Shared.Audit.
- Messaging and workflows: MassTransit for messaging (tests reference MassTransit.Testing); Camunda/Zeebe planned for workflow orchestration.
- Core patterns (from CLAUDE.md): CQRS with MediatR (notably in Loan Origination), event-sourced audit trail (append-only AuditEvents), Anti-Corruption Layer for PMEC integration, and resilience via Polly (circuit breakers, retries).
- Local infra (docker-compose.yml): SQL Server, RabbitMQ, Redis, MinIO, Vault; ports are configurable via .env (COMPOSE_PROJECT_NAME, MSSQL_PORT, RABBITMQ_AMQP_PORT/RABBITMQ_HTTP_PORT, REDIS_PORT, MINIO_API_PORT/MINIO_CONSOLE_PORT, VAULT_PORT).
- Frontend: Next.js app in apps/frontend (tablet-optimized per project docs).

Notes from CLAUDE.md (essentials)
- Domain focus: Zambian microfinance loan management, with BoZ compliance and strict in-country data residency.
- Services span Identity, Client Management, Loan Origination, Financials/GL, Collections, Credit Bureau integration, Communications, Reporting.

CI/CD, linting, and formatting
- .NET analyzers/formatting: use dotnet-format locally if desired.
```bash path=null start=null
dotnet tool restore  # if dotnet-format is defined as a tool in the future
dotnet format        # format solution using .editorconfig when present
```
- Frontend lint: npm run lint in apps/frontend.

References
- global.json pins .NET SDK: 9.0.100.
- Solution file: IntelliFin.sln (groups apps, libs, tests, and tools).
- Setup scripts: tools/scripts/setup-solution.ps1 (.sh for macOS/Linux).
- Infrastructure: docker-compose.yml at repo root; service-specific Dockerfiles under apps/ (e.g., apps/IntelliFin.AdminService/Dockerfile).
