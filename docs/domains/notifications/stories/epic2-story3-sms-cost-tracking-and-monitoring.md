# Story 2.3: SMS Cost Tracking and Usage Monitoring

**Epic:** Epic 2 - SMS Provider Migration to Africa's Talking
**Story ID:** COMM-023
**Status:** Draft
**Priority:** High
**Effort:** 6 Story Points

## User Story
**As a** finance manager
**I want** comprehensive SMS cost tracking and usage monitoring across all providers
**So that** I can manage communication budgets, track ROI, and optimize spending across providers

## Business Value
- **Budget Management**: Real-time visibility into SMS spending across all providers
- **Cost Optimization**: Identify cost-effective providers and usage patterns
- **ROI Analysis**: Track SMS effectiveness and customer engagement metrics
- **Budget Compliance**: Automated alerts when approaching spending limits
- **Financial Reporting**: Detailed cost analytics for management reporting
- **Provider Comparison**: Cost analysis to inform provider selection decisions

## Acceptance Criteria

### Primary Functionality
- [ ] **Real-time Cost Tracking**: Track costs per SMS, per provider, per branch
  - Record actual costs from provider billing APIs
  - Support both estimated and actual cost tracking
  - Handle different pricing models (per SMS, bulk rates, etc.)
- [ ] **Usage Analytics**: Comprehensive SMS usage statistics
  - Volume tracking by time period, branch, user, campaign
  - Success/failure rate analysis per provider
  - Delivery time and retry statistics
- [ ] **Budget Management**: Budget allocation and monitoring
  - Set budgets by branch, department, or campaign
  - Real-time budget consumption tracking
  - Automated alerts at configurable thresholds (75%, 90%, 100%)
- [ ] **Provider Cost Comparison**: Analyze costs across providers
  - Side-by-side cost comparison reports
  - Historical cost trend analysis
  - Provider efficiency metrics

### Reporting and Analytics
- [ ] **Cost Reports**: Detailed financial reporting
  - Daily, weekly, monthly cost summaries
  - Cost breakdown by provider, branch, message type
  - Export capabilities (CSV, Excel, PDF)
- [ ] **Usage Reports**: Operational analytics
  - SMS volume trends and patterns
  - Peak usage identification
  - Success rate analysis by provider
- [ ] **Budget Reports**: Budget tracking and forecasting
  - Budget vs. actual spending analysis
  - Spending trend projections
  - Cost per successful delivery metrics

### Alerting and Notifications
- [ ] **Budget Alerts**: Automated spending notifications
  - Configurable threshold alerts (email, in-app)
  - Daily/weekly spending summaries
  - Unusual spending pattern detection
- [ ] **Usage Alerts**: Operational monitoring
  - High volume usage notifications
  - Provider failure rate alerts
  - Cost anomaly detection

## Technical Implementation

### Components to Implement

#### 1. Cost Tracking Models
```csharp
// File: apps/IntelliFin.Communications/Models/CostTrackingModels.cs
public class SmsUsageRecord
{
    public Guid Id { get; set; }
    public Guid NotificationLogId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string RecipientNumber { get; set; } = string.Empty;
    public decimal EstimatedCost { get; set; }
    public decimal? ActualCost { get; set; }
    public string Currency { get; set; } = "ZMW";
    public int MessageParts { get; set; } = 1;
    public string MessageType { get; set; } = "Standard"; // Standard, Premium, etc.
    public int BranchId { get; set; }
    public string? CampaignId { get; set; }
    public string? Department { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? BilledAt { get; set; }
    public string Status { get; set; } = string.Empty; // Sent, Delivered, Failed
}

public class SmsBudget
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // Branch, Department, Campaign
    public string EntityId { get; set; } = string.Empty; // BranchId, DepartmentId, etc.
    public decimal BudgetAmount { get; set; }
    public decimal SpentAmount { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Currency { get; set; } = "ZMW";
    public bool IsActive { get; set; } = true;
    public List<BudgetAlert> Alerts { get; set; } = new();
}

public class BudgetAlert
{
    public Guid Id { get; set; }
    public Guid BudgetId { get; set; }
    public decimal ThresholdPercentage { get; set; }
    public bool IsTriggered { get; set; }
    public DateTime? TriggeredAt { get; set; }
    public string AlertType { get; set; } = string.Empty; // Email, InApp, SMS
    public List<string> Recipients { get; set; } = new();
}

public class ProviderCostConfig
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public decimal CostPerSms { get; set; }
    public decimal CostPerLongSms { get; set; }
    public int LongSmsThreshold { get; set; } = 160;
    public string Currency { get; set; } = "ZMW";
    public DateTime EffectiveDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public bool IsActive { get; set; } = true;
}
```

