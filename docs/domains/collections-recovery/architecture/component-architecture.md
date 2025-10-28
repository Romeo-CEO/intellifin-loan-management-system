# Component Architecture

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
