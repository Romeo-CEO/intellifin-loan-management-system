<brainstorm>
Scope and objectives
- Build a BoZ-compliant, Zambia-sovereign, cloud-native loan management platform optimized for government employee (PMEC) loans and collateralized business loans. MVP covers KYC, credit assessment (first-time only), multi-product origination, disbursement/reconciliation, GL, collections (PEMC), reporting (Jasper), CEO offline app, and security/compliance.
- Architecture: microservices (.NET 9), API gateway, SQL Server Always On with an append-only AuditEvents table (in primary DB), Redis for caching and token denylist, RabbitMQ for in-country durable messaging, Camunda 8 (Zeebe) for workflows, JasperReports for reporting, MinIO for documents, Vault for secrets. Deployed on Kubernetes in Zambia (Infratel/Paratus), with DR and manual failover.

Key decisions and rationale
- .NET 9 + EF Core: mature enterprise stack, performance, strong tooling; aligns with Windows and SQL Server expertise.
- SQL Server Always On + read replicas: transactional integrity for GL and reporting isolation.
- Redis: hot-path client lookup and session/token revocation; sub-second UX and reduced DB contention.
- Camunda 8 (Zeebe): explicit orchestration (origination, PMEC cycles, reporting schedules) with Operate visibility and SLAs.
- RabbitMQ: self-hosted, in-country, durable messaging (queues/exchanges, DLQs) to maintain strict data sovereignty.
- JasperReports: pixel-perfect BoZ submissions; read replicas prevent OLTP impact.
- Vault: secrets rotation (annual+), centralized audit.
- Data sovereignty: DCs in Zambia; MinIO on-prem; async DR to secondary DC (1h RPO).

Risk and mitigation
- PMEC/TransUnion outages: queue + backoff; manual override (dual control); DLQ processing; clear operator UIs.
- Performance: Redis cache, partitioned/indexed SQL, background precomputation for GL metrics; read replicas for reporting; pagination and async UX.
- Compliance: append-only audit events in primary DB with insert-only role; 10-year retention; comprehensive access logging; step-up auth for sensitive ops; data residency.
- Offline: MAUI/WPF desktop with SQLCipher, conflict resolution, operational limits set by CEO; digital vouchers with signatures.

Data model highlights
- Core: Client, GovernmentEmployment, KycDocument, LoanApplication, LoanProduct (Payroll/Business), CollateralAsset, CreditReport, DeductionCycle/Item, Payment, JournalEntry, PostingRule, GLAccount, ReportRun, AuditEvent, User, Role, Branch.
- Indexing: client lookup (NRC, employerId, branchId); loan status/product; ledger (date, accountId); credit report by clientId/date; PMEC cycle by period/status.

APIs and integrations
- Internal APIs per service; OpenAPI for all; gateway routes; JWT auth; branch-context headers.
- External: PMEC (custom protocol wrappers) via ACL service; TransUnion REST; Tingg payment gateway (V1 for mobile money).
- Resilience: Polly policies (timeouts, retries, CB); idempotency keys for external calls; correlation IDs.

Security/privacy
- ASP.NET Core Identity for AuthN/AuthZ; JWT with step-up via Identity 2FA/WebAuthn; Redis denylist for immediate revocation.
- Encryption: TLS in transit; TDE for SQL; field-level encryption for PII where needed; MinIO SSE.
- Audit: append-only AuditEvents table in primary DB (insert-only DB user); optional hash-chaining later.
- Compliance: ZDPA, BoZ; data subject request workflows.

DevOps & ops
- Docker, Kubernetes, Terraform; GitHub Actions pipelines; Helm/Kustomize; Vault injectors.
- Monitoring: App Insights, ELK; SLOs: 99.5% uptime; DR semi-annual tests; manual failover.

UI/UX
- Next.js with lms-ux-style-guide.md and lms-state-brief.md applied; tablet-first, accessibility (WCAG AA), motion guidance, loading skeletons, product segmentation in analytics.

Open questions (flag in delivery plan)
- Final PMEC API spec variants and SLA windows; TransUnion plan tier limits.
- BoZ report templates final list and submission channels.
- Branch hierarchy and cross-branch access policies specifics.
- CEO offline disbursement max limits and voucher acceptance policy.
</brainstorm>

