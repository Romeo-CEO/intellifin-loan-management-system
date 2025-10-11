# Story 1.31: BoZ Compliance Dashboards in Grafana

## Story Metadata

| Field | Value |
|-------|-------|
| **Story ID** | 1.31 |
| **Epic** | System Administration Control Plane Enhancement |
| **Phase** | Phase 6: Advanced Observability |
| **Sprint** | Sprint 11-12 |
| **Story Points** | 8 |
| **Estimated Effort** | 5-7 days |
| **Priority** | P1 (High - Compliance) |
| **Status** | 📋 Backlog |
| **Assigned To** | TBD |
| **Dependencies** | Grafana (Story 1.24), Centralized Audit (Story 1.14), Access Recertification (Story 1.20) |
| **Blocks** | BoZ audit preparation, Compliance reporting |

---

## User Story

**As a** Compliance Officer,  
**I want** Grafana dashboards showing Bank of Zambia compliance KPIs,  
**so that** I can monitor regulatory metrics and generate compliance reports for BoZ audits.

---

## Business Value

BoZ Compliance Dashboards provide critical regulatory and operational benefits:

- **Regulatory Compliance**: Meet Bank of Zambia reporting requirements for IT governance
- **Real-Time Monitoring**: Continuous visibility into compliance posture
- **Audit Readiness**: Pre-built reports ready for BoZ inspections
- **Risk Management**: Early detection of compliance violations
- **Operational Efficiency**: Automated compliance tracking reduces manual effort
- **Stakeholder Confidence**: Demonstrate regulatory adherence to board and regulators
- **Trend Analysis**: Historical compliance metrics for continuous improvement

This story is **critical** for regulatory compliance and audit preparation.

---

## Acceptance Criteria

### AC1: BoZ Compliance Overview Dashboard
**Given** Compliance officers need regulatory KPI visibility  
**When** accessing the BoZ Compliance Dashboard  
**Then**:
- Grafana dashboard `BoZ-Compliance-Overview.json` created
- Dashboard sections:
  - **Audit Metrics**: Event counts, coverage, completeness
  - **Access Control**: User access, violations, certifications
  - **Security Incidents**: Count, severity, resolution time
  - **Loan Classification**: Accuracy, overrides, trends
  - **System Availability**: Uptime, RTO/RPO compliance
- Dashboard layout: Clean, professional, BoZ-branded
- Navigation: Links to detailed drill-down dashboards
- Time range selector: Default 30 days, options for 7d, 30d, 90d, 180d, 1y
- Auto-refresh: Every 30 seconds for real-time monitoring

### AC2: Audit Event Metrics Panel
**Given** BoZ requires audit trail completeness evidence  
**When** viewing audit metrics  
**Then**:
- Panel displays:
  - **Total Audit Events**: Count for selected time range
  - **Daily Event Rate**: Line graph showing events per day
  - **Monthly Event Rate**: Bar chart showing events per month
  - **Annual Event Rate**: Single stat with year-over-year comparison
  - **Event Coverage**: Percentage of system actions audited (target: 100%)
  - **Audit Chain Integrity**: Status (intact/broken) with last validation timestamp
- Metrics sourced from Elasticsearch audit logs
- Panel includes threshold indicators:
  - Green: >95% coverage
  - Yellow: 90-95% coverage
  - Red: <90% coverage
- Drill-down: Click panel → View detailed audit log query in Elasticsearch

### AC3: Access Recertification Compliance Panel
**Given** BoZ requires quarterly access reviews  
**When** monitoring access recertification  
**Then**:
- Panel displays:
  - **Current Quarter Completion Rate**: Gauge showing % complete
  - **Overdue Certifications**: Count with owner list
  - **Certification Trend**: Line graph showing completion over time
  - **High-Risk Access Reviews**: Count of privileged accounts needing certification
  - **Next Certification Due**: Countdown timer to next quarter deadline
- Metrics sourced from Admin Service database (AccessCertifications table)
- Thresholds:
  - Green: 100% complete by quarter end
  - Yellow: 80-99% complete
  - Red: <80% complete or overdue
- Alert annotation: Red banner if overdue certifications exist

### AC4: Security Incident Tracking Panel
**Given** BoZ requires incident response documentation  
**When** viewing security incidents  
**Then**:
- Panel displays:
  - **Total Incidents**: Count for time range
  - **Incidents by Severity**: Pie chart (Critical, High, Medium, Low)
  - **Open Incidents**: Count with aging breakdown
  - **Mean Time To Resolution (MTTR)**: Average hours to resolve
  - **Incident Trend**: Line graph showing incidents over time
  - **Top Incident Categories**: Bar chart (unauthorized access, data breach, system failure)
- Metrics sourced from Incident Management system (Admin Service)
- Severity color coding:
  - Critical: Red
  - High: Orange
  - Medium: Yellow
  - Low: Green
- Drill-down: Click incident → View full incident details in Admin UI

### AC5: Loan Classification Accuracy Panel
**Given** BoZ requires loan portfolio quality monitoring  
**When** viewing loan classification metrics  
**Then**:
- Panel displays:
  - **Classification Accuracy**: Percentage of correctly classified loans
  - **Classification Distribution**: Pie chart showing breakdown:
    - Current (performing)
    - Special Mention
    - Substandard
    - Doubtful
    - Loss (non-performing)
  - **Reclassification Rate**: Percentage of loans reclassified monthly
  - **Manual Overrides**: Count with approval audit trail
  - **NPL Ratio**: Non-Performing Loan ratio (target: per BoZ guidelines)
