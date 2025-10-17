# Generate Stories 1.6-1.17 for Client Management Compliance Engine
# SM Bob - Automated Story Generation from PRD
# Date: 2025-10-16

Write-Host "🏃 SM Bob - Story Generation Script" -ForegroundColor Cyan
Write-Host "Creating stories 1.6-1.17 from PRD content..." -ForegroundColor Yellow
Write-Host ""

# Story Template Function
function New-Story {
    param(
        [string]$StoryNumber,
        [string]$Title,
        [string]$Role,
        [string]$Want,
        [string]$SoThat,
        [string[]]$AcceptanceCriteria,
        [string]$DevNotesContent
    )
    
    $template = @"
# Story $StoryNumber: $Title

## Status

**Draft**

## Story

**As a** $Role,  
**I want** $Want,  
**so that** $SoThat.

## Acceptance Criteria

$($AcceptanceCriteria -join "`n`n")

## Tasks / Subtasks

_(Tasks generated from acceptance criteria - to be detailed during story refinement)_

## Dev Notes

$DevNotesContent

### Testing

**Test Coverage Target:** 85-90% for services, 80% for workers

**Test Frameworks:** xUnit, Moq/NSubstitute, FluentAssertions, TestContainers

## Change Log

| Date | Version | Description | Author |
|------|---------|-------------|--------|
| 2025-10-16 | 1.0 | Initial story from PRD | SM Bob |

## Dev Agent Record

### Agent Model Used
_(To be populated by Dev Agent)_

### Debug Log References
_(To be populated by Dev Agent)_

### Completion Notes
_(To be populated by Dev Agent)_

### File List
_(To be populated by Dev Agent)_

## QA Results
_(To be populated by QA Agent)_
"@
    
    return $template
}

# Create docs/stories directory if not exists
$storiesDir = "docs/stories"
if (-not (Test-Path $storiesDir)) {
    New-Item -Path $storiesDir -ItemType Directory -Force | Out-Null
}

Write-Host "✅ Stories 1.1-1.5 already created" -ForegroundColor Green
Write-Host ""
Write-Host "Creating stories 1.6-1.17..." -ForegroundColor Yellow
Write-Host ""

# Story 1.6: KycDocumentService Integration
$story = New-Story -StoryNumber "1.6" `
    -Title "KycDocumentService Integration (Phase 1 - HTTP Client)" `
    -Role "Loan Officer" `
    -Want "to upload KYC documents for a client and have them stored in MinIO via KycDocumentService" `
    -SoThat "I can attach supporting documents to customer profiles without duplicating MinIO integration logic" `
    -AcceptanceCriteria @(
        "1. ``KycDocumentServiceClient.cs`` Refit interface created in ``Integration/`` with POST /documents, GET /documents/{id}, GET /documents/{id}/download",
        "2. ``ClientDocument`` entity created with fields: Id, ClientId, DocumentType, ObjectKey, BucketName, FileName, FileHashSha256, UploadStatus, UploadedAt, UploadedBy, VerifiedAt, VerifiedBy, ExpiryDate, RetentionUntil",
        "3. ``DocumentLifecycleService`` created with UploadDocumentAsync, GetDocumentMetadataAsync, GenerateDownloadUrlAsync methods",
        "4. API endpoints: POST /api/clients/{id}/documents, GET /api/clients/{id}/documents, GET /api/clients/{id}/documents/{docId}, GET /api/clients/{id}/documents/{docId}/download",
        "5. Document upload validates file size (max 10MB), content type (PDF, JPG, PNG), sets RetentionUntil = NOW() + 7 years",
        "6. Integration tests with WireMock for KycDocumentService",
        "",
        "**Integration Verification:**",
        "- **IV1:** HTTP requests match existing KycDocumentService API schema",
        "- **IV2:** Documents successfully stored in MinIO with 7-year Object Lock retention",
        "- **IV3:** ClientDocument SQL records match MinIO object metadata"
    ) `
    -DevNotesContent @"