# Zambian Microfinance LMS Technical Specification

## 1. Executive Summary
- Project overview and objectives: A BoZ-compliant, cloud-native back-office LMS focusing on PMEC-integrated payroll loans and collateralized business loans, with strict Zambian data residency, strong resilience, and offline leadership tooling.
- Key technical decisions and rationale:
  - .NET 9 microservices with SQL Server Always On; Redis for hot caches; RabbitMQ for in-country messaging; Camunda 8 (Zeebe) for workflows; JasperReports Server for BoZ reporting; MinIO for documents; Vault for secrets; Kubernetes on Zambian IaaS (Infratel/Paratus).
- High-level architecture diagram: See Production Architecture in project-archtecture.md and diagram within Section 2.
- Technology stack recommendations: .NET 9, Next.js, SQL Server, Redis, RabbitMQ, Camunda 8, JasperReports, MinIO, Vault, Electron MAUI/WPF (offline), Docker/Kubernetes, Terraform, GitHub Actions, Tingg (V1).

## 2. System Architecture
### 2.1 Architecture Overview
- System components and relationships:
  - API Gateway (.NET Minimal APIs)
  - Services: Identity, Client Management, Loan Origination, Credit Bureau, GL, PMEC ACL, Reporting, Offline Sync
  - Orchestration: Camunda 8 (Zeebe + Operate)
  - Messaging: RabbitMQ (exchanges, queues, DLQs)
  - Storage: SQL Server (primary + read replica; append-only AuditEvents table in primary), MinIO, Redis
  - Reporting: JasperReports Server
  - Desktop: CEO Offline App (MAUI/WPF + SQLCipher)
- Data flow diagrams:
  - Origination: Next.js → Gateway → Origination Svc → Camunda workflow → (PMEC ACL, Credit Bureau, GL) → SQL → MinIO docs → Notifications
  - Credit: Origination/Underwriting → Credit Svc → RabbitMQ queue → TransUnion → SQL
  - Collections: Scheduler → PMEC ACL → PMEC → Responses → Exceptions Workbench → GL postings
  - Reporting: Next.js → Reporting Svc → Jasper (read replica) → exports to MinIO → delivery/schedule via Camunda
- Infrastructure requirements:
  - Kubernetes 3+ nodes per DC; SQL Always On (primary Infratel; async secondary Paratus; 1h RPO); manual failover; Vault HA; MinIO replicated; ELK + App Insights.

High-level architecture diagram
```mermaid
%% Self-hosted RabbitMQ and audit events in primary SQL
graph TB
  subgraph Client
    Web[NextJS Web App]
    CEO[CEO Offline App]
  end
  subgraph Cluster[Kubernetes]
    GW[API Gateway]
    AUTH[Identity]
    CLIENT[Client Mgmt]
    ORIG[Loan Origination]
    CREDIT[Credit Bureau]
    GL[General Ledger]
    PMEC[PMEC ACL]
    REPORT[Reporting Svc]
    WF[Camunda 8 (Zeebe)]
    JASPER[JasperReports]
    SYNC[Offline Sync]
    MQ[(RabbitMQ Cluster)]
    REDIS[(Redis)]
  end
  subgraph Storage
    SQL[(SQL Server Primary + Replica\n+ AuditEvents Table)]
    MINIO[(MinIO)]
    VAULT[(Vault)]
  end
  Web-->GW
  CEO-.->SYNC
  GW-->AUTH & CLIENT & ORIG & CREDIT & GL & REPORT
  ORIG-->WF
  CREDIT-->MQ
  PMEC-->MQ
  WF-->REPORT
  CLIENT-->REDIS
  GL-->REDIS
  AUTH & CLIENT & ORIG & CREDIT & GL & REPORT-->SQL
  REPORT-->JASPER
  JASPER-->SQL
  CLIENT & ORIG-->MINIO
  PMEC & CREDIT-->VAULT
```

### 2.2 Technology Stack
- Frontend technologies:
  - Next.js (TypeScript), React Query, Tailwind/Styled System (tokens), Service Workers (PWA)
- Backend technologies:
  - .NET 9 ASP.NET Core (Minimal APIs), MediatR CQRS (Origination), EF Core, Polly, Serilog