#### 2. Cost Tracking Service
```csharp
// File: apps/IntelliFin.Communications/Services/SmsCostTrackingService.cs
public interface ISmsCostTrackingService
{
    Task<SmsUsageRecord> TrackSmsUsageAsync(SmsUsageTrackingRequest request);
    Task UpdateActualCostAsync(Guid usageRecordId, decimal actualCost);
    Task<SmsCostSummary> GetCostSummaryAsync(CostSummaryRequest request);
    Task<List<ProviderCostComparison>> GetProviderCostComparisonAsync(DateTimeOffset start, DateTimeOffset end);
    Task<bool> CheckBudgetAvailabilityAsync(string entityType, string entityId, decimal estimatedCost);
    Task<List<BudgetAlert>> ProcessBudgetAlertsAsync();
}

public class SmsCostTrackingService : ISmsCostTrackingService
{
    private readonly ISmsCostRepository _costRepository;
    private readonly IBudgetRepository _budgetRepository;
    private readonly IProviderCostRepository _providerCostRepository;
    private readonly INotificationService _notificationService;
    private readonly ILogger<SmsCostTrackingService> _logger;

    public async Task<SmsUsageRecord> TrackSmsUsageAsync(SmsUsageTrackingRequest request)
    {
        // Calculate estimated cost based on provider configuration
        var providerConfig = await _providerCostRepository.GetActiveConfigAsync(request.Provider);
        var estimatedCost = CalculateEstimatedCost(request.MessageLength, providerConfig);

        var usageRecord = new SmsUsageRecord
        {
            Id = Guid.NewGuid(),
            NotificationLogId = request.NotificationLogId,
            Provider = request.Provider,
            RecipientNumber = request.RecipientNumber,
            EstimatedCost = estimatedCost,
            Currency = providerConfig?.Currency ?? "ZMW",
            MessageParts = CalculateMessageParts(request.MessageLength),
            MessageType = request.MessageType,
            BranchId = request.BranchId,
            CampaignId = request.CampaignId,
            Department = request.Department,
            CreatedAt = DateTime.UtcNow,
            Status = request.Status
        };

        await _costRepository.CreateUsageRecordAsync(usageRecord);

        // Update budget spending
        await UpdateBudgetSpendingAsync(usageRecord);

        return usageRecord;
    }

    public async Task<SmsCostSummary> GetCostSummaryAsync(CostSummaryRequest request)
    {
        var usageRecords = await _costRepository.GetUsageRecordsAsync(
            request.StartDate,
            request.EndDate,
            request.BranchId,
            request.Provider,
            request.Department);

        return new SmsCostSummary
        {
            TotalCost = usageRecords.Sum(r => r.ActualCost ?? r.EstimatedCost),
            TotalMessages = usageRecords.Count,
            SuccessfulMessages = usageRecords.Count(r => r.Status == "Delivered"),
            FailedMessages = usageRecords.Count(r => r.Status == "Failed"),
            CostPerSuccessfulMessage = CalculateCostPerSuccessfulMessage(usageRecords),
            ProviderBreakdown = usageRecords.GroupBy(r => r.Provider)
                .Select(g => new ProviderCostBreakdown
                {
                    Provider = g.Key,
                    TotalCost = g.Sum(r => r.ActualCost ?? r.EstimatedCost),
                    MessageCount = g.Count(),
                    SuccessRate = (decimal)g.Count(r => r.Status == "Delivered") / g.Count() * 100
                }).ToList(),
            BranchBreakdown = usageRecords.GroupBy(r => r.BranchId)
                .Select(g => new BranchCostBreakdown
                {
                    BranchId = g.Key,
                    TotalCost = g.Sum(r => r.ActualCost ?? r.EstimatedCost),
                    MessageCount = g.Count()
                }).ToList()
        };
    }

    private decimal CalculateEstimatedCost(int messageLength, ProviderCostConfig config)
    {
        if (config == null) return 0;

        var messageParts = CalculateMessageParts(messageLength);
        return messageParts > 1 ?
            config.CostPerLongSms * messageParts :
            config.CostPerSms;
    }

    private int CalculateMessageParts(int messageLength)
    {
        if (messageLength <= 160) return 1;
        return (int)Math.Ceiling((double)messageLength / 153); // SMS concatenation standard
    }

    private async Task UpdateBudgetSpendingAsync(SmsUsageRecord usageRecord)
    {
        // Update branch budget
        await UpdateEntityBudgetAsync("Branch", usageRecord.BranchId.ToString(), usageRecord.EstimatedCost);

        // Update department budget if specified
        if (!string.IsNullOrEmpty(usageRecord.Department))
        {
            await UpdateEntityBudgetAsync("Department", usageRecord.Department, usageRecord.EstimatedCost);
        }

        // Update campaign budget if specified
        if (!string.IsNullOrEmpty(usageRecord.CampaignId))
        {
            await UpdateEntityBudgetAsync("Campaign", usageRecord.CampaignId, usageRecord.EstimatedCost);
        }
    }
}
```