### KycDocumentService Phase 1 Integration
[Source: PRD Story 1.6, Architecture §10.1 - KycDocumentService Split]

**Phase 1:** Client Management consumes KycDocumentService as HTTP dependency
**Phase 2:** (Story 1.17) Deprecate KycDocumentService, migrate functionality
**Phase 3:** Archive old service

### ClientDocument Entity
[Source: brownfield-architecture.md#Data Models - ClientDocument]

MinIO metadata tracking with dual-control fields, OCR confidence, 7-year retention.

### 7-Year Retention Enforcement
[Source: PRD FR5]

MinIO Object Lock in COMPLIANCE mode with RetentionUntil metadata.
"@

$story | Out-File -FilePath "$storiesDir/1.6.kycdocument-integration.md" -Encoding UTF8
Write-Host "  ✅ Created Story 1.6" -ForegroundColor Green

# Story 1.7: CommunicationsService Integration
$story = New-Story -StoryNumber "1.7" `
    -Title "CommunicationsService Integration for Event-Based Notifications" `
    -Role "Customer" `
    -Want "to receive SMS and email notifications when my KYC status changes" `
    -SoThat "I stay informed about my loan application progress" `
    -AcceptanceCriteria @(
        "1. ``CommunicationsClient.cs`` Refit interface created with POST /api/communications/send method",
        "2. ``SendNotificationRequest`` DTO matching CommunicationsService schema (TemplateId, RecipientId, Channel, PersonalizationData)",
        "3. ``CommunicationConsent`` entity created with ConsentType, SmsEnabled, EmailEnabled, InAppEnabled, CallEnabled, ConsentGivenAt, ConsentGivenBy",
        "4. ``ConsentManagementService`` created with GetConsentAsync, UpdateConsentAsync, CheckConsentAsync methods",
        "5. API endpoints: GET /api/clients/{id}/consents, PUT /api/clients/{id}/consents",
        "6. SendConsentBasedNotificationAsync helper checks consent before calling CommunicationsClient",
        "7. Integration tests with WireMock verify notifications sent only when consent granted",
        "",
        "**Integration Verification:**",
        "- **IV1:** Notification requests conform to CommunicationsService schema",
        "- **IV2:** Notifications NOT sent if customer has SmsEnabled=false or EmailEnabled=false",
        "- **IV3:** TemplateId values reference existing templates"
    ) `
    -DevNotesContent @"
### CommunicationsService Integration
[Source: brownfield-architecture.md#Integration Points - CommunicationsService]

Templates: kyc_approved, kyc_rejected, kyc_edd_required, document_expiring_soon

### Consent Management
[Source: PRD FR11, FR12]

Consent types: Marketing, Operational, Regulatory
KYC notifications require ConsentType=Operational
"@

$story | Out-File -FilePath "$storiesDir/1.7.communications-integration.md" -Encoding UTF8
Write-Host "  ✅ Created Story 1.7" -ForegroundColor Green

# Story 1.8: Document Dual-Control Verification
$story = New-Story -StoryNumber "1.8" `
    -Title "Document Dual-Control Verification Workflow" `
    -Role "Compliance Officer" `
    -Want "to enforce dual-control verification where one officer uploads and another verifies" `
    -SoThat "we prevent fraud and satisfy BoZ dual-control requirements" `
    -AcceptanceCriteria @(
        "1. ``UploadStatus`` enum added: Uploaded, PendingVerification, Verified, Rejected",
        "2. DocumentLifecycleService.UploadDocumentAsync sets UploadStatus=Uploaded, stores UploadedBy from JWT",
        "3. New endpoint: PUT /api/clients/{id}/documents/{docId}/verify with VerifyDocumentRequest (Approved: bool, RejectionReason?: string)",
        "4. DocumentLifecycleService.VerifyDocumentAsync validates: VerifiedBy != UploadedBy, UploadStatus = Uploaded",
        "5. Database trigger/constraint: CHECK (VerifiedBy IS NULL OR VerifiedBy <> UploadedBy)",
        "6. Audit events logged for both upload and verification",
        "7. Unit tests verify dual-control enforcement",
        "",
        "**Integration Verification:**",
        "- **IV1:** Database constraint prevents self-verification",
        "- **IV2:** Both UploadedBy and VerifiedBy logged to AdminService",
        "- **IV3:** Document upload without verification works (status=Uploaded)"
    ) `
    -DevNotesContent @"
### Dual-Control Enforcement
[Source: brownfield-architecture.md#Workflow 3 - Document Dual-Control]

Service-level: VerifiedBy != UploadedBy validation
Database-level: Trigger prevents self-verification
Audit: Both actions logged to AdminService

### Architecture Decision
[Source: User Decision - Dual Control Implementation]

Camunda human tasks = source of truth
Services = guardrails (policy checks)
"@

$story | Out-File -FilePath "$storiesDir/1.8.dual-control-verification.md" -Encoding UTF8
Write-Host "  ✅ Created Story 1.8" -ForegroundColor Green

# Story 1.9: Camunda Worker Infrastructure
$story = New-Story -StoryNumber "1.9" `
    -Title "Camunda Worker Infrastructure and Topic Registration" `
    -Role "System Architect" `
    -Want "to integrate Camunda Zeebe client and register background workers for KYC workflow tasks" `
    -SoThat "the service can participate in BPMN-orchestrated business processes" `
    -AcceptanceCriteria @(
        "1. Zeebe .NET Client NuGet package added (version 2.6+)",
        "2. ``CamundaWorkerHostedService`` created as BackgroundService in ``Workflows/CamundaWorkers/``",
        "3. Camunda config in appsettings.json: GatewayAddress, WorkerName, MaxJobsToActivate, PollingIntervalSeconds",
        "4. Base ``ICamundaJobHandler`` interface with HandleJobAsync method",
        "5. Worker registration: RegisterWorker<THandler>(topicName, jobType)",
        "6. Example ``HealthCheckWorker`` for topic client.health.check",
        "7. Health check endpoint /health/camunda added",
        "8. Integration tests with Camunda Test SDK",
        "9. Error handling: exponential backoff retry, DLQ after 3 failures",
        "",
        "**Integration Verification:**",
        "- **IV1:** Service starts with Camunda workers registered (logs show topic subscriptions)",
        "- **IV2:** Worker failures don't crash main service",
        "- **IV3:** REST API endpoints remain responsive while workers poll"
    ) `
    -DevNotesContent @"
### Camunda Worker Pattern
[Source: brownfield-architecture.md#Technical Debt - No Camunda Worker Infrastructure]

Topic naming: client.{process}.{taskName}
Long-polling with exponential backoff
Correlation IDs in workflow variables

### Configuration
[Source: brownfield-architecture.md#Camunda Connection Config]

GatewayAddress, WorkerName, Topics list
"@

$story | Out-File -FilePath "$storiesDir/1.9.camunda-infrastructure.md" -Encoding UTF8
Write-Host "  ✅ Created Story 1.9" -ForegroundColor Green

# Story 1.10: KYC Status Entity and State Machine
$story = New-Story -StoryNumber "1.10" `
    -Title "KYC Status Entity and State Machine" `
    -Role "KYC Officer" `
    -Want "to track KYC compliance state (Pending, InProgress, Completed, EDD_Required, Rejected)" `
    -SoThat "I can see which clients need KYC review and their current stage" `
    -AcceptanceCriteria @(
        "1. ``KycStatus`` entity created with: CurrentState, KycStartedAt, KycCompletedAt, CamundaProcessInstanceId, HasNrc, HasProofOfAddress, HasPayslip, IsDocumentComplete, AmlScreeningComplete, RequiresEdd, EddReason",
        "2. ``KycStatusConfiguration`` with unique index on ClientId",
        "3. ``KycWorkflowService`` created with InitiateKycAsync, UpdateKycStateAsync, GetKycStatusAsync",
        "4. API endpoints: POST /api/clients/{id}/kyc/initiate, GET /api/clients/{id}/kyc-status, PUT /api/clients/{id}/kyc/state",
        "5. State machine validation: only valid transitions allowed",
        "6. InitiateKycAsync creates KycStatus with CurrentState=Pending, KycStartedAt=NOW()",
        "7. Unit tests validate state machine transitions",
        "",
        "**Integration Verification:**",
        "- **IV1:** Creating clients doesn't auto-create KycStatus (explicit initiation)",
        "- **IV2:** Invalid state transitions throw exception",
        "- **IV3:** InitiateKycAsync idempotent (no duplicates)"
    ) `
    -DevNotesContent @"
### KycStatus Entity
[Source: brownfield-architecture.md#Data Models - KycStatus]

State machine: Pending → InProgress → Completed/EDD_Required/Rejected
Document completeness flags
CamundaProcessInstanceId for workflow tracking
"@

$story | Out-File -FilePath "$storiesDir/1.10.kyc-status-entity.md" -Encoding UTF8
Write-Host "  ✅ Created Story 1.10" -ForegroundColor Green

# Story 1.11: KYC Verification Workflow
$story = New-Story -StoryNumber "1.11" `
    -Title "KYC Verification Workflow Implementation (client_kyc_v1.bpmn)" `
    -Role "KYC Officer" `
    -Want "an automated KYC workflow that checks documents, performs AML screening, and assigns review tasks" `
    -SoThat "KYC verification follows a consistent, auditable process" `
    -AcceptanceCriteria @(
        "1. client_kyc_v1.bpmn created with: Start (ClientCreatedEvent), Check documents, Gateway (complete?), AML screening, Risk assessment, Human task (KYC officer), End events",
        "2. Workers created: KycDocumentCheckWorker, AmlScreeningWorker, RiskAssessmentWorker",
        "3. KycDocumentCheckWorker queries ClientDocument, sets workflow variables: hasNrc, hasProofOfAddress, hasPayslip, documentComplete",
        "4. Human task form in Camunda for KYC officer with Approve/Comments fields",
        "5. Domain events published: KycCompletedEvent, KycRejectedEvent, EddEscalatedEvent",
        "6. Integration tests with Camunda Test SDK validate workflow",
        "",
        "**Integration Verification:**",
        "- **IV1:** BPMN deploys to Camunda, appears in Operate UI",
        "- **IV2:** Workers process tasks without errors",
        "- **IV3:** Domain events published to RabbitMQ with correct routing keys"
    ) `
    -DevNotesContent @"
### KYC Workflow
[Source: brownfield-architecture.md#Workflow 1 - Standard KYC Verification]

BPMN: client_kyc_v1.bpmn
Topics: client.kyc.check-documents, client.kyc.aml-screening, client.kyc.risk-assessment
Human task: role:kyc-officer

### Camunda Control-Plane Pattern
[Source: Architecture Decision - Camunda Integration]

Topic naming, correlation IDs, exponential backoff, DLQ
"@

$story | Out-File -FilePath "$storiesDir/1.11.kyc-workflow.md" -Encoding UTF8
Write-Host "  ✅ Created Story 1.11" -ForegroundColor Green

# Story 1.12: AML Screening and EDD Workflow
$story = New-Story -StoryNumber "1.12" `
    -Title "AML Screening and Enhanced Due Diligence (EDD) Workflow" `
    -Role "Compliance Officer" `
    -Want "to screen against sanctions/PEP lists and escalate high-risk clients to EDD" `
    -SoThat "we comply with BoZ AML requirements and prevent onboarding sanctioned individuals" `
    -AcceptanceCriteria @(
        "1. ``AmlScreening`` entity created with: ScreeningType, ScreeningProvider, ScreenedAt, IsMatch, MatchDetails, RiskLevel",
        "2. ``AmlScreeningService`` with PerformSanctionsScreeningAsync, PerformPepScreeningAsync methods",
        "3. ``AmlScreeningWorker`` for topic client.kyc.aml-screening performs checks, sets workflow variables: amlRiskLevel, sanctionsHit, pepMatch, escalateToEdd",
        "4. client_edd_v1.bpmn created: Start (EddEscalatedEvent), Generate EDD report, Compliance review, CEO approval, End",
        "5. ``EddReportGenerationWorker`` creates PDF with client profile, AML results, risk factors, stores in MinIO",
        "6. Integration tests validate EDD escalation",
        "",
        "**Integration Verification:**",
        "- **IV1:** High AML risk in KYC workflow triggers EDD workflow",
        "- **IV2:** EDD report PDF stored in MinIO with retention policy",
        "- **IV3:** CEO approval task assignable to role:ceo with MFA"
    ) `
    -DevNotesContent @"
### AML Screening
[Source: brownfield-architecture.md#Data Models - AmlScreening]

Initially manual list, API-ready for future
Screening types: Sanctions, PEP, Watchlist

### EDD Workflow
[Source: brownfield-architecture.md#Workflow 2 - Enhanced Due Diligence]

client_edd_v1.bpmn with compliance + CEO approval
Offline CEO approval as fallback (feature flag)
"@

$story | Out-File -FilePath "$storiesDir/1.12.aml-edd.md" -Encoding UTF8
Write-Host "  ✅ Created Story 1.12" -ForegroundColor Green

# Story 1.13: Vault Integration and Risk Scoring
$story = New-Story -StoryNumber "1.13" `
    -Title "Vault Integration and Risk Scoring Engine" `
    -Role "Risk Manager" `
    -Want "to compute risk scores using Vault-managed rules that update without code deployments" `
    -SoThat "we can adapt risk criteria as regulations change" `
    -AcceptanceCriteria @(
        "1. VaultSharp NuGet package added (1.15+)",
        "2. ``RiskProfile`` entity created with: RiskRating, RiskScore, RiskRulesVersion, RiskRulesChecksum, RuleExecutionLog, InputFactorsJson, IsCurrent",
        "3. ``VaultRiskConfigProvider`` implementing IRiskConfigProvider with GetCurrentConfigAsync, 60s polling, cache with version/checksum, RegisterConfigChangeCallback",
        "4. ``RiskScoringService`` with ComputeRiskAsync: retrieves Vault config, builds input factors JSON, executes JSONLogic/CEL rules, stores RiskProfile",
        "5. ``RiskAssessmentWorker`` for topic client.kyc.risk-assessment calls RiskScoringService",
        "6. API endpoint: GET /api/clients/{id}/risk-profile",
        "7. Integration tests with local Vault",
        "",
        "**Integration Verification:**",
        "- **IV1:** /health/vault verifies connection",
        "- **IV2:** Config version change detected within 60s without restart",
        "- **IV3:** RiskProfile.RiskRulesVersion matches Vault config at computation time"
    ) `
    -DevNotesContent @"
### Vault Integration
[Source: brownfield-architecture.md#Vault Integration for Risk Profiling]

Path: intellifin/client-management/risk-scoring-rules
60s polling with hot-reload callbacks
JSONLogic/CEL rule execution

### Architecture Decision
[Source: User Decision - Vault Integration]

KV v2, hot-reload via polling, version/checksum tracking, GitOps approval
"@

$story | Out-File -FilePath "$storiesDir/1.13.vault-risk-scoring.md" -Encoding UTF8
Write-Host "  ✅ Created Story 1.13" -ForegroundColor Green

# Story 1.14: Event-Driven Notifications
$story = New-Story -StoryNumber "1.14" `
    -Title "Event-Driven Notification Triggers for KYC Status Changes" `
    -Role "Customer" `
    -Want "to receive SMS notifications when my KYC status changes" `
    -SoThat "I know my loan application status without calling the branch" `
    -AcceptanceCriteria @(
        "1. Domain event handlers for: KycCompletedEvent, KycRejectedEvent, EddEscalatedEvent",
        "2. ``NotificationService`` with SendKycStatusNotificationAsync: retrieves consent, checks ConsentType=Operational && SmsEnabled, calls CommunicationsClient",
        "3. MassTransit consumers created in ``Consumers/`` for each event",
        "4. RabbitMQ config: exchange client.events, routing keys client.kyc.*",
        "5. Integration tests with MassTransit In-Memory test harness",
        "6. Notification retry: 3 retries exponential backoff, DLQ after failures",
        "",
        "**Integration Verification:**",
        "- **IV1:** No notification if SmsEnabled=false",
        "- **IV2:** Notifications queued asynchronously, don't block workflow",
        "- **IV3:** Events published in correct order"
    ) `
    -DevNotesContent @"
### Event-Driven Architecture
[Source: PRD Story 1.14]

MassTransit + RabbitMQ
Templates: kyc_approved, kyc_rejected, kyc_edd_required
Consent checking before send
"@

$story | Out-File -FilePath "$storiesDir/1.14.event-notifications.md" -Encoding UTF8
Write-Host "  ✅ Created Story 1.14" -ForegroundColor Green

# Story 1.15: Document Expiry Monitoring
$story = New-Story -StoryNumber "1.15" `
    -Title "Document Expiry Monitoring and Reminder Notifications" `
    -Role "Branch Manager" `
    -Want "automated alerts when KYC documents are approaching expiry" `
    -SoThat "I can proactively request updated documents" `
    -AcceptanceCriteria @(
        "1. ``DocumentExpiryMonitoringService`` created as BackgroundService running daily at 2 AM",
        "2. Query: SELECT documents WHERE ExpiryDate BETWEEN NOW() AND NOW() + 30 days AND IsArchived=false",
        "3. For each expiring doc: call NotificationService with template document_expiring_soon, check consent",
        "4. Audit event: DocumentExpiryReminderSent",
        "5. Config setting: DocumentExpiryReminderDays (default 30)",
        "6. Unit tests with mocked current date",
        "",
        "**Integration Verification:**",
        "- **IV1:** Job completes in < 5 min for 10k documents",
        "- **IV2:** Reminders sent once per document per day (idempotency)",
        "- **IV3:** Monitoring runs independently without blocking API"
    ) `
    -DevNotesContent @"
### Background Service Pattern
[Source: PRD Story 1.15]

BackgroundService running daily at 2 AM
Configurable warning period (default 30 days)
Idempotency to prevent duplicate reminders
"@

$story | Out-File -FilePath "$storiesDir/1.15.document-expiry.md" -Encoding UTF8
Write-Host "  ✅ Created Story 1.15" -ForegroundColor Green

# Story 1.16: Integration Testing Suite
$story = New-Story -StoryNumber "1.16" `
    -Title "Integration Testing Suite with TestContainers" `
    -Role "QA Engineer" `
    -Want "comprehensive integration tests with TestContainers for SQL/MinIO and mocked external services" `
    -SoThat "we can validate end-to-end workflows in isolated environment" `
    -AcceptanceCriteria @(
        "1. Test project IntelliFin.ClientManagement.IntegrationTests with xUnit",
        "2. TestContainers packages: Testcontainers.MsSql, Testcontainers.Minio",
        "3. ClientManagementTestFixture implementing IAsyncLifetime: SQL Server container, MinIO container, apply migrations, seed test data",
        "4. WireMock for: AdminService, CommunicationsService, KycDocumentService",
        "5. Test scenarios: Client CRUD + versioning, Document upload + dual-control, KYC workflow end-to-end, EDD escalation, Consent-based notifications",
        "6. Test coverage: 85%+ integration coverage",
        "7. CI/CD pipeline runs integration tests on every PR",
        "",
        "**Integration Verification:**",
        "- **IV1:** Tests run in parallel without interference",
        "- **IV2:** TestContainers auto-cleanup after tests",
        "- **IV3:** Integration tests complete in < 10 min in CI"
    ) `
    -DevNotesContent @"
### Integration Testing Strategy
[Source: PRD Story 1.16, NFR10]

TestContainers for SQL Server and MinIO
WireMock for external services
85%+ integration coverage
Complete in <10 min in CI/CD
"@

$story | Out-File -FilePath "$storiesDir/1.16.integration-testing.md" -Encoding UTF8
Write-Host "  ✅ Created Story 1.16" -ForegroundColor Green

# Story 1.17: KycDocumentService Migration (Optional)
$story = New-Story -StoryNumber "1.17" `
    -Title "KycDocumentService Migration - Phase 2 (Optional/Future)" `
    -Role "System Architect" `
    -Want "to consolidate document management into Client Management by migrating MinIO integration" `
    -SoThat "we eliminate service duplication and simplify architecture" `
    -AcceptanceCriteria @(
        "1. ``MinioDocumentStorageService`` created in ``Infrastructure/Storage/`` (copied from KycDocumentService)",
        "2. Minio.NET NuGet package added (6.0+)",
        "3. MinIO config: Endpoint, AccessKey, SecretKey, BucketName, UseSSL",
        "4. DocumentLifecycleService refactored with feature flag UseKycDocumentService: true=KycDocumentServiceClient, false=MinioDocumentStorageService",
        "5. Migration script for ClientDocument.ObjectKey paths if bucket structure changes",
        "6. Parallel run testing: Phase 1 and 2 tested in staging for 2 weeks",
        "7. Rollback plan: feature flag toggle back to Phase 1",
        "8. KycDocumentService marked deprecated with sunset date",
        "",
        "**Integration Verification:**",
        "- **IV1:** Service switches between Phase 1/2 with config change (no code deployment)",
        "- **IV2:** Documents uploaded via Phase 1 readable via Phase 2",
        "- **IV3:** No new features to KycDocumentService after Phase 2"
    ) `
    -DevNotesContent @"
### Migration Strategy
[Source: PRD Story 1.17, Architecture §10.1]

Phase 1: HTTP integration (Story 1.6)
Phase 2: Internal MinIO integration (this story) - OPTIONAL
Phase 3: Archive KycDocumentService

Feature flag for gradual rollout
Parallel run testing for 2 weeks
"@

$story | Out-File -FilePath "$storiesDir/1.17.kycdocument-migration.md" -Encoding UTF8
Write-Host "  ✅ Created Story 1.17" -ForegroundColor Green

Write-Host ""
Write-Host "✅ All 12 stories created successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "Story Files Created:" -ForegroundColor Cyan
Write-Host "  - docs/stories/1.6.kycdocument-integration.md" -ForegroundColor Gray
Write-Host "  - docs/stories/1.7.communications-integration.md" -ForegroundColor Gray
Write-Host "  - docs/stories/1.8.dual-control-verification.md" -ForegroundColor Gray
Write-Host "  - docs/stories/1.9.camunda-infrastructure.md" -ForegroundColor Gray
Write-Host "  - docs/stories/1.10.kyc-status-entity.md" -ForegroundColor Gray
Write-Host "  - docs/stories/1.11.kyc-workflow.md" -ForegroundColor Gray
Write-Host "  - docs/stories/1.12.aml-edd.md" -ForegroundColor Gray
Write-Host "  - docs/stories/1.13.vault-risk-scoring.md" -ForegroundColor Gray
Write-Host "  - docs/stories/1.14.event-notifications.md" -ForegroundColor Gray
Write-Host "  - docs/stories/1.15.document-expiry.md" -ForegroundColor Gray
Write-Host "  - docs/stories/1.16.integration-testing.md" -ForegroundColor Gray
Write-Host "  - docs/stories/1.17.kycdocument-migration.md" -ForegroundColor Gray
Write-Host ""
Write-Host "Total Stories: 17/17 (100% Complete)" -ForegroundColor Green
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "  1. Review generated stories for completeness" -ForegroundColor Gray
Write-Host "  2. Run: .\generate-stories-1.6-1.17.ps1" -ForegroundColor Gray
Write-Host "  3. Hand off to Dev Agent for implementation" -ForegroundColor Gray
Write-Host ""
Write-Host "Note: Run this script to create the story files:" -ForegroundColor Cyan
Write-Host "  .\generate-stories-1.6-1.17.ps1" -ForegroundColor White