- Metrics sourced from Loan Service database
- Thresholds per BoZ guidelines:
  - NPL Ratio <5%: Green
  - NPL Ratio 5-10%: Yellow
  - NPL Ratio >10%: Red
- Historical comparison: Current vs. previous quarter

### AC6: User Access Violations Panel
**Given** BoZ requires access control monitoring  
**When** tracking access violations  
**Then**:
- Panel displays:
  - **SoD Conflicts Detected**: Count of Segregation of Duties violations
  - **Unauthorized Access Attempts**: Failed authentication/authorization attempts
  - **Dormant Account Usage**: Count of inactive accounts with recent activity
  - **Privileged Access Usage**: Count of admin actions requiring justification
  - **Policy Violations**: Count by type (password policy, MFA bypass, etc.)
- Metrics sourced from Elasticsearch audit logs + Keycloak events
- Real-time alerts: Red annotation for active violations
- Drill-down: Click violation → View audit trail in Admin UI
- Remediation status: Count of resolved vs. unresolved violations

### AC7: Dashboard PDF Export
**Given** BoZ auditors require printed reports  
**When** exporting compliance dashboard  
**Then**:
- Export functionality:
  - Button in dashboard toolbar: "Export to PDF"
  - PDF generation includes:
    - All dashboard panels with current data
    - Time range header (e.g., "Compliance Report: Jan 1 - Jan 31, 2025")
    - BoZ logo and bank branding
    - Multi-page layout for readability
    - Generated timestamp and report ID
- PDF format:
  - A4 size, portrait orientation
  - High-resolution graphs (300 DPI)
  - Page numbers and table of contents
  - Footer: "Confidential - For BoZ Audit Only"
- PDF storage:
  - Automatically saved to MinIO bucket: `compliance-reports`
  - Retention: 7 years (per BoZ requirements)
  - WORM protection enabled
- Export audit trail: PDF generation logged with user, timestamp, report ID

### AC8: Role-Based Dashboard Access
**Given** Compliance data is sensitive  
**When** accessing compliance dashboards  
**Then**:
- Access control configured:
  - **Compliance Officers**: Full dashboard access, export permissions
  - **Auditors**: Read-only dashboard access, export permissions
  - **Executives (CEO, CTO)**: Read-only dashboard access
  - **Developers**: No access (restricted)
- Grafana folder permissions: `BoZ Compliance` folder restricted to authorized roles
- Audit logging: Dashboard views and exports logged in Elasticsearch
- SSO integration: Keycloak roles mapped to Grafana permissions
- Session timeout: 15 minutes inactivity for compliance users

### AC9: Compliance Metrics API
**Given** External systems need compliance data  
**When** querying compliance metrics programmatically  
**Then**:
- Admin Service API endpoints:
  - `GET /api/admin/compliance/audit-metrics`: Audit event statistics
  - `GET /api/admin/compliance/access-certifications`: Certification status
  - `GET /api/admin/compliance/incidents`: Security incident summary
  - `GET /api/admin/compliance/loan-classifications`: Loan portfolio metrics
  - `GET /api/admin/compliance/access-violations`: Access control violations
- API response format: JSON with metrics + metadata
- API authentication: OAuth 2.0 with compliance scope
- Rate limiting: 100 requests/minute per API key
- API audit: All requests logged with user, timestamp, query parameters

---

## Technical Implementation Details

### Architecture Reference

**PRD Sections**: Lines 1388-1416 (Story 1.30 in PRD, renumbered as 1.31), Phase 6 Overview  
**Architecture Sections**: Section 9 (Observability), Section 14 (Compliance), Section 6 (Security)  
**Requirements**: BoZ regulatory requirements, NFR19 (Audit completeness), NFR20 (Access certification)

### Technology Stack

- **Dashboard Platform**: Grafana 10.x
- **Data Sources**: Prometheus, Elasticsearch, PostgreSQL (Admin DB)
- **Metrics Export**: Admin Service (Prometheus exporter)
- **PDF Generation**: Grafana Image Renderer plugin
- **Authentication**: Keycloak SSO with Grafana OAuth integration
- **Storage**: MinIO (PDF report storage)

### Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                  Compliance Officers / Auditors                  │
│                      (Web Browser)                               │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
                  ┌──────────────────────┐
                  │      Grafana         │
                  │  (Compliance         │
                  │   Dashboards)        │
                  └──────────┬───────────┘
                             │
        ┌────────────────────┼────────────────────┬────────────────┐
        │                    │                    │                │
        ▼                    ▼                    ▼                ▼
┌───────────────┐    ┌──────────────┐    ┌──────────────┐  ┌──────────────┐
│  Prometheus   │    │Elasticsearch │    │ PostgreSQL   │  │  Keycloak    │
│  (Metrics)    │    │ (Audit Logs) │    │ (Admin DB)   │  │  (SSO)       │
└───────┬───────┘    └──────┬───────┘    └──────┬───────┘  └──────────────┘
        │                   │                   │
        ▼                   ▼                   ▼
