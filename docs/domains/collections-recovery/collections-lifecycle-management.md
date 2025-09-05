# Collections Lifecycle Management - Limelight Moneylink Services

## Document Information
- **Document Type**: Business Process Specification
- **Version**: 1.0
- **Last Updated**: [Date]
- **Owner**: Collections & Recovery Team
- **Compliance**: BoZ Requirements, Money Lenders Act, Consumer Protection Standards
- **Reference**: Transaction Processing Rules, Credit Assessment Process, Customer Communication Management

## Executive Summary

This document defines the system's end-to-end process for managing arrears, from automated reminders to final write-off, ensuring compliance with BoZ directives while maintaining a customer-centric approach. The collections lifecycle is orchestrated by the system with automated triggers, classifications, and provisioning, while human intervention focuses on relationship management and recovery strategies.

## Business Context

### Why Collections Lifecycle Management is Critical
- **Portfolio Performance**: Maintaining healthy loan portfolio and minimizing losses
- **Regulatory Compliance**: Adhering to BoZ directives for loan classification and provisioning
- **Cash Flow Management**: Ensuring consistent cash flow for business operations
- **Customer Relationships**: Balancing collections with customer retention and satisfaction
- **Risk Management**: Early identification and management of credit risks

### Collections Philosophy
- **Early Intervention**: Automated system alerts and communications
- **BoZ Compliance**: All classifications and actions are driven by BoZ directives
- **Automation First**: Automate reminders, classifications, and provisioning
- **Customer-Centric Communication**: Maintain a professional and respectful tone
- **Data-Driven Decisions**: All actions based on comprehensive data analysis

## Guiding Principles

### 1. Early Intervention
- **Proactive Approach**: Automated reminders before payments become due
- **System-Driven Alerts**: Real-time monitoring and immediate response
- **Customer Education**: Clear communication about payment obligations
- **Flexible Solutions**: Work with customers to find mutually beneficial arrangements

### 2. BoZ Compliance
- **Automatic Classification**: System-driven loan classification based on DPD
- **Mandatory Provisioning**: Automated loan loss provisioning per BoZ Second Schedule
- **Non-Accrual Management**: Automatic interest reversal for non-accrual loans
- **Regulatory Reporting**: Complete audit trail for all classification changes

### 3. Automation First
- **System Orchestration**: Camunda-driven workflow for all collections activities
- **Automated Communications**: Scheduled SMS and email reminders
- **Real-Time Calculations**: Daily DPD updates and classification changes
- **Automated GL Posting**: System-generated journal entries for provisioning

### 4. Customer-Centric Communication
- **Professional Tone**: Respectful and understanding communication
- **Clear Messaging**: Transparent information about obligations and options
- **Solution-Focused**: Working with customers to resolve payment issues
- **Relationship Preservation**: Maintaining long-term customer relationships

## The Automated Collections Lifecycle

### Stage 0: Pre-Delinquency (Proactive Reminders)

**Trigger**: Loan due date is approaching (e.g., T-3 days)

**System Actions**:
```
Automated Communications:
- Communications Service sends a templated, friendly SMS reminder
- Email notification (if customer has email on file)
- Reminder includes payment amount, due date, and payment methods

System Monitoring:
- Daily monitoring of approaching due dates
- Automatic reminder scheduling based on customer preferences
- Integration with payment systems for real-time status updates
```

**Customer Experience**:
- Friendly, proactive communication
- Clear payment instructions
- Multiple payment channel options
- Professional and helpful tone

### Stage 1: Early Delinquency (1-59 Days Past Due - DPD)

**System Actions (Daily)**:
```
DPD Calculation:
- The system automatically calculates DPD for all loans
- Updates loan status and classification in real-time
- Triggers appropriate communication sequences

Automated Communications:
- Pre-defined sequence of increasingly urgent SMS and email reminders
- Specific DPD milestones: 1 DPD, 7 DPD, 30 DPD
- Escalating tone while maintaining professionalism
- Clear consequences and available assistance options
```

**Human Tasks**:
```
Collections Workbench:
- System creates "Soft Collection" task for collections officer
- Task includes customer profile, payment history, and risk assessment
- Officer makes follow-up phone call within 24 hours
- Focus on understanding customer circumstances and finding solutions
```

