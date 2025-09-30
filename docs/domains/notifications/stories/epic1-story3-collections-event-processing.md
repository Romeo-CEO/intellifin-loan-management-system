# Story 1.3: Collections Event Processing

## Status
Draft

## Story
**As a** collections officer
**I want** automated overdue payment notifications
**so that** I can focus on complex cases rather than routine reminders

## Acceptance Criteria
1. ✅ PaymentOverdueConsumer triggers at configurable DPD thresholds
2. ✅ Escalating message tone based on DPD severity
3. ✅ Integration with collections workflow processes
4. ✅ Automated provisioning trigger notifications to finance team
5. ✅ PaymentReceivedConsumer acknowledges successful payments
6. ✅ PMEC payroll deduction failure notifications for government employees
7. ✅ Collections officer workload balancing through intelligent assignment

## Tasks / Subtasks

- [ ] Task 1: Implement PaymentOverdueConsumer (AC: 1, 2, 3)
  - [ ] Create PaymentOverdueConsumer inheriting from BaseNotificationConsumer
  - [ ] Implement configurable DPD threshold triggers (1, 7, 30, 60, 90 days)
  - [ ] Add escalating message tone logic based on severity
  - [ ] Integrate with collections workflow state management
  - [ ] Add collections officer assignment and notification routing
  - [ ] Implement customer payment reminder SMS with escalating urgency

- [ ] Task 2: Implement PaymentReceivedConsumer (AC: 5)
  - [ ] Create PaymentReceivedConsumer inheriting from BaseNotificationConsumer
  - [ ] Implement customer payment confirmation SMS
  - [ ] Add payment receipt details (amount, date, outstanding balance)
  - [ ] Create collections officer payment success notification
  - [ ] Implement automated collections case closure for full payments

- [ ] Task 3: Implement PMECPayrollDeductionFailedConsumer (AC: 6)
  - [ ] Create PMECPayrollDeductionFailedConsumer for government employee loans
  - [ ] Implement customer notification for failed PMEC deductions
  - [ ] Add alternative payment method instructions
  - [ ] Create collections officer alert for PMEC failures
  - [ ] Implement escalation to finance team for recurring failures

- [ ] Task 4: Implement AutomatedProvisioningConsumer (AC: 4)
  - [ ] Create AutomatedProvisioningConsumer for loan loss provisions
  - [ ] Implement finance team notifications for provisioning triggers
  - [ ] Add regulatory compliance notifications for BoZ reporting
  - [ ] Create audit trail for provisioning decisions
  - [ ] Implement provision calculation verification alerts

- [ ] Task 5: Event Model Definitions (AC: All)
  - [ ] Define PaymentOverdue event model with DPD and arrears data
  - [ ] Define PaymentReceived event model with transaction details
  - [ ] Define PMECPayrollDeductionFailed event model
  - [ ] Define AutomatedProvisioningTriggered event model
  - [ ] Add validation rules for collections event data integrity

- [ ] Task 6: DPD-Based Template System (AC: 2)
  - [ ] Create payment-overdue-gentle template (DPD 1)
  - [ ] Create payment-overdue-reminder template (DPD 2-7)
  - [ ] Create payment-overdue-urgent template (DPD 8-30)
  - [ ] Create payment-overdue-final-notice template (DPD 31-60)
  - [ ] Create payment-overdue-serious-delinquency template (DPD >60)
  - [ ] Add PMEC-specific templates for government employees

- [ ] Task 7: Collections Workflow Integration (AC: 3)
  - [ ] Implement collections case status updates
  - [ ] Add workflow state transitions based on DPD progression
  - [ ] Create collections action logging and audit trail
  - [ ] Implement collections officer workload tracking
  - [ ] Add automated case escalation rules

- [ ] Task 8: Business Logic Implementation (AC: 7)
  - [ ] Implement intelligent collections officer assignment algorithm
  - [ ] Add workload balancing based on case count and complexity
  - [ ] Create branch-based collections officer routing
  - [ ] Implement customer relationship preservation rules
  - [ ] Add special handling for VIP customers and large loans