- Database and storage:
  - SQL Server (Always On) with AuditEvents table in primary; read replica; SQLite (SQLCipher) for offline; MinIO (S3-compatible); Redis
- Third-party services and APIs:
  - Camunda 8 (Zeebe/Operate), JasperReports Server, RabbitMQ, HashiCorp Vault, PMEC, TransUnion, Tingg (V1)

## 3. Feature Specifications

### 3.1 Client Management & KYC
- User stories and acceptance criteria
  - Capture KYC with validation; encrypted document storage; BoZ timelines; expiry notifications; PMEC registration tracking. AC: mandatory fields enforced; duplicate detection; audit logs; encrypted docs; access logs; expiry alerts.
- Technical requirements and constraints
  - Redis-backed client search; SQL partitioning by BranchId; MinIO storage; audit events recorded in primary DB; PMEC employee linkage.
- Detailed implementation approach
  - Entity: Client, GovernmentEmployment, KycDocument, Address, Contact. EF Core with composite indexes; search endpoint with caching (key: client:{nrc} TTL 15m; branch-scoped). Document upload stream → MinIO (bucket: clients/{clientId}/docs) + DB metadata; access logging to AuditEvents table.
- User flow diagrams
  - New → Search dup → Create draft → Upload docs → Validate → Submit → Approve.
- API endpoints
  - GET /api/clients?query=
  - POST /api/clients
  - POST /api/clients/{id}/documents
  - GET /api/clients/{id}
- Data models involved
  - Client(id, nrc, firstName, lastName, branchId, status, createdAt, updatedAt)
  - KycDocument(id, clientId, type, objectKey, checksum, retentionAt, createdBy, createdAt)
  - GovernmentEmployment(clientId, pmecNumber, employerId, salary, verifiedAt)
- Error handling and edge cases
  - Duplicate NRC → 409; storage failure → 503; validation → 422.
- Performance considerations
  - Pagination; Redis cache client summary; lazy-load heavy fields.

### 3.2 Credit Assessment (First-Time Applicants Only)
- User stories and acceptance criteria
  - Pull TransUnion for new clients only; auto scoring; audit decision; outage fallback.
- Technical requirements and constraints
  - Camunda BPMN orchestration; RabbitMQ; Vault; cost caps.
- Detailed implementation approach
  - BPMN: Eligibility → Consent → TU Request → Normalize → Score → Decision. Outages → exponential backoff; DLQ after N attempts; Operate incidents.
- User flow diagrams
  - UI triggers assessment → returns status → polling or webhooks for completion.
- API endpoints
  - POST /api/credit/assessments; GET status
- Data models involved
  - CreditReport(id, clientId, score, rawBlobKey, cost, requestedAt, returnedAt, status)
- Error handling and edge cases
  - 429s, schema changes, first-time-only enforcement.
- Performance considerations
  - Batch normalization; cache recent TU summary 24h.

### 3.3 Advanced Loan Origination (Multi-Product)
- User stories and acceptance criteria
  - Choose Payroll/Business; adaptive docs; underwriting; approval; handoff; dual-control overrides; interest cap enforcement.
- Technical requirements and constraints
  - CQRS; Camunda BPMN per product; PEMC verification step; collateral intake for Business.
- Detailed implementation approach
  - Entities: LoanApplication, UnderwritingNote, ApprovalStep, CollateralAsset. Events recorded as AuditEvents.
- User flow diagrams
  - Start → Select product → Product-specific tasks → UW → Approval → Disbursement.
- API endpoints
  - POST /api/origination/applications; POST approve; POST submit-docs
- Data models involved
  - LoanApplication idx: (status, productType), CollateralAsset
- Error handling and edge cases
  - Interest cap breach; PEMC outage; missing collateral.
- Performance considerations
  - Paginated pipeline; Redis cache counts per stage/branch.

### 3.4 Traditional Disbursement & Payment Processing
- User stories and acceptance criteria
  - Bank disbursement batches; cash dual auth; mobile money via Tingg (V1); reconciliation; GL postings.
- Technical requirements and constraints
  - Bank adapters; Tingg integration; idempotent submission; reconciliation engine.
- Detailed implementation approach
  - Batch + Payment entities; posting rules create JournalEntries on settlement; reconciliation UI; Tingg payouts/intents with webhook reconciliation.