**Customer Relationship Management**:
- **Professional Communication**: Respectful and understanding approach
- **Solution-Oriented**: Working with customer to resolve payment issues
- **Flexible Arrangements**: Offering payment plans and temporary relief
- **Education and Support**: Providing financial counseling and guidance

### Stage 2: BoZ Classification & Provisioning (60+ DPD)

**Trigger**: Automated nightly job

**System Actions (BoZ Directive 15)**:
```
Automatic Loan Classification:
- Special Mention: 60-89 DPD
- Substandard: 90-179 DPD
- Doubtful: 180-364 DPD
- Loss: 365+ DPD

Classification Updates:
- Real-time status changes in loan management system
- Automatic notification to relevant stakeholders
- Updated risk assessment and monitoring requirements
```

**System Actions (BoZ Directive 9 & 10)**:
```
Non-Accrual Status Management:
- When loan classified as Substandard (90+ DPD)
- System automatically sets AccrualStatus to 'Non-Accrual'
- General Ledger Service reverses any unpaid accrued interest
- Posting: DR 4100X - Interest Income, CR 12011 - Interest Receivable - Past Due
```

**System Actions (BoZ Second Schedule)**:
```
Automated Loan Loss Provisioning:
- General Ledger Service calculates required minimum provision
- Provisioning rates: 20% Substandard, 50% Doubtful, 100% Loss
- Automatic journal entry posting
- Posting: DR 55000 - Provision for Credit Losses, CR 1300X - Allowance for Credit Losses
- Regulatory Loan Loss Reserve update: DR 55000, CR 33002 - Regulatory Loan Loss Reserve
```

**Enhanced Monitoring**:
- Increased frequency of customer contact
- Enhanced risk assessment and monitoring
- Regular review of customer circumstances
- Documentation of all collection efforts

### Stage 3: Active Recovery & Legal (91+ DPD)

**Collections Workbench Priority**:
```
High-Priority Files:
- Collections Workbench presents these files with higher priority
- Enhanced customer profile with complete payment history
- Risk assessment and recovery potential analysis
- Legal action history and costs tracking
```

**Collections Officer Responsibilities**:
```
Recovery Strategies:
- Negotiate repayment plans (system provides modeling tools)
- Initiate settlement discussions with appropriate authority
- Escalate files for legal action when necessary
- Document all recovery efforts and customer communications

System Tools:
- Payment plan modeling and approval workflow
- Settlement calculation and approval system
- Legal action tracking and cost management
- Recovery progress monitoring and reporting
```

**Legal Action Management**:
```
Legal Action Module:
- Track legal proceedings and associated costs
- Post legal costs to General Ledger
- Monitor recovery progress and outcomes
- Document all legal actions and results

Cost Management:
- Legal fees and court costs tracking
- Recovery cost analysis and reporting
- Cost-benefit analysis for continued legal action
- Settlement vs. legal action decision support
```

**Customer Relationship Management**:
- **Professional Approach**: Maintaining dignity and respect
- **Transparent Communication**: Clear explanation of consequences
- **Solution-Focused**: Working toward mutually acceptable resolutions
- **Documentation**: Complete record of all interactions and agreements

### Stage 4: Write-Off

**Trigger**: Loan deemed uncollectible after all recovery efforts have failed

**Human Task**:
```
Write-Off Authorization:
- Authorized manager (Head of Credit) initiates write-off workflow
- Complete documentation of all recovery efforts
- Cost-benefit analysis of continued collection efforts
- Regulatory compliance verification
```

**System Actions**:
```
Dual-Control Approval:
- Requires approval from senior finance manager
- System enforces dual-control workflow
- Complete audit trail of approval process
- Regulatory notification and reporting

General Ledger Posting:
- Upon final approval, GL Service posts final write-off transaction
- Debit: Allowance for Credit Losses
- Credit: Loans Receivable
- Loan status changed to "Written-Off"
- Complete audit trail maintained
```