- [ ] Task 9: Configuration Management (AC: 1)
  - [ ] Create DPD threshold configuration system
  - [ ] Implement collections strategy configuration per loan product
  - [ ] Add branch-specific collections policies
  - [ ] Create holiday and weekend scheduling for collections
  - [ ] Implement customer communication preference management

- [ ] Task 10: Consumer Registration and Configuration (AC: All)
  - [ ] Register collections consumers in MassTransit configuration
  - [ ] Configure consumer-specific retry policies for collections events
  - [ ] Add dead letter queue handling for failed collections processing
  - [ ] Implement consumer health monitoring and alerting
  - [ ] Configure collections event routing and filtering

- [ ] Task 11: Unit Testing (AC: All)
  - [ ] Write unit tests for PaymentOverdueConsumer with DPD scenarios
  - [ ] Write unit tests for PaymentReceivedConsumer
  - [ ] Write unit tests for PMECPayrollDeductionFailedConsumer
  - [ ] Write unit tests for AutomatedProvisioningConsumer
  - [ ] Write tests for DPD-based template selection logic
  - [ ] Write tests for collections workflow integration

- [ ] Task 12: Integration Testing (AC: All)
  - [ ] Write integration tests for end-to-end collections flow
  - [ ] Write integration tests for PMEC failure handling
  - [ ] Write integration tests for payment acknowledgment flow
  - [ ] Write integration tests for provisioning trigger flow
  - [ ] Write tests for collections officer assignment logic

## Dev Notes

### Previous Story Dependencies
**Story 1.1 Foundation**: This story builds on the BaseNotificationConsumer and event processing infrastructure from Story 1.1.
**Story 1.2 Extensions**: Leverages the personalization and template rendering capabilities established in Story 1.2.

