# Infrastructure and Deployment Integration

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