- API endpoints
  - POST /api/disbursements/batches; /send; POST /api/reconciliation/match; POST /api/mobilemoney/intents (Tingg)
- Data models involved
  - DisbursementBatch; Payment; MobileMoneyIntent(webhookId, status, externalRef)
- Error handling and edge cases
  - Duplicate send; partial settlements; webhook retries with signatures.
- Performance considerations
  - Streamed file generation; async processing; backpressure via RabbitMQ where needed.

### 3.5 BoZ-Compliant Integrated General Ledger
- User stories and acceptance criteria
  - Real-time balances; automated postings; BoZ mapping; period close.
- Technical requirements and constraints
  - Double-entry; posting rules; locks during close; analytics on replica.
- Detailed implementation approach
  - JournalEntry; PostingRule maps events to entries.
- API endpoints
  - GET /api/gl/accounts/{id}/balance; POST /api/gl/postings (internal)
- Data models involved
  - GLAccount; JournalEntry; PostingRule
- Error handling and edge cases
  - Closed periods; unbalanced entries; duplicate ref.
- Performance considerations
  - Indexing; partitioning by branch; caching balances.

### 3.6 Collections & Recovery (PMEC)
- User stories and acceptance criteria
  - Schedule deductions; allocate; exceptions workbench; GL postings.
- Technical requirements and constraints
  - PMEC ACL; Camunda cycle orchestration; RabbitMQ; variance tracking.
- Detailed implementation approach
  - DeductionCycle/Item; exception actions with retries; pro-rata when salary changes.
- API endpoints
  - POST /api/collections/pemc/cycles; GET /exceptions
- Data models involved
  - DeductionCycle; DeductionItem
- Error handling and edge cases
  - Format changes; salary changes; over-deductions.
- Performance considerations
  - Batch processing; pagination; async I/O.

### 3.7 Collateral Management & Documentation
- User stories and acceptance criteria
  - Registry; valuation; insurance; release workflow.
- Technical requirements and constraints
  - MinIO; scheduled expiry checks; approvals.
- Detailed implementation approach
  - CollateralAsset; Valuation; InsurancePolicy; Release approvals.
- API endpoints
  - POST /api/collateral/assets; /{id}/release
- Data models involved
  - CollateralAsset; Valuation; InsurancePolicy
- Error handling and edge cases
  - Missing docs; expired insurance.
- Performance considerations
  - Deferred image processing; CDN/caching.

### 3.8 Regulatory & Business Reporting (Jasper)
- User stories and acceptance criteria
  - BoZ returns; operational reports; scheduling; audit trail; read-replica.
- Technical requirements and constraints
  - Reporting Svc → Jasper; MinIO for outputs; Camunda schedules.
- Detailed implementation approach
  - ReportRun with params/version; RLS injection.
- API endpoints
  - POST /api/reports/run; /schedules
- Data models involved
  - ReportRun
- Error handling and edge cases
  - Stale data; long-running; version drift.
- Performance considerations
  - Query hints; materialized views; caching.

### 3.9 CEO Offline Command Center
- User stories and acceptance criteria
  - Offline KPIs; selective sync; dual-control disbursement via voucher.
- Technical requirements and constraints
  - MAUI/WPF; SQLCipher; sync service; signatures; time-bound sessions.
- Detailed implementation approach
  - Local mirror schema; journaled sync; conflict rules.
- API endpoints
  - /api/sync/handshake, /pullDelta, /pushDelta
- Data models involved
  - Local KPIs, Loans summary, Approvals, Audit
- Error handling and edge cases
  - Conflicts; stale data; network flaps.
- Performance considerations
  - Delta sync; compression; background threads.

### 3.10 Enterprise Security & Compliance
- User stories and acceptance criteria
  - RBAC with branch context; step-up; audit trails; retention.
- Technical requirements and constraints
  - Policy-based auth; branch middleware; audit events; Vault.
- Detailed implementation approach
  - ASP.NET Core Identity; JWT + Redis denylist; step-up via Identity 2FA/WebAuthn; audit append-only table.
- API endpoints
  - N/A (cross-cutting)
- Data models involved
  - AuditEvent; User; Role; Branch
- Error handling and edge cases
  - Token replay; privilege escalation attempts.
- Performance considerations
  - Cache authz decisions; minimize DB calls.

