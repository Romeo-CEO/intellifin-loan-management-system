# IntelliFin.Collections Brownfield Enhancement PRD

## Intro Project Analysis and Context

### Existing Project Overview

#### Analysis Source

- Document-project output available at: `docs/domains/collections-recovery/brownfield-architecture.md`

#### Current Project State

The `IntelliFin.Collections` service is a .NET 9.0 ASP.NET Core microservice, currently configured with basic endpoints for health checks and OpenAPI. It leverages `IntelliFin.Shared.Observability` for OpenTelemetry integration. It follows the standard IntelliFin microservice structure, serving as a placeholder or a new service for collections.

### Available Documentation Analysis

- Note: "Document-project analysis available - using existing technical documentation"

#### Available Documentation

- Tech Stack Documentation ✓
- Source Tree/Architecture ✓
- Coding Standards [[LLM: May be partial]]
- API Documentation ✓
- External API Documentation ✓
- UX/UI Guidelines [[LLM: May not be in document-project]]
- Technical Debt Documentation ✓
- "Other: collections-lifecycle-management.md"

### Enhancement Scope Definition

#### Enhancement Type

- New Feature Addition ✓
- Major Feature Modification
- Integration with New Systems ✓
- Performance/Scalability Improvements
- UI/UX Overhaul
- Technology Stack Upgrade
- Bug Fix and Stability Improvements
- "Other: Automated Collections Lifecycle Management"

#### Enhancement Description

The Collections & Recovery module will manage all post-disbursement loan activities, including repayment scheduling, payment reconciliation, arrears tracking, BoZ classification and provisioning, automated collections workflows, and customer notifications. This module will integrate with existing IntelliFin services such as Loan Origination, Credit Assessment, Client Management, Treasury, PMEC, AdminService, and CommunicationService to close the credit lifecycle and ensure regulatory compliance.

#### Impact Assessment

- Minimal Impact (isolated additions)
- Moderate Impact (some existing code changes)
- Significant Impact (substantial existing code changes) ✓
- Major Impact (architectural changes required) ✓

### Goals and Background Context

#### Goals

-   Maintain healthy loan portfolio and minimize losses.
-   Adhere to Bank of Zambia (BoZ) directives for loan classification and provisioning.
-   Ensure consistent cash flow for business operations.
-   Balance collections with customer retention and satisfaction.
-   Early identification and management of credit risks.
-   Achieve a fully digital and auditable lending loop from origination to closure.

#### Background Context

With Credit Assessment and upstream loan origination workflows now stable, the Collections & Recovery module is the next critical piece of the IntelliFin Loan Management System. This module will manage all activities after a loan has been disbursed, encompassing repayments, arrears tracking, automated reminders, and recovery workflows. Its successful implementation is paramount for maintaining the business's financial health, ensuring compliance with BoZ provisioning and classification requirements, and transforming raw loan data into actionable financial operations and compliance insights.

### Change Log

| Change               | Date       | Version | Description              | Author |
| -------------------- | ---------- | ------- | ------------------------ | ------ |
| Initial PRD Creation | 2025-10-22 | 1.0     | Drafted new collections PRD based on brownfield architecture. | John (PM) |

## Requirements

These requirements are based on my understanding of your existing system and the `Collections Lifecycle Management` document. Please review carefully and confirm they align with your project's reality.

### Functional

1.  **FR1**: The system shall automatically generate repayment schedules for each disbursed loan based on product configuration (principal, interest, term, frequency) retrieved from Vault.
2.  **FR2**: Each installment in the repayment schedule shall have a due date, expected amount, and status (Pending, Paid, Overdue).
3.  **FR3**: The system shall support recalculation of amortization details upon loan restructuring or partial prepayment.
4.  **FR4**: The system shall process incoming payments from various sources (manual, PMEC, Treasury) and match them to the correct loan installments.
5.  **FR5**: Upon successful payment, the system shall update installment status (Paid/Partially Paid), adjust outstanding balances, and publish a `RepaymentPosted` event.
6.  **FR6**: Overpayments or misapplied transactions shall trigger a Camunda reconciliation task for manual correction, with reconciled data flowing to Treasury.
7.  **FR7**: The system shall automatically evaluate loan delinquency days (DPD) nightly via a background job or Camunda batch workflow.
8.  **FR8**: The system shall automatically classify each loan into BoZ arrears categories (Performing, Watch, Substandard, Doubtful, Loss) based on DPD thresholds configured in Vault.
9.  **FR9**: The system shall perform automated provisioning calculations for regulatory reporting based on BoZ provisioning rates configured in Vault.
10. **FR10**: Loan classification changes and provisioning calculations shall be stored as part of the loan's version history.
11. **FR11**: The system shall orchestrate a Camunda-managed Collections workflow for overdue loans, including stages for reminders, call tasks, escalation, and write-off.
12. **FR12**: Each stage of the Collections workflow shall emit audit events to AdminService and trigger notifications via CommunicationService (SMS/email).
13. **FR13**: The system shall send automated customer notifications (payment reminders, payment confirmations, overdue alerts, settlement notices) via CommunicationService, using pre-approved templates and respecting client consent preferences.
14. **FR14**: The system shall be capable of generating daily aging reports, Portfolio-at-Risk (PAR) summaries, provisioning summaries, and recovery rate analytics.

