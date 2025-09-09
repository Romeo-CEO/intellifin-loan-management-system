# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

IntelliFin is a Bank of Zambia (BoZ)-compliant, cloud-native loan management system designed specifically for Zambian microfinance institutions. The system focuses on PMEC-integrated government employee payroll loans and collateralized business loans, with strict Zambian data sovereignty requirements.

## Architecture

This is a **microservices-based monorepo** with the following structure:

- **`apps/`** - Microservices and applications (API Gateway, Identity, Client Management, Loan Origination, etc.)
- **`libs/`** - Shared libraries (Domain Models, Infrastructure, Authentication, Logging, Validation, UI)
- **`tests/`** - Test projects (Unit, Integration, E2E)
- **`docs/`** - Comprehensive domain and technical documentation
- **`tools/scripts/`** - Setup and automation scripts

### Core Technology Stack

- **.NET 9** - Backend microservices with ASP.NET Core Web APIs
- **SQL Server Always On** - Primary database with read replicas for reporting
- **Redis** - Caching layer for performance optimization
- **Camunda 8 (Zeebe)** - Workflow orchestration for loan processing and collections
- **JasperReports Server** - Regulatory and business reporting
- **MinIO** - Document storage with audit logging
- **HashiCorp Vault** - Secrets management with annual rotation
- **NextJS** - Frontend application (tablet-optimized)
- **Electron/MAUI** - CEO offline command center application
- **Kubernetes** - Container orchestration with horizontal pod autoscaling

### Key Architectural Patterns

- **CQRS with MediatR** - Command Query Responsibility Segregation in Loan Origination
- **Event Sourcing** - Comprehensive audit trail with append-only AuditEvents table
- **Anti-Corruption Layer** - PMEC integration service isolating government system complexities
- **Circuit Breaker Pattern** - Polly resilience patterns for external API reliability
- **Hybrid Analytics** - Pre-computed historical metrics + real-time operational data

## Common Development Commands

### Solution Management
```bash
# Build the entire solution
dotnet build IntelliFin.sln

# Restore all packages
dotnet restore IntelliFin.sln

# Run tests
dotnet test IntelliFin.sln

# Run a specific service (example: FinancialService)
dotnet run --project apps/IntelliFin.FinancialService

# Run tests for a specific project
dotnet test tests/IntelliFin.FinancialService.Tests
```

### First-Time Setup
```powershell
# Complete setup (creates projects, solution, infrastructure)
pwsh -File tools/scripts/setup-solution.ps1 -Build -ComposeProjectName intf_dev

# macOS/Linux
tools/scripts/setup-solution.sh -Build -ComposeProjectName intf_dev
```

### Infrastructure
```bash
# Start local development infrastructure
docker compose --project-name intf_dev up -d

# Check infrastructure status
docker compose --project-name intf_dev ps

# View logs
docker compose --project-name intf_dev logs -f
```

### Frontend Development
```bash
# Navigate to frontend directory
cd apps/IntelliFin.Frontend

# Install dependencies
npm install

# Start development server
npm run dev

# Build for production
npm run build
```

## Key Domain Areas

### Financial Services
- **General Ledger** (`apps/IntelliFin.FinancialService`) - Double-entry accounting, payment processing, PMEC integration
- **Collections** - PMEC payroll deduction cycles and exception handling
- **Payment Processing** - Traditional bank transfers, mobile money (Tingg integration)

### Loan Management
- **Loan Origination** - Multi-product workflows (Payroll/Business loans)
- **Credit Assessment** - TransUnion integration for first-time applicants
- **Collateral Management** - Asset registry and valuation workflows

### Client Management
- **KYC/AML Compliance** - Document management with 10-year retention
- **Government Employee Verification** - PMEC integration for payroll loan eligibility

### Regulatory & Reporting
- **BoZ Compliance** - Automated prudential reporting with JasperReports
- **Audit Trail** - Comprehensive event logging in append-only AuditEvents table

## Data Architecture