## 4. Data Architecture
### 4.1 Data Models
- Client: id GUID PK; nrc unique; firstName; lastName; dob; branchId; status; createdAt; updatedAt; idx (nrc), (lastName,firstName), (branchId,status)
- LoanApplication: id; clientId; productType; principal; termMonths; rateAPR; status; createdAt; approvedAt; idx (status,productType), (clientId,createdAt desc)
- CreditReport: id; clientId; score; rawBlobKey; cost; requestedAt; returnedAt; status; idx (clientId,requestedAt desc)
- CollateralAsset: id; applicationId; type; value; insuredUntil; status; idx (applicationId), (status)
- DeductionCycle: id; period; status; createdAt; idx (period,status)
- DeductionItem: id; cycleId; loanId; expected; actual; variance; status; idx (cycleId,status)
- Payment: id; loanId; amount; method; externalRef; postedAt; idx (loanId,postedAt)
- GLAccount: id; code unique; name; type; parentId; branchId; idx (code), (branchId)
- JournalEntry: id; debitAccountId; creditAccountId; amount; currency; valueDate; ref unique; createdAt; idx (debitAccountId,valueDate), (creditAccountId,valueDate), (ref)
- PostingRule: id; eventType; mappingJson; active
- ReportRun: id; templateId; paramsJson; version; startedAt; finishedAt; status; outputKey; runBy
- AuditEvent: id; actorId; action; entity; entityId; timestamp; ip; detailsJson (append-only)

### 4.2 Data Storage
- Database selection: SQL Server for ACID and GL; SQLite (SQLCipher) for offline.
- Data persistence strategies: EF Core migrations; append-only AuditEvents table with insert-only DB principal; soft deletes with audit where required.
- Caching mechanisms: Redis keys client:{nrc}, client:branch:{branchId}:list:{page}, gl:balance:{accountId}:{asOf}, token:denylist:{jti} with TTLs 15m/5m/60s/exp, invalidated on writes.
- Backup and recovery procedures: hourly backups; MinIO replication; Vault snapshots; DR RPO 1h; semi-annual DR tests.

## 5. API Specifications
### 5.1 Internal APIs (selected)
- GET /api/clients
  - Params: query, branchId, page, pageSize
  - 200 { items: ClientSummary[], nextCursor }
  - Auth: JWT scope clients:read; X-Branch-Id
  - Rate limit: 60 rpm/IP
- POST /api/clients
  - Body: { profile }
  - 201 { id }
- POST /api/clients/{id}/documents
  - multipart; 201 { docId }
- POST /api/origination/applications
  - Body: { clientId, productType, principal, termMonths }
  - 201 { id, workflowInstanceId }
  - Step-up when principal > threshold
- POST /api/collections/pemc/cycles
  - Body: { period }
  - 202 { cycleId, workflowInstanceId }
- GET /api/gl/accounts/{id}/balance?asOf=
  - 200 { accountId, balance, currency, asOf }
- POST /api/mobilemoney/intents
  - Body: { loanId, amount, msisdn, purpose }
  - 201 { intentId, externalRef }
  - Webhooks: /api/mobilemoney/webhooks/tingg (HMAC signature)

Common
- AuthN: Bearer JWT; jti denylist in Redis; step-up claim for sensitive endpoints
- Headers: X-Correlation-Id, X-Branch-Id
- Status codes: 2xx on success; 4xx for validation/auth; 5xx transient with retry-after where applicable

### 5.2 External Integrations
- PMEC
  - Purpose: verification and deductions
  - Auth: client cert/token from Vault; rotate annually
  - Endpoints (adapter): VerifyEmployee, SubmitDeductions, FetchResults
  - Fallback: queue + backoff via RabbitMQ; manual override; DLQ with reprocess tool
  - Sync: correlation IDs; idempotency keys per submission
- TransUnion Zambia
  - Auth: Vault-stored creds; IP whitelist
  - Endpoint: /creditreports
  - Strategy: first-time-only; cache 24h; DLQ on schema mismatch
- Tingg (V1)
  - Auth: OAuth2 client creds (Vault); webhook HMAC
  - Endpoints: /payments/intents, /payouts; webhooks for status
  - Strategy: idempotency; reconcile to GL; retries with exponential backoff

