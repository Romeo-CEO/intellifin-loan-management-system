# Story 1.30: BoZ Compliance Dashboards in Grafana

## Story Metadata

| Field | Value |
|-------|-------|
| **Story ID** | 1.30 |
| **Epic** | System Administration Control Plane Enhancement |
| **Phase** | Phase 5: Observability & Infrastructure |
| **Sprint** | Sprint 9-10 |
| **Story Points** | 13 |
| **Estimated Effort** | 8-12 days |
| **Priority** | P1 (High) |
| **Status** | 📋 Backlog |
| **Assigned To** | TBD |
| **Dependencies** | Azure subscription, Prometheus (Story 1.24) |
| **Blocks** | Budget planning, Cost optimization initiatives |

---

## User Story

**As a** FinOps Engineer and System Administrator,  
**I want** comprehensive infrastructure cost tracking with budget alerts and optimization recommendations,  
**so that** I can control cloud spending, allocate costs accurately, and optimize resource utilization.

---

## Business Value

Infrastructure cost tracking provides critical financial and operational benefits:

- **Cost Visibility**: Real-time visibility into cloud spending across services and environments
- **Budget Control**: Automated alerts when spending approaches or exceeds budgets
- **Cost Allocation**: Accurate cost attribution to teams, projects, and business units
- **Optimization Opportunities**: Identify underutilized resources and savings opportunities
- **Financial Planning**: Historical trends for accurate forecasting and budgeting
- **Compliance**: Meet BoZ requirements for IT expenditure tracking and reporting
- **ROI Analysis**: Measure infrastructure costs against business value delivered
- **Waste Reduction**: Detect and eliminate orphaned resources and idle infrastructure

This story is **critical** for financial accountability and operational efficiency.

---

## Acceptance Criteria

### AC1: Cloud Cost Data Collection
**Given** Infrastructure runs on Azure  
**When** collecting cost data  
**Then**:
- Azure Cost Management API integration configured
- Daily cost data retrieval automated (midnight UTC)
- Cost data collected for:
  - Virtual Machines (VMs)
  - Azure Kubernetes Service (AKS)
  - Storage accounts (Blob, File, Queue, Table)
  - Databases (PostgreSQL, SQL Server, Cosmos DB)
  - Networking (Load Balancer, VPN Gateway, Bandwidth)
  - Azure Monitor, Log Analytics
  - Key Vault, Container Registry
- Cost data granularity: Per resource, per service, per resource group
- Historical data retention: 2 years
- Data stored in PostgreSQL (Admin DB)

### AC2: Resource Tagging Strategy
**Given** Resources need cost allocation  
**When** tagging infrastructure resources  
**Then**:
- Mandatory tags defined:
  - `Environment`: dev, staging, production
  - `CostCenter`: department/team code (e.g., IT-001, ENG-002)
  - `Project`: project identifier (e.g., intellifin-lms)
  - `Owner`: team or individual email
  - `Service`: service name (identity-service, loan-service, etc.)
  - `ManagedBy`: terraform, manual, helm
- Tag enforcement via Azure Policy
- Untagged resources detected and reported daily
- Terraform modules auto-apply standard tags
- Tag compliance dashboard in Admin UI
- Tag validation in CI/CD pipelines

### AC3: Cost Allocation and Reporting
**Given** Costs need attribution to teams/services  
**When** allocating infrastructure costs  
**Then**:
- Cost allocation by dimensions:
  - By environment (dev, staging, production)
  - By cost center (teams)
  - By service (microservices)
  - By resource type (compute, storage, network, database)
- Allocation rules:
  - Shared resources (AKS, monitoring) split proportionally
  - Dedicated resources (databases) allocated directly
  - Network egress costs split by traffic volume
- Cost reports generated:
  - Daily cost summary (email to stakeholders)
  - Weekly cost trends (Slack notification)
  - Monthly cost breakdown (PDF export)
  - Quarterly cost review (executive dashboard)
- Reports include:
  - Total spend
  - Cost by category
  - Month-over-month change (%)
  - Budget utilization (%)
  - Top 10 most expensive resources

### AC4: Budget Management and Alerts
**Given** Teams have allocated budgets  
**When** monitoring spending against budgets  
**Then**:
- Budgets configured per:
  - Environment (e.g., production: $10k/month)
  - Cost center (e.g., Engineering: $15k/month)
  - Project (e.g., LMS: $20k/month)
- Budget thresholds:
  - 50% utilization: INFO alert (email)
  - 80% utilization: WARNING alert (email + Slack)
  - 100% utilization: CRITICAL alert (email + Slack + PagerDuty)
  - 120% utilization: EMERGENCY alert (all channels + SMS)
- Alert recipients:
  - Budget owner (mandatory)
  - Finance team (>80% utilization)
  - CTO (>100% utilization)
- Budget forecasting:
  - Projected month-end spend based on current trend
  - Estimated overage amount
  - Forecast accuracy tracked (actual vs. predicted)
- Budget adjustments via Admin UI (approval workflow)

### AC5: Cost Anomaly Detection
**Given** Unexpected cost spikes need detection  
**When** monitoring cost patterns  
**Then**:
- Anomaly detection algorithm:
  - Machine learning model trained on historical data
  - Detects cost spikes >2 standard deviations
  - Analyzes daily, weekly, monthly trends
- Anomaly alerts include:
  - Anomalous resource (VM, storage, etc.)
  - Current cost vs. expected cost
  - Percentage increase
  - Possible root cause (e.g., runaway autoscaling)
- Alert triggered within 4 hours of anomaly
- Admin UI displays anomaly timeline
- Anomaly investigation workflow:
  - Review resource metrics (CPU, memory, network)
  - Check recent deployments/changes
  - Correlate with incidents
  - Document root cause

