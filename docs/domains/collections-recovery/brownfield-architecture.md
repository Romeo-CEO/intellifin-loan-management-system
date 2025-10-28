# IntelliFin.Collections Brownfield Architecture Document

## Introduction

This document captures the CURRENT STATE of the `IntelliFin.Collections` service, including its existing structure, technology stack, data models, and integration patterns. It serves as a foundational reference for understanding the service before any enhancements are planned. This analysis is based on the `apps/IntelliFin.Collections` codebase and the `docs/domains/collections-recovery/collections-lifecycle-management.md` business process specification.

### Document Scope

Focused on documenting the existing `IntelliFin.Collections` service to understand its current capabilities and integration points.

### Change Log

| Date       | Version | Description                   | Author      |
| ---------- | ------- | ----------------------------- | ----------- |
| 2025-10-22 | 1.0     | Initial brownfield analysis   | BMad Orchestrator |

## Quick Reference - Key Files and Entry Points

### Critical Files for Understanding the System

-   **Main Entry**: `apps/IntelliFin.Collections/Program.cs` - ASP.NET Core application entry point, setting up services and middleware.
-   **Configuration**: `apps/IntelliFin.Collections/appsettings.json`, `apps/IntelliFin.Collections/appsettings.Development.json` - Service configuration, logging, OpenTelemetry settings.
-   **Project File**: `apps/IntelliFin.Collections/IntelliFin.Collections.csproj` - Defines project dependencies and target framework.
-   **Collections Lifecycle Document**: `docs/domains/collections-recovery/collections-lifecycle-management.md` - Business process specification for collections, detailing automated actions and human tasks.

## High Level Architecture

### Technical Summary

The `IntelliFin.Collections` service is a .NET 9.0 ASP.NET Core microservice, currently configured with basic endpoints for health checks and OpenAPI. It leverages `IntelliFin.Shared.Observability` for OpenTelemetry integration. As a brownfield service, it is expected to integrate with other IntelliFin services using established patterns for messaging (RabbitMQ/MassTransit), configuration (Vault), audit logging (AdminService), and workflow orchestration (Camunda).

### Actual Tech Stack

| Category  | Technology      | Version     | Notes                                       |
| --------- | --------------- | ----------- | ------------------------------------------- |
| Runtime   | .NET            | 9.0         | Latest .NET framework                       |
| Framework | ASP.NET Core    | 9.0         | Web application framework                   |
| Messaging | RabbitMQ        | 3.x         | Via MassTransit, used for inter-service communication |
| Config    | HashiCorp Vault | N/A         | For centralized secret and config management|
| Observability | OpenTelemetry | N/A         | Via `IntelliFin.Shared.Observability` |
| Workflow  | Camunda (Zeebe) | Self-Hosted | For process orchestration and external tasks |
| Database  | SQL Server      | N/A         | Standard for IntelliFin services (assumed)  |

### Repository Structure Reality Check

-   **Type**: Polyrepo (each service is its own project under `apps/`)
-   **Package Manager**: NuGet for .NET dependencies
-   **Notable**: The `IntelliFin.Collections` project is a basic ASP.NET Core service with minimal custom code, serving as a placeholder or a new service for collections. It follows the standard IntelliFin microservice structure.

## Source Tree and Module Organization

### Project Structure (Actual)

```text
Intellifin Loan Management System/
├── apps/
│   └── IntelliFin.Collections/
│       ├── appsettings.Development.json
│       ├── appsettings.json
│       ├── IntelliFin.Collections.csproj
│       ├── Program.cs             # Service entry point and configuration
│       └── Properties/            # Launch settings
├── libs/
│   └── IntelliFin.Shared.Observability/ # Shared observability library
├── docs/
│   └── domains/
│       └── collections-recovery/
│           └── collections-lifecycle-management.md # Business process specification
└── ...
```

### Key Modules and Their Purpose (Current IntelliFin.Collections Service)