┌────────────────────────────────────────────────────┐
│           Admin Service                            │
│  (Compliance Metrics Exporter)                     │
│  - Prometheus metrics                              │
│  - Audit event aggregation                         │
│  - Access certification tracking                   │
│  - Incident management                             │
└────────────────────────────────────────────────────┘

                   ┌──────────────────────┐
                   │  Grafana Image       │
                   │  Renderer            │
                   │  (PDF Export)        │
                   └──────────┬───────────┘
                              │
                              ▼
                   ┌──────────────────────┐
                   │      MinIO           │
                   │  (PDF Storage)       │
                   │  - compliance-reports│
                   │  - 7-year retention  │
                   │  - WORM enabled      │
                   └──────────────────────┘
```

### Grafana Dashboard JSON

```json
{
  "dashboard": {
    "title": "BoZ Compliance Overview",
    "tags": ["compliance", "boz", "regulatory"],
    "timezone": "Africa/Lusaka",
    "editable": false,
    "time": {
      "from": "now-30d",
      "to": "now"
    },
    "refresh": "30s",
    "panels": [
      {
        "id": 1,
        "title": "Audit Event Metrics",
        "type": "row",
        "gridPos": { "h": 1, "w": 24, "x": 0, "y": 0 }
      },
      {
        "id": 2,
        "title": "Total Audit Events",
        "type": "stat",
        "targets": [
          {
            "expr": "sum(increase(audit_events_total[30d]))",
            "legendFormat": "Total Events"
          }
        ],
        "fieldConfig": {
          "defaults": {
            "unit": "short",
            "thresholds": {
              "mode": "absolute",
              "steps": [
                { "value": 0, "color": "red" },
                { "value": 10000, "color": "yellow" },
                { "value": 50000, "color": "green" }
              ]
            }
          }
        },
        "gridPos": { "h": 8, "w": 6, "x": 0, "y": 1 }
      },
      {
        "id": 3,
        "title": "Daily Audit Event Rate",
        "type": "graph",
        "targets": [
          {
            "expr": "sum(rate(audit_events_total[1d]))",
            "legendFormat": "Events/Day"
          }
        ],
        "gridPos": { "h": 8, "w": 18, "x": 6, "y": 1 }
      },
      {
        "id": 4,
        "title": "Audit Chain Integrity",
        "type": "stat",
        "targets": [
          {
            "expr": "audit_chain_integrity_status",
            "legendFormat": "Chain Status"
          }
        ],
        "fieldConfig": {
          "defaults": {
            "mappings": [
              { "value": 1, "text": "Intact", "color": "green" },
              { "value": 0, "text": "BROKEN", "color": "red" }
            ]
          }
        },
        "gridPos": { "h": 4, "w": 6, "x": 0, "y": 9 }
      },
      {
        "id": 5,
        "title": "Audit Coverage",
        "type": "gauge",
        "targets": [
          {
            "expr": "(audit_events_total / system_actions_total) * 100",
            "legendFormat": "Coverage %"
          }
        ],
        "fieldConfig": {
          "defaults": {
            "unit": "percent",
            "min": 0,
            "max": 100,
            "thresholds": {
              "steps": [
                { "value": 0, "color": "red" },
                { "value": 90, "color": "yellow" },
                { "value": 95, "color": "green" }
              ]
            }
          }
        },
        "gridPos": { "h": 4, "w": 6, "x": 6, "y": 9 }
      },
      {
        "id": 6,
        "title": "Access Control & Certifications",
        "type": "row",
        "gridPos": { "h": 1, "w": 24, "x": 0, "y": 13 }
      },
      {
        "id": 7,
        "title": "Access Recertification Completion",
        "type": "gauge",
        "targets": [
          {
            "expr": "(access_certifications_completed / access_certifications_required) * 100",
            "legendFormat": "Completion %"
          }
        ],
        "fieldConfig": {
          "defaults": {
            "unit": "percent",
            "min": 0,
            "max": 100,
            "thresholds": {
              "steps": [
                { "value": 0, "color": "red" },
                { "value": 80, "color": "yellow" },
                { "value": 100, "color": "green" }
              ]
            }
          }
        },
        "gridPos": { "h": 8, "w": 8, "x": 0, "y": 14 }
      },
      {
        "id": 8,
        "title": "Overdue Certifications",
        "type": "stat",
        "targets": [
          {
            "expr": "access_certifications_overdue",
            "legendFormat": "Overdue"
          }
        ],
        "fieldConfig": {
          "defaults": {
            "unit": "short",
            "thresholds": {
              "steps": [
                { "value": 0, "color": "green" },
                { "value": 1, "color": "red" }
              ]
            }
          }
        },
        "gridPos": { "h": 8, "w": 8, "x": 8, "y": 14 }
      },
      {
        "id": 9,
        "title": "User Access Violations",
        "type": "stat",
        "targets": [
          {
            "expr": "sum(security_violations_total)",
            "legendFormat": "Total Violations"
          }
        ],
        "fieldConfig": {
          "defaults": {
            "unit": "short",
            "thresholds": {
              "steps": [
                { "value": 0, "color": "green" },
                { "value": 1, "color": "yellow" },
                { "value": 10, "color": "red" }
              ]
            }
          }
        },
        "gridPos": { "h": 8, "w": 8, "x": 16, "y": 14 }
      },
      {
        "id": 10,
        "title": "Security Incidents",
        "type": "row",
        "gridPos": { "h": 1, "w": 24, "x": 0, "y": 22 }
      },
      {
        "id": 11,
        "title": "Incidents by Severity",
        "type": "piechart",
        "targets": [
          {
            "expr": "sum by (severity) (security_incidents_total)",
            "legendFormat": "{{ severity }}"
          }
        ],
        "gridPos": { "h": 8, "w": 12, "x": 0, "y": 23 }
      },
      {
        "id": 12,
        "title": "Mean Time To Resolution (MTTR)",
        "type": "stat",
        "targets": [
          {
            "expr": "avg(security_incident_resolution_time_hours)",
            "legendFormat": "MTTR"
          }
        ],
        "fieldConfig": {
          "defaults": {
            "unit": "hours",
            "decimals": 1,
            "thresholds": {
              "steps": [
                { "value": 0, "color": "green" },
                { "value": 24, "color": "yellow" },
                { "value": 48, "color": "red" }
              ]
            }
          }
        },
        "gridPos": { "h": 8, "w": 6, "x": 12, "y": 23 }
      },
      {
        "id": 13,
        "title": "Open Incidents",
        "type": "stat",
        "targets": [
          {
            "expr": "sum(security_incidents_open)",
            "legendFormat": "Open"
          }
        ],
        "fieldConfig": {
          "defaults": {
            "unit": "short",
            "thresholds": {
              "steps": [
                { "value": 0, "color": "green" },
                { "value": 1, "color": "yellow" },
                { "value": 5, "color": "red" }
              ]
            }
          }
        },
        "gridPos": { "h": 8, "w": 6, "x": 18, "y": 23 }
      },
      {
        "id": 14,
        "title": "Loan Classification",
        "type": "row",
        "gridPos": { "h": 1, "w": 24, "x": 0, "y": 31 }
      },
      {
        "id": 15,
        "title": "Classification Distribution",
        "type": "piechart",
        "targets": [
          {
            "expr": "sum by (classification) (loan_classification_total)",
            "legendFormat": "{{ classification }}"
          }
        ],
        "gridPos": { "h": 8, "w": 12, "x": 0, "y": 32 }
      },
      {
        "id": 16,
        "title": "NPL Ratio",
        "type": "gauge",
        "targets": [
          {
            "expr": "(sum(loan_classification_total{classification=~\"Substandard|Doubtful|Loss\"}) / sum(loan_classification_total)) * 100",
            "legendFormat": "NPL %"
          }
        ],
        "fieldConfig": {
          "defaults": {
            "unit": "percent",
            "min": 0,
            "max": 20,
            "thresholds": {
              "steps": [
                { "value": 0, "color": "green" },
                { "value": 5, "color": "yellow" },
                { "value": 10, "color": "red" }
              ]
            }
          }
        },
        "gridPos": { "h": 8, "w": 6, "x": 12, "y": 32 }
      },
      {
        "id": 17,
        "title": "Classification Accuracy",
        "type": "stat",
        "targets": [
          {
            "expr": "loan_classification_accuracy_percent",
            "legendFormat": "Accuracy"
          }
        ],
        "fieldConfig": {
          "defaults": {
            "unit": "percent",
            "thresholds": {
              "steps": [
                { "value": 0, "color": "red" },
                { "value": 90, "color": "yellow" },
                { "value": 95, "color": "green" }
              ]
            }
          }
        },
        "gridPos": { "h": 8, "w": 6, "x": 18, "y": 32 }
      }
    ]
  }
}
```

### Prometheus Metrics Export (Admin Service)

```csharp
// Services/ComplianceMetricsExporter.cs
using Prometheus;
using Microsoft.Extensions.Hosting;

