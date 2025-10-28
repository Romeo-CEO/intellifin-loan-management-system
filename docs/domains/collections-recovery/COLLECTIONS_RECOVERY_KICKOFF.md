# Collections & Recovery Module - Implementation Kickoff

**Date:** 2025-10-22  
**Branch:** `feature/collections-recovery`  
**Status:** 🟢 Ready for Development - Stories 1.1-1.6 Approved

---

## Executive Summary

You are about to implement the Collections & Recovery module that closes the credit lifecycle post-disbursement. This service automates repayment scheduling, payment processing and reconciliation, BoZ-compliant arrears classification and provisioning, Camunda-driven collections workflows, automated notifications, and reporting.

### What You're Building:
- Automated repayment schedules with immutable installments
- Payment ingestion (manual, PMEC, Treasury) and reconciliation
- Nightly BoZ arrears classification and provisioning
- Camunda-orchestrated collections workflows with dual control for write-offs
- Customer notifications via CommunicationService respecting consent
- Reporting: aging, PAR, provisioning, recovery analytics
- Tamper-evident audit trails via AdminService

---

## Current State Assessment

**Location:** `apps/IntelliFin.Collections/`

**Foundation Present:**
- ASP.NET Core minimal scaffold with OpenTelemetry and health checks
- Service wiring and basic configuration

**To be implemented in this epic:**
- EF Core DbContext, entities, and migrations (Collections-specific)
- Messaging consumers (MassTransit/RabbitMQ)
- Camunda workers and BPMN assets
- Vault integrations for rules and rates
- REST APIs for reporting and manual payment entry

---

## Scope (Epic 1)

Stories created and approved in `docs/domains/collections-recovery/stories/`:
1. 1.1 Repayment Schedule Generation and Persistence
2. 1.2 Payment Processing and Reconciliation
3. 1.3 Daily Arrears Classification and Provisioning
4. 1.4 Collections Workflow Orchestration
5. 1.5 Automated Customer Notifications
6. 1.6 Collections Reporting and Analytics

These cover FR1–FR14 plus NFRs and compatibility requirements per PRD and Architecture.

---

## Implementation Overview

### Total Scope: 6 Stories (Epic 1)

#### Phase 1: Data & Scheduling (Stories 1.1, 1.3)
- Entities: RepaymentSchedule, Installment, ArrearsClassificationHistory
- Nightly classification job / Camunda batch
- Vault-driven classification/provisioning rates

#### Phase 2: Payments & Reconciliation (Story 1.2)
- PaymentTransaction, ReconciliationTask
- Treasury/PMEC consumers, manual payment API
- Camunda reconciliation workflow

#### Phase 3: Workflows & Notifications (Stories 1.4, 1.5)
- Camunda BPMN + workers (reminders, calls, escalation, write-offs)
- Dual control for write-offs
- CommunicationService + Client consent

#### Phase 4: Reporting (Story 1.6)
- ReportingService
- APIs for aging, PAR, provisioning, recovery analytics

---

## Story Priority & Order

1) Story 1.1 – Repayment Schedule (blocks payments/classification)  
2) Story 1.2 – Payment Processing & Reconciliation  
3) Story 1.3 – Nightly Classification & Provisioning  
4) Story 1.4 – Collections Workflow (Camunda)  
5) Story 1.5 – Notifications (CommunicationService + consent)  
6) Story 1.6 – Reporting & Analytics

---

## Key Technical Integrations

### HashiCorp Vault
- Product schedule parameters, BoZ classification thresholds, provisioning rates
- Paths agreed in architecture; hot-reload not required for this epic

### Camunda (Zeebe)
- BPMN: `collections_management_v1.bpmn`, `daily_arrears_classification_v1.bpmn`
- Workers: reminders, calls, escalation, write-off approvals, reconciliation

### RabbitMQ/MassTransit
- Consumers: Treasury payments, PMEC confirmations, LoanDisbursed
- Idempotent processing and correlation propagation

### AdminService (Audit)
- All critical actions audited: schedule generation, payments, classification, workflow transitions, notifications, reports

### CommunicationService
- Template-based notifications; respects consent from Client Management

---

## Architecture Patterns

```
apps/IntelliFin.Collections/
├── API/
├── Application/
│   └── Services/
├── Domain/
│   ├── Aggregates/
│   └── Entities/
├── Infrastructure/
│   ├── Messaging/
│   ├── Persistence/
│   ├── Vault/
│   └── HealthChecks/
└── Workflows/
    ├── BPMN/
    └── CamundaWorkers/
```

Principles: async/await, correlation IDs, centralized deps, clean layering, testable units, OpenTelemetry, feature flags where needed.

---

## Implementation Guidelines

### Before Each Story
1. Read the story file and acceptance criteria
2. Confirm dependencies (order above)
3. Review `docs/domains/collections-recovery/architecture/*`

### During Implementation
1. Follow proposed source tree and patterns
2. Enforce JWT + role-based authorization per story
3. Emit AdminService audit events for critical operations
4. Use Testcontainers for integration tests (DB, RabbitMQ, Camunda when applicable)

### After Implementation
1. `dotnet build` – 0 errors
2. `dotnet test` – all tests pass
3. Run service locally and execute IV steps from stories
4. Update story Status accordingly

---

## Quality Gates

### Coverage Targets
- Services: 85%+
- Camunda Workers: 80%+

### Performance Targets
- Payment posting p95 < 250ms
- Nightly classification completes within SLA window

### Security Requirements
- JWT bearer auth, claims-based RBAC
- Dual control enforced for write-offs
- No secrets in code; use Vault

---

## Build & Test Commands

```bash
# Build solution
dotnet build

# Run all tests
dotnet test

# Run the Collections service locally
cd apps/IntelliFin.Collections
dotnet run
```

---

## Next Steps

1. Start with Story 1.1 implementation (data models, DbContext, migration, schedule generation).  
2. Proceed sequentially through stories as listed in priority order.  
3. Engage QA after each story for gate review.

---

## References

- PRD: `docs/domains/collections-recovery/prd/`
- Architecture: `docs/domains/collections-recovery/architecture/`
- Stories: `docs/domains/collections-recovery/stories/`

---

## Handoff Notes

- All six stories are approved and ready.  
- Audit, security, and observability requirements are embedded in stories.  
- Use existing IntelliFin conventions for headers, tracing, and error handling.


