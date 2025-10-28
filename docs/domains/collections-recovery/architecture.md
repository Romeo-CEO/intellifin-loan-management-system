# IntelliFin.Collections Brownfield Enhancement Architecture

## Introduction

This document outlines the architectural approach for enhancing IntelliFin Loan Management System with The Collections & Recovery module, which will manage all post-disbursement loan activities, including repayment scheduling, payment reconciliation, arrears tracking, BoZ classification and provisioning, automated collections workflows, and customer notifications. This module will integrate with existing IntelliFin services such as Loan Origination, Credit Assessment, Client Management, Treasury, PMEC, AdminService, and CommunicationService to close the credit lifecycle and ensure regulatory compliance. Its primary goal is to serve as the guiding architectural blueprint for AI-driven development of new features while ensuring seamless integration with the existing system.

**Relationship to Existing Architecture:**
This document supplements existing project architecture by defining how new components will integrate with current systems. Where conflicts arise between new and existing patterns, this document provides guidance on maintaining consistency while implementing enhancements.

### Existing Project Analysis

**Rationale**: I have analyzed the `docs/domains/collections-recovery/brownfield-architecture.md` document to extract the current state of the `IntelliFin.Collections` service.

Based on my analysis of your project, I've identified the following about your existing system:

-   **Primary Purpose:** The `IntelliFin.Collections` service is a .NET 9.0 ASP.NET Core microservice, currently configured with basic endpoints for health checks and OpenAPI. It leverages `IntelliFin.Shared.Observability` for OpenTelemetry integration. It follows the standard IntelliFin microservice structure, serving as a placeholder or a new service for collections.
-   **Current Tech Stack:** The service utilizes .NET 9.0 (C#) and ASP.NET Core 9.0. It's built for integration with RabbitMQ (MassTransit), HashiCorp Vault, Camunda (Zeebe), AdminService, CommunicationService, Treasury Service, PMEC Service, Loan Origination, and Credit Assessment. SQL Server is the assumed standard database.
-   **Architecture Style:** The overall project uses a polyrepo structure with individual services under `apps/`, indicative of a microservice architecture.
-   **Deployment Method:** Deployment is Docker/Kubernetes based, with the `IntelliFin.Collections` service expected to be deployed as a containerized microservice.

#### Available Documentation

-   `docs/domains/collections-recovery/brownfield-architecture.md` (Current state analysis of `IntelliFin.Collections` service)
-   `docs/domains/collections-recovery/collections-lifecycle-management.md` (Business process specification for collections)
-   `docs/domains/collections-recovery/prd.md` (Product Requirements Document for Collections & Recovery enhancement)
-   `docs/architecture/messaging.md` (Messaging architecture, RabbitMQ/MassTransit conventions)
-   `apps/IntelliFin.ClientManagement/Infrastructure/Vault/VaultService.cs` (Example Vault integration)
-   `apps/IntelliFin.ClientManagement/Workflows/CamundaWorkers/CamundaWorkerHostedService.cs` (Example Camunda worker implementation)
-   `apps/IntelliFin.LoanOriginationService/Services/ExternalTaskWorkerService.cs` (Example Camunda external task worker)
-   `apps/IntelliFin.AdminService/Options/AuditRabbitMqOptions.cs` (AdminService audit RabbitMQ options)
-   `apps/IntelliFin.AdminService/Services/AuditService.cs` (AdminService audit service)
-   `docs/domains/client-management/stories/1.5.adminservice-audit.story.md` (AdminService audit integration story)
-   `apps/IntelliFin.Communications/Services/EventRoutingService.cs` (CommunicationService event routing)
-   `docs/architecture/system-architecture.md` (Overall system architecture, including communication workflow and core data models)
-   `docs/technical-spec.md` (Technical specifications, data models, APIs, security)

#### Identified Constraints

-   **Minimal Current Implementation**: The `IntelliFin.Collections` service is a barebones ASP.NET Core application, requiring significant development for domain logic, database integration, messaging handlers, and Camunda workers.
-   **BoZ Compliance**: All loan classification and provisioning calculations must strictly adhere to Bank of Zambia directives (e.g., Directive 15 for classification, Second Schedule for provisioning, Directive 9 & 10 for non-accrual management).
-   **Vault Configuration**: Critical business rules (DPD thresholds, provisioning rates, penalty rules) must be configurable and retrieved from HashiCorp Vault.
-   **Messaging Conventions**: Adherence to existing MassTransit/RabbitMQ kebab-case naming conventions for exchanges and queues.
-   **AdminService Audit Logging**: All critical events and actions must be logged to AdminService using the existing `AuditEventDto` schema and fire-and-forget pattern.
-   **Camunda Integration**: New collections workflows must be orchestrated via Camunda (Zeebe) with workers implemented as `BackgroundService`s, following existing patterns for topic subscription and job handling.
-   **Dual Control**: Write-offs and loan restructuring require dual control approval processes.
-   **Access Control**: Only Collections Officers can modify payment or reconciliation records.
-   **Database Integration**: The CollectionsService will manage its own dedicated SQL Server database, consuming external data primarily through eventing.
-   **API Consistency**: New APIs must align with existing IntelliFin API patterns (JWT auth, correlation IDs, branch IDs, standard HTTP status codes).
-   **Additive Schema Changes**: Any new database schema changes must be additive and backward-compatible.
-   **UI/UX Consistency**: Future UI components should adhere to the `lms-ux-style-guide.md` and overall IntelliFin design system.

### Change Log

| Change               | Date       | Version | Description                                                      | Author |
| :------------------- | :--------- | :------ | :--------------------------------------------------------------- | :----- |
| Initial Architecture | 2025-10-22 | 1.0     | Drafted new collections enhanced architecture based on PRD and current state analysis. | Winston (Architect) |

## Enhancement Scope and Integration Strategy

Based on my analysis, the integration approach I'm proposing takes into account the existing IntelliFin microservice architecture, established messaging patterns (RabbitMQ/MassTransit), centralized configuration management (Vault), and workflow orchestration (Camunda). These integration points and boundaries respect your current architecture patterns. Is this assessment accurate?

### Enhancement Overview

**Enhancement Type:** New Feature Addition, Integration with New Systems, Automated Collections Lifecycle Management
**Scope:** Implement the IntelliFin Collections & Recovery module, encompassing automated repayment scheduling, payment reconciliation, BoZ-compliant arrears classification and provisioning, a Camunda-orchestrated collections workflow, and automated customer notifications.
**Integration Impact:** Significant Impact (substantial existing code changes), Major Impact (architectural changes required)

### Integration Approach

**Code Integration Strategy:** The CollectionsService will be a new, independent microservice. It will not directly modify existing codebase of other services, but will interact through defined APIs and events. Shared libraries will be used for common contracts and utilities where appropriate.

**Database Integration:** The CollectionsService will manage its own dedicated SQL Server database for all collections-related persistent data. Data from other services (e.g., loan details, client information) will be consumed via event streams to maintain data ownership and loose coupling.

**API Integration:** The CollectionsService will expose a new set of REST APIs for collections-specific operations and will consume existing IntelliFin APIs (e.g., Vault). All new APIs will adhere to existing IntelliFin API patterns for authentication, authorization, and headers.

**UI Integration:** No direct UI integration within this service. Future UI components (e.g., Collections Workbench) will consume the CollectionsService's exposed APIs.

### Compatibility Requirements

-   **Existing API Compatibility:** New CollectionsService APIs must adhere to existing IntelliFin API patterns (JWT auth, `X-Correlation-Id`, `X-Branch-Id` headers, standard HTTP status codes).
-   **Database Schema Compatibility:** Any new database schema changes must be additive and not break existing dependent services (e.g., Loan Origination).
-   **UI/UX Consistency:** Any future UI components related to collections (e.g., Collections Workbench) must adhere to the existing `lms-ux-style-guide.md` and overall IntelliFin design system.
-   **Performance Impact:** The CollectionsService's operations, especially nightly batch jobs, must not negatively impact the performance of existing IntelliFin services or overall system responsiveness.

## Tech Stack

### Existing Technology Stack

| Category  | Current Technology | Version | Usage in Enhancement | Notes                                        |
| :-------- | :----------------- | :------ | :------------------- | :------------------------------------------- |
| Runtime   | .NET               | 9.0     | CollectionsService   | Latest .NET framework                        |
| Framework | ASP.NET Core       | 9.0     | CollectionsService   | Web application framework                    |
| Messaging | RabbitMQ           | 3.x     | CollectionsService   | Via MassTransit, used for inter-service communication |
| Config    | HashiCorp Vault    | N/A     | CollectionsService   | For centralized secret and config management |
| Observability | OpenTelemetry  | N/A     | CollectionsService   | Via `IntelliFin.Shared.Observability`        |
| Workflow  | Camunda (Zeebe)    | Self-Hosted | CollectionsService | For process orchestration and external tasks |
| Database  | SQL Server         | N/A     | CollectionsService   | Standard for IntelliFin services             |

## Data Models and Schema Changes

Define new data models and how they integrate with existing schema:

1. Identify new entities required for the enhancement
2. Define relationships with existing data models
3. Plan database schema changes (additions, modifications)
4. Ensure backward compatibility

### New Data Models

#### RepaymentSchedule

**Purpose:** To store the comprehensive schedule of expected loan repayments, including all installments.
**Integration:** Linked to existing Loan entity (via LoanId).
**Key Attributes:**
-   `ScheduleId`: GUID - Primary key, unique identifier for the repayment schedule.
-   `LoanId`: GUID - Foreign key to the existing Loan entity.
-   `GeneratedDate`: DateTime - When the schedule was created.
-   `Status`: String (e.g., 'Active', 'Restructured', 'Completed') - Current status of the schedule.
-   `Currency`: String - Currency of the loan.

**Relationships:**
-   **With Existing:** One-to-one or one-to-many with the existing Loan entity.
-   **With New:** One-to-many with `Installment` (RepaymentSchedule has many Installments).

#### Installment

**Purpose:** To store details of each individual scheduled payment within a a`RepaymentSchedule`.
**Integration:** Linked to `RepaymentSchedule` (via ScheduleId) and Loan (via LoanId).
**Key Attributes:**
-   `InstallmentId`: GUID - Primary key, unique identifier for an installment.
-   `ScheduleId`: GUID - Foreign key to `RepaymentSchedule`.
-   `LoanId`: GUID - Foreign key to the existing Loan entity.
-   `DueDate`: DateTime - The date the installment is due.
-   `ExpectedPrincipal`: Decimal - Expected principal amount for this installment.
-   `ExpectedInterest`: Decimal - Expected interest amount for this installment.
-   `ExpectedTotal`: Decimal - Sum of ExpectedPrincipal and ExpectedInterest.
-   `PaidAmount`: Decimal - Actual amount paid for this installment.
-   `Status`: String (e.g., 'Pending', 'Paid', 'Partially Paid', 'Overdue', 'WrittenOff').
-   `DaysPastDue`: Int - Calculated days past due for this installment.
-   `Version`: Int - Optimistic concurrency versioning.

**Relationships:**
-   **With Existing:** One-to-one or one-to-many with the existing Loan entity.
-   **With New:** Many-to-one with `RepaymentSchedule` (many Installments belong to one RepaymentSchedule).

#### PaymentTransaction

**Purpose:** To record all incoming payment transactions and link them to `Installments`.
**Integration:** Linked to `Installment` (via InstallmentId) and Loan (via LoanId).
**Key Attributes:**
-   `TransactionId`: GUID - Primary key, unique identifier for a payment transaction.
-   `LoanId`: GUID - Foreign key to the existing Loan entity.
-   `InstallmentId`: GUID - Foreign key to `Installment` (nullable for overpayments).
-   `Amount`: Decimal - The amount of the payment.
-   `PaymentDate`: DateTime - Date and time the payment was received.
-   `Source`: String (e.g., 'Manual', 'PMEC', 'Treasury') - Origin of the payment.
-   `Reference`: String - External reference number (e.g., PMEC transaction ID).
-   `Status`: String (e.g., 'Completed', 'Failed', 'Reversed').
-   `IsReconciled`: Boolean - Indicates if the payment has been reconciled.

**Relationships:**
-   **With Existing:** One-to-many with the existing Loan entity.
-   **With New:** Many-to-one or one-to-one with `Installment`.

#### ArrearsClassificationHistory

**Purpose:** To store historical records of a loan's BoZ arrears classification and provisioning.
**Integration:** Linked to Loan (via LoanId).
**Key Attributes:**
-   `HistoryId`: GUID - Primary key.
-   `LoanId`: GUID - Foreign key to the existing Loan entity.
-   `ClassificationDate`: DateTime - Date of this classification.
-   `DPD`: Int - Days Past Due at the time of classification.
-   `BoZCategory`: String (e.g., 'Performing', 'Watch', 'Substandard', 'Doubtful', 'Loss').
-   `ProvisionRate`: Decimal - Applicable BoZ provisioning rate.
-   `ProvisionAmount`: Decimal - Calculated provision amount.
-   `NonAccrualStatus`: Boolean - True if loan is in non-accrual status.
-   `VaultConfigVersion`: String - Version/hash of Vault config used for classification.

**Relationships:**
-   **With Existing:** One-to-many with the existing Loan entity.
-   **With New:** None directly.

#### ReconciliationTask

**Purpose:** To manage manual reconciliation tasks generated for overpayments or misapplied transactions.
**Integration:** Linked to `PaymentTransaction` (via TransactionId) and potentially Loan.
**Key Attributes:**
-   `TaskId`: GUID - Primary key.
-   `TransactionId`: GUID - Foreign key to `PaymentTransaction`.
-   `LoanId`: GUID - Foreign key to the existing Loan entity (if applicable).
-   `AssignedTo`: String - User ID of the Collections Officer assigned.
-   `Status`: String (e.g., 'Open', 'InProgress', 'Completed', 'Escalated').
-   `CreatedDate`: DateTime.
-   `ResolutionDate`: DateTime (nullable).
-   `Notes`: String - Details of the issue and resolution.
-   `CamundaProcessInstanceId`: String - Reference to Camunda process instance.

**Relationships:**
-   **With Existing:** Potentially with existing User/Collections Officer entities (via AssignedTo).
-   **With New:** Many-to-one with `PaymentTransaction`.

#### WriteOffRequest

**Purpose:** To manage the workflow and audit trail for loan write-offs.
**Integration:** Linked to Loan (via LoanId).
**Key Attributes:**
-   `RequestId`: GUID - Primary key.
-   `LoanId`: GUID - Foreign key to the existing Loan entity.
-   `InitiatedBy`: String - User ID of the Collections Officer initiating.
-   `RequestedDate`: DateTime.
-   `ApprovedByCredit`: String - User ID of Head of Credit approval (nullable).
-   `ApprovedByFinance`: String - User ID of Senior Finance Manager approval (nullable).
-   `ApprovalDateCredit`: DateTime (nullable).
-   `ApprovalDateFinance`: DateTime (nullable).
-   `Status`: String (e.g., 'PendingApprovalCredit', 'PendingApprovalFinance', 'Approved', 'Rejected').
-   `Reason`: String - Reason for write-off.
-   `WriteOffAmount`: Decimal.
-   `CamundaProcessInstanceId`: String - Reference to Camunda process instance.

**Relationships:**
-   **With Existing:** One-to-one with existing Loan entity (a loan can have one active write-off request).
-   **With New:** None directly.

### Schema Integration Strategy

**Database Changes Required:**
-   **New Tables:**
    -   `RepaymentSchedules` (for `RepaymentSchedule` entity)
    -   `Installments` (for `Installment` entity)
    -   `PaymentTransactions` (for `PaymentTransaction` entity)
    -   `ArrearsClassificationHistory` (for `ArrearsClassificationHistory` entity)
    -   `ReconciliationTasks` (for `ReconciliationTask` entity)
    -   `WriteOffRequests` (for `WriteOffRequest` entity)
-   **Modified Tables:** None (new service owns its data)
-   **New Indexes:** Appropriate indexes on foreign keys (e.g., `LoanId`, `ScheduleId`, `InstallmentId`, `TransactionId`) and frequently queried columns (e.g., `DueDate`, `ClassificationDate`, `Status`).
-   **Migration Strategy:** Entity Framework Core migrations will be used to manage schema changes in the CollectionsService's dedicated database. Migrations will be versioned and applied as part of the deployment pipeline.

**Seed Data / Initial Data Setup:**
A strategy for seed data and initial data setup will be implemented. This includes:
-   **Reference Data:** Seeding of initial reference data (e.g., default BoZ classification rules, initial provisioning rates) required for service operation.
-   **Development/Testing Data:** Automated generation of realistic but anonymized data for development and testing environments to ensure comprehensive testing scenarios. This will be integrated into the test project setup.

**Backward Compatibility:**
-   **Additive Changes Only**: All schema changes will be strictly additive, introducing new tables and columns without modifying or deleting existing schema that might be accessed by other services (though direct DB access from other services is discouraged).
-   **Event-Driven Data Sharing**: Primary method of sharing data with other services will be through well-defined events, ensuring loose coupling and avoiding direct schema dependencies.
-   **No Impact on Existing Services**: The new database for CollectionsService will not directly impact the schema or data of existing IntelliFin services.

## Component Architecture

Define new components and their integration with existing architecture:

1. Identify new components required for the enhancement
2. Define interfaces with existing components
3. Establish clear boundaries and responsibilities
4. Plan integration points and data flow

The new components I'm proposing follow the existing architectural patterns I identified in your codebase, particularly the microservice pattern with distinct layers (e.g., Domain, Application, Infrastructure). The integration interfaces respect your current component structure and communication patterns (e.g., MassTransit for messaging, HTTP for API calls, Zeebe client for Camunda). Does this match your project's reality?

### New Components

#### CollectionsDbContext

**Responsibility:** Manages database interactions for all CollectionsService data models (RepaymentSchedule, Installment, PaymentTransaction, etc.) using Entity Framework Core.
**Integration Points:** SQL Server Database.
**Key Interfaces:**
-   `DbContext` (Entity Framework Core)
-   `IUnitOfWork` (assumed standard pattern for transactional operations)
**Dependencies:**
-   **Existing Components:** None direct, relies on existing .NET infrastructure for database connection management.
-   **New Components:** `RepaymentScheduleRepository`, `InstallmentRepository`, etc.
**Technology Stack:** .NET 9.0, Entity Framework Core, SQL Server.

#### RepaymentScheduleService

**Responsibility:** Handles the business logic for generating, persisting, and managing repayment schedules.
**Integration Points:** Loan Origination (event consumer), Vault (for product configuration), CollectionsDbContext.
**Key Interfaces:**
-   `IRepaymentScheduleService` (defines public contract for schedule management)
**Dependencies:**
-   **Existing Components:** Loan Origination (via `LoanDisbursedEvent` consumer).
-   **New Components:** `RepaymentScheduleRepository`, `InstallmentRepository`, `VaultConfigService`.
**Technology Stack:** .NET 9.0, C#.

#### PaymentProcessingService

**Responsibility:** Processes incoming payments, matches them to installments, updates balances, and handles overpayments/misapplied transactions.
**Integration Points:** Treasury (event consumer), PMEC (event consumer), CollectionsDbContext, Camunda (for reconciliation tasks), AdminService (audit), CommunicationService (confirmations).
**Key Interfaces:**
-   `IPaymentProcessingService`
**Dependencies:**
-   **Existing Components:** Treasury (via `PaymentReceivedEvent` consumer), PMEC (via `PMECTransactionConfirmedEvent` consumer).
-   **New Components:** `InstallmentRepository`, `PaymentTransactionRepository`, `ReconciliationTaskService`, `CamundaClient`, `AdminServiceClient`, `CommunicationServiceClient`.
**Technology Stack:** .NET 9.0, C#, MassTransit.

#### ArrearsClassificationService

**Responsibility:** Calculates Days Past Due (DPD) and classifies loans into BoZ arrears categories nightly.
**Integration Points:** CollectionsDbContext, Vault (for BoZ rules), Camunda (batch workflow), AdminService (audit), General Ledger Service (provisioning/interest reversal).
**Key Interfaces:**
-   `IArrearsClassificationService` (defines public contract for classification process)
**Dependencies:**
-   **Existing Components:** General Ledger Service (via `GLPostingClient`), Vault.
-   **New Components:** `InstallmentRepository`, `ArrearsClassificationHistoryRepository`, `VaultConfigService`.
**Technology Stack:** .NET 9.0, C#, MassTransit, Camunda.

#### CollectionsWorkflowService

**Responsibility:** Orchestrates Camunda-managed collections workflows and interacts with Camunda workers.
**Integration Points:** Camunda (Zeebe), PaymentProcessingService, ArrearsClassificationService, CommunicationService, AdminService.
**Key Interfaces:**
-   `ICollectionsWorkflowService` (defines methods for starting/managing Camunda processes)
**Dependencies:**
-   **Existing Components:** Camunda (via Zeebe client).
-   **New Components:** `CamundaWorkers` (e.g., `GenerateReminderWorker`, `CallTaskWorker`, `EscalationWorker`, `WriteOffApprovalWorker`), `ReconciliationTaskService`.
**Technology Stack:** .NET 9.0, C#, Zeebe Client.

#### VaultConfigService (Collections Specific)

**Responsibility:** Retrieves and caches collections-specific policies (BoZ rules, provisioning rates, penalty rules) from HashiCorp Vault.
**Integration Points:** HashiCorp Vault.
**Key Interfaces:**
-   `ICollectionsVaultConfigService`
**Dependencies:**
-   **Existing Components:** VaultSharp client (from shared libraries or new implementation).
-   **New Components:** CollectionsService business logic requiring configuration.
**Technology Stack:** .NET 9.0, VaultSharp.

#### ReportingService

**Responsibility:** Generates various collections-related reports (aging, PAR, provisioning summaries).
**Integration Points:** CollectionsDbContext.
**Key Interfaces:**
-   `IReportingService`
**Dependencies:**
-   **Existing Components:** None directly, potentially existing IntelliFin reporting infrastructure.
-   **New Components:** `InstallmentRepository`, `ArrearsClassificationHistoryRepository`.
**Technology Stack:** .NET 9.0, C#.

### Component Interaction Diagram

```mermaid
graph TD
    subgraph IntelliFin.Collections Service
        DBC[(Collections Database)]
        RS[Repayment Schedule Mgmt]
        PP[Payment Processing]
        AC[Arrears Classification]
        CW[Collections Workflow Orchestrator]
        VC[Vault Config Reader]
        RP[Reporting & Analytics]
        CWW[Camunda Workflow Workers]

        RS --> DBC
        PP --> DBC
        PP --> CWW{Reconciliation Worker}
        AC --> DBC
        AC --> VC
        CW --> CWW
        RP --> DBC
    end

    LO(Loan Origination) -- "Loan Disbursed Event" --> RS
    CA(Credit Assessment) -- "Risk Category Update" --> AC
    CM(Client Management) -- "Client Data (e.g., Preferences)" --> CS_EXT(Communication Service)
    TS(Treasury Service) -- "Payment Received Event" --> PP
    PM(PMEC Service) -- "PMEC Confirmed Event" --> PP

    CW -- "Starts/Manages Workflows" --> C(Camunda Zeebe)
    CWW -- "Fetches/Completes Jobs" --> C

    IntelliFin.Collections Service -- "Audit Events" --> AD(AdminService)
    IntelliFin.Collections Service -- "Sends Notifications" --> CS_EXT
    AC -- "GL Posting/Interest Reversal" --> GL_EXT(General Ledger Service)
    VC -- "Reads Policies" --> V(HashiCorp Vault)
```

## API Design and Integration

Define new API endpoints and integration with existing APIs:

1. Plan new API endpoints required for the enhancement
2. Ensure consistency with existing API patterns
3. Define authentication and authorization integration
4. Plan versioning strategy if needed

### API Integration Strategy

**API Integration Strategy:** The CollectionsService will expose a set of RESTful APIs to allow for manual payment posting, management of reconciliation tasks, and retrieval of collections-related data. These APIs will adhere to the existing IntelliFin API Gateway routing, authentication (JWT), authorization (role-based), and observability standards (correlation IDs, branch IDs).
**Authentication:** Bearer JWT with standard IntelliFin authentication flow. Specific endpoints will require granular, role-based authorization (e.g., `collections:manage:payments`, `collections:manage:writeoffs`). Step-up authentication will be considered for high-risk operations like write-offs or restructuring.
**Versioning:** API versioning will follow the existing IntelliFin strategy, likely using URL path versioning (e.g., `/api/v1/collections/`).

### New API Endpoints

#### Post Payment

-   **Method:** `POST`
-   **Endpoint:** `/api/v1/collections/payments`
-   **Purpose:** To allow manual posting of a new payment transaction against a loan. This will trigger the internal payment processing logic.
-   **Integration:** Integrates with `PaymentProcessingService` and may trigger `RepaymentPostedEvent`. Requires `collections:manage:payments` permission.
#### Request
```json
{
  "loanId": "guid",
  "amount": 0.00,
  "paymentDate": "datetime",
  "source": "string",
  "reference": "string",
  "installmentId": "guid" (optional)
}
```
#### Response
```json
{
  "transactionId": "guid",
  "status": "string",
  "message": "string"
}
```

#### Get Repayment Schedule

-   **Method:** `GET`
-   **Endpoint:** `/api/v1/collections/loans/{loanId}/schedule`
-   **Purpose:** To retrieve the full repayment schedule for a specific loan.
-   **Integration:** Integrates with `RepaymentScheduleService`. Requires `collections:read:schedules` permission.
#### Request
```json
{}
```
#### Response
```json
{
  "scheduleId": "guid",
  "loanId": "guid",
  "generatedDate": "datetime",
  "status": "string",
  "currency": "string",
  "installments": [
    {
      "installmentId": "guid",
      "dueDate": "datetime",
      "expectedPrincipal": 0.00,
      "expectedInterest": 0.00,
      "expectedTotal": 0.00,
      "paidAmount": 0.00,
      "status": "string",
      "daysPastDue": 0
    }
  ]
}
```

#### Get Overdue Loans

-   **Method:** `GET`
-   **Endpoint:** `/api/v1/collections/loans/overdue`
-   **Purpose:** To retrieve a list of loans currently in an overdue status. Supports filtering and pagination.
-   **Integration:** Integrates with `InstallmentRepository` and `ArrearsClassificationService`. Requires `collections:read:overdue` permission.
#### Request
```json
{
  "page": 1,
  "pageSize": 10,
  "minDaysPastDue": 1,
  "maxDaysPastDue": 30,
  "bozCategory": "string"
}
```
#### Response
```json
{
  "totalCount": 0,
  "page": 1,
  "pageSize": 10,
  "items": [
    {
      "loanId": "guid",
      "clientName": "string",
      "productName": "string",
      "currentDPD": 0,
      "bozCategory": "string",
      "totalOverdueAmount": 0.00
    }
  ]
}
```

#### Reclassify Loan

-   **Method:** `POST`
-   **Endpoint:** `/api/v1/collections/loans/{loanId}/reclassify`
-   **Purpose:** To manually re-evaluate and reclassify a loan's BoZ arrears category. Requires specific permissions and potentially dual control.
-   **Integration:** Integrates with `ArrearsClassificationService`. Requires `collections:manage:classification` permission, possibly step-up auth.
#### Request
```json
{
  "reason": "string",
  "overrideCategory": "string" (optional, with justification)
}
```
#### Response
```json
{
  "loanId": "guid",
  "oldBoZCategory": "string",
  "newBoZCategory": "string",
  "reclassificationDate": "datetime",
  "message": "string"
}
```

#### Initiate Write-Off

-   **Method:** `POST`
-   **Endpoint:** `/api/v1/collections/loans/{loanId}/writeoff`
-   **Purpose:** To initiate the write-off workflow for a loan deemed uncollectible. Triggers Camunda workflow for dual control.
-   **Integration:** Integrates with `WriteOffRequestService` and `CollectionsWorkflowService`. Requires `collections:manage:writeoffs` permission and dual control.
#### Request
```json
{
  "reason": "string",
  "writeOffAmount": 0.00
}
```
#### Response
```json
{
  "requestId": "guid",
  "loanId": "guid",
  "status": "string",
  "message": "string",
  "camundaProcessInstanceId": "string"
}
```

## Source Tree

Define how new code will integrate with existing project structure:

1. Follow existing project organization patterns
2. Identify where new files/folders will be placed
3. Ensure consistency with existing naming conventions
4. Plan for minimal disruption to existing structure

### Existing Project Structure

```plaintext
Intellifin Loan Management System/
├── apps/
│   ├── IntelliFin.Collections/
│       ├── appsettings.Development.json
│       ├── appsettings.json
│       ├── IntelliFin.Collections.csproj
│       ├── Program.cs
│       └── Properties/
├── libs/
│   └── IntelliFin.Shared.Observability/
├── docs/
│   └── domains/
│       └── collections-recovery/
│           ├── brownfield-architecture.md
│           ├── collections-lifecycle-management.md
│           └── prd.md
└── ...
```

### New File Organization

```plaintext
Intellifin Loan Management System/
├── apps/
│   └── IntelliFin.Collections/           # New Collections Service
│       ├── Program.cs
│       ├── appsettings.json
│       ├── appsettings.Development.json
│       ├── IntelliFin.Collections.csproj
│       ├── Domain/                       # Core business logic and entities
│       │   ├── Aggregates/               # Root entities like RepaymentSchedule, Loan (Collection View)
│       │   ├── Entities/                 # Value objects and child entities like Installment, PaymentTransaction
│       │   ├── Events/                   # Domain events (e.g., RepaymentPostedEvent)
│       │   └── Policies/                 # BoZ Classification Policies
│       ├── Application/                  # Application services, commands, queries
│       │   ├── Services/                 # RepaymentScheduleService, PaymentProcessingService, ArrearsClassificationService, CollectionsWorkflowService
│       │   ├── Commands/                 # DTOs for incoming commands (e.g., PostPaymentCommand)
│       │   └── Queries/                  # DTOs for outgoing query results
│       ├── Infrastructure/               # External dependencies and persistence
│       │   ├── Persistence/              # DbContext, Repositories, Migrations
│       │   │   ├── CollectionsDbContext.cs
│       │   │   ├── Repositories/
│       │   │   └── Migrations/
│       │   ├── Messaging/                # MassTransit consumers/publishers (e.g., LoanDisbursedConsumer, PaymentReceivedConsumer)
│       │   ├── Vault/                    # Vault configuration client (CollectionsVaultConfigService)
│       │   └── AdminService/             # AdminService audit client
│       ├── Workflows/                    # Camunda BPMN definitions and workers
│       │   ├── BPMN/                     # BPMN files (e.g., collections_management_v1.bpmn)
│       │   └── CamundaWorkers/           # C# implementations of Camunda workers
│       ├── API/                          # API controllers and DTOs
│       │   ├── Controllers/              # CollectionsController
│       │   └── DTOs/                     # Request/Response DTOs
│       └── Reports/                      # Reporting specific logic and DTOs
│           └── Services/                 # ReportingService
├── libs/
│   ├── IntelliFin.Shared.DomainModels/   # Shared domain models (if applicable)
│   ├── IntelliFin.Shared.Audit/          # Shared audit contracts
│   └── IntelliFin.Shared.Messaging/      # Shared messaging contracts (events)
└── docs/
    └── domains/
        └── collections-recovery/
            ├── brownfield-architecture.md
            ├── collections-lifecycle-management.md
            ├── prd.md
            └── architecture.md             # This document
```

### Integration Guidelines

-   **File Naming:** New C# files will follow PascalCase. BPMN files and MassTransit topics/queues will use kebab-case.
-   **Folder Organization:** Adherence to the proposed layered architecture (Domain, Application, Infrastructure, API, Workflows, Reports) within the `IntelliFin.Collections` service.
-   **Import/Export Patterns:** Dependencies between layers should flow inwards (e.g., Application depends on Domain, Infrastructure depends on Application). Shared contracts will reside in `libs/IntelliFin.Shared.*` projects.

## Infrastructure and Deployment Integration

Define how the enhancement will be deployed alongside existing infrastructure:

1. Use existing deployment pipeline and infrastructure
2. Identify any infrastructure changes needed
3. Plan deployment strategy to minimize risk
4. Define rollback procedures

### Existing Infrastructure

**Current Deployment:** The IntelliFin system is deployed to Kubernetes clusters, utilizing a containerized microservice approach for individual services.
**Infrastructure Tools:** Docker, Kubernetes, Helm/Kustomize (for deployment manifests), GitHub Actions (for CI/CD), HashiCorp Vault (for secrets and configuration).
**Environments:** Development, Staging, Production environments are supported.

### Enhancement Deployment Strategy

**Deployment Approach:** The `IntelliFin.Collections` service will be deployed as a new containerized microservice to the existing Kubernetes cluster. Deployment will be managed via the established GitOps pipeline.
**Infrastructure Changes:**
-   **New Database**: A new SQL Server database instance (or a new schema within an existing instance if multi-tenancy applies) will be required for the CollectionsService. This will be provisioned via existing infrastructure-as-code (e.g., Terraform/Helm).
-   **RabbitMQ Configuration**: New exchanges and queues for CollectionsService-specific events (e.g., `payment-posted-event`, `loan-arrears-classified`) will need to be configured in RabbitMQ.
-   **Camunda BPMN Deployment**: The `collections_management_v1.bpmn` workflow definition will be deployed to the Camunda Zeebe cluster.
**Pipeline Integration:** The CollectionsService will integrate into the existing IntelliFin CI/CD pipeline, including automated builds, testing, container image creation, and deployment to Kubernetes environments.

**DNS or Domain Registration Needs:**
-   **Service Discovery**: The CollectionsService will be registered in the existing Kubernetes service mesh (e.g., Linkerd or similar) for internal service-to-service communication. This relies on Kubernetes' native service discovery.
-   **API Gateway Integration**: Public exposure of the CollectionsService's APIs will be handled via the existing IntelliFin API Gateway. No direct DNS registration for the service itself is required; the API Gateway will manage external routing and domain mapping.

### Rollback Strategy

**Rollback Method:**
-   **Application Code**: Standard Kubernetes deployment rollback capabilities (reverting to a previous stable image version) will be used. Feature flags will be leveraged for granular control over new functionalities, allowing for quick disabling without a full deployment rollback.
-   **Database Schema**: Database schema changes will be additive and managed via Entity Framework Core migrations. Rollback for schema changes will involve reverting the migration, which will be carefully managed to ensure data integrity.
**Risk Mitigation:** Comprehensive automated testing (unit, integration, end-to-end) will minimize the need for rollbacks. Observability (metrics, logs, traces) will provide early detection of issues to enable quick intervention.
**Monitoring:** Continuous monitoring of service health, performance, and error rates via OpenTelemetry and integrated dashboards (e.g., App Insights, ELK). Alerts will be configured for any deviations from normal operation.

### External Service Configuration & Acquisition

**Rationale**: This section addresses the need to explicitly define steps for acquiring and configuring third-party services, which was identified as a critical missing detail.

**Third-Party Service Account/API Key Acquisition:**
-   **Vault:** Access to an existing HashiCorp Vault instance will be assumed, with necessary policies (`collections-service-policy`) and secrets paths (`secret/data/collections/config`) to be pre-configured by the DevOps team.
-   **Camunda (Zeebe):** Access to the existing Camunda Zeebe cluster (or a new dedicated cluster if required for high isolation) will be assumed. Zeebe client credentials will be managed via Kubernetes secrets and injected into the service.
-   **RabbitMQ:** RabbitMQ access credentials will be managed via Kubernetes secrets and injected into the service. Necessary exchanges and queues will be provisioned by the deployment pipeline.
-   **AdminService / CommunicationService / Treasury Service / PMEC Service / Loan Origination / Credit Assessment:** Integration with these internal IntelliFin services will leverage existing service accounts and API access patterns, with necessary permissions granted via Keycloak (IdentityService).

## Coding Standards

Ensure new code follows existing project conventions:

1. Document existing coding standards from project analysis
2. Identify any enhancement-specific requirements
3. Ensure consistency with existing codebase patterns
4. Define standards for new code organization

### Existing Standards Compliance

**Code Style:** Adherence to standard C# coding conventions (e.g., PascalCase for types and members, camelCase for local variables), with formatting enforced by `.editorconfig` and Roslyn analyzers.
**Linting Rules:** Enabled Roslyn analyzers and adherence to warnings-as-errors policy where applicable. Code quality checks integrated into the CI pipeline.
**Testing Patterns:** xUnit for unit and integration tests, following AAA (Arrange-Act-Assert) pattern. Mocking frameworks (e.g., Moq) used for isolating dependencies.
**Documentation Style:** XML documentation comments for public APIs. Markdown (`.md`) for internal project documentation.

### Critical Integration Rules

-   **Existing API Compatibility:** New CollectionsService APIs must strictly conform to existing IntelliFin API patterns, including JWT authentication, `X-Correlation-Id`, `X-Branch-Id` headers, and standard HTTP status codes.
-   **Database Integration:** Database interactions should primarily occur through Entity Framework Core. All schema changes must be additive and managed via migrations, ensuring no disruption to existing services.
-   **Error Handling:** Consistent error handling mechanisms (e.g., custom exception types, global exception filters, Problem Details for API errors) should be implemented, following existing IntelliFin patterns.
-   **Logging Consistency:** Structured logging using `ILogger` and OpenTelemetry for traces and metrics. Logs should provide sufficient context for debugging and auditing, adhering to existing severity levels and event IDs.

## Testing Strategy

Define testing approach for the enhancement:

1. Integrate with existing test suite
2. Ensure existing functionality remains intact
3. Plan for testing new features
4. Define integration testing approach

### Integration with Existing Tests

**Existing Test Framework:** xUnit 3.0+ for unit and integration tests.
**Test Organization:** Tests are typically organized in dedicated `tests/` folders (e.g., `IntelliFin.Tests.Unit`, `IntelliFin.Tests.Integration`) or within the service project itself for component-level tests.
**Coverage Requirements:** Target typically 90% for core business logic components.
**CI Integration:** All tests are integrated into the CI pipeline for automated execution on every build.

### New Testing Requirements

#### Unit Tests for New Components

-   **Framework:** xUnit 3.0+
-   **Location:** `apps/IntelliFin.Collections.Tests/Unit/` (new test project)
-   **Coverage Target:** Minimum 90% for `RepaymentScheduleService`, `PaymentProcessingService`, `ArrearsClassificationService`, `CollectionsWorkflowService`, `VaultConfigService`, and related domain logic.
-   **Integration with Existing:** Unit tests will be isolated from external dependencies using mocking frameworks (e.g., Moq) and will not directly interact with other services or databases.

#### Integration Tests

-   **Scope:** Verify interactions between CollectionsService components (e.g., service to repository, service to event consumers/publishers), and integration with external dependencies.
-   **Existing System Verification:** Integration tests will specifically verify that the CollectionsService correctly consumes events from (e.g., Loan Origination, Treasury, PMEC) and publishes events to (e.g., AdminService, CommunicationService) existing IntelliFin services. They will also validate interactions with Camunda and Vault.
-   **New Feature Testing:** Cover end-to-end flows for repayment scheduling, payment processing, arrears classification, and collections workflow triggers.
-   **Framework:** xUnit 3.0+ with Testcontainers for external dependencies.
-   **Location:** `apps/IntelliFin.Collections.Tests/Integration/` (new test project).

#### Regression Testing

-   **Existing Feature Verification:** Focus on ensuring that the introduction of CollectionsService does not cause regressions in critical upstream (Loan Origination) and downstream (Treasury, GL) financial processes.
-   **Automated Regression Suite:** Existing automated integration and E2E tests (if available) for core IntelliFin financial flows will be run as part of the CI/CD pipeline.
-   **Manual Testing Requirements:** Targeted manual testing will be performed in staging environments for complex end-to-end scenarios involving multiple services and human-in-the-loop Camunda tasks, especially around reconciliation and write-off processes.

## Security Integration

Ensure security consistency with existing system:

1. Follow existing security patterns and tools
2. Ensure new features don't introduce vulnerabilities
3. Maintain existing security posture
4. Define security testing for new components

### Existing Security Measures

**Authentication:** Bearer JWT tokens issued by IntelliFin IdentityService are used for API authentication across services.
**Authorization:** Role-based access control (RBAC) enforced via JWT claims and application-level authorization policies.
**Data Protection:** TLS in transit, TDE for SQL Server, and field-level encryption for PII where needed. Vault for managing secrets.
**Security Tools:** Roslyn analyzers for code security, security scanners in CI/CD (assumed), external penetration testing.

### Enhancement Security Requirements

**New Security Measures:**
-   **Dual Control for Write-Offs**: Implement a robust Camunda-orchestrated dual control workflow for loan write-offs, requiring approvals from specified roles (e.g., Head of Credit, Senior Finance Manager).
-   **Granular Authorization**: Implement fine-grained, role-based authorization for collections-specific operations (e.g., `collections:manage:payments`, `collections:manage:writeoffs`, `collections:read:overdue`).
-   **Step-Up Authentication**: Consider implementing step-up authentication for highly sensitive actions like manual write-off initiation or loan restructuring, integrating with the IdentityService.
**Integration Points:**
-   **IdentityService**: For user authentication and authorization (roles/claims).
-   **AdminService**: For comprehensive audit logging of all security-sensitive actions, including user who performed action, timestamps, and before/after states of critical data.
-   **Vault**: For secure storage and retrieval of sensitive collections policies and configuration.
**Compliance Requirements:** Adherence to BoZ security directives and Consumer Protection Standards regarding data privacy and fair collections practices.

### Security Testing

**Existing Security Tests:** Existing IntelliFin security testing practices will be applied, including automated security scans in CI/CD.
**New Security Test Requirements:**
-   **Authorization Matrix Tests**: Automated tests to verify correct role-based access for all new CollectionsService API endpoints.
-   **Dual Control Workflow Tests**: Dedicated integration tests for the write-off dual control workflow, ensuring all approval steps and audit trails are correctly enforced.
-   **Input Validation Tests**: Comprehensive unit and integration tests for all API inputs to prevent injection attacks (e.g., XSS, SQL Injection).
-   **Vulnerability Scanning**: Inclusion of the CollectionsService in regular vulnerability scanning and penetration testing efforts.
**Penetration Testing:** Dedicated penetration testing for the CollectionsService will be conducted, with a focus on authentication bypasses, authorization flaws, data manipulation, and workflow integrity.

## Checklist Results Report

### Executive Summary

The proposed architecture for the IntelliFin Collections & Recovery module demonstrates a **High** readiness level for implementation. It is well-aligned with the product requirements, leverages existing IntelliFin architectural patterns, and provides clear technical guidance. Critical risks have been identified with robust mitigation strategies. This is a **Backend-only (Service-only)** project, and frontend-specific sections of the checklist were intentionally skipped.

**Key Strengths of the Architecture:**
-   Strong adherence to existing IntelliFin microservice patterns and tech stack.
-   Comprehensive integration strategy with existing services (RabbitMQ, Vault, Camunda, AdminService).
-   Detailed new data models and clear schema integration strategy.
-   Explicit security requirements, including dual control and granular authorization.
-   Robust testing strategy with emphasis on integration and regression.

**Critical Risks Identified:**
-   Complexity of financial calculations (DPD, provisioning).
-   Multiple, critical integration points with existing financial services.
-   Camunda workflow complexity for orchestration and human tasks.
-   BoZ compliance failures due to calculation errors or audit deficiencies.

### Section Analysis

(Note: All scores are based on the current architecture document and PRD. `N/A` indicates sections skipped due to this being a backend-only service.)

| Section                     | Pass Rate | Most Concerning Gaps/Failures | Recommendations                                     |
| :-------------------------- | :-------- | :---------------------------- | :-------------------------------------------------- |
| 1. Requirements Alignment   | 100%      | N/A                           | Clear alignment between architecture and PRD requirements. |
| 2. Architecture Fundamentals | 95%       | N/A                           | Diagramming is comprehensive, clear separation of concerns. |
| 3. Technical Stack & Decisions | 90%       | N/A                           | Specific technology versions are generally assumed (N/A) if not explicitly defined. |
| 3.2 Frontend Architecture   | N/A       | N/A                           | Skipped (Backend-only project).                     |
| 4. Frontend Design & Implementation | N/A   | N/A                           | Skipped (Backend-only project).                     |
| 5. Resilience & Operational Readiness | 95% | N/A                           | Comprehensive error handling, monitoring, and deployment strategies. |
| 6. Security & Compliance    | 95%       | N/A                           | Strong authentication, authorization, and data protection strategies. |
| 7. Implementation Guidance  | 90%       | N/A                           | Clear coding standards and testing expectations.     |
| 8. Dependency & Integration Management | 95% | N/A                           | Clear mapping of internal and external dependencies. |
| 9. AI Agent Implementation Suitability | 90% | N/A                           | Highly modular, clear patterns for AI agent understanding. |
| 10. Accessibility Implementation | N/A   | N/A                           | Skipped (Backend-only project).                     |

**Sections Requiring Immediate Attention:**
-   None; the architecture document provides sufficient detail and strategy for all key areas.

### Risk Assessment

(Referencing the detailed "Risk Assessment and Mitigation" section in this document.)

**Top 5 Risks by Severity:**
1.  **Complexity of Financial Calculations (Technical Risk)**: High impact if errors occur (regulatory, financial).
    *   **Mitigation**: Phased development, extensive automated testing, automated validation of BoZ rules.
2.  **Multiple Integration Points (Integration Risk)**: High potential for points of failure, data mismatch.
    *   **Mitigation**: Contract testing, robust error handling, observability, phased development.
3.  **Camunda Workflow Complexity (Integration Risk)**: High risk of implementation errors, unexpected behavior.
    *   **Mitigation**: Incremental development, extensive testing of workflows, clear worker responsibilities.
4.  **BoZ Compliance Failures (Regulatory/Compliance Risk)**: Severe penalties and reputational damage.
    *   **Mitigation**: Automated validation of rules, comprehensive audit logging, security by design.
5.  **Vault Configuration Reliance (Integration Risk)**: Potential for service unavailability if Vault inaccessible.
    *   **Mitigation**: Robust caching and fallback mechanisms for Vault configuration.

**Timeline Impact of Addressing Issues:**
-   All identified risks have mitigation strategies outlined within the architecture document. Implementing these mitigations will be integrated into the development timeline and require dedicated effort, particularly for testing and robust error handling.

### Recommendations

**Must-fix items before development:**
-   None. All previously identified must-fix items have been addressed within this architecture document.

**Should-fix items for better quality:**
-   **Development Environment Preservation**: While implied, explicitly state how the development environment will preserve existing functionality to avoid any ambiguity. *(Checklist Item 1.2)*
-   **Explicit Development Environment Setup**: Include explicit steps for local development environment setup and dependency installation to streamline onboarding for new developers. *(Checklist Item 1.3)*
-   **Blue-Green/Canary Deployment Implementation**: While "considered," explicitly detail the implementation plan for blue-green or canary deployments for critical updates. *(Checklist Item 2.3)*
-   **API Limits/Constraints Acknowledgement**: Explicitly document API limits or constraints for external integrations. *(Checklist Item 3.2)*
-   **Backup and Recovery Procedures Update**: Explicitly mention updating or verifying backup and recovery procedures for the new CollectionsService database. *(Checklist Item 7.2)*
-   **Comprehensive Developer Setup Instructions**: Ensure that developer setup instructions are comprehensive, beyond just listing frameworks. *(Checklist Item 9.1)*
-   **Error Message and User Feedback Clarity**: Enhance documentation on error messages and ensure clear user feedback mechanisms. *(Checklist Item 9.2)*

### AI Implementation Readiness

The architecture is highly suitable for AI agent implementation.
-   **Modularity**: Components are clearly defined with single responsibilities and minimized dependencies.
-   **Clarity & Predictability**: Consistent patterns, clear naming conventions, and detailed integration points reduce ambiguity.
-   **Implementation Guidance**: Detailed data models, API specifications, and source tree organization provide clear directives.
-   **Complexity Hotspots**: The complexity of financial calculations, BoZ rules, and Camunda workflows are explicitly acknowledged, guiding AI agents to focus extra attention on these areas for robust and compliant implementation.

## Next Steps

After completing the brownfield architecture:

1. Review integration points with existing system
2. Begin story implementation with Dev agent
3. Set up deployment pipeline integration
4. Plan rollback and monitoring procedures

### Story Manager Handoff

"This enhanced architecture document (`docs/domains/collections-recovery/architecture.md`) is now complete, building upon the PRD (`docs/domains/collections-recovery/prd.md`) and the current state analysis (`docs/domains/collections-recovery/brownfield-architecture.md`).

Please review the 'Epic 1: Collections & Recovery Module Implementation' section within the PRD, particularly 'Story 1.1 Repayment Schedule Generation and Persistence', and the subsequent stories.

**Key considerations for story development:**
-   **Integration Requirements**: Ensure stories clearly define how new components integrate with Loan Origination, Treasury, PMEC, AdminService, CommunicationService, Camunda, and Vault, leveraging established patterns.
-   **Existing System Constraints**: Adhere to existing messaging conventions, API patterns, and security controls identified in the architecture document.
-   **First Story Integration Checkpoints**: For Story 1.1 (Repayment Schedule Generation and Persistence), ensure clear checkpoints for verifying successful event consumption from Loan Origination and correct configuration retrieval from Vault.
-   **Maintain Existing System Integrity**: Each story must explicitly include acceptance criteria and verification steps to ensure no regressions in existing functionality.

The architecture ensures the CollectionsService is highly modular and suitable for AI agent implementation. Focus on clear, concise story definitions that provide sufficient technical context, referencing this architecture document for details."

### Developer Handoff

"This enhanced architecture document (`docs/domains/collections-recovery/architecture.md`) provides the comprehensive technical blueprint for implementing the IntelliFin Collections & Recovery module. Please refer to this document, the PRD (`docs/domains/collections-recovery/brownfield-recovery/prd.md`), and the existing brownfield architecture (`docs/domains/collections-recovery/brownfield-architecture.md`) throughout development.

**Key technical decisions based on real project constraints:**
-   **Microservice Architecture**: Develop `IntelliFin.Collections` as an independent .NET 9.0 ASP.NET Core microservice with its own dedicated SQL Server database.
-   **Event-Driven Integration**: Utilize MassTransit/RabbitMQ for consuming events (e.g., `LoanDisbursedEvent`, `PaymentReceivedEvent`, `PMECTransactionConfirmedEvent`) and publishing audit/notification events.
-   **Vault Integration**: Implement a `CollectionsVaultConfigService` for dynamic retrieval of BoZ rules and other policies.
-   **Camunda Workflow**: Implement C# Camunda Workers (`BackgroundService`) for orchestrating collections workflows (`collections_management_v1.bpmn`), including reconciliation and write-off processes.
-   **AdminService Audit Logging**: Ensure all critical actions generate `AuditEventDto`s and are sent to AdminService.
-   **Security**: Implement granular role-based authorization for APIs and dual-control workflows for sensitive operations (write-offs).

**Existing system compatibility requirements with specific verification steps:**
-   **API Consistency**: New REST APIs must conform to existing IntelliFin API patterns (JWT auth, `X-Correlation-Id`, `X-Branch-Id` headers).
-   **Database Schema**: All database changes must be additive and managed via Entity Framework Core migrations, ensuring backward compatibility.
-   **Messaging**: Adhere to existing MassTransit naming conventions and event schemas.
-   **Testing**: Implement comprehensive unit, integration (with Testcontainers), and regression tests, focusing on critical financial calculations and integration points.

**Clear sequencing of implementation to minimize risk to existing functionality:**
-   Start with foundational components like `CollectionsDbContext`, `RepaymentScheduleService`, `VaultConfigService`.
-   Implement event consumers for `LoanDisbursedEvent` to generate repayment schedules.
-   Progress to payment processing, arrears classification, and finally Camunda workflows for collections and write-offs.
-   Prioritize rigorous testing and phased deployments to mitigate risks associated with a new critical financial service in a brownfield environment."