namespace IntelliFin.Admin.Services
{
    public class ComplianceMetricsExporter : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ComplianceMetricsExporter> _logger;

        // Prometheus metrics
        private static readonly Gauge AuditEventsTotal = Metrics.CreateGauge(
            "audit_events_total",
            "Total number of audit events",
            new GaugeConfiguration { LabelNames = new[] { "event_type" } });

        private static readonly Gauge AuditChainIntegrity = Metrics.CreateGauge(
            "audit_chain_integrity_status",
            "Audit chain integrity status (1=intact, 0=broken)");

        private static readonly Gauge AuditCoverage = Metrics.CreateGauge(
            "audit_coverage_percent",
            "Percentage of system actions audited");

        private static readonly Gauge AccessCertificationsCompleted = Metrics.CreateGauge(
            "access_certifications_completed",
            "Number of completed access certifications");

        private static readonly Gauge AccessCertificationsRequired = Metrics.CreateGauge(
            "access_certifications_required",
            "Number of required access certifications");

        private static readonly Gauge AccessCertificationsOverdue = Metrics.CreateGauge(
            "access_certifications_overdue",
            "Number of overdue access certifications");

        private static readonly Gauge SecurityIncidentsTotal = Metrics.CreateGauge(
            "security_incidents_total",
            "Total number of security incidents",
            new GaugeConfiguration { LabelNames = new[] { "severity" } });

        private static readonly Gauge SecurityIncidentsOpen = Metrics.CreateGauge(
            "security_incidents_open",
            "Number of open security incidents");

        private static readonly Gauge SecurityIncidentMTTR = Metrics.CreateGauge(
            "security_incident_resolution_time_hours",
            "Mean time to resolution for incidents in hours");

        private static readonly Gauge SecurityViolationsTotal = Metrics.CreateGauge(
            "security_violations_total",
            "Total number of security violations",
            new GaugeConfiguration { LabelNames = new[] { "violation_type" } });

