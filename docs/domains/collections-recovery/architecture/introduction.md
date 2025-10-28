# Introduction

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
