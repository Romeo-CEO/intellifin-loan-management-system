# LMS State Brief — Comprehensive Screen & State Snapshots

This comprehensive brief enumerates key screens and states across all LMS domains, describing UI/UX, behaviors, motion, and accessibility. Colors and components reference the LMS UX/UI Style Guide.

Legend: Colors → Primary Deep Teal #0B6B62, Primary Ink #0D1B2A, Secondary Slate #334155, Secondary Blue #2563EB, Soft Teal #14B8A6, Success #16A34A, Warning #D97706, Error #DC2626, Info #0EA5E9, BG Base #F8FAFC, Surface #FFFFFF, Border #E5E7EB.

---

## BoZ-Compliant Client Management & KYC

### Screen: Client Profile
#### State: Empty (New Client)
- Clean Surface card with H1 title (Primary Ink). Empty-state illustration, concise copy, CTA "Create Client" (Primary button, Deep Teal). Secondary "Search Existing" (Outlined).
- Progressive disclosure: advanced fields hidden; basic ID/contact fields visible.
- Animation: card fade-in (200ms), button hover darken; skeletons for profile sections while loading.
- Accessibility: focus ring Soft Teal, labels tied to inputs, 40dp tap targets.

#### State: Draft In-Progress
- Multi-step header (Identity → Address → Employment → BoZ Declarations) with progress bar (Soft Teal). Incomplete chips in Neutral.
- Inline validation in Error Red beneath fields, microcopy with fix guidance.
- Autosave toast (Info Blue), 4s dismiss; “Continue later” tertiary action.

