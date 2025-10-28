# Testing Strategy

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