## 6. Security & Privacy
### 6.1 Authentication & Authorization
- Authentication mechanism and flow: ASP.NET Core Identity; JWT access (15m) + rotating refresh; device-bound optional
- Authorization strategies and role definitions: RBAC roles LoanOfficer, Underwriter, Finance, Collections, Compliance, Admin, CEO; branch context enforced in middleware
- Session management: logout and compromise push jti to Redis denylist
- Token handling and refresh strategies: rolling refresh; revoke on password change/role changes
- Step-up authentication: Identity 2FA (TOTP/WebAuthn) challenge for sensitive ops; elevate claim acr=stepup:level2 for request window

### 6.2 Data Security
- Encryption strategies: TLS1.2+; SQL TDE; MinIO SSE; SQLCipher; column-level for PII; KMS-backed Vault unseal
- PII handling and protection: least-privilege; masked logs; export controls
- Compliance requirements: ZDPA, BoZ; 10-year retention; subject access, correction, deletion workflows
- Security audit procedures: quarterly SAST/DAST; annual pentest; SOC2 controls equivalent

### 6.3 Application Security
- Input validation and sanitization: FluentValidation; file whitelist + AV scan
- OWASP compliance measures: CSRF for browser POSTs; rate limiting; CSP/HSTS/XFO/XCTO headers
- Security headers and policies: default secure headers; strict transport
- Vulnerability management: SCA; Dependabot; patch cadence

## 7. User Interface Specifications
### 7.1 Design System
- Visual design principles, brand, component library, responsive approach, accessibility standards: see lms-ux-style-guide.md

### 7.2 Design Foundations
#### 7.2.1 Color System
- Primary, secondary, accent, semantic colors, backgrounds, dark mode per style guide; ensure AA contrast.
#### 7.2.2 Typography
- Inter font scale and weights; responsive rules; text colors.
#### 7.2.3 Spacing & Layout
- 4pt base; spacing scale; container widths; grid specs.
#### 7.2.4 Interactive Elements
- Buttons, fields, animation timing/easing, hover/focus/active, loading/transition patterns.
#### 7.2.5 Component Specifications
- Tokens, variants, composition, state viz, platform adaptations.

### 7.3 User Experience Flows
- Key user journeys with wireframes/mockups: see lms-state-brief.md and lms-prototype-map.md
- Navigation structure: sidebar primary; product filters; search
- State management: React Query; transitions per motion guidance
- Error states and feedback: inline + banners; retries
- Loading and empty states: skeletons and instructive empties

## 8. Infrastructure & Deployment
### 8.1 Infrastructure Requirements
- Hosting environment: Infratel (primary), Paratus (secondary) — Zambia data residency
- Server requirements: Kubernetes cluster with HPA; node sizing per service SLOs
- Network architecture: private subnets; egress controls to TU/PMEC/Tingg; ingress via LB
- Storage: SQL Always On; MinIO replicated; Redis HA; Vault HA
- Messaging: RabbitMQ operators/helm charts; HA with quorum queues; DLQs

### 8.2 Deployment Strategy
- CI/CD pipeline: GitHub Actions build/test/scan/publish/deploy; Helm-based releases; smoke tests
- Environment management: dev, staging, prod; secrets via Vault; config via Helm values
- Deployment procedures: canary/blue-green; rollback on health degradation; feature flags for risky changes
- Configuration management: Terraform IaC; versioned infra; drift detection

Appendix — Project Structure Examples
- Backend
  - /services/{service-name}/
    - Api/, Application/, Domain/, Infrastructure/
  - /shared/{contracts, domain, security, infrastructure}
  - /deploy/{helm,k8s,terraform}
  - Tests: *.tests.csproj
- Frontend (Next.js)
  - /app, /features/{kyc, origination, gl, collections, reporting}, /shared, /styles/tokens.css
  - Tests: *.test.tsx
- Desktop
  - /src (MAUI/WPF), /sync, /crypto, /ui

References
- Architecture: file:///d:/Projects/LMS/project-archtecture.md
- Style Guide: file:///d:/Projects/LMS/lms-ux-style-guide.md
- States: file:///d:/Projects/LMS/lms-state-brief.md
- Prototype Map: file:///d:/Projects/LMS/lms-prototype-map.md