### Data Models
**PaymentOverdue Event Model**: Core collections event for overdue payments
- Fields: EventId, LoanId, CustomerId, DaysPastDue, OverdueAmount, TotalArrears, DueDate, CustomerName, CustomerPhone, LastPaymentDate, BranchId, LoanType, CollectionsOfficerId
- Purpose: Trigger automated collections notifications with escalating urgency
[Reference: docs/domains/notifications/architecture/event-processing-architecture.md#paymentoverdue-consumer-new-implementation]

**PaymentReceived Event Model**: Payment confirmation for collections closure
- Fields: EventId, LoanId, CustomerId, PaymentAmount, PaymentDate, PaymentMethod, OutstandingBalance, ReferenceNumber, CustomerName, CustomerPhone, BranchId
- Purpose: Trigger payment confirmation and collections case updates

**PMECPayrollDeductionFailed Event Model**: Government employee payroll integration failures
- Fields: EventId, LoanId, CustomerId, FailureReason, PayrollCycle, ExpectedAmount, FailureDate, RetryCount, CustomerName, CustomerPhone, PMECReference
- Purpose: Handle PMEC system integration failures for government employees

**AutomatedProvisioningTriggered Event Model**: Loan loss provisioning notifications
- Fields: EventId, LoanId, CustomerId, ProvisionAmount, ProvisionCategory, TriggerReason, DaysPastDue, OutstandingBalance, BranchId, RequiresManagerApproval
- Purpose: Notify finance team of automated provisioning decisions

### API Specifications
**Collections Consumer Pattern**: Specialized business logic for collections processing
```csharp
public class PaymentOverdueConsumer : BaseNotificationConsumer<PaymentOverdue>
{
    private readonly ICollectionsService _collectionsService;
    private readonly IWorkloadBalancingService _workloadService;

    protected override async Task ProcessEventAsync(PaymentOverdue eventData, ConsumeContext<PaymentOverdue> context)
    {
        // DPD-based template selection
        // Customer notification with escalating tone
        // Collections officer assignment and notification
        // Workflow state update
    }
}
```

**DPD Template Selection Logic**: Escalating message tone implementation
```csharp
private string GetOverdueTemplateName(int daysPastDue)
{
    return daysPastDue switch
    {
        1 => "payment-overdue-gentle",
        <= 7 => "payment-overdue-reminder",
        <= 30 => "payment-overdue-urgent",
        <= 60 => "payment-overdue-final-notice",
        _ => "payment-overdue-serious-delinquency"
    };
}
```

### Component Specifications
**PaymentOverdueConsumer**: Automated collections notification processing
- Customer SMS: DPD-based escalating tone with payment instructions
- Collections Officer In-App: Case assignment with customer context
- Branch Manager In-App: High-risk account alerts (DPD >30, Amount >K50,000)
- Dependencies: ICollectionsService, IWorkloadBalancingService, ICustomerRiskService

**PaymentReceivedConsumer**: Payment acknowledgment and collections closure
- Customer SMS: Payment confirmation with outstanding balance
- Collections Officer In-App: Case update with payment details
- Collections Manager In-App: Performance metrics update
- Dependencies: IPaymentService, ICollectionsWorkflowService

**PMECPayrollDeductionFailedConsumer**: Government employee special handling
- Customer SMS: PMEC failure notification with alternative payment methods
- Collections Officer In-App: PMEC-specific case handling instructions
- Finance Team In-App: PMEC integration failure tracking
- Dependencies: IPMECIntegrationService, IAlternativePaymentService

**AutomatedProvisioningConsumer**: Regulatory and finance team notifications
- Finance Team In-App: Provisioning calculation and approval workflow
- Compliance Officer In-App: Regulatory reporting requirements
- Branch Manager In-App: Portfolio risk management alerts
- Dependencies: IProvisioningCalculationService, IComplianceService

### File Locations
- **New Consumers**: apps/IntelliFin.Communications/Consumers/PaymentOverdueConsumer.cs
- **New Consumers**: apps/IntelliFin.Communications/Consumers/PaymentReceivedConsumer.cs
- **New Consumers**: apps/IntelliFin.Communications/Consumers/PMECPayrollDeductionFailedConsumer.cs
- **New Consumers**: apps/IntelliFin.Communications/Consumers/AutomatedProvisioningConsumer.cs
- **Event Models**: libs/IntelliFin.Shared.Infrastructure/Messaging/Contracts/CollectionsEvents.cs
- **Templates**: apps/IntelliFin.Communications/Templates/Collections/
- **Business Logic**: apps/IntelliFin.Communications/Services/CollectionsWorkflowService.cs
- **Configuration**: apps/IntelliFin.Communications/Configuration/CollectionsConfiguration.cs
- **Unit Tests**: tests/IntelliFin.Communications.Tests/Consumers/Collections/
- **Integration Tests**: tests/IntelliFin.Communications.Tests/Integration/CollectionsEventProcessingTests.cs

### Business Rules
**DPD Threshold Configuration**: Configurable collections escalation
- DPD 1: Gentle reminder with payment due date
- DPD 2-7: Standard reminder with grace period message
- DPD 8-30: Urgent notice with payment plan options
- DPD 31-60: Final notice with legal action warning
- DPD >60: Serious delinquency with account acceleration notice

**Collections Officer Assignment**: Workload balancing algorithm
- Active case count balancing across available officers
- Customer relationship preservation (same officer when possible)
- Branch-based assignment for local customer management
- Specialization-based assignment (PMEC loans, high-value accounts)
- Holiday and absence coverage automatic reassignment

**PMEC Integration Failure Handling**: Government employee special processing
- First Failure: Customer notification with alternative payment instructions
- Second Failure: Collections officer manual intervention required
- Third Failure: Finance team escalation for PMEC system investigation
- Automatic retry scheduling based on PMEC payroll cycle timing

**Automated Provisioning Triggers**: Regulatory compliance automation
- DPD 90: 25% provision calculation and finance team notification
- DPD 180: 50% provision calculation with manager approval
- DPD 365: 100% provision with write-off consideration
- High-value loans (>K500,000): Manual review required for all provisions

### Technical Constraints
- **Event Processing Latency**: ≤5 seconds for collections notifications
- **DPD Calculation Accuracy**: Real-time calculation based on business day calendar
- **Collections Workflow Integration**: Zero-downtime integration with existing workflow
- **PMEC Integration Resilience**: Handle PMEC system downtime gracefully
- **Notification Timing**: Respect customer communication preferences and quiet hours
- **Audit Trail Completeness**: Full logging for regulatory compliance
[Source: docs/domains/notifications/prd/epic-1-business-event-processing.md#success-metrics]

### Template Specifications
**payment-overdue-gentle Template**: DPD 1 customer notification
```
Dear {{CustomerName}}, your loan payment of K{{OverdueAmount}} was due on {{DueDate}}. Please make payment to avoid late fees. IntelliFin MicroFinance.
```

**payment-overdue-urgent Template**: DPD 8-30 customer notification
```
URGENT: Dear {{CustomerName}}, your loan payment is {{DaysPastDue}} days overdue. Amount: K{{OverdueAmount}}. Please contact us immediately to arrange payment. IntelliFin MicroFinance.
```

**pmec-deduction-failed Template**: Government employee PMEC failure
```
Dear {{CustomerName}}, your PMEC payroll deduction failed for {{PayrollCycle}}. Please make direct payment of K{{ExpectedAmount}} or contact us for assistance. IntelliFin MicroFinance.
```

**payment-received-confirmation Template**: Payment acknowledgment
```
Dear {{CustomerName}}, we confirm receipt of K{{PaymentAmount}} for your loan. Outstanding balance: K{{OutstandingBalance}}. Thank you for your payment. IntelliFin MicroFinance.
```

### Collections Workflow Integration
**Workflow State Management**: Integration with existing collections processes
- Case Creation: Automatic case creation for DPD 7+ accounts
- Case Assignment: Intelligent routing to available collections officers
- Case Updates: Real-time status updates based on payment events
- Case Closure: Automatic closure for full payment or restructuring
- Escalation Rules: Automated escalation based on DPD progression

**Performance Metrics Tracking**: Collections efficiency monitoring
- Collections Officer Performance: Response time, resolution rate, recovery amount
- Portfolio Health: DPD distribution, provision requirements, recovery trends
- Customer Satisfaction: Communication effectiveness, complaint tracking
- System Efficiency: Event processing speed, notification delivery rate

### Testing Requirements
**Unit Testing Standards**: Comprehensive collections logic testing
- DPD threshold boundary testing (1, 7, 30, 60, 90+ days)
- Template selection logic validation
- Collections officer assignment algorithm testing
- PMEC failure handling scenario testing
- Provisioning calculation verification

**Integration Testing**: End-to-end collections flow validation
- Collections workflow state management integration
- PMEC integration failure simulation and recovery
- Payment processing integration with acknowledgment flow
- Multi-branch collections officer assignment testing
- Real-time event processing with database consistency

**Performance Testing**: Collections processing load validation
- High-volume overdue payment event processing
- Concurrent collections officer assignment handling
- DPD calculation performance under load
- Template rendering performance with large datasets

### Testing
**Test File Location**: tests/IntelliFin.Communications.Tests/Consumers/Collections/
**Testing Frameworks**: xUnit for unit tests, TestContainers for integration tests, NBomber for performance tests
**Testing Patterns**: Collections workflow testing, event-driven architecture testing, PMEC integration testing
**Specific Testing Requirements**:
- DPD progression scenario testing
- Collections officer workload balancing validation
- PMEC failure recovery mechanism testing
- Template escalation logic verification
- Regulatory compliance audit trail testing

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