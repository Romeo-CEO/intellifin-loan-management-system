# Next Steps

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
