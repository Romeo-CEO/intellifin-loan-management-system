# LMS Priority Design Flows (Approved Scope)

This document details the three priority design flows, aligned to the confirmed core loops and personas. It follows the specified structure and incorporates the UX guidance principles.

---

## Features List

### Originate & Disburse (Multi-Product Ready)
#### Multi-Product Origination – Product Selection and Dynamic Steps
- [] User Stories
  - [] As a Loan Officer, I want to select Payroll-based (MOU) or Business (Collateral-based) up front, so that the workflow, documents, and approvals adapt automatically.
  - [] As an Underwriter, I want product-specific underwriting rules and SLAs, so that decisions are consistent and fast.
  - [] As Compliance, I want immutable decision and approval trails with BoZ checks, so that audits are straightforward.
  - [] As a Branch Manager, I want visibility of per-stage queues and aging, so that I can reassign and meet SLAs.
##### UX/UI Considerations
- [] Core Experience
  - [] Screens: Start Application → Product Selection → Applicant Details → Documents → Underwriting → Approval → Disbursement Handoff.
  - [] States: Intake, Docs Pending, Underwriting, Approval, Ready to Disburse, Exception, Withdrawn.
  - [] Visual: product cards with concise copy; status pills; requirement chips (unchecked → checked on completion); SLA timers on pipeline cards.
  - [] Motion/IA/PD/VH: physics-based slide when advancing stages; progressive disclosure for advanced terms; clear typographic hierarchy and generous whitespace; content-first layouts.
- [] Step-by-step journey (screen-by-screen)
  1. Start Application
     - Minimal form: search existing client; if new → quick-create KYC stub; duplicate detection banner with merge.
     - Actions: Save draft, Continue.
  2. Product Selection
     - Two cards: Payroll (MOU) vs Business (Collateral). Cards show brief requirements and typical time-to-approval.
     - Selection spawns the corresponding Camunda 8 (Zeebe) process instance with product variable.
  3a. Payroll Path
     - Consent & PMEC Authorization: capture digital consent; show microcopy about data use; proceed only if signed.
- PMEC Verification: fetch salary, employer, and registration. Show read-only factsheet with last update time; retry on transient failures; queue if PMEC down.
     - Affordability & Limits: auto-calc based on policy and salary; interest cap validation with inline warnings; override requires dual-control.
     - Documents: auto-generated checklist (ID, payslips, MOU form, etc.); drag-and-drop; virus scan; encryption confirmation; retention tag.
     - Underwriting: factors panel (employment stability, DSR, prior history); decision proposal; notes capture.
     - Approval: stage-gated approvers; SLA countdown; Operate-linked incident hint if blocked.
  3b. Business (Collateral) Path
     - Collateral Intake: asset type selector; metadata templates; photos; ownership docs.
     - Valuation & Insurance: add valuation amount and agent; upload valuation report; insurance policy details and expiry reminders.
     - Security Docs: liens, registration numbers, legal docs; checklist status turns green on completion.
     - Underwriting: LTV, DSCR, exposure checks; exception ribbons for breaches; request for more info loop.
     - Approval: segregation-of-duties enforced; digital signatures; SLA.
  4. Disbursement Handoff
     - Summary: product, terms, schedule; final checks; handoff CTA to Finance.
     - System posts underwriting packet to GL pre-posting queue (read-only preview).
- [] Advanced Users & Edge Cases
  - [] PMEC downtime → enqueue Zeebe job; show banner with ETA/backoff; allow manual contact and notes.
  - [] Interest cap breach → block with rationale; show permissible range; route to exception approval.
  - [] KYC expired mid-flow → yellow banner; quick “Renew KYC” action (modal) without losing context.
  - [] Duplicate client → merge flow with diff viewer; logs preserved.
  - [] Offline capture (field) → save local draft; sync diff resolution UI on reconnect.

---

### Collect & Reconcile (Multi-Product Aware)
#### PMEC Exceptions Workbench
- [] User Stories
  - [] As Collections, I want a unified workbench for exceptions (missing, partial, over-deductions, mismatches), so that I resolve them fast and accurately.
  - [] As Finance, I want auto-allocation and GL postings with clear reconciliation, so that month-end closes smoothly.
  - [] As Compliance, I want full traceability of changes and communications, so that I pass audits.
##### UX/UI Considerations
- [] Core Experience
  - [] Screens: Exceptions List → Exception Detail (side panel) → Allocation Ledger → Bulk Actions → Communications → Resolution Summary.
  - [] States: New, Investigating, Waiting (Customer/PMEC), Resolved, Written-off, Reversed.
  - [] Visual: filter chips (Type, Product, Branch, Severity, Age); color-coded badges (e.g., red = Missing, amber = Partial); severity sort; row density optimized for scanning.
  - [] Motion/IA/PD/VH: smooth expand/collapse of detail panel; animated counters when filters change; progressive disclosure of advanced allocation tools; maintain focus order.