#### 3. Budget Management Service
```csharp
// File: apps/IntelliFin.Communications/Services/SmsBudgetService.cs
public interface ISmsBudgetService
{
    Task<SmsBudget> CreateBudgetAsync(CreateBudgetRequest request);
    Task<SmsBudget> UpdateBudgetAsync(Guid budgetId, UpdateBudgetRequest request);
    Task<bool> DeleteBudgetAsync(Guid budgetId);
    Task<List<SmsBudget>> GetActiveBudgetsAsync(string entityType = null, string entityId = null);
    Task<BudgetStatus> GetBudgetStatusAsync(Guid budgetId);
    Task<List<BudgetAlert>> CheckBudgetThresholdsAsync();
}

public class SmsBudgetService : ISmsBudgetService
{
    private readonly IBudgetRepository _budgetRepository;
    private readonly ISmsCostRepository _costRepository;
    private readonly INotificationService _notificationService;
    private readonly ILogger<SmsBudgetService> _logger;

    public async Task<List<BudgetAlert>> CheckBudgetThresholdsAsync()
    {
        var activeBudgets = await _budgetRepository.GetActiveBudgetsAsync();
        var triggeredAlerts = new List<BudgetAlert>();

        foreach (var budget in activeBudgets)
        {
            var spentPercentage = (budget.SpentAmount / budget.BudgetAmount) * 100;

            foreach (var alert in budget.Alerts.Where(a => !a.IsTriggered))
            {
                if (spentPercentage >= alert.ThresholdPercentage)
                {
                    alert.IsTriggered = true;
                    alert.TriggeredAt = DateTime.UtcNow;

                    await _budgetRepository.UpdateAlertAsync(alert);
                    await SendBudgetAlertAsync(budget, alert, spentPercentage);

                    triggeredAlerts.Add(alert);
                }
            }
        }

        return triggeredAlerts;
    }

    private async Task SendBudgetAlertAsync(SmsBudget budget, BudgetAlert alert, decimal spentPercentage)
    {
        var message = $"Budget Alert: {budget.Name} has reached {spentPercentage:F1}% of allocated budget ({budget.SpentAmount:C}/{budget.BudgetAmount:C})";

        foreach (var recipient in alert.Recipients)
        {
            if (alert.AlertType == "Email")
            {
                await _notificationService.SendEmailAsync(recipient, "SMS Budget Alert", message);
            }
            else if (alert.AlertType == "InApp")
            {
                await _notificationService.SendInAppNotificationAsync(recipient, message);
            }
        }
    }
}
```

