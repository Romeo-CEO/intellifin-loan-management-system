# Epic 1: Collections & Recovery Module Implementation

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