-   **Service Entry**: `Program.cs` - Configures the web host, adds OpenTelemetry, OpenAPI, and health checks. It exposes a basic root endpoint.
-   **Configuration**: `appsettings.json` - Defines service name, version, and OpenTelemetry endpoint.

## Data Models and APIs

### Data Models (Existing for IntelliFin)

Based on `docs/technical-spec.md` and `docs/architecture/system-architecture.md`, IntelliFin has core data models for:
-   **Client Management**: Client, GovernmentEmployment, KycDocument, Address, Contact.
-   **Loan Origination**: LoanApplication, LoanProduct, CollateralAsset, UnderwritingNote, ApprovalStep.
-   **General Ledger**: JournalEntry, GLAccount, PostingRule.
-   **Audit**: AuditEvent (append-only table with tamper-evident chain).

The `IntelliFin.Collections` service does not currently define any specific data models within its codebase.

### API Specifications (Existing for IntelliFin)

The `IntelliFin.Collections` service currently exposes a `/health` endpoint and a root `/` endpoint returning basic service info. OpenAPI is enabled for development.

Common API patterns across IntelliFin services:
-   **AuthN**: Bearer JWT; jti denylist in Redis; step-up claim for sensitive endpoints.
-   **Headers**: X-Correlation-Id, X-Branch-Id.
-   **Status codes**: 2xx on success; 4xx for validation/auth; 5xx transient with retry-after.

## Technical Debt and Known Issues (Current IntelliFin.Collections Service)

-   **Minimal Implementation**: The service is currently a barebones ASP.NET Core application, lacking any domain-specific logic, data models, or integration code related to collections. This is not technical debt, but a starting point for development.
-   **No Database Integration**: There is no explicit database context or persistence layer configured, which will be a necessary addition.
-   **No Messaging Handlers**: No MassTransit consumers or publishers are set up, which are crucial for event-driven integrations.
-   **No Camunda Workers**: The service currently lacks any Camunda worker implementations, which are central to the collections workflow.

## Integration Points and External Dependencies (Current IntelliFin Context)

### External Services (IntelliFin System)

| Service           | Purpose                     | Integration Type      | Key Files/Documents                                   |
| ----------------- | --------------------------- | --------------------- | ----------------------------------------------------- |
| RabbitMQ          | Asynchronous Messaging      | MassTransit (.NET)    | `docs/architecture/messaging.md`                      |
| HashiCorp Vault   | Secret/Config Management    | API (.NET Client)     | `apps/IntelliFin.ClientManagement/Infrastructure/Vault/VaultService.cs`, `docs/domains/credit-assessment/stories/1.8.vault-integration.md` |
| Camunda (Zeebe)   | Workflow Orchestration      | gRPC Client (.NET)    | `apps/IntelliFin.ClientManagement/Workflows/CamundaWorkers/CamundaWorkerHostedService.cs`, `apps/IntelliFin.LoanOriginationService/Services/ExternalTaskWorkerService.cs` |
| AdminService      | Centralized Audit Logging   | HTTP API / RabbitMQ   | `apps/IntelliFin.AdminService/Options/AuditRabbitMqOptions.cs`, `apps/IntelliFin.AdminService/Services/AuditService.cs`, `docs/domains/client-management/stories/1.5.adminservice-audit.story.md` |
| CommunicationService | Notifications (SMS/Email) | Event-driven (RabbitMQ) | `docs/architecture/system-architecture.md#4.-communications-workflow`, `apps/IntelliFin.Communications/Services/EventRoutingService.cs` |
| Treasury Service  | Payment Inflows/Reconciliation | Event-driven (RabbitMQ) | _Specific schemas/events not explicitly found, assumed to follow MassTransit patterns._ |
| PMEC Service      | Payroll Deductions        | Event-driven (RabbitMQ) | `docs/technical-spec.md` (mentions custom protocol wrappers), _Specific schemas/events not explicitly found, assumed to follow MassTransit patterns._ |
| Loan Origination  | Loan Details, Terms         | Event-driven (RabbitMQ) | `docs/architecture/messaging.md#loanapplicationcreated` |
| Credit Assessment | Risk Category               | Event-driven (RabbitMQ) | _No explicit event for risk category found, assumed a future event to be published._ |