- [] Step-by-step journey (screen-by-screen)
  1. Exceptions List
     - Tabs or filters by Type: Missing deduction, Partial deduction, Over-deduction, Identity mismatch, Salary change, Stopped deduction.
     - KPI header: count by type, aging buckets, financial impact.
     - Bulk selection with action bar (retry, reallocate, write-off proposal, contact).
  2. Exception Detail Panel
     - Context: client, loan(s), expected vs actual schedule, PMEC payload and timestamps, prior resolutions.
     - Actions: retry queue (Zeebe command), manual allocation wizard, split across installments, write-off proposal (role-gated), add note, attach evidence.
  3. Allocation Ledger
     - Ledger rows: expected, actual, variance; proposed entries; preview GL postings; confirm & post.
     - Guardrails: cannot post if balance negative; show BoZ classification effects.
  4. Communications
     - Templates for SMS/letter; personalization; consent status; delivery receipts.
     - Log all outreach with timestamps; export evidence.
  5. Resolution Summary
     - Outcome: resolved method, GL references, notes; related incidents closed in Operate; audit link.
- [] Advanced Users & Edge Cases
  - [] PMEC file format change → validation error with schema diff; safe quarantine and vendor alert.
  - [] Salary decreased mid-cycle → pro-rata engine suggests revised allocations; confirm with preview.
  - [] Over-deduction → auto-flag refund process; repayment plan; GL holding account transitions.
  - [] Multi-loan clients → smart allocation by oldest overdue or by policy; manual override tracked.
  - [] Dispute flag → freeze further automation; route to compliance with SLA timers.

---

### Oversee & Report (Consolidated View)
#### Reporting Catalog with Product Filters (JasperReports)
- [] User Stories
  - [] As a CEO, I want a catalog of BoZ and operational reports with product filters, so that I can see consolidated and per-product views (e.g., PAR for Business-only).
  - [] As Compliance, I want preflight validation and versioned templates, so that submissions are correct and auditable.
  - [] As Management, I want scheduling, distribution, and history, so that reporting is timely and consistent.
##### UX/UI Considerations
- [] Core Experience
  - [] Screens: Report Catalog → Report Details & Parameters → Validation Preflight → Run Status → Results & Exports → Schedule Setup → History & Audit.
  - [] States: Ready, Parameterized, Validating, Running, Succeeded, Failed, Scheduled, Delivered.
  - [] Visual: product filter chips (All, Payroll, Business); branch/time filters; template version badge; read-replica badge for performance.
  - [] Motion/IA/PD/VH: parameters drawer with progressive disclosure (advanced filters hidden by default); skeleton loaders during run; success/failure toasts; clean hierarchy of results (KPIs → tables → footnotes).
- [] Step-by-step journey (screen-by-screen)
  1. Report Catalog
     - Sections: BoZ Prudential, Portfolio Analytics, Collections, Finance; search and favorites.
     - Each tile shows last run, typical duration, and product-compatibility tags.
  2. Report Details & Parameters
     - Required: date range, product type (All/Payroll/Business), branch, currency; optional advanced filters (risk band, LTV, delinquency bucket).
     - Save parameter presets; role-based defaults.
  3. Validation Preflight
     - Checks: data staleness (timestamp), GL period closure, template version, required inputs; show warnings/errors.
     - Proceed enabled only when critical checks pass; link to remediation.
  4. Run Status
     - Real-time progress; queue position if concurrent runs; estimate time; cancel token (role-guarded).
  5. Results & Exports
     - Views: table and chart; drill-through to records; export (XLS/PDF/CSV); watermark for draft vs submitted.
     - PAR views show both consolidated and product-specific metrics.
  6. Schedule Setup
     - Create schedules (daily/weekly/monthly); recipients; delivery channels (email/portal); product filter persisted.
     - Failure alerts to owners; retry policy.
  7. History & Audit
     - Run history with parameters, template version, user, duration, outcome; download prior outputs; immutable audit trail.
- [] Advanced Users & Edge Cases
  - [] Stale data → banner with option to re-run after overnight ETL; allow override with justification.
  - [] Long-running reports → background mode with notification; partial results preview.
  - [] Parameter drift between template versions → mapping helper and change log.
  - [] Sensitive reports → redaction presets and watermarks; access control prompts.

---

## Orchestration & Reporting Notes
- Camunda 8 (Zeebe) orchestrates origination stages and PMEC exception actions; SLAs and incidents monitored via Operate. User actions (approve, reassign, escalate) emit Zeebe commands; service tasks handle integrations (PMEC, GL).
- JasperReports serves all BoZ and operational reports with versioned templates and parameter auditing; runs target read replicas to protect OLTP workloads.

## Design Principles Applied
- Bold simplicity with intuitive navigation; consistent layout patterns across flows.
- Breathable whitespace, strategic accent colors, and typographic hierarchy to guide focus.
- Progressive disclosure to reduce cognitive load; power-user shortcuts preserved.
- Accessibility: WCAG AA contrast, keyboard navigation, screen reader labels, focus-visible.
- Motion choreography: physics-based transitions for spatial continuity; subtle micro-animations for feedback.
- Performance: skeletons, optimistic updates where safe, and clear status indicators.

## Acceptance Signals (MVP of these flows)
- Multi-Product Origination: A Loan Officer can complete either product path end-to-end, with adaptive checklists, PMEC verification (Payroll), and collateral/valuation (Business), visible in the pipeline with SLAs.
- PMEC Exceptions Workbench: Collections can triage, bulk act, and resolve exception types with GL postings preview and immutable audit logs.
- Reporting Catalog: CEO/Compliance can run BoZ reports with product filters, pass preflight validation, export, schedule, and review a full audit history.
