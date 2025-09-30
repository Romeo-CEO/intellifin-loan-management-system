# Story 1.2: Loan Status Change Notifications

## Status
Draft

## Story
**As a** customer
**I want** SMS notifications about loan status changes
**so that** I stay informed about application progress

## Acceptance Criteria
1. ✅ LoanApprovedConsumer sends approval notification with next steps
2. ✅ LoanDeclinedConsumer sends decline notification with reason (if allowed)
3. ✅ LoanDisbursedConsumer sends disbursement confirmation with details
4. ✅ All notifications personalized with customer-specific information
5. ✅ Loan officers receive in-app notifications for status changes they initiated
6. ✅ Branch managers receive notifications for high-value loan decisions

## Tasks / Subtasks

- [ ] Task 1: Implement LoanApprovedConsumer (AC: 1, 4, 5)
  - [ ] Create LoanApprovedConsumer inheriting from BaseNotificationConsumer
  - [ ] Implement customer SMS notification with approval details
  - [ ] Add loan officer in-app notification for approved loans
  - [ ] Include next steps template with disbursement timeline
  - [ ] Add personalization context for approval amount and terms

- [ ] Task 2: Implement LoanDeclinedConsumer (AC: 2, 4, 5)
  - [ ] Create LoanDeclinedConsumer inheriting from BaseNotificationConsumer
  - [ ] Implement customer SMS notification with appropriate decline messaging
  - [ ] Add conditional reason inclusion based on business rules
  - [ ] Create loan officer in-app notification for declined loans
  - [ ] Implement sensitive data filtering for decline reasons

- [ ] Task 3: Implement LoanDisbursedConsumer (AC: 3, 4, 6)
  - [ ] Create LoanDisbursedConsumer inheriting from BaseNotificationConsumer
  - [ ] Implement customer SMS notification with disbursement confirmation
  - [ ] Add disbursement details (amount, account, reference number)
  - [ ] Create loan officer success notification
  - [ ] Add finance team notification for disbursement record keeping

- [ ] Task 4: Event Model Definitions (AC: All)
  - [ ] Define LoanApproved event model with approval details
  - [ ] Define LoanDeclined event model with decline information
  - [ ] Define LoanDisbursed event model with disbursement data
  - [ ] Add validation rules for event data integrity
  - [ ] Document event publishing requirements for source services

- [ ] Task 5: Template Development (AC: 4)
  - [ ] Create loan-approved-sms template with personalization
  - [ ] Create loan-declined-sms template with sensitive data handling
  - [ ] Create loan-disbursed-sms template with transaction details
  - [ ] Create loan-approved-officer in-app template
  - [ ] Create loan-declined-officer in-app template
  - [ ] Create loan-disbursed-finance in-app template

- [ ] Task 6: Business Logic Implementation (AC: 6)
  - [ ] Implement high-value loan notification logic (>K100,000)
  - [ ] Add branch manager notification routing
  - [ ] Implement loan type-specific notification rules
  - [ ] Add PMEC loan special handling for government employees
  - [ ] Create notification priority rules based on loan amount

- [ ] Task 7: Consumer Registration and Configuration (AC: All)
  - [ ] Register new consumers in MassTransit configuration
  - [ ] Configure consumer-specific retry policies
  - [ ] Add consumer endpoint configurations
  - [ ] Implement consumer-specific error handling
  - [ ] Configure dead letter queue routing for each consumer

- [ ] Task 8: Unit Testing (AC: All)
  - [ ] Write unit tests for LoanApprovedConsumer
  - [ ] Write unit tests for LoanDeclinedConsumer
  - [ ] Write unit tests for LoanDisbursedConsumer
  - [ ] Write tests for event model validation
  - [ ] Write tests for template rendering with personalization
  - [ ] Write tests for business logic routing rules