**Post-Write-Off Management**:
- **Asset Recovery**: Continued efforts to recover any available assets
- **Legal Closure**: Finalization of all legal proceedings
- **Regulatory Reporting**: Compliance with BoZ reporting requirements
- **Customer Relationship**: Professional closure of relationship

## System Integration Points

### 1. Camunda Workflow Orchestration
**Collections Workflow Engine**:
```
Automated Triggers:
- DPD calculation and classification updates
- Communication sequence management
- Task creation and assignment
- Escalation and approval workflows

Workflow States:
- Current (0 DPD)
- Early Delinquency (1-59 DPD)
- BoZ Classified (60+ DPD)
- Active Recovery (91+ DPD)
- Legal Action (121+ DPD)
- Write-Off (365+ DPD)
```

### 2. Communications Service Integration
**Automated Communication Management**:
```
SMS Reminder System:
- Scheduled job for payment reminders
- Configurable timing and content
- Customer preference management
- Delivery status tracking

Email Communications:
- Automated email sequences
- Professional templates
- Customer preference management
- Delivery and engagement tracking
```

### 3. General Ledger Service Integration
**Automated Accounting**:
```
Provisioning Automation:
- Daily calculation of required provisions
- Automatic journal entry posting
- Regulatory reserve updates
- Audit trail maintenance

Interest Reversal:
- Automatic non-accrual status updates
- Interest reversal posting
- Income statement adjustments
- Complete audit trail
```

### 4. Collections Workbench
**Human Interface**:
```
Task Management:
- Automated task creation and assignment
- Priority-based task queuing
- Customer profile integration
- Communication history tracking

Recovery Tools:
- Payment plan modeling
- Settlement calculation tools
- Legal action tracking
- Recovery progress monitoring
```

## Performance Controls

### Timeline Management
**Processing Targets**:
```
Communication Response:
- Pre-delinquency reminders: T-3 days
- Early delinquency contact: Within 24 hours of 1 DPD
- BoZ classification: Automated nightly
- Active recovery initiation: Within 48 hours of 90 DPD
- Legal action initiation: Within 7 days of 120 DPD
```

**Performance Metrics**:
```
Key Indicators:
- DPD calculation accuracy: 100%
- Communication delivery rate: >95%
- Customer contact success rate: >80%
- Recovery rate by DPD bucket
- Legal action success rate
- Write-off accuracy and timing
```

## Compliance Requirements

### Regulatory Compliance
**BoZ Requirements**:
```
Classification Standards:
- Automatic DPD-based classification
- Mandatory provisioning per Second Schedule
- Non-accrual status management
- Regulatory reporting compliance

Audit Requirements:
- Complete audit trail for all actions
- Documentation of all customer communications
- Legal action tracking and reporting
- Write-off authorization and documentation
```

**Consumer Protection**:
```
Fair Collections Practices:
- Professional and respectful communication
- Clear explanation of rights and obligations
- Reasonable payment arrangements
- Privacy and data protection compliance
```

## Quality Assurance

### Process Monitoring
**Quality Metrics**:
```
Performance Indicators:
- Collections efficiency by DPD bucket
- Customer satisfaction scores
- Recovery rate trends
- Legal action success rates
- Write-off accuracy
- Regulatory compliance rate
```

**Quality Controls**:
```
Verification Steps:
- Random review of collections activities
- Customer communication quality assessment
- Recovery strategy effectiveness review
- Legal action cost-benefit analysis
- Write-off decision validation
```

## Next Steps

This Collections Lifecycle Management document serves as the foundation for:
1. **System Development** - Collections workflow and automation implementation
2. **Process Implementation** - Collections procedures and customer relationship management
3. **Staff Training** - Collections officer training and customer service standards
4. **Quality Assurance** - Collections performance monitoring and improvement
5. **Compliance Management** - Regulatory compliance and audit requirements

## Document Approval

- **Collections Manager**: [Name] - [Date]
- **Risk Management Officer**: [Name] - [Date]
- **Compliance Officer**: [Name] - [Date]
- **Technical Lead**: [Name] - [Date]
- **Head of Credit**: [Name] - [Date]

---

**Document Control**: This document must be reviewed and updated quarterly or when business processes change.
