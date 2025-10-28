# Data Models and Schema Changes

Define new data models and how they integrate with existing schema:

1. Identify new entities required for the enhancement
2. Define relationships with existing data models
3. Plan database schema changes (additions, modifications)
4. Ensure backward compatibility

### New Data Models

#### RepaymentSchedule

**Purpose:** To store the comprehensive schedule of expected loan repayments, including all installments.
**Integration:** Linked to existing Loan entity (via LoanId).
**Key Attributes:**
-   `ScheduleId`: GUID - Primary key, unique identifier for the repayment schedule.
-   `LoanId`: GUID - Foreign key to the existing Loan entity.
-   `GeneratedDate`: DateTime - When the schedule was created.
-   `Status`: String (e.g., 'Active', 'Restructured', 'Completed') - Current status of the schedule.
-   `Currency`: String - Currency of the loan.

**Relationships:**
-   **With Existing:** One-to-one or one-to-many with the existing Loan entity.
-   **With New:** One-to-many with `Installment` (RepaymentSchedule has many Installments).

#### Installment

**Purpose:** To store details of each individual scheduled payment within a a`RepaymentSchedule`.
**Integration:** Linked to `RepaymentSchedule` (via ScheduleId) and Loan (via LoanId).
**Key Attributes:**
-   `InstallmentId`: GUID - Primary key, unique identifier for an installment.
-   `ScheduleId`: GUID - Foreign key to `RepaymentSchedule`.
-   `LoanId`: GUID - Foreign key to the existing Loan entity.
-   `DueDate`: DateTime - The date the installment is due.
-   `ExpectedPrincipal`: Decimal - Expected principal amount for this installment.
-   `ExpectedInterest`: Decimal - Expected interest amount for this installment.
-   `ExpectedTotal`: Decimal - Sum of ExpectedPrincipal and ExpectedInterest.
-   `PaidAmount`: Decimal - Actual amount paid for this installment.
-   `Status`: String (e.g., 'Pending', 'Paid', 'Partially Paid', 'Overdue', 'WrittenOff').
-   `DaysPastDue`: Int - Calculated days past due for this installment.
-   `Version`: Int - Optimistic concurrency versioning.

**Relationships:**
-   **With Existing:** One-to-one or one-to-many with the existing Loan entity.
-   **With New:** Many-to-one with `RepaymentSchedule` (many Installments belong to one RepaymentSchedule).

#### PaymentTransaction

**Purpose:** To record all incoming payment transactions and link them to `Installments`.
**Integration:** Linked to `Installment` (via InstallmentId) and Loan (via LoanId).
**Key Attributes:**
-   `TransactionId`: GUID - Primary key, unique identifier for a payment transaction.
-   `LoanId`: GUID - Foreign key to the existing Loan entity.
-   `InstallmentId`: GUID - Foreign key to `Installment` (nullable for overpayments).
-   `Amount`: Decimal - The amount of the payment.
-   `PaymentDate`: DateTime - Date and time the payment was received.
-   `Source`: String (e.g., 'Manual', 'PMEC', 'Treasury') - Origin of the payment.
-   `Reference`: String - External reference number (e.g., PMEC transaction ID).
-   `Status`: String (e.g., 'Completed', 'Failed', 'Reversed').
-   `IsReconciled`: Boolean - Indicates if the payment has been reconciled.

**Relationships:**
-   **With Existing:** One-to-many with the existing Loan entity.
-   **With New:** Many-to-one or one-to-one with `Installment`.

#### ArrearsClassificationHistory

**Purpose:** To store historical records of a loan's BoZ arrears classification and provisioning.
**Integration:** Linked to Loan (via LoanId).
**Key Attributes:**
-   `HistoryId`: GUID - Primary key.
-   `LoanId`: GUID - Foreign key to the existing Loan entity.
-   `ClassificationDate`: DateTime - Date of this classification.
-   `DPD`: Int - Days Past Due at the time of classification.
-   `BoZCategory`: String (e.g., 'Performing', 'Watch', 'Substandard', 'Doubtful', 'Loss').
-   `ProvisionRate`: Decimal - Applicable BoZ provisioning rate.
-   `ProvisionAmount`: Decimal - Calculated provision amount.
-   `NonAccrualStatus`: Boolean - True if loan is in non-accrual status.
-   `VaultConfigVersion`: String - Version/hash of Vault config used for classification.