- [ ] Task 9: Integration Testing (AC: All)
  - [ ] Write integration tests for end-to-end loan approval flow
  - [ ] Write integration tests for end-to-end loan decline flow
  - [ ] Write integration tests for end-to-end loan disbursement flow
  - [ ] Write tests for MassTransit consumer registration
  - [ ] Write tests for database audit trail logging

## Dev Notes

### Previous Story Dependencies
**Story 1.1 Foundation**: This story builds directly on the BaseNotificationConsumer, NotificationRepository, and event processing infrastructure established in Story 1.1.

### Data Models
**LoanApproved Event Model**: New business event for loan approval notifications
- Fields: EventId, LoanId, CustomerId, ApprovedAmount, ApprovedBy, ApprovalDate, CustomerName, CustomerPhone, CustomerEmail, BranchId, LoanTerms, NextSteps
- Purpose: Trigger approval notifications with complete loan details
[Reference: docs/domains/notifications/architecture/event-processing-architecture.md#loan-related-events]

**LoanDeclined Event Model**: New business event for loan decline notifications
- Fields: EventId, LoanId, CustomerId, DeclinedBy, DeclineDate, CustomerName, CustomerPhone, CustomerEmail, BranchId, DeclineCategory, PublicReason
- Purpose: Trigger decline notifications with appropriate messaging
[Note: Sensitive decline reasons handled separately for privacy]

**LoanDisbursed Event Model**: New business event for loan disbursement notifications
- Fields: EventId, LoanId, CustomerId, DisbursedAmount, DisbursementDate, AccountNumber, ReferenceNumber, CustomerName, CustomerPhone, BranchId, DisbursementMethod
- Purpose: Trigger disbursement confirmation notifications

### API Specifications
**Consumer Implementation Pattern**: Following BaseNotificationConsumer abstract class
```csharp
public class LoanApprovedConsumer : BaseNotificationConsumer<LoanApproved>
{
    protected override async Task ProcessEventAsync(LoanApproved eventData, ConsumeContext<LoanApproved> context)
    {
        // Customer SMS notification
        // Loan officer in-app notification
        // Branch manager notification (if high-value)
    }
}
```
[Source: docs/domains/notifications/architecture/event-processing-architecture.md#loanaapproved-consumer-new-implementation]

**Template Categories**: Extending notification template categories
- "LoanOrigination" category for customer-facing loan status notifications
- "LoanOperations" category for internal staff notifications
- "LoanManagement" category for management-level notifications

### Component Specifications
**LoanApprovedConsumer**: Customer and staff notifications for loan approvals
- Customer SMS: Approval confirmation with next steps
- Loan Officer In-App: Success notification with loan details
- Branch Manager In-App: High-value loan approval alert (>K100,000)
- Dependencies: ILoanService for loan details, IStaffService for officer lookup

**LoanDeclinedConsumer**: Customer and staff notifications for loan declines
- Customer SMS: Respectful decline notification
- Loan Officer In-App: Decline confirmation with internal details
- Privacy: Sensitive decline reasons only in staff notifications
- Dependencies: IDeclineReasonService for appropriate customer messaging

**LoanDisbursedConsumer**: Customer and operational notifications for disbursements
- Customer SMS: Disbursement confirmation with transaction details
- Finance Team In-App: Disbursement record for reconciliation
- Loan Officer In-App: Successful completion notification
- Dependencies: IDisbursementService for transaction details

### File Locations
- **New Consumers**: apps/IntelliFin.Communications/Consumers/LoanApprovedConsumer.cs
- **New Consumers**: apps/IntelliFin.Communications/Consumers/LoanDeclinedConsumer.cs
- **New Consumers**: apps/IntelliFin.Communications/Consumers/LoanDisbursedConsumer.cs
- **Event Models**: libs/IntelliFin.Shared.Infrastructure/Messaging/Contracts/LoanEvents.cs
- **Templates**: apps/IntelliFin.Communications/Templates/LoanOrigination/
- **Unit Tests**: tests/IntelliFin.Communications.Tests/Consumers/
- **Integration Tests**: tests/IntelliFin.Communications.Tests/Integration/LoanStatusNotificationTests.cs

### Business Rules
**High-Value Loan Thresholds**: Branch manager notifications
- Standard Loans: >K100,000 requires manager notification
- PMEC Government Loans: >K200,000 requires manager notification
- Business Loans: >K500,000 requires manager notification

**Decline Reason Privacy**: Customer protection guidelines
- Public Reasons: Credit policy, incomplete documentation, employment verification
- Private Reasons: Credit bureau findings, internal risk assessment, fraud indicators
- Default Message: "Application does not meet current lending criteria"

**Notification Timing**: Immediate processing requirements
- Loan Approved: Within 5 seconds of approval event
- Loan Declined: Within 5 seconds of decline event
- Loan Disbursed: Within 5 seconds of disbursement confirmation

### Technical Constraints
- **Event Processing Latency**: ≤5 seconds from business event to notification trigger
- **Success Rate**: ≥99% successful processing requirement
- **Personalization**: All customer notifications must include personalized data
- **Audit Trail**: Complete logging for all loan status change notifications
- **Idempotency**: Prevent duplicate notifications for the same status change
- **Privacy Compliance**: Sensitive decline information only in staff channels
[Source: docs/domains/notifications/prd/epic-1-business-event-processing.md#success-metrics]

### Template Specifications
**loan-approved-sms Template**: Customer approval notification
```
Dear {{CustomerName}}, your loan application {{ApplicationRef}} has been approved for K{{ApprovedAmount}}. {{NextSteps}}. IntelliFin MicroFinance.
```

**loan-declined-sms Template**: Customer decline notification
```
Dear {{CustomerName}}, we regret to inform you that your loan application {{ApplicationRef}} cannot be approved at this time. {{PublicReason}}. You may reapply after addressing the requirements. IntelliFin MicroFinance.
```

**loan-disbursed-sms Template**: Customer disbursement confirmation
```
Dear {{CustomerName}}, K{{DisbursedAmount}} has been disbursed to your account {{AccountNumber}}. Reference: {{ReferenceNumber}}. Thank you for choosing IntelliFin MicroFinance.
```

### Testing Requirements
**Unit Testing Standards**: xUnit framework with comprehensive consumer testing
- Mock all external dependencies (ILoanService, IStaffService, etc.)
- Test all notification routing scenarios
- Validate template rendering with personalization data
- Test business logic for high-value loan rules

**Integration Testing**:
- End-to-end event processing from publish to notification delivery
- Database audit trail verification
- MassTransit consumer registration and routing
- Template rendering engine integration
- Multi-channel notification delivery testing

**Test Coverage Requirements**:
- Minimum 80% code coverage for business logic
- 100% coverage for business rule implementations
- Complete scenario testing for all loan status combinations

### Testing
**Test File Location**: tests/IntelliFin.Communications.Tests/Consumers/
**Testing Frameworks**: xUnit for unit tests, TestContainers for integration tests
**Testing Patterns**: Consumer testing with MassTransit test harness, repository pattern testing
**Specific Testing Requirements**:
- Event processing with personalization context
- Business rule validation for high-value loans
- Privacy compliance for decline reason handling
- Multi-recipient notification scenarios
- Template rendering with complex data structures

## Change Log
| Date | Version | Description | Author |
|------|---------|-------------|---------|
| 2025-09-16 | 1.0 | Initial story creation for Epic 1 | SM Agent |

## Dev Agent Record
*This section will be populated by the development agent during implementation*

### Agent Model Used
*To be filled by dev agent*

### Debug Log References
*To be filled by dev agent*

### Completion Notes List
*To be filled by dev agent*

### File List
*To be filled by dev agent*

## QA Results
*Results from QA Agent review will be added here*