#### 4. Cost Analytics Service
```csharp
// File: apps/IntelliFin.Communications/Services/SmsCostAnalyticsService.cs
public interface ISmsCostAnalyticsService
{
    Task<CostTrendAnalysis> GetCostTrendAnalysisAsync(CostTrendRequest request);
    Task<ProviderEfficiencyReport> GetProviderEfficiencyReportAsync(DateTimeOffset start, DateTimeOffset end);
    Task<UsagePatternAnalysis> GetUsagePatternAnalysisAsync(UsagePatternRequest request);
    Task<byte[]> ExportCostReportAsync(CostReportExportRequest request);
}

public class SmsCostAnalyticsService : ISmsCostAnalyticsService
{
    public async Task<CostTrendAnalysis> GetCostTrendAnalysisAsync(CostTrendRequest request)
    {
        var usageData = await _costRepository.GetUsageRecordsAsync(
            request.StartDate, request.EndDate, request.BranchId);

        var dailyTrends = usageData
            .GroupBy(r => r.CreatedAt.Date)
            .Select(g => new DailyCostTrend
            {
                Date = g.Key,
                TotalCost = g.Sum(r => r.ActualCost ?? r.EstimatedCost),
                MessageCount = g.Count(),
                AverageCostPerMessage = g.Average(r => r.ActualCost ?? r.EstimatedCost)
            })
            .OrderBy(t => t.Date)
            .ToList();

        var monthlyProjection = CalculateMonthlyProjection(dailyTrends);

        return new CostTrendAnalysis
        {
            DailyTrends = dailyTrends,
            MonthlyProjection = monthlyProjection,
            TrendDirection = CalculateTrendDirection(dailyTrends),
            AverageGrowthRate = CalculateAverageGrowthRate(dailyTrends)
        };
    }

    public async Task<ProviderEfficiencyReport> GetProviderEfficiencyReportAsync(
        DateTimeOffset start, DateTimeOffset end)
    {
        var usageData = await _costRepository.GetUsageRecordsAsync(start, end);

        var providerMetrics = usageData
            .GroupBy(r => r.Provider)
            .Select(g => new ProviderEfficiencyMetrics
            {
                Provider = g.Key,
                TotalMessages = g.Count(),
                SuccessfulMessages = g.Count(r => r.Status == "Delivered"),
                FailedMessages = g.Count(r => r.Status == "Failed"),
                TotalCost = g.Sum(r => r.ActualCost ?? r.EstimatedCost),
                AverageCostPerMessage = g.Average(r => r.ActualCost ?? r.EstimatedCost),
                SuccessRate = (decimal)g.Count(r => r.Status == "Delivered") / g.Count() * 100,
                CostPerSuccessfulMessage = g.Where(r => r.Status == "Delivered").Average(r => r.ActualCost ?? r.EstimatedCost),
                AverageDeliveryTime = CalculateAverageDeliveryTime(g.ToList())
            })
            .OrderByDescending(m => m.SuccessRate)
            .ThenBy(m => m.CostPerSuccessfulMessage)
            .ToList();

        return new ProviderEfficiencyReport
        {
            ReportPeriod = new { Start = start, End = end },
            ProviderMetrics = providerMetrics,
            MostCostEffective = providerMetrics.OrderBy(m => m.CostPerSuccessfulMessage).FirstOrDefault(),
            HighestSuccessRate = providerMetrics.OrderByDescending(m => m.SuccessRate).FirstOrDefault(),
            Recommendations = GenerateProviderRecommendations(providerMetrics)
        };
    }
}
```

#### 5. Cost Tracking Controller
```csharp
// File: apps/IntelliFin.Communications/Controllers/SmsCostController.cs
[ApiController]
[Route("api/sms/cost")]
[Authorize]
public class SmsCostController : ControllerBase
{
    private readonly ISmsCostTrackingService _costTrackingService;
    private readonly ISmsCostAnalyticsService _analyticsService;
    private readonly ISmsBudgetService _budgetService;

    [HttpGet("summary")]
    public async Task<ActionResult<SmsCostSummary>> GetCostSummaryAsync(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] int? branchId = null,
        [FromQuery] string provider = null,
        [FromQuery] string department = null)
    {
        var request = new CostSummaryRequest
        {
            StartDate = startDate,
            EndDate = endDate,
            BranchId = branchId,
            Provider = provider,
            Department = department
        };

        var summary = await _costTrackingService.GetCostSummaryAsync(request);
        return Ok(summary);
    }

    [HttpGet("trends")]
    public async Task<ActionResult<CostTrendAnalysis>> GetCostTrendsAsync(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] int? branchId = null)
    {
        var request = new CostTrendRequest
        {
            StartDate = startDate,
            EndDate = endDate,
            BranchId = branchId
        };

        var trends = await _analyticsService.GetCostTrendAnalysisAsync(request);
        return Ok(trends);
    }

    [HttpGet("provider-efficiency")]
    public async Task<ActionResult<ProviderEfficiencyReport>> GetProviderEfficiencyAsync(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        var report = await _analyticsService.GetProviderEfficiencyReportAsync(startDate, endDate);
        return Ok(report);
    }

    [HttpPost("budgets")]
    [Authorize(Roles = "Finance,Admin")]
    public async Task<ActionResult<SmsBudget>> CreateBudgetAsync([FromBody] CreateBudgetRequest request)
    {
        var budget = await _budgetService.CreateBudgetAsync(request);
        return CreatedAtAction(nameof(GetBudgetAsync), new { id = budget.Id }, budget);
    }

    [HttpGet("budgets/{id}")]
    public async Task<ActionResult<BudgetStatus>> GetBudgetAsync(Guid id)
    {
        var status = await _budgetService.GetBudgetStatusAsync(id);
        return Ok(status);
    }

    [HttpGet("export")]
    public async Task<ActionResult> ExportCostReportAsync(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] string format = "xlsx",
        [FromQuery] int? branchId = null)
    {
        var request = new CostReportExportRequest
        {
            StartDate = startDate,
            EndDate = endDate,
            Format = format,
            BranchId = branchId
        };

        var fileData = await _analyticsService.ExportCostReportAsync(request);
        var fileName = $"sms-cost-report-{startDate:yyyy-MM-dd}-{endDate:yyyy-MM-dd}.{format}";

        return File(fileData, GetMimeType(format), fileName);
    }
}
```