### AC6: Cost Optimization Recommendations
**Given** Infrastructure has optimization opportunities  
**When** analyzing resource utilization  
**Then**:
- Optimization recommendations generated:
  - **Rightsizing**: VMs with low CPU (<20%) or memory (<30%) utilization
  - **Reserved Instances**: Steady-state workloads (>730 hours/month uptime)
  - **Spot Instances**: Fault-tolerant workloads (dev/test environments)
  - **Storage Tiering**: Infrequently accessed data (move to cool/archive tier)
  - **Orphaned Resources**: Unattached disks, unused load balancers, idle IPs
  - **Idle Resources**: VMs stopped >7 days, databases with no connections
- Recommendations include:
  - Current cost
  - Potential savings ($/month)
  - Confidence level (high, medium, low)
  - Implementation effort (low, medium, high)
  - Risk assessment
- Recommendations ranked by savings potential
- Admin UI displays recommendations with "Apply" button
- Recommendation history tracked (accepted, rejected, implemented)

### AC7: Kubernetes Cost Allocation
**Given** Multiple services run in AKS  
**When** allocating Kubernetes costs  
**Then**:
- Kubernetes resource usage tracked:
  - CPU requests/limits per pod
  - Memory requests/limits per pod
  - Storage (PVC) usage per namespace
  - Network ingress/egress per service
- Cost allocation methodology:
  - AKS cluster cost split by namespace
  - Proportional allocation based on resource requests
  - Storage costs allocated to PVC owners
  - Network costs split by traffic volume
- Namespace cost dashboard:
  - Cost per namespace (daily, weekly, monthly)
  - Cost per workload (deployment, statefulset)
  - Top 10 most expensive pods
  - Resource efficiency (requests vs. actual usage)
- Kubernetes cost optimization:
  - Over-provisioned pods (requests >> usage)
  - Under-utilized nodes (total usage <50%)
  - Unused PVCs (no attached pods)

### AC8: Admin UI Cost Management
**Given** Stakeholders need cost visibility  
**When** accessing cost data via Admin UI  
**Then**:
- Cost dashboard displays:
  - Current month spend (total, by service)
  - Budget utilization progress bars
  - Cost trend chart (last 6 months)
  - Top 5 cost drivers
  - Anomaly alerts (if any)
  - Optimization recommendations (top 5)
- Cost drill-down:
  - Click service → view resource breakdown
  - Click resource → view cost timeline
  - Filter by date range, environment, cost center
- Cost comparison:
  - Month-over-month comparison
  - Year-over-year comparison
  - Budget vs. actual
- Export capabilities:
  - CSV export (detailed cost data)
  - PDF report (executive summary)
  - JSON API (programmatic access)
- Role-based access:
  - Admins: View all costs, manage budgets
  - Finance: View all costs, export reports
  - Team leads: View own team costs
  - Developers: View own service costs

### AC9: Cost Forecasting and Planning
**Given** Finance needs cost projections  
**When** forecasting future costs  
**Then**:
- Forecasting models:
  - Linear regression (trend-based)
  - Seasonal ARIMA (accounts for seasonality)
  - Machine learning (Prophet algorithm)
- Forecast horizons:
  - 30-day forecast (daily granularity)
  - 90-day forecast (weekly granularity)
  - 12-month forecast (monthly granularity)
- Forecast inputs:
  - Historical cost data (last 12 months)
  - Known upcoming changes (new deployments, migrations)
  - Business growth projections (user growth, transaction volume)
- Forecast outputs:
  - Predicted spend (min, max, expected)
  - Confidence intervals (95%)
  - Cost breakdown by category
- Scenario planning:
  - "What-if" analysis (e.g., 20% user growth)
  - Cost impact of new features
  - Optimization savings projections
- Forecast accuracy tracked and displayed

---

## Technical Implementation Details

### Architecture Reference

**PRD Sections**: Lines 1358-1382 (Story 1.30), Phase 5 Overview  
**Architecture Sections**: Section 13 (Cost Management), Section 11 (Cloud Infrastructure), Section 5 (FinOps)  
**Requirements**: NFR22 (Infrastructure cost tracking), NFR23 (Budget alerts)

### Technology Stack

- **Cloud Provider**: Azure Cost Management API
- **Data Storage**: PostgreSQL (Admin DB)
- **Data Processing**: .NET background services
- **Visualization**: Grafana, Admin UI (React)
- **Machine Learning**: ML.NET (anomaly detection, forecasting)
- **Notifications**: Email, Slack, PagerDuty
- **Reporting**: PDF generation (QuestPDF)

### Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                    Azure Cost Management API                     │
│           (Daily cost data for all resources)                    │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
              ┌──────────────────────────┐
              │  Cost Data Collector     │
              │  (Background Service)    │
              │  - Fetch cost data       │
              │  - Process & transform   │
              └──────────┬───────────────┘
                         │
                         ▼
              ┌──────────────────────────┐
              │    PostgreSQL            │
              │  (Cost data storage)     │
              │  - CostRecords           │
              │  - Budgets               │
              │  - Recommendations       │
              └──────────┬───────────────┘
                         │
        ┌────────────────┼────────────────┬───────────────┐
        │                │                │               │
        ▼                ▼                ▼               ▼
┌──────────────┐  ┌──────────┐  ┌──────────────┐  ┌────────────┐
│   Budget     │  │ Anomaly  │  │Optimization  │  │  Forecast  │
│  Monitoring  │  │ Detection│  │Recommender   │  │  Engine    │
│  Service     │  │ (ML.NET) │  │  Service     │  │  (ML.NET)  │
└──────┬───────┘  └────┬─────┘  └──────┬───────┘  └──────┬─────┘
       │               │               │                  │
       └───────────────┴───────────────┴──────────────────┘
                              │
                              ▼
                   ┌──────────────────────┐
                   │  Notification        │
                   │  Service             │
                   │  (Email, Slack, PD)  │
                   └──────────────────────┘

                   ┌──────────────────────┐
                   │  Admin UI            │
                   │  (Cost Dashboard)    │
                   └──────────────────────┘

                   ┌──────────────────────┐
                   │  Grafana             │
                   │  (Cost Metrics)      │
                   └──────────────────────┘
```

### Database Schema

```sql
-- Admin Service Database