### Primary Databases
- **SQL Server (Primary)** - Transactional data with Always On availability
- **SQL Server (Read Replica)** - Reporting queries to isolate from OLTP workload
- **Redis** - Caching layer with keys like `client:{nrc}`, `gl:balance:{accountId}`
- **SQLCipher** - Encrypted local storage for offline CEO application

### Performance Considerations
- **Intelligent Caching** - Redis-backed client search with 15-minute TTL
- **Database Partitioning** - Client data partitioned by BranchId
- **Read Replica Strategy** - Separate reporting workloads from transactions
- **Background Processing** - Pre-computed portfolio metrics via background services

## Security & Compliance

### Authentication & Authorization
- **JWT Tokens** - 15-minute access tokens with refresh rotation
- **Step-up Authentication** - Required for sensitive operations via Identity 2FA/WebAuthn
- **Branch Context** - All operations scoped to user's branch via X-Branch-Id header
- **Role-based Access** - LoanOfficer, Underwriter, Finance, Collections, Compliance, Admin, CEO roles

### Data Protection
- **Zambian Data Sovereignty** - All data hosted within Zambia (Infratel/Paratus data centers)
- **Encryption** - TLS 1.2+, SQL TDE, MinIO SSE, column-level encryption for PII
- **Document Retention** - 10-year retention policy with automated lifecycle management
- **Audit Compliance** - Append-only AuditEvents table with separate insert-only database principal

## External Integrations

### Government Systems
- **PMEC Integration** - Government employee payroll system via Anti-Corruption Layer
- **Queue Resilience** - Local queuing system handles PMEC downtime
- **Credential Management** - Annual rotation via HashiCorp Vault

### Credit Bureau
- **TransUnion Zambia** - First-time applicant credit checks only
- **Cost Optimization** - Intelligent client classification and caching
- **Resilience** - Queue-based processing with exponential backoff

### Payment Gateways
- **Tingg** - Mobile money disbursements and collections (Phase 2)
- **Traditional Banking** - Batch bank transfers with reconciliation

## Development Guidelines

### Code Organization
- Follow **Clean Architecture** principles in microservices
- Use **MediatR** for CQRS pattern in complex domains
- Implement comprehensive **audit logging** for all business operations
- Apply **Polly resilience patterns** for external API calls

### Performance Standards
- **Sub-second response times** for customer lookup across branches
- **Sub-10-second dashboard** responses for complex queries
- **95%+ cache hit rates** for frequently accessed data
- **Support for 50+ concurrent users** without performance degradation

### Security Standards
- **Never log sensitive data** (NRC numbers, PMEC identifiers, financial amounts)
- **Always validate input** using FluentValidation
- **Implement rate limiting** on all public endpoints
- **Use correlation IDs** for request tracing

## Testing Strategy

- **Unit Tests** - Service layer logic and domain models
- **Integration Tests** - Database interactions and external API contracts
- **E2E Tests** - Critical user workflows and compliance scenarios
- **Load Testing** - Performance validation under concurrent user load

## Deployment & Operations

### Environment Configuration
- **Development** - Local Docker Compose with all services
- **Staging** - Kubernetes cluster mimicking production
- **Production** - Geo-redundant Kubernetes across Zambian data centers

### Monitoring & Alerting
- **Application Insights** - Performance metrics and error tracking
- **Centralized Logging** - ELK stack for operational events
- **Health Checks** - Kubernetes liveness/readiness probes
- **Business Metrics** - Real-time loan processing and PMEC success rates

## Important Notes

- **Data Sovereignty** - All data must remain within Zambian borders
- **BoZ Compliance** - Regulatory reporting templates are strictly versioned
- **PMEC Criticality** - Government payroll integration is mission-critical
- **Offline Capability** - CEO application must function without connectivity
- **Manual Failover** - Database failover requires management team authorization
- **Audit Requirements** - All business operations must be comprehensively logged

For detailed architecture information, see `docs/project-archtecture.md` and `docs/technical-spec.md`.