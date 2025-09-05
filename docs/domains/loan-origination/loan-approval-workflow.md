# Loan Approval Workflow - Limelight Moneylink Services

## Document Information
- **Document Type**: Business Process Specification
- **Version**: 2.0
- **Last Updated**: [Date]
- **Owner**: Loan Origination Team
- **Compliance**: BoZ Requirements, Money Lenders Act, Internal Control Standards
- **Reference**: Loan Product Catalog, Credit Assessment Process, PMEC Integration

## Executive Summary

This document defines the dynamic, system-enforced loan approval workflow for the Limelight Moneylink Services LMS system. The workflow is orchestrated by Camunda and automatically routes each application to the correct approver(s) based on a clear, non-bypassable set of business rules. This approach ensures proper risk management, regulatory compliance, and operational control while maintaining efficient loan processing and eliminating manual routing decisions.

## Business Context

### Why Approval Workflow is Critical
- **Risk Control**: Ensures proper oversight and approval of all lending decisions
- **Regulatory Compliance**: Meets BoZ requirements for prudent lending practices
- **Operational Control**: Maintains proper segregation of duties and authorization
- **Portfolio Quality**: Ensures consistent decision-making across all loan types
- **Audit Readiness**: Provides complete audit trail for all approval decisions
- **Speed**: Automated routing eliminates manual delays and ensures consistent processing

### Workflow Design Principles
- **System-Enforced**: Camunda orchestrates all routing decisions based on business rules
- **Rules-Based**: Clear, non-bypassable approval authority matrix
- **Risk-Based Approval**: Higher risk applications require higher authority
- **Segregation of Duties**: Clear separation between assessment and approval
- **Full Audit Trail**: Complete history of approval path for every loan
- **Elimination of Bypasses**: System prevents manual override of approval rules

## Approval Workflow Overview

### High-Level Process Flow
```
Application Submitted → Underwriter Review → Approval Routing (System) → Final Approval / Rejection → Loan Authorized
```

### Workflow Stages
1. **Decision Recommendation** (Credit Assessment Results)
2. **Automated Approval Routing** (Orchestrated by Camunda)
3. **Final Decision** (Approval, Rejection, or Conditional)

## Stage 1: Decision Recommendation

### 1.1 Credit Assessment Summary
**Assessment Results Compilation**:
```
Credit Assessment Package:
- Complete credit assessment report
- Risk classification and score
- Financial analysis summary
- Collateral evaluation (if applicable)
- External verification results
- Compliance check results
- Risk mitigation recommendations
```

**Underwriter Output**:
```
The output of the Credit Assessment process is a file containing:
- All application data and documents
- Results from PMEC and TransUnion integrations
- The system's Calculated Risk Grade (A, B, C, D, F)

The underwriter reviews this complete package and makes a formal recommendation within the system:
- Recommend Approval or Recommend Rejection
- Justification notes and risk assessment
- Any specific conditions or modifications
```

### 1.2 Risk Classification for Approval
**Risk-Based Approval Categories**:
```
Risk Categories:
- Low Risk (Grade A): Standard approval process
- Medium Risk (Grade B): Standard approval process
- Elevated Risk (Grade C): Enhanced review required
- High Risk (Grade D): Senior management review
- Very High Risk (Grade F): Executive approval required
```

## Stage 2: Automated Approval Routing (Orchestrated by Camunda)

### 2.1 System-Driven Routing Process
**Workflow Engine Actions**:
```
Once an underwriter submits their recommendation, the Camunda workflow engine automatically initiates the approval routing process.

The System's Job: The workflow engine will execute a series of business rules based on the Loan Amount and the Calculated Risk Grade to determine the required approval path.

The "Approval Authority Matrix" will not be a document; it will be a set of configurable rules within our system.
```

### 2.2 Approval Authority Matrix (as System Rules)
**Definitive V1 Approval Rules**:

**Rule Set 1: Single-Level Approval (Low Risk)**
```
Condition: Loan Amount <= ZMW 50,000 AND Risk Grade is 'A' or 'B'
Action: The application is routed to the Credit Analyst user group's queue for a single, final approval
```

**Rule Set 2: Escalated Approval (Medium Risk or Higher Amount)**
```
Condition: (Loan Amount > ZMW 50,000 AND Loan Amount <= ZMW 250,000) OR (Risk Grade is 'C')
Action: The application is routed to the Head of Credit user group's queue for a single, final approval
```

**Rule Set 3: Dual Control / Executive Approval (High Risk or Very High Amount)**
```
Condition: Loan Amount > ZMW 250,000 OR Risk Grade is 'D' or 'F'
Action: This triggers a two-step approval process:
- First, it must be approved by the Head of Credit
- Only after the Head of Credit approves, it is then routed to the CEO's queue for the final, second approval
```

**Special Rule: Offline Provisional Approvals**
```
The CEO has the unique authority to issue a Provisional Approval via the offline desktop app, which is then subject to the server-side sync and conflict resolution workflow
```

### 2.3 User Group Definitions
**Approval User Groups**:
```
Credit Analyst Group:
- Standard loan officers
- Authority: Up to ZMW 50,000 (Low Risk: A, B)
- Single approval required

Head of Credit Group:
- Senior credit managers
- Authority: Up to ZMW 250,000 (Medium Risk: C)
- Single approval required
- First approval for dual control scenarios

CEO Group:
- Chief Executive Officer
- Authority: Unlimited (High Risk: D, F)
- Second approval for dual control scenarios
- Final authority for all high-risk applications
```