CREATE TABLE CostRecords (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    RecordDate DATE NOT NULL,
    
    -- Azure resource identification
    SubscriptionId NVARCHAR(100) NOT NULL,
    ResourceGroupName NVARCHAR(200) NOT NULL,
    ResourceId NVARCHAR(500) NOT NULL,
    ResourceName NVARCHAR(200) NOT NULL,
    ResourceType NVARCHAR(100) NOT NULL,  -- Microsoft.Compute/virtualMachines, etc.
    ResourceLocation NVARCHAR(50) NOT NULL,
    
    -- Cost details
    CostUSD DECIMAL(18, 4) NOT NULL,
    Currency NVARCHAR(3) NOT NULL DEFAULT 'USD',
    BillingPeriod NVARCHAR(20) NOT NULL,  -- 2025-01
    
    -- Resource tags
    Environment NVARCHAR(50),
    CostCenter NVARCHAR(50),
    Project NVARCHAR(100),
    Owner NVARCHAR(200),
    Service NVARCHAR(100),
    ManagedBy NVARCHAR(50),
    
    -- Metadata
    MeterCategory NVARCHAR(100),  -- Compute, Storage, Networking, etc.
    MeterSubCategory NVARCHAR(100),
    MeterName NVARCHAR(200),
    UnitOfMeasure NVARCHAR(50),
    Quantity DECIMAL(18, 4),
    
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    INDEX IX_RecordDate (RecordDate DESC),
    INDEX IX_ResourceId (ResourceId),
    INDEX IX_ResourceType (ResourceType),
    INDEX IX_Environment (Environment),
    INDEX IX_CostCenter (CostCenter),
    INDEX IX_Service (Service),
    INDEX IX_BillingPeriod (BillingPeriod)
);

CREATE TABLE Budgets (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    BudgetId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() UNIQUE,
    
    BudgetName NVARCHAR(200) NOT NULL,
    BudgetType NVARCHAR(50) NOT NULL,  -- Environment, CostCenter, Project
    Scope NVARCHAR(200) NOT NULL,  -- production, IT-001, intellifin-lms, etc.
    
    MonthlyBudgetUSD DECIMAL(18, 2) NOT NULL,
    Currency NVARCHAR(3) NOT NULL DEFAULT 'USD',
    
    -- Alert thresholds
    InfoThresholdPercent INT NOT NULL DEFAULT 50,
    WarningThresholdPercent INT NOT NULL DEFAULT 80,
    CriticalThresholdPercent INT NOT NULL DEFAULT 100,
    EmergencyThresholdPercent INT NOT NULL DEFAULT 120,
    
    -- Alert recipients
    OwnerEmail NVARCHAR(200) NOT NULL,
    AlertEmails NVARCHAR(MAX),  -- JSON array
    
    IsActive BIT NOT NULL DEFAULT 1,
    
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy NVARCHAR(100) NOT NULL,
    UpdatedAt DATETIME2,
    UpdatedBy NVARCHAR(100),
    
    INDEX IX_BudgetType_Scope (BudgetType, Scope),
    INDEX IX_IsActive (IsActive)
);

CREATE TABLE BudgetAlerts (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    AlertId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() UNIQUE,
    
    BudgetId UNIQUEIDENTIFIER NOT NULL,
    
    AlertLevel NVARCHAR(20) NOT NULL,  -- INFO, WARNING, CRITICAL, EMERGENCY
    AlertDate DATE NOT NULL,
    
    CurrentSpendUSD DECIMAL(18, 2) NOT NULL,
    BudgetAmountUSD DECIMAL(18, 2) NOT NULL,
    UtilizationPercent DECIMAL(5, 2) NOT NULL,
    ProjectedSpendUSD DECIMAL(18, 2),
    
    AlertSent BIT NOT NULL DEFAULT 0,
    AlertSentAt DATETIME2,
    
    FOREIGN KEY (BudgetId) REFERENCES Budgets(BudgetId),
    INDEX IX_BudgetId (BudgetId),
    INDEX IX_AlertDate (AlertDate DESC),
    INDEX IX_AlertSent (AlertSent)
);

CREATE TABLE CostAnomalies (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    AnomalyId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() UNIQUE,
    
    DetectedDate DATE NOT NULL,
    ResourceId NVARCHAR(500) NOT NULL,
    ResourceName NVARCHAR(200) NOT NULL,
    ResourceType NVARCHAR(100) NOT NULL,
    
    CurrentCostUSD DECIMAL(18, 4) NOT NULL,
    ExpectedCostUSD DECIMAL(18, 4) NOT NULL,
    CostDifferenceUSD DECIMAL(18, 4) NOT NULL,
    PercentageIncrease DECIMAL(5, 2) NOT NULL,
    
    AnomalyScore DECIMAL(5, 4) NOT NULL,  -- 0.0 to 1.0
    ConfidenceLevel NVARCHAR(20) NOT NULL,  -- Low, Medium, High
    
    PossibleRootCause NVARCHAR(500),
    
    Status NVARCHAR(50) NOT NULL DEFAULT 'New',  -- New, Investigating, Resolved, False Positive
    ResolvedAt DATETIME2,
    ResolvedBy NVARCHAR(100),
    ResolutionNotes NVARCHAR(MAX),
    
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    INDEX IX_DetectedDate (DetectedDate DESC),
    INDEX IX_ResourceId (ResourceId),
    INDEX IX_Status (Status)
);