### Configuration Structure
```json
{
  "SmsCostTracking": {
    "EnableRealTimeTracking": true,
    "DefaultCurrency": "ZMW",
    "BudgetCheckFrequencyMinutes": 15,
    "CostCalculationMode": "Estimated", // Estimated, Actual, Both
    "AlertSettings": {
      "EnableBudgetAlerts": true,
      "DefaultThresholds": [75, 90, 100],
      "AlertMethods": ["Email", "InApp"]
    },
    "ReportSettings": {
      "EnableExport": true,
      "MaxExportRecords": 50000,
      "SupportedFormats": ["xlsx", "csv", "pdf"]
    }
  }
}
```

## Dependencies
- **Story 2.1**: Africa's Talking provider implementation
- **Story 2.2**: Provider abstraction layer
- **Epic 1**: Notification infrastructure and logging
- **Database Schema**: Cost tracking and budget tables

## Risks and Mitigation

### Technical Risks
- **Cost Calculation Accuracy**: Validate against actual provider billing
- **Performance Impact**: Optimize cost tracking queries and indexes
- **Data Volume**: Implement data archiving and cleanup strategies
- **Real-time Processing**: Handle high-volume SMS scenarios efficiently

### Business Risks
- **Budget Overruns**: Implement hard limits and approval workflows
- **Cost Discrepancies**: Regular reconciliation with provider billing
- **Reporting Accuracy**: Validate calculations and data integrity

## Testing Strategy

### Unit Tests
- [ ] Cost calculation logic
- [ ] Budget threshold detection
- [ ] Provider cost comparison algorithms
- [ ] Export functionality
- [ ] Alert trigger conditions

### Integration Tests
- [ ] Cost tracking workflow
- [ ] Budget management operations
- [ ] Report generation
- [ ] Alert delivery
- [ ] Provider billing integration

### Performance Tests
- [ ] High-volume cost tracking
- [ ] Report generation performance
- [ ] Real-time budget updates
- [ ] Concurrent usage scenarios

## Success Metrics
- **Cost Tracking Accuracy**: >99% correlation with provider billing
- **Budget Alert Response Time**: <5 minutes from threshold breach
- **Report Generation**: <10 seconds for standard reports
- **Budget Management**: 100% of budgets actively monitored
- **Cost Optimization**: Demonstrate measurable cost savings

## Definition of Done
- [ ] All acceptance criteria implemented and tested
- [ ] Real-time cost tracking operational
- [ ] Budget management system functional
- [ ] Comprehensive reporting and analytics available
- [ ] Alert system operational and tested
- [ ] Export functionality validated
- [ ] Performance requirements met
- [ ] Security review completed
- [ ] Integration tests passing
- [ ] Documentation completed

## Related Stories
- **Prerequisite**: Story 2.1 (Africa's Talking provider), Story 2.2 (Provider abstraction)
- **Related**: Story 2.4 (Migration strategy), Story 2.6 (Configuration management)

This comprehensive cost tracking and monitoring system provides complete financial visibility and control over SMS operations across all providers.