### Messaging Conventions

-   **Message Broker**: RabbitMQ 3.x with MassTransit.
-   **Endpoint Naming**: Kebab-case for exchanges and queues (`loan-application-created`, `communications_loan-application-created`).
-   **Audit Events**: Dedicated `audit.events` exchange with `admin-service.audit.events` queue for AdminService ingestion.
-   **Event Types**: Standard C# record types for event contracts.

### Vault Integration

-   Existing services (e.g., ClientManagement, AdminService) use `VaultSharp` for integration.
-   Configuration values (e.g., connection strings, sensitive settings) are retrieved from Vault paths like `secret/intellifin/credit-assessment/config`.
-   AppRole authentication is used.

### AdminService Audit Logging

-   Audit events are sent to AdminService via HTTP API or RabbitMQ.
-   `AuditEventDto` schema: `Actor`, `Action`, `EntityType`, `EntityId`, `CorrelationId`, `IpAddress`, `EventData` (JSON), `Timestamp`.
-   Actions follow PascalCase convention (e.g., `ClientCreated`, `LoanApproved`).
-   Fire-and-forget pattern for audit logging.

### Camunda Workflow Integration

-   Camunda 8 (Zeebe) self-hosted, used for loan origination, PMEC, and other workflows.
-   Workers are implemented as `BackgroundService`s that connect to Zeebe gateway.
-   Workers subscribe to topics, fetch jobs, execute business logic, and complete tasks.
-   Example workers: `HealthCheckWorker`, `AmlScreeningWorker`, `KycDocumentCheckWorker`, `RiskAssessmentWorker` in `IntelliFin.ClientManagement`.
-   BPMN process definitions are deployed to Zeebe.

## Development and Deployment

-   **Local Development**: Standard .NET SDK for development. `appsettings.Development.json` for local overrides.
-   **Build**: .NET `dotnet build`.
-   **Deployment**: Docker/Kubernetes based; `IntelliFin.Collections` is expected to be deployed as a containerized microservice.
-   **OpenTelemetry**: Integrated for distributed tracing and metrics.

## Testing Reality (Current IntelliFin Context)

-   **Test Framework**: xUnit 3.0+ (for unit and integration tests).
-   **Test Containers**: Used for integration testing with external dependencies like RabbitMQ.
-   **Coverage Requirements**: Typically 90% for core components.
-   **CI Integration**: Tests are integrated into CI pipelines.

## Enhancement Impact Areas (CollectionsService)

The `IntelliFin.Collections` service will be responsible for a new set of functionalities described in `collections-lifecycle-management.md`. This will involve:

-   **Repayment Scheduling**: Storing and managing repayment schedules.
-   **Payment & Reconciliation**: Processing incoming payments against schedules, adjusting balances, and triggering reconciliation workflows.
-   **Arrears & Provisioning**: Daily calculation of DPD, BoZ classification, and automated provisioning.
-   **Collections Workflow**: Orchestrating collection activities via Camunda, from reminders to legal escalation.
-   **Customer Notifications**: Sending automated reminders and confirmations via CommunicationService.
-   **Reporting & Compliance**: Generating aging reports, PAR summaries, and provisioning summaries.

This will necessitate new data models, API endpoints, message consumers, background jobs, Camunda workers, and extensive integration with existing IntelliFin services, all while adhering to the established technical patterns and regulatory requirements.

## Appendix - Useful Commands and Scripts

-   `dotnet run`: Starts the `IntelliFin.Collections` service locally.
-   `dotnet build`: Builds the project.
-   `dotnet test`: Runs any tests defined in the project (currently none for `IntelliFin.Collections`).