CREATE TABLE OptimizationRecommendations (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    RecommendationId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() UNIQUE,
    
    RecommendationType NVARCHAR(50) NOT NULL,  -- Rightsizing, ReservedInstance, SpotInstance, StorageTiering, Orphaned, Idle
    
    ResourceId NVARCHAR(500) NOT NULL,
    ResourceName NVARCHAR(200) NOT NULL,
    ResourceType NVARCHAR(100) NOT NULL,
    
    CurrentCostUSD DECIMAL(18, 2) NOT NULL,
    ProjectedCostUSD DECIMAL(18, 2) NOT NULL,
    MonthlySavingsUSD DECIMAL(18, 2) NOT NULL,
    AnnualSavingsUSD DECIMAL(18, 2) NOT NULL,
    
    ConfidenceLevel NVARCHAR(20) NOT NULL,  -- Low, Medium, High
    ImplementationEffort NVARCHAR(20) NOT NULL,  -- Low, Medium, High
    RiskLevel NVARCHAR(20) NOT NULL,  -- Low, Medium, High
    
    RecommendationDetails NVARCHAR(MAX),  -- JSON with specific actions
    
    Status NVARCHAR(50) NOT NULL DEFAULT 'New',  -- New, Accepted, Rejected, Implemented
    StatusChangedAt DATETIME2,
    StatusChangedBy NVARCHAR(100),
    StatusNotes NVARCHAR(MAX),
    
    GeneratedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ExpiresAt DATETIME2,  -- Recommendations expire after 30 days
    
    INDEX IX_RecommendationType (RecommendationType),
    INDEX IX_ResourceId (ResourceId),
    INDEX IX_Status (Status),
    INDEX IX_MonthlySavings (MonthlySavingsUSD DESC)
);

CREATE TABLE CostForecasts (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    ForecastId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() UNIQUE,
    
    ForecastDate DATE NOT NULL,
    ForecastHorizon INT NOT NULL,  -- Days into future (30, 90, 365)
    
    Scope NVARCHAR(200) NOT NULL,  -- Total, Environment, CostCenter, Service
    ScopeValue NVARCHAR(200),  -- production, IT-001, loan-service
    
    ForecastModel NVARCHAR(50) NOT NULL,  -- LinearRegression, ARIMA, Prophet
    
    PredictedCostUSD DECIMAL(18, 2) NOT NULL,
    LowerBoundUSD DECIMAL(18, 2) NOT NULL,
    UpperBoundUSD DECIMAL(18, 2) NOT NULL,
    ConfidenceInterval DECIMAL(3, 2) NOT NULL,  -- 0.95 for 95%
    
    GeneratedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    INDEX IX_ForecastDate (ForecastDate DESC),
    INDEX IX_Scope (Scope, ScopeValue)
);

-- View for current month costs by service
CREATE VIEW vw_CurrentMonthCostsByService AS
SELECT 
    Service,
    Environment,
    SUM(CostUSD) AS TotalCostUSD,
    COUNT(DISTINCT ResourceId) AS ResourceCount,
    MIN(RecordDate) AS FirstRecordDate,
    MAX(RecordDate) AS LastRecordDate
FROM CostRecords
WHERE BillingPeriod = FORMAT(GETUTCDATE(), 'yyyy-MM')
GROUP BY Service, Environment
ORDER BY TotalCostUSD DESC;
GO

-- View for budget utilization
CREATE VIEW vw_BudgetUtilization AS
SELECT 
    b.BudgetId,
    b.BudgetName,
    b.BudgetType,
    b.Scope,
    b.MonthlyBudgetUSD,
    COALESCE(SUM(cr.CostUSD), 0) AS CurrentSpendUSD,
    (COALESCE(SUM(cr.CostUSD), 0) / b.MonthlyBudgetUSD * 100) AS UtilizationPercent,
    b.MonthlyBudgetUSD - COALESCE(SUM(cr.CostUSD), 0) AS RemainingBudgetUSD
FROM Budgets b
LEFT JOIN CostRecords cr ON 
    (b.BudgetType = 'Environment' AND cr.Environment = b.Scope) OR
    (b.BudgetType = 'CostCenter' AND cr.CostCenter = b.Scope) OR
    (b.BudgetType = 'Project' AND cr.Project = b.Scope)
WHERE b.IsActive = 1
  AND cr.BillingPeriod = FORMAT(GETUTCDATE(), 'yyyy-MM')
GROUP BY b.BudgetId, b.BudgetName, b.BudgetType, b.Scope, b.MonthlyBudgetUSD;
GO
```

### Cost Data Collector Service

```csharp
// Services/CostDataCollectorService.cs
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.CostManagement;
using Microsoft.Extensions.Hosting;

namespace IntelliFin.Admin.Services
{
    public class CostDataCollectorService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CostDataCollectorService> _logger;
        private readonly IConfiguration _config;

        public CostDataCollectorService(
            IServiceProvider serviceProvider,
            ILogger<CostDataCollectorService> logger,
            IConfiguration config)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Starting cost data collection at {Time}", DateTime.UtcNow);

                    using var scope = _serviceProvider.CreateScope();
                    var costService = scope.ServiceProvider.GetRequiredService<ICostManagementService>();

                    // Collect yesterday's cost data (Azure has ~24h delay)
                    var targetDate = DateTime.UtcNow.Date.AddDays(-1);
                    await costService.CollectDailyCostDataAsync(targetDate, stoppingToken);

                    _logger.LogInformation("Cost data collection completed successfully");

                    // Run once daily at midnight UTC
                    var now = DateTime.UtcNow;
                    var tomorrow = now.Date.AddDays(1);
                    var delay = tomorrow - now;
                    