### Non Functional

1.  **NFR1**: All arrears classifications, provisioning percentages, and penalty rules shall be configurable and stored in Vault.
2.  **NFR2**: The CollectionsService shall be a standalone microservice.
3.  **NFR3**: Daily DPD calculations and BoZ classification must happen automatically each night.
4.  **NFR4**: Audit events for every payment, classification, and escalation shall be routed to AdminService with timestamps, user roles, and before/after balances.
5.  **NFR5**: The system shall enforce dual control for write-offs and loan restructuring.
6.  **NFR6**: Only Collections Officers can modify payment or reconciliation records.

### Compatibility Requirements

1.  **CR1: Existing API Compatibility**: New CollectionsService APIs must adhere to existing IntelliFin API patterns (JWT auth, `X-Correlation-Id`, `X-Branch-Id` headers, standard HTTP status codes).
2.  **CR2: Database Schema Compatibility**: Any new database schema changes must be additive and not break existing dependent services (e.g., Loan Origination).
3.  **CR3: UI/UX Consistency**: Any future UI components related to collections (e.g., Collections Workbench) must adhere to the existing `lms-ux-style-guide.md` and overall IntelliFin design system.
4.  **CR4: Integration Compatibility**: The CollectionsService must seamlessly integrate with Loan Origination, Credit Assessment, Client Management, Treasury, PMEC, AdminService, CommunicationService, and Camunda using established messaging and API patterns.

## Technical Constraints and Integration Requirements

This section replaces separate architecture documentation. It gathers detailed technical constraints from the existing project analysis.

### Existing Technology Stack

