# Technical Constraints and Integration Requirements

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