                    _logger.LogInformation("Next collection scheduled in {Delay}", delay);
                    await Task.Delay(delay, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in cost data collection");
                    
                    // Retry after 1 hour on failure
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
            }
        }
    }

    public interface ICostManagementService
    {
        Task CollectDailyCostDataAsync(DateTime date, CancellationToken cancellationToken);
        Task<List<CostRecord>> GetCostsByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken);
        Task<List<ServiceCostSummary>> GetCostsByServiceAsync(string environment, DateTime startDate, DateTime endDate, CancellationToken cancellationToken);
    }

    public class CostManagementService : ICostManagementService
    {
        private readonly ArmClient _armClient;
        private readonly AdminDbContext _dbContext;
        private readonly ILogger<CostManagementService> _logger;
        private readonly IConfiguration _config;

        public CostManagementService(
            AdminDbContext dbContext,
            ILogger<CostManagementService> logger,
            IConfiguration config)
        {
            _dbContext = dbContext;
            _logger = logger;
            _config = config;

            var credential = new DefaultAzureCredential();
            _armClient = new ArmClient(credential);
        }

        public async Task CollectDailyCostDataAsync(DateTime date, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Collecting cost data for date: {Date}", date);

            var subscriptionId = _config["Azure:SubscriptionId"];
            var subscription = _armClient.GetSubscriptionResource(
                new Azure.Core.ResourceIdentifier($"/subscriptions/{subscriptionId}"));

            // Query Azure Cost Management API
            var queryRequest = new
            {
                type = "Usage",
                timeframe = "Custom",
                timePeriod = new
                {
                    from = date.ToString("yyyy-MM-dd"),
                    to = date.ToString("yyyy-MM-dd")
                },
                dataset = new
                {
                    granularity = "Daily",
                    aggregation = new Dictionary<string, object>
                    {
                        ["totalCost"] = new { name = "Cost", function = "Sum" }
                    },
                    grouping = new[]
                    {
                        new { type = "Dimension", name = "ResourceId" },
                        new { type = "Dimension", name = "ResourceType" },
                        new { type = "Dimension", name = "ResourceLocation" },
                        new { type = "Dimension", name = "MeterCategory" },
                        new { type = "TagKey", name = "Environment" },
                        new { type = "TagKey", name = "CostCenter" },
                        new { type = "TagKey", name = "Service" }
                    }
                }
            };

            // Execute cost query
            var costData = await QueryCostManagementApiAsync(subscription, queryRequest, cancellationToken);

            // Transform and save to database
            var costRecords = costData.Select(item => new CostRecord
            {
                RecordDate = date,
                SubscriptionId = subscriptionId,
                ResourceGroupName = ExtractResourceGroup(item.ResourceId),
                ResourceId = item.ResourceId,
                ResourceName = ExtractResourceName(item.ResourceId),
                ResourceType = item.ResourceType,
                ResourceLocation = item.ResourceLocation,
                CostUSD = item.Cost,
                Currency = "USD",
                BillingPeriod = date.ToString("yyyy-MM"),
                Environment = item.Tags?.GetValueOrDefault("Environment"),
                CostCenter = item.Tags?.GetValueOrDefault("CostCenter"),
                Project = item.Tags?.GetValueOrDefault("Project"),
                Owner = item.Tags?.GetValueOrDefault("Owner"),
                Service = item.Tags?.GetValueOrDefault("Service"),
                ManagedBy = item.Tags?.GetValueOrDefault("ManagedBy"),
                MeterCategory = item.MeterCategory,
                MeterSubCategory = item.MeterSubCategory,
                MeterName = item.MeterName,
                UnitOfMeasure = item.UnitOfMeasure,
                Quantity = item.Quantity
            }).ToList();

            await _dbContext.CostRecords.AddRangeAsync(costRecords, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Saved {Count} cost records for date {Date}", costRecords.Count, date);
        }

        private string ExtractResourceGroup(string resourceId)
        {
            // /subscriptions/{sub}/resourceGroups/{rg}/providers/...
            var parts = resourceId.Split('/');
            var rgIndex = Array.IndexOf(parts, "resourceGroups");
            return rgIndex >= 0 && rgIndex + 1 < parts.Length ? parts[rgIndex + 1] : "Unknown";
        }

        private string ExtractResourceName(string resourceId)
        {
            var parts = resourceId.Split('/');
            return parts.Length > 0 ? parts[^1] : "Unknown";
        }
    }
}
```

### Budget Monitoring Service

```csharp
// Services/BudgetMonitoringService.cs
using Microsoft.Extensions.Hosting;

namespace IntelliFin.Admin.Services
{
    public class BudgetMonitoringService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BudgetMonitoringService> _logger;

        public BudgetMonitoringService(
            IServiceProvider serviceProvider,
            ILogger<BudgetMonitoringService> logger)
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
                    _logger.LogInformation("Starting budget monitoring at {Time}", DateTime.UtcNow);

                    using var scope = _serviceProvider.CreateScope();
                    var budgetService = scope.ServiceProvider.GetRequiredService<IBudgetService>();

                    await budgetService.CheckBudgetUtilizationAsync(stoppingToken);

                    _logger.LogInformation("Budget monitoring completed successfully");