**Languages**: .NET (C#)
**Frameworks**: ASP.NET Core 9.0
**Database**: SQL Server (assumed standard for IntelliFin services)
**Infrastructure**: Docker, Kubernetes (for deployment of microservices)
**External Dependencies**: RabbitMQ (MassTransit), HashiCorp Vault, Camunda (Zeebe), AdminService, CommunicationService, Treasury Service, PMEC Service, Loan Origination, Credit Assessment.

### Integration Approach

**Database Integration Strategy**: The CollectionsService will manage its own dedicated SQL Server database for persistent collections data (e.g., repayment schedules, individual installments, arrears classifications, provisioning history, reconciliation events). It will consume necessary loan and client details from other services primarily through eventing, rather than direct database access, adhering to microservice best practices.

**API Integration Strategy**: The CollectionsService will expose well-defined REST API endpoints for operations such as manual payment posting, managing reconciliation tasks (if not fully Camunda-driven), and retrieving collections-related data. It will consume existing IntelliFin APIs (e.g., Vault for configuration, potentially Client Management for real-time contact details if not event-sourced). All API interactions will follow established IntelliFin patterns for authentication, authorization, headers, and status codes.

**Frontend Integration Strategy**: While this PRD focuses on the backend service, it is anticipated that a future "Collections Workbench" or similar UI will consume the REST APIs exposed by the CollectionsService. The service's API design will consider the needs of a modern frontend application, ensuring clear contract definition and efficient data retrieval.

**Testing Integration Strategy**: The CollectionsService will implement a comprehensive testing strategy including unit, integration, and end-to-end tests. Integration tests will simulate interactions with RabbitMQ (using test containers), Camunda (using mock Zeebe clients or test containers), and other external services (using test doubles or contract testing). This will ensure robust integration without relying on live external dependencies during component testing.

### Code Organization and Standards

**File Structure Approach**: The `IntelliFin.Collections` service will follow the established IntelliFin microservice project structure, with clear separation of concerns (e.g., Domain, Application, Infrastructure, Presentation layers if applicable). New features will reside within logical folders (e.g., `Domain/Aggregates`, `Application/Commands`, `Infrastructure/Persistence`). Camunda BPMN files will be located under `Workflows/BPMN` and corresponding workers under `Workflows/CamundaWorkers`.

**Naming Conventions**:
-   **C# Code**: PascalCase for classes, methods, properties (e.g., `RepaymentScheduleService`, `ProcessPaymentAsync`).
-   **Database**: PascalCase for tables, columns (e.g., `RepaymentSchedules`, `InstallmentAmount`).
-   **Messaging**: Kebab-case for MassTransit exchanges and queues (e.g., `payment-posted-event`, `collections_repayment-posted`).
-   **BPMN**: Kebab-case for process IDs and topic names (e.g., `collections-management-v1`, `collections.process.arrears`).

**Coding Standards**: Adherence to .NET best practices, Clean Architecture principles where appropriate, and any overarching IntelliFin coding guidelines. This includes consistent formatting, error handling, logging (via OpenTelemetry), and asynchronous programming patterns.

**Documentation Standards**: Markdown (`.md`) for internal project documentation (like this PRD and architecture documents). OpenAPI/Swagger for API documentation. Camunda BPMN files (`.bpmn`) for workflow definitions.

### Deployment and Operations

**Build Process Integration**: The `IntelliFin.Collections` service will integrate into the existing IntelliFin CI/CD pipeline. This involves standard .NET `dotnet build` commands to produce containerized images (Docker). Automated tests will be run as part of the build pipeline to ensure code quality and functionality before deployment.

**Deployment Strategy**: The service will be deployed as a containerized microservice to the existing Kubernetes cluster, following established GitOps practices. New deployments will utilize Helm charts or Kustomize configurations to manage Kubernetes resources. Blue/Green or Canary deployment strategies will be considered for critical updates to minimize downtime and risk.

**Monitoring and Logging**: Leveraging the existing `IntelliFin.Shared.Observability` library, the CollectionsService will emit metrics and traces to the OpenTelemetry Collector, which is then forwarded to the central monitoring stack (e.g., App Insights, ELK). Structured logging will be implemented using Serilog or similar, with logs aggregated centrally. Health checks (`/health` endpoint) will be integrated into Kubernetes for liveness and readiness probes.

**Configuration Management**: Application configuration will be managed via `appsettings.json` and environment variables. Sensitive configurations and dynamic policies (e.g., BoZ classification thresholds, provisioning rates, penalty rules) will be retrieved securely from HashiCorp Vault at runtime, adhering to the Vault integration patterns established across IntelliFin services.

### Risk Assessment and Mitigation

**Technical Risks**:
-   **Minimal Current Implementation**: The `IntelliFin.Collections` service is currently a barebones ASP.NET Core application, lacking domain logic, database integration, messaging handlers, and Camunda workers. This necessitates extensive development from scratch, increasing the risk of scope creep or unforeseen technical challenges during implementation.
-   **Complexity of Financial Calculations**: Accurate calculation of Days Past Due (DPD), interest accrual reversal, and loan loss provisioning per BoZ directives is complex. Errors in these calculations could lead to significant financial and regulatory impact.
-   **Data Consistency**: Ensuring consistency of repayment schedules, outstanding balances, and loan classifications across the CollectionsService and other services (e.g., Loan Origination, Treasury) is challenging, especially with asynchronous event-driven integrations.
-   **Amortization Recalculation**: Recalculating amortization details accurately for loan restructuring or partial prepayments introduces significant complexity and potential for calculation errors.

**Integration Risks**:
-   **Multiple Integration Points**: The CollectionsService integrates with numerous existing services (Loan Origination, Credit Assessment, Client Management, Treasury, PMEC, AdminService, CommunicationService, Camunda). Each integration introduces potential points of failure, data mismatch, or unexpected behavior.
-   **Event Ordering and Idempotency**: For payment and other critical events, ensuring correct event ordering and handling idempotent operations is crucial to prevent double-processing or data corruption, especially with Treasury and PMEC integrations.
-   **Camunda Workflow Complexity**: Designing and implementing robust Camunda workflows (`collections_management_v1.bpmn`) with automated tasks, human tasks, escalations, and seamless integration with C# workers is inherently complex and requires careful testing.
-   **Vault Configuration Reliance**: Over-reliance on Vault for critical configuration (BoZ rules, provisioning rates) without robust caching and fallback mechanisms could impact service availability if Vault becomes unreachable.

**Deployment Risks**:
-   **New Critical Service Introduction**: Introducing a new core financial service into a live production environment always carries a risk of unforeseen issues, performance bottlenecks, or resource contention.
-   **Rollback Complexity**: Due to the nature of financial transactions and regulatory data, a clean rollback strategy for CollectionsService changes might be complex, especially after data modifications or GL postings.

**Regulatory/Compliance Risks**:
-   **BoZ Compliance Failures**: Inaccurate or delayed BoZ loan classification and provisioning calculations could lead to severe regulatory penalties and reputational damage.
-   **Audit Trail Deficiencies**: Failure to log all required actions to AdminService with sufficient detail could result in audit findings and non-compliance.
-   **Security Controls**: Insufficient enforcement of dual control for write-offs and access restrictions for Collections Officers could lead to fraudulent activities or unauthorized data modification.

**Mitigation Strategies**:
-   **Phased Development & Incremental Deployment**: Implement and deploy core functionalities incrementally, starting with less risky components (e.g., repayment scheduling) before moving to high-risk areas (e.g., BoZ provisioning, write-offs).
-   **Extensive Automated Testing**: Implement comprehensive unit, integration, and end-to-end tests for all calculations, business logic, and integration points. Utilize test containers for messaging and Camunda to simulate real environments.
-   **Contract Testing**: Implement contract tests with other services (e.g., Treasury, PMEC) to ensure API and event schema compatibility.
-   **Robust Error Handling & Observability**: Implement resilient error handling with retry mechanisms, dead-letter queues, and circuit breakers. Leverage OpenTelemetry for detailed monitoring, tracing, and alerting for all critical operations and integrations.
-   **Feature Flags**: Utilize feature flags for new functionalities to enable controlled rollout and easy rollback in production if issues arise.
-   **Clear Data Versioning & Immutability**: For critical data like loan classifications and provisioning, implement versioning and immutable records to ensure an auditable history.
-   **Automated Validation of BoZ Rules**: Implement automated tests that specifically validate BoZ classification and provisioning rules against predefined scenarios.
-   **Security by Design**: Embed security best practices from the outset, including robust authentication/authorization, input validation, and secure handling of sensitive data.
-   **Comprehensive Audit Logging**: Ensure every significant action and state change is logged to AdminService, including "before" and "after" states for critical financial records.
-   **Defined Rollback Procedures**: Establish clear, tested rollback procedures for all deployments, especially those involving database schema changes or critical business logic.
-   **Performance Testing**: Conduct load and stress testing to ensure the service can handle expected transaction volumes, particularly for daily batch jobs and real-time payment processing.

## Epic and Story Structure

Based on my analysis of your existing project, I believe this enhancement should be structured as **a single comprehensive epic** because the Collections & Recovery module represents a cohesive, end-to-end lifecycle within a single service, with clearly defined stages and interdependencies. Breaking it into multiple high-level epics might fragment the holistic view of the collections process. Does this align with your understanding of the work required?

### Epic Approach

**Epic Structure Decision**: A single comprehensive epic (e.g., "Collections & Recovery Module Implementation") will encapsulate all the functional and non-functional requirements outlined in this PRD. This approach ensures that all stories contribute to a unified goal, making it easier to track progress, manage dependencies, and maintain a holistic view of the Collections Lifecycle Management.

## Epic 1: Collections & Recovery Module Implementation

**Epic Goal**: To implement the IntelliFin Collections & Recovery module, encompassing automated repayment scheduling, payment reconciliation, BoZ-compliant arrears classification and provisioning, a Camunda-orchestrated collections workflow, and automated customer notifications, thereby closing the credit lifecycle with a fully digital and auditable solution.

**Integration Requirements**:
-   Seamless integration with Loan Origination for loan details and repayment terms.
-   Consumption of risk categories from Credit Assessment.
-   Retrieval of contact details and communication preferences from Client Management.
-   Integration with Treasury for payment inflows and reconciliation.
-   Confirmation of payroll deductions from PMEC.
-   Centralized audit trail via AdminService for all events and actions.
-   Customer notifications orchestrated through CommunicationService.
-   Workflow orchestration and human task management via Camunda (Zeebe).

### Story 1.1 Repayment Schedule Generation and Persistence

As a Loan Operations Specialist,
I want the system to automatically generate and persist a detailed repayment schedule for each disbursed loan,
so that I have a clear, auditable record of all expected payments.

#### Acceptance Criteria

1.  The system shall generate a repayment schedule based on loan product configuration (principal, interest, term, frequency) obtained from Vault upon loan disbursement.
2.  Each generated installment shall include a unique identifier, due date, expected principal amount, expected interest amount, and an initial status of 'Pending'.
3.  The system shall store the complete repayment schedule and individual installments in a dedicated and persistent data store within the CollectionsService.
4.  The system shall provide an auditable record of when each repayment schedule was generated and linked to its corresponding loan.
5.  Existing loan origination data remains unchanged after repayment schedule generation.
6.  The system shall gracefully handle cases where product configuration from Vault is unavailable, potentially using a default or flagging for manual review.

#### Integration Verification

1.  **IV1**: Verify that a newly disbursed loan in Loan Origination successfully triggers the generation of a repayment schedule in CollectionsService.
2.  **IV2**: Confirm that the generated repayment schedule details (principal, interest, term, frequency) accurately reflect the loan product configuration as stored in Vault.
3.  **IV3**: Validate that the CollectionsService database contains the complete repayment schedule and individual installment records for the disbursed loan.