**Relationships:**
-   **With Existing:** One-to-many with the existing Loan entity.
-   **With New:** None directly.

#### ReconciliationTask

**Purpose:** To manage manual reconciliation tasks generated for overpayments or misapplied transactions.
**Integration:** Linked to `PaymentTransaction` (via TransactionId) and potentially Loan.
**Key Attributes:**
-   `TaskId`: GUID - Primary key.
-   `TransactionId`: GUID - Foreign key to `PaymentTransaction`.
-   `LoanId`: GUID - Foreign key to the existing Loan entity (if applicable).
-   `AssignedTo`: String - User ID of the Collections Officer assigned.
-   `Status`: String (e.g., 'Open', 'InProgress', 'Completed', 'Escalated').
-   `CreatedDate`: DateTime.
-   `ResolutionDate`: DateTime (nullable).
-   `Notes`: String - Details of the issue and resolution.
-   `CamundaProcessInstanceId`: String - Reference to Camunda process instance.

**Relationships:**
-   **With Existing:** Potentially with existing User/Collections Officer entities (via AssignedTo).
-   **With New:** Many-to-one with `PaymentTransaction`.

#### WriteOffRequest

**Purpose:** To manage the workflow and audit trail for loan write-offs.
**Integration:** Linked to Loan (via LoanId).
**Key Attributes:**
-   `RequestId`: GUID - Primary key.
-   `LoanId`: GUID - Foreign key to the existing Loan entity.
-   `InitiatedBy`: String - User ID of the Collections Officer initiating.
-   `RequestedDate`: DateTime.
-   `ApprovedByCredit`: String - User ID of Head of Credit approval (nullable).
-   `ApprovedByFinance`: String - User ID of Senior Finance Manager approval (nullable).
-   `ApprovalDateCredit`: DateTime (nullable).
-   `ApprovalDateFinance`: DateTime (nullable).
-   `Status`: String (e.g., 'PendingApprovalCredit', 'PendingApprovalFinance', 'Approved', 'Rejected').
-   `Reason`: String - Reason for write-off.
-   `WriteOffAmount`: Decimal.
-   `CamundaProcessInstanceId`: String - Reference to Camunda process instance.

**Relationships:**
-   **With Existing:** One-to-one with existing Loan entity (a loan can have one active write-off request).
-   **With New:** None directly.

### Schema Integration Strategy

**Database Changes Required:**
-   **New Tables:**
    -   `RepaymentSchedules` (for `RepaymentSchedule` entity)
    -   `Installments` (for `Installment` entity)
    -   `PaymentTransactions` (for `PaymentTransaction` entity)
    -   `ArrearsClassificationHistory` (for `ArrearsClassificationHistory` entity)
    -   `ReconciliationTasks` (for `ReconciliationTask` entity)
    -   `WriteOffRequests` (for `WriteOffRequest` entity)
-   **Modified Tables:** None (new service owns its data)
-   **New Indexes:** Appropriate indexes on foreign keys (e.g., `LoanId`, `ScheduleId`, `InstallmentId`, `TransactionId`) and frequently queried columns (e.g., `DueDate`, `ClassificationDate`, `Status`).
-   **Migration Strategy:** Entity Framework Core migrations will be used to manage schema changes in the CollectionsService's dedicated database. Migrations will be versioned and applied as part of the deployment pipeline.

**Seed Data / Initial Data Setup:**
A strategy for seed data and initial data setup will be implemented. This includes:
-   **Reference Data:** Seeding of initial reference data (e.g., default BoZ classification rules, initial provisioning rates) required for service operation.
-   **Development/Testing Data:** Automated generation of realistic but anonymized data for development and testing environments to ensure comprehensive testing scenarios. This will be integrated into the test project setup.

**Backward Compatibility:**
-   **Additive Changes Only**: All schema changes will be strictly additive, introducing new tables and columns without modifying or deleting existing schema that might be accessed by other services (though direct DB access from other services is discouraged).
-   **Event-Driven Data Sharing**: Primary method of sharing data with other services will be through well-defined events, ensuring loose coupling and avoiding direct schema dependencies.
-   **No Impact on Existing Services**: The new database for CollectionsService will not directly impact the schema or data of existing IntelliFin services.