                    // Run every 4 hours
                    await Task.Delay(TimeSpan.FromHours(4), stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in budget monitoring");
                    await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
                }
            }
        }
    }

    public interface IBudgetService
    {
        Task CheckBudgetUtilizationAsync(CancellationToken cancellationToken);
        Task<List<BudgetUtilization>> GetBudgetUtilizationAsync(CancellationToken cancellationToken);
        Task<Budget> CreateBudgetAsync(BudgetCreateRequest request, string userId, CancellationToken cancellationToken);
    }

    public class BudgetService : IBudgetService
    {
        private readonly AdminDbContext _dbContext;
        private readonly INotificationService _notificationService;
        private readonly ILogger<BudgetService> _logger;

        public BudgetService(
            AdminDbContext dbContext,
            INotificationService notificationService,
            ILogger<BudgetService> logger)
        {
            _dbContext = dbContext;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task CheckBudgetUtilizationAsync(CancellationToken cancellationToken)
        {
            var budgets = await _dbContext.Budgets
                .Where(b => b.IsActive)
                .ToListAsync(cancellationToken);

            var currentMonth = DateTime.UtcNow.ToString("yyyy-MM");

            foreach (var budget in budgets)
            {
                var currentSpend = await CalculateCurrentSpendAsync(budget, currentMonth, cancellationToken);
                var utilization = (currentSpend / budget.MonthlyBudgetUSD) * 100;

                _logger.LogInformation(
                    "Budget {BudgetName}: ${CurrentSpend:F2} / ${Budget:F2} ({Utilization:F1}%)",
                    budget.BudgetName, currentSpend, budget.MonthlyBudgetUSD, utilization);

                // Determine alert level
                string? alertLevel = null;
                if (utilization >= budget.EmergencyThresholdPercent)
                    alertLevel = "EMERGENCY";
                else if (utilization >= budget.CriticalThresholdPercent)
                    alertLevel = "CRITICAL";
                else if (utilization >= budget.WarningThresholdPercent)
                    alertLevel = "WARNING";
                else if (utilization >= budget.InfoThresholdPercent)
                    alertLevel = "INFO";

                if (alertLevel != null)
                {
                    // Check if alert already sent today
                    var today = DateTime.UtcNow.Date;
                    var existingAlert = await _dbContext.BudgetAlerts
                        .Where(a => a.BudgetId == budget.BudgetId 
                                 && a.AlertDate == today 
                                 && a.AlertLevel == alertLevel
                                 && a.AlertSent)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (existingAlert == null)
                    {
                        // Create and send alert
                        var alert = new BudgetAlert
                        {
                            BudgetId = budget.BudgetId,
                            AlertLevel = alertLevel,
                            AlertDate = today,
                            CurrentSpendUSD = currentSpend,
                            BudgetAmountUSD = budget.MonthlyBudgetUSD,
                            UtilizationPercent = utilization,
                            ProjectedSpendUSD = ProjectMonthEndSpend(currentSpend)
                        };

                        await _dbContext.BudgetAlerts.AddAsync(alert, cancellationToken);
                        await _dbContext.SaveChangesAsync(cancellationToken);

                        await SendBudgetAlertAsync(budget, alert, cancellationToken);

                        alert.AlertSent = true;
                        alert.AlertSentAt = DateTime.UtcNow;
                        await _dbContext.SaveChangesAsync(cancellationToken);
                    }
                }
            }
        }

        private async Task<decimal> CalculateCurrentSpendAsync(
            Budget budget, 
            string billingPeriod, 
            CancellationToken cancellationToken)
        {
            var query = _dbContext.CostRecords
                .Where(cr => cr.BillingPeriod == billingPeriod);

            query = budget.BudgetType switch
            {
                "Environment" => query.Where(cr => cr.Environment == budget.Scope),
                "CostCenter" => query.Where(cr => cr.CostCenter == budget.Scope),
                "Project" => query.Where(cr => cr.Project == budget.Scope),
                _ => query
            };

            return await query.SumAsync(cr => cr.CostUSD, cancellationToken);
        }

        private decimal ProjectMonthEndSpend(decimal currentSpend)
        {
            var daysInMonth = DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month);
            var dayOfMonth = DateTime.UtcNow.Day;
            return currentSpend / dayOfMonth * daysInMonth;
        }

        private async Task SendBudgetAlertAsync(
            Budget budget, 
            BudgetAlert alert, 
            CancellationToken cancellationToken)
        {
            var message = $@"
Budget Alert: {alert.AlertLevel}

Budget: {budget.BudgetName} ({budget.BudgetType}: {budget.Scope})
Current Spend: ${alert.CurrentSpendUSD:F2}
Budget: ${alert.BudgetAmountUSD:F2}
Utilization: {alert.UtilizationPercent:F1}%
Projected Month-End: ${alert.ProjectedSpendUSD:F2}

Please review your spending and take appropriate action.
";

            // Send email
            await _notificationService.SendEmailAsync(
                budget.OwnerEmail,
                $"Budget Alert: {alert.AlertLevel} - {budget.BudgetName}",
                message,
                cancellationToken);

            // Send Slack notification for WARNING and above
            if (alert.AlertLevel is "WARNING" or "CRITICAL" or "EMERGENCY")
            {
                await _notificationService.SendSlackNotificationAsync(
                    "#finance-alerts",
                    message,
                    cancellationToken);
            }

            // Send PagerDuty alert for CRITICAL and EMERGENCY
            if (alert.AlertLevel is "CRITICAL" or "EMERGENCY")
            {
                await _notificationService.SendPagerDutyAlertAsync(
                    "Budget Alert",
                    message,
                    alert.AlertLevel == "EMERGENCY" ? "critical" : "warning",
                    cancellationToken);
            }
        }
    }
}
```

### Admin Service API - Cost Management Controller

```csharp
// Controllers/CostManagementController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace IntelliFin.Admin.Controllers
{
    [ApiController]
    [Route("api/admin/costs")]
    [Authorize]
    public class CostManagementController : ControllerBase
    {
        private readonly ICostManagementService _costService;
        private readonly IBudgetService _budgetService;
        private readonly IOptimizationService _optimizationService;
        private readonly ILogger<CostManagementController> _logger;

        public CostManagementController(
            ICostManagementService costService,
            IBudgetService budgetService,
            IOptimizationService optimizationService,
            ILogger<CostManagementController> logger)
        {
            _costService = costService;
            _budgetService = budgetService;
            _optimizationService = optimizationService;
            _logger = logger;
        }

        /// <summary>
        /// Get cost summary for current month
        /// </summary>
        [HttpGet("summary")]
        [ProducesResponseType(typeof(CostSummaryDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCostSummary(
            [FromQuery] string? environment = null,
            CancellationToken cancellationToken = default)
        {
            var startDate = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var endDate = DateTime.UtcNow.Date;

            var costs = await _costService.GetCostsByDateRangeAsync(
                startDate, 
                endDate, 
                cancellationToken);

            if (!string.IsNullOrEmpty(environment))
            {
                costs = costs.Where(c => c.Environment == environment).ToList();
            }

            var summary = new CostSummaryDto
            {
                TotalCostUSD = costs.Sum(c => c.CostUSD),
                StartDate = startDate,
                EndDate = endDate,
                CostByCategory = costs
                    .GroupBy(c => c.MeterCategory)
                    .Select(g => new CategoryCostDto
                    {
                        Category = g.Key,
                        CostUSD = g.Sum(c => c.CostUSD)
                    })
                    .OrderByDescending(c => c.CostUSD)
                    .ToList(),
                CostByService = costs
                    .GroupBy(c => c.Service)
                    .Select(g => new ServiceCostDto
                    {
                        Service = g.Key,
                        CostUSD = g.Sum(c => c.CostUSD)
                    })
                    .OrderByDescending(c => c.CostUSD)
                    .Take(10)
                    .ToList()
            };

            return Ok(summary);
        }

        /// <summary>
        /// Get cost trend (last 6 months)
        /// </summary>
        [HttpGet("trend")]
        [ProducesResponseType(typeof(List<MonthCostDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetCostTrend(
            [FromQuery] int months = 6,
            [FromQuery] string? environment = null,
            CancellationToken cancellationToken = default)
        {
            var trend = new List<MonthCostDto>();

            for (int i = months - 1; i >= 0; i--)
            {
                var month = DateTime.UtcNow.AddMonths(-i);
                var startDate = new DateTime(month.Year, month.Month, 1);
                var endDate = startDate.AddMonths(1).AddDays(-1);

                var costs = await _costService.GetCostsByDateRangeAsync(
                    startDate,
                    endDate,
                    cancellationToken);

                if (!string.IsNullOrEmpty(environment))
                {
                    costs = costs.Where(c => c.Environment == environment).ToList();
                }

                trend.Add(new MonthCostDto
                {
                    Month = month.ToString("yyyy-MM"),
                    TotalCostUSD = costs.Sum(c => c.CostUSD)
                });
            }

            return Ok(trend);
        }

        /// <summary>
        /// Get budget utilization
        /// </summary>
        [HttpGet("budgets")]
        [Authorize(Roles = "System Administrator,Finance Manager")]
        [ProducesResponseType(typeof(List<BudgetUtilizationDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetBudgetUtilization(
            CancellationToken cancellationToken)
        {
            var utilization = await _budgetService.GetBudgetUtilizationAsync(cancellationToken);
            return Ok(utilization);
        }

        /// <summary>
        /// Get optimization recommendations
        /// </summary>
        [HttpGet("recommendations")]
        [ProducesResponseType(typeof(List<OptimizationRecommendationDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetOptimizationRecommendations(
            [FromQuery] string? type = null,
            [FromQuery] int limit = 10,
            CancellationToken cancellationToken = default)
        {
            var recommendations = await _optimizationService.GetRecommendationsAsync(
                type,
                limit,
                cancellationToken);

            return Ok(recommendations);
        }

        /// <summary>
        /// Export cost data to CSV
        /// </summary>
        [HttpGet("export")]
        [Authorize(Roles = "System Administrator,Finance Manager")]
        [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
        public async Task<IActionResult> ExportCostData(
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate,
            CancellationToken cancellationToken)
        {
            var costs = await _costService.GetCostsByDateRangeAsync(
                startDate,
                endDate,
                cancellationToken);

            var csv = GenerateCsv(costs);
            var bytes = System.Text.Encoding.UTF8.GetBytes(csv);

            return File(bytes, "text/csv", $"costs_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.csv");
        }

        private string GenerateCsv(List<CostRecord> costs)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Date,ResourceName,ResourceType,Environment,Service,CostCenter,CostUSD");

            foreach (var cost in costs)
            {
                sb.AppendLine($"{cost.RecordDate:yyyy-MM-dd},{cost.ResourceName},{cost.ResourceType},{cost.Environment},{cost.Service},{cost.CostCenter},{cost.CostUSD:F2}");
            }

            return sb.ToString();
        }
    }
}
```

### Grafana Cost Dashboard

```json
{
  "dashboard": {
    "title": "Infrastructure Cost Tracking",
    "panels": [
      {
        "title": "Total Monthly Spend",
        "targets": [
          {
            "expr": "sum(azure_cost_usd{billing_period=~\".*\"})",
            "legendFormat": "Total Cost"
          }
        ],
        "type": "stat",
        "fieldConfig": {
          "defaults": {
            "unit": "currencyUSD"
          }
        }
      },
      {
        "title": "Cost by Environment",
        "targets": [
          {
            "expr": "sum(azure_cost_usd) by (environment)",
            "legendFormat": "{{ environment }}"
          }
        ],
        "type": "piechart"
      },
      {
        "title": "Cost Trend (Last 6 Months)",
        "targets": [
          {
            "expr": "sum(azure_cost_usd) by (billing_period)",
            "legendFormat": "{{ billing_period }}"
          }
        ],
        "type": "graph"
      },
      {
        "title": "Top 10 Most Expensive Resources",
        "targets": [
          {
            "expr": "topk(10, sum(azure_cost_usd) by (resource_name))",
            "legendFormat": "{{ resource_name }}"
          }
        ],
        "type": "table"
      },
      {
        "title": "Budget Utilization",
        "targets": [
          {
            "expr": "(sum(azure_cost_usd{environment=\"production\"}) / scalar(azure_budget_usd{environment=\"production\"})) * 100",
            "legendFormat": "Production"
          }
        ],
        "type": "gauge",
        "fieldConfig": {
          "defaults": {
            "unit": "percent",
            "thresholds": {
              "steps": [
                { "value": 0, "color": "green" },
                { "value": 80, "color": "yellow" },
                { "value": 100, "color": "red" }
              ]
            }
          }
        }
      },
      {
        "title": "Kubernetes Cost Allocation",
        "targets": [
          {
            "expr": "sum(azure_aks_cost_usd) by (namespace)",
            "legendFormat": "{{ namespace }}"
          }
        ],
        "type": "piechart"
      }
    ]
  }
}
```

---

## Integration Verification

### IV1: Cost Data Collection
**Verification Steps**:
1. Deploy cost data collector service
2. Trigger manual collection for yesterday
3. Verify cost records in database
4. Check all resources have cost data
5. Validate tags captured correctly

**Success Criteria**:
- Cost data collected successfully
- All resources represented
- Tags populated where available
- Data accuracy within 5% of Azure portal

### IV2: Budget Alerts
**Verification Steps**:
1. Create test budget with $100 limit
2. Simulate spending to trigger thresholds
3. Verify alerts sent at 50%, 80%, 100%
4. Check email, Slack, PagerDuty notifications
5. Confirm alert deduplication works

**Success Criteria**:
- Alerts triggered at correct thresholds
- Notifications sent to all channels
- Alert deduplication prevents spam
- Budget utilization calculated correctly

### IV3: Optimization Recommendations
**Verification Steps**:
1. Run optimization analysis
2. Review recommendations generated
3. Verify savings calculations
4. Test "Apply Recommendation" workflow
5. Track recommendation status

**Success Criteria**:
- Recommendations generated for all types
- Savings calculations accurate
- Risk/effort levels reasonable
- Status tracking works correctly

---

## Testing Strategy

### Unit Tests

```csharp
[Fact]
public async Task CalculateCurrentSpend_EnvironmentBudget_ReturnsCorrectTotal()
{
    // Arrange
    var service = CreateBudgetService();
    var budget = new Budget
    {
        BudgetType = "Environment",
        Scope = "production",
        MonthlyBudgetUSD = 10000
    };

    // Act
    var spend = await service.CalculateCurrentSpendAsync(
        budget, 
        "2025-01", 
        CancellationToken.None);

    // Assert
    Assert.InRange(spend, 0, budget.MonthlyBudgetUSD * 1.2m);
}
```

### Integration Tests

```bash
#!/bin/bash
# test-cost-tracking.sh

echo "Testing cost tracking..."

# Test 1: Get cost summary
echo "Test 1: Get cost summary"
SUMMARY=$(curl -s -X GET "$ADMIN_API/costs/summary" \
  -H "Authorization: Bearer $TOKEN")

TOTAL_COST=$(echo "$SUMMARY" | jq -r '.totalCostUSD')
echo "Total cost: $$TOTAL_COST"

if [ "$TOTAL_COST" != "null" ]; then
  echo "✅ Cost summary retrieved"
else
  echo "❌ Cost summary failed"
  exit 1
fi

# Test 2: Get budget utilization
echo "Test 2: Get budget utilization"
BUDGETS=$(curl -s -X GET "$ADMIN_API/costs/budgets" \
  -H "Authorization: Bearer $TOKEN")

BUDGET_COUNT=$(echo "$BUDGETS" | jq '. | length')
echo "Budget count: $BUDGET_COUNT"

# Test 3: Get optimization recommendations
echo "Test 3: Get recommendations"
RECS=$(curl -s -X GET "$ADMIN_API/costs/recommendations?limit=5" \
  -H "Authorization: Bearer $TOKEN")

REC_COUNT=$(echo "$RECS" | jq '. | length')
echo "Recommendation count: $REC_COUNT"

if [ "$REC_COUNT" -gt 0 ]; then
  echo "✅ Recommendations generated"
else
  echo "❌ No recommendations found"
fi

echo "All tests passed! ✅"
```

---

## Risks and Mitigation

| Risk | Impact | Probability | Mitigation |
|------|---------|-------------|------------|
| Azure API rate limiting | Cost data collection failure | Low | Implement retry logic with exponential backoff. Cache data. Monitor API quotas. |
| Cost data delay | Alerts delayed 24-48 hours | Medium | Accept Azure delay. Supplement with projected spend. Use trending for early warning. |
| Tag compliance | Inaccurate cost allocation | High | Azure Policy enforcement. Daily untagged resource reports. CI/CD validation. |
| Budget accuracy | Incorrect alerts | Medium | Regular budget reviews. Forecast validation. Historical trend analysis. |
| Forecast accuracy | Poor planning decisions | Medium | Multiple models (ensemble). Continuous retraining. Confidence intervals. Accuracy tracking. |

---

## Definition of Done

- [ ] Cost data collector service implemented and deployed
- [ ] Azure Cost Management API integration tested
- [ ] Database schema created and migrated
- [ ] Budget monitoring service implemented
- [ ] Budget alert notifications configured (email, Slack, PagerDuty)
- [ ] Optimization recommendation engine implemented
- [ ] Admin Service cost management API endpoints complete
- [ ] Admin UI cost dashboard implemented
- [ ] Grafana cost dashboards created
- [ ] Cost forecasting models trained and deployed
- [ ] Azure Policy for tag enforcement configured
- [ ] Integration tests: Cost collection, budget alerts, recommendations
- [ ] Documentation: Cost allocation methodology, optimization guides
- [ ] Runbooks: Cost investigation, budget adjustments

---

## Related Documentation

### PRD References
- **Lines 1358-1382**: Story 1.30 detailed requirements
- **Lines 1244-1408**: Phase 5 (Observability & Infrastructure) overview
- **NFR22**: Infrastructure cost tracking
- **NFR23**: Budget alerts

### Architecture References
- **Section 13**: Cost Management
- **Section 11**: Cloud Infrastructure
- **Section 5**: FinOps

### External Documentation
- [Azure Cost Management API](https://docs.microsoft.com/en-us/rest/api/cost-management/)
- [Azure Tagging Best Practices](https://docs.microsoft.com/en-us/azure/azure-resource-manager/management/tag-resources)
- [FinOps Foundation](https://www.finops.org/)
- [Cloud Cost Optimization Guide](https://azure.microsoft.com/en-us/solutions/cost-optimization/)

---

## Notes for Development Team

### Pre-Implementation Checklist
- [ ] Azure Cost Management API access configured
- [ ] Service principal with Cost Reader role created
- [ ] PostgreSQL database capacity planned
- [ ] Tag taxonomy documented and approved
- [ ] Budget allocations defined per team
- [ ] Notification channels configured
- [ ] Stakeholder list for alerts finalized
- [ ] Cost allocation rules documented

### Post-Implementation Handoff
- [ ] Train finance team on cost dashboard
- [ ] Document cost allocation methodology
- [ ] Create user guide for budget management
- [ ] Set up monthly cost review meetings
- [ ] Establish cost optimization process
- [ ] Schedule quarterly budget reviews
- [ ] Create incident response for cost spikes
- [ ] Document disaster recovery procedures

### Technical Debt / Future Enhancements
- [ ] Implement multi-cloud support (AWS, GCP)
- [ ] Add predictive cost anomaly detection (ML)
- [ ] Integrate with capacity planning tools
- [ ] Implement automated cost optimization (auto-rightsizing)
- [ ] Add carbon footprint tracking
- [ ] Create mobile app for cost alerts
- [ ] Implement cost allocation for on-premises infrastructure
- [ ] Add cost comparison across regions/vendors

---

**Story Created**: 2025-10-11  
**Last Updated**: 2025-10-11  
**Next Phase**: [Phase 6: Advanced Features & Optimization](../../phase-6-advanced-features.md)