#### State: Approved
- Status chip Success (bg #DCFCE7) near name. Audit trail link (Secondary Blue). Summary panel with key KYC dates; renewal due badge (Accent Gold) if <30 days.

### Screen: Document Manager (Client)
#### State: Uploading
- Dropzone with dashed Border; drag-over glow (Soft Teal). File rows with progress bars, virus scan spinner.
- Motion: progress bar ease-out; on complete → checkmark morph (150ms).

#### State: Encrypted & Tagged
- Rows show lock icon, retention tag (Neutral chip). Filters by type/date. Hover reveals actions: view, replace, revoke access.

#### State: Error/Unsupported
- Row turns Error background tint; inline fix actions (compress/convert); help link.

### Screen: Compliance Timeline
#### State: Normal
- Timeline chips (Surface Subtle) with dates; icons: submitted/approved/renewed. Next milestone highlighted Soft Teal.

#### State: Due Soon / Overdue
- Badge Warning (amber) with countdown; Overdue turns Error with banner and “Renew KYC” inline CTA.

---

## Credit Assessment (First-Time Applicants Only)

### Screen: Credit Pull Console
#### State: Eligibility Check
- Consent checkbox, clear microcopy, Link to policy (Secondary Blue). Primary CTA "Request TransUnion Report".

#### State: Running
- Skeleton for scorecard; spinner with copy "Fetching report…"; cancellable (outlined danger) if >10s.
- Motion: shimmer on skeletons, progress indicator slide.

#### State: Result Returned
- Score dial (Teal/Amber/Red thresholds). Factors list with contributions. Risk band badge. Buttons: "Proceed to Underwriting", "Manual Review".

#### State: Outage Fallback
- Info banner (Info Blue) describing queueing; queued chip with ETA. Retry button; manual route with justification modal.

### Screen: Scoring Configuration (Role-Gated)
#### State: Read-Only
- Factors list in table; lock icon; request change flow (tertiary).

#### State: Edit (Draft Changes)
- Sliders with value chips; preview impact sparkline; save draft; publish (dual-control confirmation).

---

## Multi-Product Loan Origination (Back-Office)

### Screen: Start Application
#### State: Choose Product
- Two product cards: Payroll (MOU) and Business (Collateral). Each card shows requirements and typical SLA (Accent Gold small chip). Select → Primary button "Start".
- Motion: card elevation on hover, selection check animates in.

### Screen: Payroll Path — PEMC Verification
#### State: Consent Required
- Summary of data usage; checkbox; CTA enabled on consent. Colors: Secondary Slate text, Primary button.

#### State: Verifying (Live)
- Factsheet panel skeletons; stepper showing "Contacting PEMC → Parsing → Verifying" with Soft Teal progress.

#### State: Verified
- Read-only factsheet: employer, salary, registration status; timestamp (Caption). Error-resilient banner if data >30 days old.

#### State: PEMC Down / Retry Queue
- Warning banner with backoff; queued state chip; secondary CTA "Manual Proceed (Dual-Control)".

### Screen: Business Path — Collateral Intake
#### State: Add Asset
- Form: asset type select, metadata template, photo uploader (grid). Validation for required docs. KPI chip showing current LTV.

#### State: Valuation & Insurance
- Inputs for value, agent; upload valuation report; insurance policy date with expiry indicator; reminders setup.

### Screen: Origination Pipeline Board
#### State: Default
- Columns: Intake, Docs Pending, Underwriting, Approval, Ready to Disburse. Cards show client, product, SLA timer (Accent Gold), blockers badges.

#### State: Dense Mode (Ops)
- Compact 40dp rows; keyboard navigation hints; quick actions on hover.

---

## Traditional Disbursement & Payment Processing

### Screen: Disbursement Batch
#### State: Selection
- Table of approved loans; filters; totals footer. Primary CTA "Create Batch". Disabled if policy checks fail.

#### State: Generated File / API Submit
- Tabs: Bank File, API. Show payload preview (monospace). Confirm & Send (Primary). Secondary "Download".

#### State: Sent → Settled
- Status chips per item (Queued, Sent, Settled, Failed). Reconcile button appears post-settlement.

### Screen: Reconciliation
#### State: Matching
- Two-pane view: bank statement vs expected; auto-match (Success), mismatches (Warning). Actions: match, split, write-off proposal.

#### State: Error
- Banner (Error) with failure reason; retry with debounce; log link.

### Screen: Mobile Money (Phase 2 via Tingg)
#### State: Disabled (Future)
- Informational card with roadmap; toggle disabled; link to integration guide.

---

## Multi-Branch Architecture (Future-Ready)

### Screen: Branch Switcher & Permissions
#### State: Default
- Top bar branch selector (pill). Content scoped badges; filters persist per branch.

#### State: Delegated Access
- Banner shows time-bound delegated access; revoke button; audit link.

### Screen: Branch KPIs
#### State: Consolidated
- Tiles per branch; heatmap; variance vs targets; product filters.

#### State: Drill-down
- Table with branch details; sticky header; export pack.

---

## BoZ-Compliant Integrated General Ledger

### Screen: Posting Rules & BoZ Mapping
#### State: View
- Matrix of transaction → BoZ category; search, filter. Locked badges; change request CTA.

#### State: Edit Proposal
- Inline editors with validation; preview of impacted accounts; submit for approval (dual-control), audit note required.

### Screen: GL Balances & Ratios
#### State: Live
- KPI cards (Teal positive, Error for breaches). Trend lines; date picker; branch/product filters.

#### State: Period Close
- Banner showing period status; lock/unlock with confirmation; checklist of close tasks with progress.

---

## Collections & Recovery (Government Employee Focused)

### Screen: PEMC Cycle Overview
#### State: Before Run
- Next schedule with countdown (Accent Gold). Generate list CTA; last run summary.

#### State: In Progress
- Steps tracker; throughput gauge; exceptions count chip updating live.

### Screen: Exceptions Workbench
#### State: List
- Filter chips (Type, Product, Branch, Severity, Age). Rows with variance amount, status chips.

#### State: Detail
- Side panel with expected vs actual schedule; actions: retry (queue), manual allocation wizard, refund flow.

#### State: Resolved
- Summary with GL refs; green success banner; print/export.

---

## Collateral Management & Documentation

### Screen: Collateral Registry
#### State: Default
- Table of assets with valuation, insurance, liens; status chips for expiring.

#### State: Add/Edit Asset
- Form with field groups; photo grid; documents area; save and validate; warnings for missing insurance.

### Screen: Release Workflow
#### State: Pending Approvals
- Approval chain list; each step with avatar, SLA; approve/reject; audit log panel.

#### State: Released
- Confirmation, document pack download; asset status changes to Released with date.

---

## Comprehensive Reporting Suite

### Screen: Report Catalog (JasperReports)
#### State: Default
- Tiles grouped (BoZ, Portfolio, Collections, Finance). Each shows last run, duration, product compatibility.

#### State: Search/Filter
- Query field; chips for Product (All/Payroll/Business), Branch, Period. Live filter animation.

### Screen: Report Run
#### State: Parameterize
- Form with required params; presets dropdown; validation preflight panel showing staleness, template version.

#### State: Running
- Progress with estimate; background option; notification on completion.

#### State: Results
- Table/chart with drill-through; export buttons (XLS/PDF/CSV); watermarked until submitted.

#### State: Scheduled
- List of schedules; recipients, channels; failure alerts setup.

---

## CEO Secure Offline Command Center

### Screen: Dashboard (Offline Capable)
#### State: Fresh
- KPIs: PAR, cash, daily flows, branch performance, compliance, PEMC rates, top delinquents. Product filter chips.

#### State: Stale
- Stale badge; subtle desaturation; "Sync Now" primary CTA.

#### State: Syncing
- Progress bar; list of synced modules; cancel disabled; conflict count chip.

#### State: Conflict
- Conflict banner; diff viewer; accept mine/theirs; audit notes.

### Screen: Offline Origination & Voucher
#### State: Dual-Control Required
- Two-step auth UI; secure entry; time-bound session indicator.

#### State: Voucher Generated
- Encrypted voucher code display; print/share disabled by default; export with role gate.

---

## Data Protection & Zambian Compliance

### Screen: Security Center
#### State: Overview
- Residency badge; encryption status; key rotation schedule; recent access logs table.

#### State: Alerts
- DLP alerts panel; filter by severity; acknowledge/assign actions.

### Screen: Subject Rights Requests
#### State: New Request
- Wizard: identify subject, verify identity, select action (access/correct/delete); SLA timer.

#### State: In Progress
- Checklist of steps; generated documents; reviewer assignment; communications log.

#### State: Completed / On Hold
- Immutable summary; legal hold badge; export evidence pack.

---

---

## Notifications & Communications System

### Screen: Notification Center
#### State: Empty (No Notifications)
- Clean Surface card with centered illustration (bell icon in Neutral Gray)
- H1 title "No Notifications" (Primary Ink), body text "You're all caught up!" (Secondary Slate)
- Subtle animation: gentle pulse on bell icon (2s cycle)
- Accessibility: screen reader announces "No new notifications"

#### State: Unread Notifications
- Header with unread count badge (Error Red background, white text)
- Filter chips: [All] [Workflow] [System] [Compliance] with active state (Soft Teal background)
- Notification cards with left border accent (Soft Teal for workflow, Warning Amber for system, Error Red for compliance)
- Hover state: subtle elevation (shadow Y=4, Blur=12)
- Animation: new notifications slide in from top (300ms ease-out)

#### State: Notification Detail
- Side panel (420px width) with notification content
- Action buttons: [Mark as Read] [View Details] [Archive]
- Related notifications section at bottom
- Close button (X) in top-right with 40dp touch target

### Screen: Toast Notifications
#### State: Success Toast
- Fixed position bottom-right, 320px width
- Background: Success Green (#16A34A), white text
- Icon: checkmark circle, auto-dismiss after 4s
- Animation: slide in from right (200ms), fade out (150ms)
- Pause on hover, resume on mouse leave

#### State: Warning Toast
- Background: Warning Amber (#D97706), white text
- Icon: warning triangle, manual dismiss required
- Action button: [Dismiss] (outlined white)
- Animation: slide in with bounce (spring animation)

#### State: Error Toast
- Background: Error Red (#DC2626), white text
- Icon: X circle, manual dismiss required
- Action buttons: [Dismiss] [Retry] (if applicable)
- Animation: slide in with shake (attention-grabbing)

---

## Loan Origination Workflow

### Screen: Application Dashboard
#### State: New Application
- Hero section with "Start New Application" (Primary Deep Teal button, 48dp height)
- Recent applications table with status chips and SLA timers
- Quick stats cards: Today's Applications (24), Pending Review (8), Approved (12)
- Animation: stats cards count up on load (1s duration)

#### State: Product Selection
- Two product cards side-by-side: Payroll (MOU) and Business (Collateral)
- Each card: icon, title, description, requirements list, typical SLA chip (Accent Gold)
- Selection state: card elevation +2, Soft Teal left border (4dp)
- Animation: card selection with scale transform (1.05x) and color transition (200ms)

#### State: Application Form (Payroll Path)
- Multi-step header with progress indicator (Soft Teal progress bar)
- Step indicators: [Client Info] [PMEC Verification] [Documents] [Review]
- Form sections with progressive disclosure
- Save draft button (Secondary Blue) with autosave indicator (Info Blue dot)
- Animation: step transitions slide left (320ms ease-out)

#### State: PMEC Verification
- Loading state: skeleton cards for employer info, salary details
- Success state: read-only factsheet with green checkmarks
- Error state: retry button with backoff timer display
- Animation: shimmer effect on skeletons (1200ms linear)

#### State: Document Upload
- Drag-and-drop zone with dashed border (Border color)
- File list with progress bars and status indicators
- Virus scan spinner with "Scanning..." text
- Animation: progress bars ease-out, checkmark morph on completion (150ms)

### Screen: Underwriting Console
#### State: Application Review
- Split view: application details (left) and decision panel (right)
- Risk factors panel with color-coded indicators
- Decision buttons: [Approve] [Reject] [Request More Info]
- Animation: risk factors animate in sequentially (100ms stagger)

#### State: Decision Made
- Confirmation modal with decision summary
- Audit trail section showing all actions taken
- Next steps guidance with action buttons
- Animation: modal slide in from center (280ms spring)

---

## Collections & Recovery Management

### Screen: Collections Dashboard
#### State: Portfolio Overview
- KPI cards: PAR 30 (2.3%), PAR 90 (1.1%), Collection Rate (98.5%)
- Color coding: green for good metrics, amber for attention, red for critical
- Collections pipeline: [Current] [1-30 DPD] [31-90 DPD] [90+ DPD]
- Animation: KPI cards pulse on data refresh

#### State: Overdue Loans List
- Table with filters: [All] [Government] [Business] [Branch]
- Status chips: Current (Success Green), Overdue (Warning Amber), Critical (Error Red)
- Action buttons: [Send Reminder] [Schedule Call] [Escalate]
- Animation: row highlight on hover, smooth transitions

#### State: Collection Call Scheduler
- Calendar view with available time slots
- Customer details panel with contact information
- Call outcome form with predefined options
- Animation: time slot selection with Soft Teal highlight

### Screen: PMEC Exceptions Workbench
#### State: Exceptions List
- Filter chips: [Missing] [Partial] [Over-deduction] [Identity Mismatch]
- Exception cards with severity indicators (color-coded left border)
- Bulk action bar appears on selection
- Animation: filter changes with smooth content transition

#### State: Exception Detail
- Side panel with exception details and resolution history
- Allocation wizard with step-by-step guidance
- GL impact preview with before/after comparison
- Animation: panel slide in from right (300ms)

---

## Financial Accounting & GL

### Screen: General Ledger Dashboard
#### State: Live Balances
- Account balance cards with trend indicators
- Color coding: positive (Success Green), negative (Error Red)
- Real-time update indicators (pulsing dot)
- Animation: balance changes with subtle flash effect

#### State: Transaction Entry
- Double-entry form with debit/credit validation
- Account selector with search and categorization
- Real-time balance impact preview
- Animation: validation errors slide in below fields

#### State: BoZ Reporting
- Report generation wizard with parameter selection
- Validation checklist with green checkmarks
- Export options: [PDF] [Excel] [Submit to BoZ]
- Animation: progress bar during report generation

### Screen: Reconciliation Console
#### State: Bank Statement Import
- File upload area with format validation
- Import progress with row-by-row processing
- Error summary with fix suggestions
- Animation: progress bar with percentage display

#### State: Matching Process
- Two-pane view: bank statement vs expected transactions
- Auto-match indicators (Success Green checkmarks)
- Manual match interface for exceptions
- Animation: matched items fade to green (200ms)

---

## Credit Assessment & Risk Management

### Screen: Credit Pull Console
#### State: Eligibility Check
- Consent form with clear policy links (Secondary Blue)
- Customer information summary
- "Request TransUnion Report" button (Primary Deep Teal)
- Animation: form validation with inline feedback

#### State: Credit Report Processing
- Loading skeleton with progress indicator
- "Fetching report..." text with spinner
- Cancellation option after 10 seconds
- Animation: shimmer effect on skeleton (1200ms)

#### State: Credit Score Display
- Score dial with color zones (Green/Amber/Red)
- Risk factors breakdown with contribution percentages
- Decision recommendations with rationale
- Animation: score dial fills progressively (1s duration)

### Screen: Risk Assessment Framework
#### State: Risk Model Configuration
- Factor sliders with real-time impact preview
- Risk band definitions with color coding
- Save draft functionality with version control
- Animation: slider changes with immediate preview update

---

## Treasury & Branch Operations

### Screen: Cash Management
#### State: Daily Cash Position
- Cash flow chart with inflows/outflows
- Branch-wise cash allocation
- Replenishment alerts with action buttons
- Animation: chart updates with smooth transitions

#### State: End-of-Day Procedures
- Checklist with completion status
- Reconciliation summary with variance alerts
- GL posting confirmation
- Animation: checklist items check off sequentially

### Screen: Branch Performance
#### State: Branch Dashboard
- Performance tiles with KPI metrics
- Heatmap showing branch comparison
- Drill-down capability to detailed views
- Animation: tile hover effects with elevation

---

## System Administration & Security

### Screen: User Management
#### State: User List
- Table with role badges and status indicators
- Bulk action toolbar for user operations
- Search and filter capabilities
- Animation: row selection with Soft Teal highlight

#### State: Role Assignment
- Role matrix with permission checkboxes
- Impact preview showing affected users
- Audit trail for permission changes
- Animation: checkbox changes with ripple effect

### Screen: Security Center
#### State: Security Overview
- Security score dashboard with trend indicators
- Recent security events timeline
- Compliance status badges
- Animation: security score updates with color transitions

#### State: Access Logs
- Filterable log table with search functionality
- Risk indicators for unusual access patterns
- Export capabilities for audit purposes
- Animation: log entries fade in on load

---

## Offline Operations & CEO Command Center

### Screen: Offline Dashboard
#### State: Connected
- Real-time sync indicator (Success Green dot)
- Last sync timestamp
- Online features fully available
- Animation: sync indicator pulse (2s cycle)

#### State: Offline Mode
- Offline banner with sync status
- Limited functionality with clear indicators
- Queue of pending operations
- Animation: offline banner slide down (200ms)

#### State: Sync Conflict Resolution
- Conflict resolution interface with diff viewer
- Accept/reject options for each conflict
- Audit trail for resolution decisions
- Animation: conflict items highlight on selection

### Screen: Voucher Generation
#### State: Voucher Creation
- Dual-control authentication interface
- Voucher details form with validation
- Security confirmation with biometric option
- Animation: form sections reveal progressively

#### State: Voucher Issued
- Encrypted voucher code display
- Print/share options (role-gated)
- Expiration timer with countdown
- Animation: voucher code appears with typewriter effect

---

## Cross-Cutting Behaviors
- Loading: skeletons within 100ms; shimmer 1200ms; keep layout stable.
- Errors: inline messages near field; page banners for systemic issues; retry affordances.
- Focus & Keyboard: visible Soft Teal ring; logical tab order; shortcuts in dense tables.
- Responsive: tablet-first (≥768px), fluid grid; sticky headers/first columns in data-heavy screens.
- Motion: prefer transform/opacity; page transitions 320–360ms; respect reduced motion.
- Microcopy: concise, directive; avoid jargon; always suggest next best action.
- Notifications: real-time updates via SignalR; toast notifications for immediate feedback.
- Accessibility: WCAG 2.1 AA compliance; screen reader support; keyboard navigation.
- Performance: lazy loading; progressive enhancement; offline-first architecture.
- Security: role-based access; audit trails; encrypted communications.
