# Coding Standards

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