        private static readonly Gauge LoanClassificationTotal = Metrics.CreateGauge(
            "loan_classification_total",
            "Number of loans by classification",
            new GaugeConfiguration { LabelNames = new[] { "classification" } });

        private static readonly Gauge LoanClassificationAccuracy = Metrics.CreateGauge(
            "loan_classification_accuracy_percent",
            "Loan classification accuracy percentage");

        public ComplianceMetricsExporter(
            IServiceProvider serviceProvider,
            ILogger<ComplianceMetricsExporter> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Updating compliance metrics at {Time}", DateTime.UtcNow);

                    using var scope = _serviceProvider.CreateScope();
                    var complianceService = scope.ServiceProvider.GetRequiredService<IComplianceMetricsService>();

                    await UpdateAuditMetricsAsync(complianceService, stoppingToken);
                    await UpdateAccessCertificationMetricsAsync(complianceService, stoppingToken);
                    await UpdateSecurityIncidentMetricsAsync(complianceService, stoppingToken);
                    await UpdateLoanClassificationMetricsAsync(complianceService, stoppingToken);

                    _logger.LogInformation("Compliance metrics updated successfully");

                    // Update every 30 seconds
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating compliance metrics");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }
        }

        private async Task UpdateAuditMetricsAsync(
            IComplianceMetricsService service,
            CancellationToken cancellationToken)
        {
            var metrics = await service.GetAuditMetricsAsync(cancellationToken);

            AuditEventsTotal.WithLabels("all").Set(metrics.TotalEvents);
            AuditChainIntegrity.Set(metrics.ChainIntact ? 1 : 0);
            AuditCoverage.Set(metrics.CoveragePercent);
        }

        private async Task UpdateAccessCertificationMetricsAsync(
            IComplianceMetricsService service,
            CancellationToken cancellationToken)
        {
            var metrics = await service.GetAccessCertificationMetricsAsync(cancellationToken);

            AccessCertificationsCompleted.Set(metrics.CompletedCount);
            AccessCertificationsRequired.Set(metrics.RequiredCount);
            AccessCertificationsOverdue.Set(metrics.OverdueCount);
        }

        private async Task UpdateSecurityIncidentMetricsAsync(
            IComplianceMetricsService service,
            CancellationToken cancellationToken)
        {
            var metrics = await service.GetSecurityIncidentMetricsAsync(cancellationToken);

            SecurityIncidentsTotal.WithLabels("critical").Set(metrics.CriticalCount);
            SecurityIncidentsTotal.WithLabels("high").Set(metrics.HighCount);
            SecurityIncidentsTotal.WithLabels("medium").Set(metrics.MediumCount);
            SecurityIncidentsTotal.WithLabels("low").Set(metrics.LowCount);
            SecurityIncidentsOpen.Set(metrics.OpenCount);
            SecurityIncidentMTTR.Set(metrics.MeanTimeToResolutionHours);

            SecurityViolationsTotal.WithLabels("sod_conflict").Set(metrics.SodConflicts);
            SecurityViolationsTotal.WithLabels("unauthorized_access").Set(metrics.UnauthorizedAccess);
            SecurityViolationsTotal.WithLabels("dormant_account").Set(metrics.DormantAccountUsage);
        }

        private async Task UpdateLoanClassificationMetricsAsync(
            IComplianceMetricsService service,
            CancellationToken cancellationToken)
        {
            var metrics = await service.GetLoanClassificationMetricsAsync(cancellationToken);

            LoanClassificationTotal.WithLabels("Current").Set(metrics.CurrentCount);
            LoanClassificationTotal.WithLabels("SpecialMention").Set(metrics.SpecialMentionCount);
            LoanClassificationTotal.WithLabels("Substandard").Set(metrics.SubstandardCount);
            LoanClassificationTotal.WithLabels("Doubtful").Set(metrics.DoubtfulCount);
            LoanClassificationTotal.WithLabels("Loss").Set(metrics.LossCount);
            LoanClassificationAccuracy.Set(metrics.AccuracyPercent);
        }
    }
}
```

### Compliance Metrics Service

```csharp
// Services/ComplianceMetricsService.cs
using Microsoft.EntityFrameworkCore;

namespace IntelliFin.Admin.Services
{
    public interface IComplianceMetricsService
    {
        Task<AuditMetrics> GetAuditMetricsAsync(CancellationToken cancellationToken);
        Task<AccessCertificationMetrics> GetAccessCertificationMetricsAsync(CancellationToken cancellationToken);
        Task<SecurityIncidentMetrics> GetSecurityIncidentMetricsAsync(CancellationToken cancellationToken);
        Task<LoanClassificationMetrics> GetLoanClassificationMetricsAsync(CancellationToken cancellationToken);
    }

    public class ComplianceMetricsService : IComplianceMetricsService
    {
        private readonly AdminDbContext _dbContext;
        private readonly IElasticsearchService _elasticsearchService;
        private readonly ILogger<ComplianceMetricsService> _logger;

        public ComplianceMetricsService(
            AdminDbContext dbContext,
            IElasticsearchService elasticsearchService,
            ILogger<ComplianceMetricsService> logger)
        {
            _dbContext = dbContext;
            _elasticsearchService = elasticsearchService;
            _logger = logger;
        }