## Stage 3: Final Decision

### 3.1 Decision Actions
**Approver Actions**:
```
When an application appears in an approver's queue, they will have three primary actions:

Approve: Confirms the loan and moves it to the "Ready for Disbursement" stage
- For dual control scenarios, the application progresses to the next approver
- For single approval scenarios, the application is immediately authorized

Approve with Conditions: Conditional approval with specific requirements
- Additional documentation required
- Enhanced monitoring requirements
- Specific terms and conditions
- Regular review requirements
- Performance monitoring conditions
- Compliance requirements

Reject: Rejects the loan, requiring the approver to:
- Select a reason from a pre-defined list
- Add detailed notes explaining the rejection
- The workflow terminates and the application is marked as rejected
```

### 3.2 Decision Documentation
**Required Documentation**:
```
Decision Record:
- Application summary
- Credit assessment results
- Risk grade and score
- Decision rationale
- Approval conditions (if applicable)
- Authority approval
- Decision timestamp
- Complete approval summary
- Policy compliance status
- Risk mitigation measures
- Monitoring requirements
- Authority verification
- Camunda workflow execution log
- Audit trail
```

**Decision Communication**:
```
Communication Process:
1. Decision preparation
2. Customer notification
3. Terms communication
4. Documentation delivery
5. Acceptance confirmation
6. Process completion

Communication Requirements:
- Clear messaging
- Complete information
- Timely delivery
- Professional tone
- Compliance requirements
- Customer service
- Internal communication
- Regulatory reporting
- Audit trail maintenance
- Performance tracking
- Quality assurance
```

### 3.3 Conditional Approval Management
**Condition Types**:
```
Condition Categories:
- Documentation conditions
- Monitoring conditions
- Performance conditions
- Compliance conditions
- Review conditions
- Reporting conditions
```

**Condition Management Process**:
```
Condition Implementation:
1. Condition identification
2. Condition assessment
3. Condition implementation
4. Monitoring setup
5. Performance tracking
6. Review and adjustment
```

## Camunda Workflow Orchestration

### 3.4 Workflow Engine Configuration
**Business Rules Engine**:
```
Rule Configuration:
- Loan amount thresholds
- Risk grade mappings
- User group assignments
- Approval path logic
- Escalation triggers
- Conflict resolution procedures
```

**Workflow States**:
```
Application States:
- Underwriter Review
- Pending Credit Analyst Approval
- Pending Head of Credit Approval
- Pending CEO Approval
- Approved
- Rejected
- Ready for Disbursement
```

### 3.5 Audit Trail and Compliance
**Complete Workflow History**:
```
Camunda maintains:
- Complete decision history
- Authority verification
- Policy compliance
- Risk assessment
- Decision rationale
- Communication records
- Performance tracking
- Workflow execution path
- Time stamps for all actions
- User authentication for all decisions
```

## Performance Controls

### Timeline Management
**Processing Targets**:
```
Decision recommendation: Same day as assessment
Automated routing: Immediate (system-driven)
Approval decision: 
- Single approval: 4 business hours
- Dual control: 8 business hours (4 + 4)
Total approval time: 4-8 business hours
```

**Performance Metrics**:
```
Key Indicators:
- Approval time compliance
- Decision quality
- Policy compliance
- Risk management effectiveness
- Customer satisfaction
- Portfolio performance
- Audit readiness
- Workflow execution efficiency
```

## Compliance Requirements

### Regulatory Compliance
**BoZ Requirements**:
```
Regulatory Standards:
- Prudent lending practices
- Risk management framework
- Portfolio quality standards
- Capital adequacy requirements
- Liquidity management
- Regulatory reporting
```

**Money Lenders Act**:
```
Act Compliance:
- Interest rate limits (48% EAR)
- Fee structure compliance
- Loan term requirements
- Documentation standards
- Customer protection
- Regulatory reporting
```

### Internal Control Compliance
**Control Requirements**:
```
Internal Controls:
- Segregation of duties
- Authority matrix compliance
- Dual control requirements
- Documentation standards
- Audit trail maintenance
- Performance monitoring
- System-enforced routing
- Non-bypassable approval rules
```

## Risk Management

### Portfolio Risk Management
**Risk Monitoring**:
```
Monitoring Areas:
- Portfolio risk distribution
- Approval decision patterns
- Risk rating trends
- Default rate monitoring
- Loss rate tracking
- Portfolio quality metrics
- Workflow execution efficiency
```

**Risk Mitigation**:
```
Mitigation Strategies:
- Enhanced approval requirements
- Additional monitoring
- Risk-based pricing
- Collateral requirements
- Guarantor requirements
- Early warning systems
- Automated risk-based routing
```

## Next Steps

This revised Loan Approval Workflow document serves as the foundation for:
1. **System Configuration** - Camunda workflow setup and business rules configuration
2. **Staff Training** - Approval process and authority training
3. **Policy Development** - Approval policy and authority matrix
4. **Quality Assurance** - Approval quality and compliance monitoring
5. **Technical Implementation** - Camunda workflow engine configuration

## Document Approval

- **Loan Origination Manager**: [Name] - [Date]
- **Risk Management Officer**: [Name] - [Date]
- **Compliance Officer**: [Name] - [Date]
- **Technical Lead**: [Name] - [Date]
- **CEO**: [Name] - [Date]

---

**Document Control**: This document must be reviewed and updated quarterly or when business processes change.
