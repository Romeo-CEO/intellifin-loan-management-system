# LMS User Stories - Template Compliance Guide

## Overview

This directory contains individual user stories that follow the proper `story-tmpl.yaml` template structure. Each story is a self-contained document that provides complete context for development.

## Story Template Structure

Each story follows this exact structure:

### 1. Status
- Draft, Approved, InProgress, Review, Done

### 2. Story
- Standard format: "As a [role], I want [action], so that [benefit]"

### 3. Acceptance Criteria
- Numbered list of specific, testable criteria
- Each AC should be measurable and verifiable

### 4. Tasks / Subtasks
- Detailed breakdown of implementation tasks
- Each task references relevant AC numbers
- Hierarchical structure with main tasks and subtasks

### 5. Dev Notes
- **Previous Story Insights**: Key learnings from previous stories
- **Data Models**: Specific schemas and relationships [with source references]
- **API Specifications**: Endpoint details and formats [with source references]
- **Component Specifications**: UI components and technical details [with source references]
- **File Locations**: Exact paths based on project structure
- **Testing Requirements**: Specific test cases and strategies
- **Technical Constraints**: Version requirements and compliance rules

### 6. Testing
- **Testing Standards**: Framework, location, coverage requirements
- **Test Scenarios**: Specific test cases for the story

### 7. Change Log
- Table tracking all changes to the story

### 8. Dev Agent Record
- Populated by development agent during implementation
- Includes agent model, debug logs, completion notes, file list

### 9. QA Results
- Populated by QA agent after implementation review

## Created Stories

### Foundation & Infrastructure (Epic 1)
- ✅ **1.1: Monorepo Infrastructure Setup** - Nx workspace, project structure (APPROVED)
- ✅ **1.2: Database Schema Creation** - Complete database schema with all tables (APPROVED)
- ✅ **1.3: API Gateway Setup** - Ocelot gateway, authentication, routing (APPROVED)

### Identity & Security (Epic 2)
- ✅ **2.1: User Authentication System** - JWT, password hashing, session management

### Client Management (Epic 3)
- ✅ **3.1: Customer Registration** - KYC information capture and verification

### Loan Origination (Epic 4)
- ✅ **4.1: Loan Product Selection** - Product catalog, eligibility, recommendations

## Remaining Stories to Create

### Foundation & Infrastructure (Epic 1)
- [ ] **1.4: Message Queue Setup** - RabbitMQ configuration and messaging
- [ ] **1.5: Redis Cache Setup** - Caching layer configuration
- [ ] **1.6: MinIO Object Storage** - Document storage setup

**Note**: Stories 1.1, 1.2, and 1.3 are APPROVED and ready for development.

### Identity & Security (Epic 2)
- [ ] **2.2: Role-Based Access Control** - RBAC implementation
- [ ] **2.3: Step-Up Authentication** - Multi-factor authentication

### Client Management (Epic 3)
- [ ] **3.2: KYC Document Verification** - Document validation workflow
- [ ] **3.3: Customer Profile Management** - Profile updates and management

### Loan Origination (Epic 4)
- [ ] **4.2: Loan Application Form** - Dynamic form based on product
- [ ] **4.3: Loan Application Workflow** - Camunda workflow integration

### Credit Assessment (Epic 5)
- [ ] **5.1: Credit Bureau Integration** - TransUnion API integration (Credit Bureau Service)
- [ ] **5.2: Credit Scoring Engine** - Scoring algorithm implementation (Loan Origination Service)
- [ ] **5.3: Credit Decision Workflow** - Decision workflow implementation (Loan Origination Service)

### Collections & Recovery (Epic 6)
- [ ] **6.1: PMEC Payroll Integration** - Government payroll integration
- [ ] **6.2: Collections Lifecycle Management** - DPD calculation and classification
- [ ] **6.3: Payment Processing** - Tingg payment gateway integration

### Financial Accounting (Epic 7)
- [ ] **7.1: Chart of Accounts Setup** - BoZ-compliant chart of accounts
- [ ] **7.2: Transaction Processing** - Double-entry bookkeeping
- [ ] **7.3: Financial Reporting** - JasperReports integration

### Communications (Epic 8)
- [ ] **8.1: SMS Notification System** - Africa's Talking integration
- [ ] **8.2: Email Notification System** - Email service integration
- [ ] **8.3: In-App Notifications** - Real-time notification system

### Reporting & Compliance (Epic 9)
- [ ] **9.1: Prudential Reporting** - BoZ report generation
- [ ] **9.2: Audit Trail System** - Comprehensive audit logging
- [ ] **9.3: Compliance Monitoring** - Real-time compliance dashboards

### Offline Operations (Epic 10)
- [ ] **10.1: Offline Loan Origination** - CEO offline capabilities
- [ ] **10.2: Offline Sync System** - Data synchronization

### System Administration (Epic 11)
- [ ] **11.1: System Monitoring** - Application Insights integration
- [ ] **11.2: Backup and Recovery** - Comprehensive backup procedures

## Story Creation Guidelines

### 1. Follow the Template Exactly
- Use the exact structure from `story-tmpl.yaml`
- Include all required sections
- Maintain consistent formatting

### 2. Include Architecture References
- Always include `[Source: docs/architecture/filename.md#section]` references
- Extract information only from existing architecture documents
- Don't invent new technical details

### 3. Provide Complete Context
- Include previous story insights
- Reference specific data models and APIs
- Provide exact file locations
- Include testing requirements

### 4. Make Stories Self-Contained
- Dev agent should not need to read architecture docs
- Include all necessary technical details
- Provide clear implementation guidance

### 5. Ensure Testability
- All acceptance criteria should be testable
- Include specific test scenarios
- Reference testing standards and frameworks

## Next Steps

1. **Review Created Stories**: Validate the template compliance
2. **Create Remaining Stories**: Use the same pattern for all remaining stories
3. **Validate Story Quality**: Run story checklist on each story
4. **Begin Development**: Start with Story 1.1 (Monorepo Setup)

## Template Compliance Checklist

For each story, ensure:
- [ ] Status section with proper choices
- [ ] Standard story format (As a/I want/So that)
- [ ] Numbered acceptance criteria
- [ ] Detailed tasks with AC references
- [ ] Comprehensive dev notes with architecture references
- [ ] Testing section with standards and scenarios
- [ ] Change log table
- [ ] Dev agent record section
- [ ] QA results section

All stories must follow this exact structure for consistency and completeness.
