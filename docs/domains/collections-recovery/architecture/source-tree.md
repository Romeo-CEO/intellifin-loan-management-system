# Source Tree

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