        public async Task<AuditMetrics> GetAuditMetricsAsync(CancellationToken cancellationToken)
        {
            // Query Elasticsearch for audit event counts
            var totalEvents = await _elasticsearchService.CountAuditEventsAsync(
                DateTime.UtcNow.AddMonths(-1),
                DateTime.UtcNow,
                cancellationToken);

            // Check audit chain integrity
            var chainIntact = await _dbContext.AuditChainValidations
                .OrderByDescending(v => v.ValidatedAt)
                .Select(v => v.IsValid)
                .FirstOrDefaultAsync(cancellationToken);

            // Calculate audit coverage
            var totalSystemActions = await GetTotalSystemActionsAsync(cancellationToken);
            var coveragePercent = totalSystemActions > 0 
                ? (double)totalEvents / totalSystemActions * 100 
                : 100;

            return new AuditMetrics
            {
                TotalEvents = totalEvents,
                ChainIntact = chainIntact,
                CoveragePercent = Math.Min(coveragePercent, 100)
            };
        }

        public async Task<AccessCertificationMetrics> GetAccessCertificationMetricsAsync(
            CancellationToken cancellationToken)
        {
            var currentQuarter = GetCurrentQuarter();

            var completedCount = await _dbContext.AccessCertifications
                .Where(c => c.Quarter == currentQuarter && c.Status == "Completed")
                .CountAsync(cancellationToken);

            var requiredCount = await _dbContext.AccessCertifications
                .Where(c => c.Quarter == currentQuarter)
                .CountAsync(cancellationToken);

            var overdueCount = await _dbContext.AccessCertifications
                .Where(c => c.Quarter == currentQuarter 
                         && c.Status != "Completed" 
                         && c.DueDate < DateTime.UtcNow)
                .CountAsync(cancellationToken);

            return new AccessCertificationMetrics
            {
                CompletedCount = completedCount,
                RequiredCount = requiredCount,
                OverdueCount = overdueCount
            };
        }

        public async Task<SecurityIncidentMetrics> GetSecurityIncidentMetricsAsync(
            CancellationToken cancellationToken)
        {
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

            var incidents = await _dbContext.SecurityIncidents
                .Where(i => i.CreatedAt >= thirtyDaysAgo)
                .ToListAsync(cancellationToken);

            var openIncidents = await _dbContext.SecurityIncidents
                .Where(i => i.Status == "Open" || i.Status == "Investigating")
                .CountAsync(cancellationToken);

            var resolvedIncidents = await _dbContext.SecurityIncidents
                .Where(i => i.Status == "Resolved" && i.ResolvedAt.HasValue)
                .ToListAsync(cancellationToken);

            var mttr = resolvedIncidents.Any()
                ? resolvedIncidents.Average(i => (i.ResolvedAt.Value - i.CreatedAt).TotalHours)
                : 0;

            // Get violation counts
            var violations = await _elasticsearchService.GetSecurityViolationsAsync(
                thirtyDaysAgo,
                DateTime.UtcNow,
                cancellationToken);

            return new SecurityIncidentMetrics
            {
                CriticalCount = incidents.Count(i => i.Severity == "Critical"),
                HighCount = incidents.Count(i => i.Severity == "High"),
                MediumCount = incidents.Count(i => i.Severity == "Medium"),
                LowCount = incidents.Count(i => i.Severity == "Low"),
                OpenCount = openIncidents,
                MeanTimeToResolutionHours = mttr,
                SodConflicts = violations.SodConflicts,
                UnauthorizedAccess = violations.UnauthorizedAccess,
                DormantAccountUsage = violations.DormantAccounts
            };
        }

        public async Task<LoanClassificationMetrics> GetLoanClassificationMetricsAsync(
            CancellationToken cancellationToken)
        {
            // This would query the Loan Service database
            // For now, return mock data structure
            return new LoanClassificationMetrics
            {
                CurrentCount = 850,
                SpecialMentionCount = 75,
                SubstandardCount = 35,
                DoubtfulCount = 25,
                LossCount = 15,
                AccuracyPercent = 97.5
            };
        }

        private string GetCurrentQuarter()
        {
            var quarter = (DateTime.UtcNow.Month - 1) / 3 + 1;
            return $"{DateTime.UtcNow.Year}-Q{quarter}";
        }

        private async Task<long> GetTotalSystemActionsAsync(CancellationToken cancellationToken)
        {
            // Query system metrics for total actions performed
            // This would come from application metrics
            return 100000; // Placeholder
        }
    }

    public record AuditMetrics
    {
        public long TotalEvents { get; init; }
        public bool ChainIntact { get; init; }
        public double CoveragePercent { get; init; }
    }

    public record AccessCertificationMetrics
    {
        public int CompletedCount { get; init; }
        public int RequiredCount { get; init; }
        public int OverdueCount { get; init; }
    }

    public record SecurityIncidentMetrics
    {
        public int CriticalCount { get; init; }
        public int HighCount { get; init; }
        public int MediumCount { get; init; }
        public int LowCount { get; init; }
        public int OpenCount { get; init; }
        public double MeanTimeToResolutionHours { get; init; }
        public int SodConflicts { get; init; }
        public int UnauthorizedAccess { get; init; }
        public int DormantAccountUsage { get; init; }
    }

