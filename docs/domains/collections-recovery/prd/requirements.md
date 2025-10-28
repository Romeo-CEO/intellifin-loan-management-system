# Requirements

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