    public record LoanClassificationMetrics
    {
        public int CurrentCount { get; init; }
        public int SpecialMentionCount { get; init; }
        public int SubstandardCount { get; init; }
        public int DoubtfulCount { get; init; }
        public int LossCount { get; init; }
        public double AccuracyPercent { get; init; }
    }
}
```

### Admin Service API - Compliance Controller

```csharp
// Controllers/ComplianceController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace IntelliFin.Admin.Controllers
{
    [ApiController]
    [Route("api/admin/compliance")]
    [Authorize(Roles = "Compliance Officer,Auditor")]
    public class ComplianceController : ControllerBase
    {
        private readonly IComplianceMetricsService _complianceService;
        private readonly IPdfExportService _pdfExportService;
        private readonly ILogger<ComplianceController> _logger;

        public ComplianceController(
            IComplianceMetricsService complianceService,
            IPdfExportService pdfExportService,
            ILogger<ComplianceController> logger)
        {
            _complianceService = complianceService;
            _pdfExportService = pdfExportService;
            _logger = logger;
        }

        /// <summary>
        /// Get audit metrics for compliance dashboard
        /// </summary>
        [HttpGet("audit-metrics")]
        [ProducesResponseType(typeof(AuditMetrics), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAuditMetrics(
            CancellationToken cancellationToken)
        {
            var metrics = await _complianceService.GetAuditMetricsAsync(cancellationToken);
            return Ok(metrics);
        }

        /// <summary>
        /// Get access certification status
        /// </summary>
        [HttpGet("access-certifications")]
        [ProducesResponseType(typeof(AccessCertificationMetrics), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAccessCertifications(
            CancellationToken cancellationToken)
        {
            var metrics = await _complianceService.GetAccessCertificationMetricsAsync(cancellationToken);
            return Ok(metrics);
        }

        /// <summary>
        /// Get security incident summary
        /// </summary>
        [HttpGet("incidents")]
        [ProducesResponseType(typeof(SecurityIncidentMetrics), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetIncidents(
            CancellationToken cancellationToken)
        {
            var metrics = await _complianceService.GetSecurityIncidentMetricsAsync(cancellationToken);
            return Ok(metrics);
        }

        /// <summary>
        /// Get loan classification metrics
        /// </summary>
        [HttpGet("loan-classifications")]
        [ProducesResponseType(typeof(LoanClassificationMetrics), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetLoanClassifications(
            CancellationToken cancellationToken)
        {
            var metrics = await _complianceService.GetLoanClassificationMetricsAsync(cancellationToken);
            return Ok(metrics);
        }

        /// <summary>
        /// Export compliance dashboard to PDF
        /// </summary>
        [HttpPost("export-pdf")]
        [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
        public async Task<IActionResult> ExportDashboardToPdf(
            [FromBody] DashboardExportRequest request,
            CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            _logger.LogInformation(
                "Compliance dashboard PDF export requested: User={UserId}, TimeRange={TimeRange}",
                userId, request.TimeRange);

            var pdf = await _pdfExportService.ExportDashboardAsync(
                "BoZ Compliance Overview",
                request.TimeRange,
                cancellationToken);

            // Store PDF in MinIO
            await _pdfExportService.StorePdfAsync(
                pdf,
                "compliance-reports",
                $"compliance-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.pdf",
                cancellationToken);

            return File(pdf, "application/pdf", $"compliance-report-{DateTime.UtcNow:yyyyMMdd}.pdf");
        }
    }
}
```

---

## Integration Verification

### IV1: Dashboard Metrics Accuracy
**Verification Steps**:
1. Query metrics from Grafana dashboard
2. Query same metrics directly from source databases
3. Compare values for accuracy
4. Validate calculations (percentages, averages)
5. Test with different time ranges

**Success Criteria**:
- Metrics match source data within 1% margin
- All calculations correct
- Time range filtering works accurately

### IV2: PDF Export Quality
**Verification Steps**:
1. Export dashboard to PDF
2. Verify all panels included
3. Check image resolution (300 DPI)
4. Validate multi-page layout
5. Test with different time ranges

**Success Criteria**:
- PDF includes all dashboard panels
- Images clear and readable
- Professional formatting maintained
- PDF generation time <30 seconds

### IV3: Dashboard Performance
**Verification Steps**:
1. Load dashboard with 30-day time range
2. Measure load time
3. Test auto-refresh functionality
4. Load with 1-year time range
5. Check memory usage

**Success Criteria**:
- Dashboard loads in <5 seconds (30-day range)
- Auto-refresh works without performance degradation
- 1-year range loads in <15 seconds
- Memory usage reasonable (<500MB)

---

## Testing Strategy

### Unit Tests

```csharp
[Fact]
public async Task GetAuditMetrics_ReturnsCorrectCoverage()
{
    // Arrange
    var service = CreateComplianceMetricsService();

    // Act
    var metrics = await service.GetAuditMetricsAsync(CancellationToken.None);

    // Assert
    Assert.InRange(metrics.CoveragePercent, 0, 100);
    Assert.True(metrics.TotalEvents > 0);
}

[Fact]
public async Task GetAccessCertificationMetrics_CalculatesOverdueCorrectly()
{
    // Arrange
    var service = CreateComplianceMetricsService();

    // Act
    var metrics = await service.GetAccessCertificationMetricsAsync(CancellationToken.None);

    // Assert
    Assert.True(metrics.OverdueCount >= 0);
    Assert.True(metrics.CompletedCount <= metrics.RequiredCount);
}
```

### Integration Tests

```bash
#!/bin/bash
# test-compliance-dashboard.sh

echo "Testing compliance dashboard..."

# Test 1: Verify dashboard exists
echo "Test 1: Check dashboard exists"
DASHBOARD=$(curl -s -X GET "$GRAFANA_URL/api/dashboards/uid/boz-compliance" \
  -H "Authorization: Bearer $GRAFANA_TOKEN")

if echo "$DASHBOARD" | jq -e '.dashboard' > /dev/null; then
  echo "✅ Dashboard found"
else
  echo "❌ Dashboard not found"
  exit 1
fi

# Test 2: Test compliance API
echo "Test 2: Test compliance API endpoints"
AUDIT_METRICS=$(curl -s -X GET "$ADMIN_API/compliance/audit-metrics" \
  -H "Authorization: Bearer $TOKEN")

TOTAL_EVENTS=$(echo "$AUDIT_METRICS" | jq -r '.totalEvents')
echo "Total audit events: $TOTAL_EVENTS"

if [ "$TOTAL_EVENTS" -gt 0 ]; then
  echo "✅ Audit metrics retrieved"
else
  echo "❌ No audit metrics"
  exit 1
fi

# Test 3: Test PDF export
echo "Test 3: Test PDF export"
PDF_EXPORT=$(curl -s -X POST "$ADMIN_API/compliance/export-pdf" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"timeRange": "30d"}' \
  -o compliance-test.pdf)

if [ -f compliance-test.pdf ] && [ -s compliance-test.pdf ]; then
  echo "✅ PDF export successful"
  rm compliance-test.pdf
else
  echo "❌ PDF export failed"
  exit 1
fi

echo "All tests passed! ✅"
```

---

## Risks and Mitigation

| Risk | Impact | Probability | Mitigation |
|------|---------|-------------|------------|
| Metric data delay | Dashboard shows outdated compliance status | Medium | 30-second refresh rate. Display last updated timestamp. Alert if data >5 minutes old. |
| PDF export failure | Unable to generate BoZ audit reports | Low | Fallback to manual screenshot. Test PDF generation weekly. Monitoring alerts. |
| Dashboard performance degradation | Slow load times impact usability | Medium | Optimize Prometheus queries. Implement query caching. Use recording rules for complex calculations. |
| Access control misconfiguration | Unauthorized access to compliance data | Low | Regular RBAC audits. Principle of least privilege. Grafana permission reviews. |
| Metric calculation errors | Incorrect compliance KPIs reported | Medium | Automated validation tests. Cross-reference with source data. Manual spot checks. |

---

## Definition of Done

- [ ] Grafana dashboard created and deployed
- [ ] All 17 panels implemented with correct queries
- [ ] Prometheus metrics exporter running
- [ ] Compliance metrics service implemented
- [ ] Admin Service API endpoints complete
- [ ] PDF export functionality tested
- [ ] Role-based access control configured
- [ ] Dashboard performance tested (<5s load time)
- [ ] Integration tests: Metrics accuracy, PDF export
- [ ] Documentation: Dashboard guide, metric definitions
- [ ] User training: Compliance officers trained on dashboard usage

---

## Related Documentation

### PRD References
- **Lines 1388-1416**: Story 1.30 detailed requirements (renumbered as 1.31)
- **Lines 1386-1541**: Phase 6 (Advanced Observability) overview
- **NFR19**: Audit completeness
- **NFR20**: Access certification quarterly

### Architecture References
- **Section 9**: Observability
- **Section 14**: Compliance
- **Section 6**: Security

### External Documentation
- [Grafana Dashboard Best Practices](https://grafana.com/docs/grafana/latest/dashboards/)
- [Prometheus Metrics Design](https://prometheus.io/docs/practices/naming/)
- [Bank of Zambia IT Governance Guidelines](https://www.boz.zm/)

---

## Notes for Development Team

### Pre-Implementation Checklist
- [ ] Grafana instance deployed and accessible
- [ ] Prometheus configured and scraping metrics
- [ ] Elasticsearch cluster healthy with audit logs
- [ ] Admin Service database schema includes compliance tables
- [ ] Grafana Image Renderer plugin installed
- [ ] MinIO bucket for PDF storage created
- [ ] Keycloak roles mapped to Grafana permissions

### Post-Implementation Handoff
- [ ] Train compliance team on dashboard navigation
- [ ] Document metric definitions and calculations
- [ ] Create user guide for PDF export
- [ ] Schedule quarterly dashboard reviews
- [ ] Establish process for BoZ audit report generation
- [ ] Set up monitoring for dashboard availability
- [ ] Document troubleshooting procedures

### Technical Debt / Future Enhancements
- [ ] Implement drill-down dashboards for each panel
- [ ] Add predictive analytics for compliance trends
- [ ] Integrate with BoZ reporting portal (if available)
- [ ] Implement automated compliance report scheduling
- [ ] Add mobile-responsive dashboard version
- [ ] Create compliance chatbot for Q&A
- [ ] Implement real-time compliance scoring
- [ ] Add AI-powered compliance recommendations

---

**Story Created**: 2025-10-11  
**Last Updated**: 2025-10-11  
**Next Story**: [Story 1.32: Cost-Performance Monitoring Dashboards](./story-1.32-cost-performance-dashboards.md)
