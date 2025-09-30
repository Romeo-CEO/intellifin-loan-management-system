# SMS Cost Tracking and Optimization System

**Story ID:** SMS-2.5
**Epic:** Epic 2 - SMS Provider Migration to Africa's Talking
**Status:** Draft
**Story Points:** 5
**Priority:** Medium

## User Story
**As a** finance manager
**I want** comprehensive SMS cost tracking and optimization features
**So that** I can monitor communication expenses and optimize spending across providers

## Background
Implement enhanced cost tracking, budget monitoring, and optimization features for SMS communications. This includes real-time cost monitoring, budget alerts, cost comparison between providers, and optimization recommendations.

## Acceptance Criteria

### ✅ Enhanced Cost Tracking
- [ ] Track SMS costs per message with provider breakdown
- [ ] Store cost data with currency, exchange rates, and timestamps
- [ ] Support bulk SMS cost allocation and tracking
- [ ] Track failed message costs and refund handling
- [ ] Generate cost reports by time period, branch, and provider

### ✅ Budget Management
- [ ] Configure SMS budget limits by branch and time period
- [ ] Real-time budget consumption monitoring
- [ ] Automated alerts when approaching budget thresholds
- [ ] Budget overage warnings and spending controls
- [ ] Historical budget vs. actual spending analysis

### ✅ Provider Cost Comparison
- [ ] Compare costs between Africa's Talking and legacy providers
- [ ] Track cost savings from provider migration
- [ ] Analyze cost efficiency by message type and destination
- [ ] Generate cost comparison reports for financial analysis
- [ ] Provide ROI calculations for provider migration

### ✅ Cost Optimization Features
- [ ] Identify high-cost messaging patterns and suggest optimizations
- [ ] Recommend optimal sending times based on delivery success rates
- [ ] Analyze message content for cost-effective alternatives
- [ ] Provide bulk messaging optimization suggestions
- [ ] Generate cost optimization reports with actionable insights

### ✅ Financial Reporting
- [ ] Monthly and quarterly SMS cost summaries
- [ ] Cost allocation reports by department and cost center
- [ ] Provider billing reconciliation capabilities
- [ ] Export cost data for financial system integration
- [ ] Audit trail for all cost-related transactions

## Technical Implementation

### Cost Tracking Service
```csharp
public interface ISmsostTrackingService
{
    Task<SmsostResult> RecordCostAsync(SmostrackingRequest request);
    Task<List<SmscostSummary>> GetCostSummaryAsync(CostSummaryRequest request);
    Task<BudgetStatus> CheckBudgetStatusAsync(string branchId, DateTime period);
    Task<CostComparisonReport> GenerateProviderComparisonAsync(ComparisonRequest request);
}

public class SmsCostTrackingService : ISmpsCostTrackingService
{
    private readonly INotificationRepository _repository;
    private readonly ICostCalculationEngine _calculator;
    private readonly IBudgetService _budgetService;
    private readonly ILogger<SmsCostTrackingService> _logger;

    public async Task<SmsCostResult> RecordCostAsync(SmsCostTrackingRequest request)
    {
        try
        {
            // Calculate final cost with exchange rates
            var cost = await _calculator.CalculateFinalCostAsync(request);

            // Create cost record
            var costRecord = new SmsCostRecord
            {
                NotificationId = request.NotificationId,
                Provider = request.Provider,
                RawCost = request.RawCost,
                Currency = request.Currency,
                ExchangeRate = cost.ExchangeRate,
                FinalCostZMW = cost.FinalCostZMW,
                BranchId = request.BranchId,
                CostCenter = request.CostCenter,
                Timestamp = DateTime.UtcNow
            };

            await _repository.CreateCostRecordAsync(costRecord);

            // Check budget implications
            await CheckAndAlertBudgetAsync(request.BranchId, cost.FinalCostZMW);

            return new SmsCostResult { Success = true, FinalCost = cost.FinalCostZMW };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record SMS cost");
            return new SmsCostResult { Success = false, Error = ex.Message };
        }
    }
}
```

### Budget Monitoring Service
```csharp
public interface IBudgetService
{
    Task<BudgetStatus> GetBudgetStatusAsync(string branchId, DateTime period);
    Task<bool> CheckBudgetExceedsAsync(string branchId, decimal additionalCost);
    Task TriggerBudgetAlertAsync(BudgetAlert alert);
}

public class BudgetService : IBudgetService
{
    private readonly IBudgetRepository _repository;
    private readonly INotificationService _notificationService;

    public async Task<BudgetStatus> GetBudgetStatusAsync(string branchId, DateTime period)
    {
        var budget = await _repository.GetBudgetAsync(branchId, period);
        var spending = await _repository.GetSpendingAsync(branchId, period);

        return new BudgetStatus
        {
            BranchId = branchId,
            Period = period,
            BudgetAmount = budget.Amount,
            SpentAmount = spending.TotalSpent,
            RemainingAmount = budget.Amount - spending.TotalSpent,
            PercentageUsed = (spending.TotalSpent / budget.Amount) * 100,
            IsOverBudget = spending.TotalSpent > budget.Amount,
            DaysRemaining = (period.AddMonths(1) - DateTime.Today).Days
        };
    }

    public async Task<bool> CheckBudgetExceedsAsync(string branchId, decimal additionalCost)
    {
        var status = await GetBudgetStatusAsync(branchId, DateTime.Today);
        return (status.SpentAmount + additionalCost) > status.BudgetAmount;
    }
}
```

### Cost Comparison Engine
```csharp
public class CostComparisonEngine
{
    public async Task<CostComparisonReport> GenerateComparisonAsync(ComparisonRequest request)
    {
        var africasTalkingCosts = await GetProviderCostsAsync("AfricasTalking", request.Period);
        var legacyCosts = await GetProviderCostsAsync("Legacy", request.Period);

        return new CostComparisonReport
        {
            Period = request.Period,
            AfricasTalkingCosts = africasTalkingCosts,
            LegacyCosts = legacyCosts,
            TotalSavings = legacyCosts.TotalCost - africasTalkingCosts.TotalCost,
            SavingsPercentage = CalculateSavingsPercentage(africasTalkingCosts, legacyCosts),
            VolumeComparison = CompareVolumes(africasTalkingCosts, legacyCosts),
            Recommendations = GenerateOptimizationRecommendations(africasTalkingCosts, legacyCosts)
        };
    }

    private List<OptimizationRecommendation> GenerateOptimizationRecommendations(
        ProviderCostSummary africasTalking, ProviderCostSummary legacy)
    {
        var recommendations = new List<OptimizationRecommendation>();

        // Analyze cost per message
        if (africasTalking.CostPerMessage < legacy.CostPerMessage)
        {
            recommendations.Add(new OptimizationRecommendation
            {
                Type = "ProviderMigration",
                Title = "Migrate remaining traffic to Africa's Talking",
                PotentialSavings = CalculateMigrationSavings(africasTalking, legacy),
                Priority = "High"
            });
        }

        // Analyze delivery success rates
        if (africasTalking.DeliverySuccessRate > legacy.DeliverySuccessRate)
        {
            recommendations.Add(new OptimizationRecommendation
            {
                Type = "DeliveryOptimization",
                Title = "Improve delivery success reduces retry costs",
                PotentialSavings = CalculateDeliverySavings(africasTalking, legacy),
                Priority = "Medium"
            });
        }

        return recommendations;
    }
}
```

## Files to Create/Modify

### New Files
- `apps/IntelliFin.Communications/Services/SmsCostTrackingService.cs`
- `apps/IntelliFin.Communications/Services/BudgetService.cs`
- `apps/IntelliFin.Communications/Services/CostComparisonEngine.cs`
- `apps/IntelliFin.Communications/Models/CostTrackingModels.cs`
- `apps/IntelliFin.Communications/Controllers/SmsCostController.cs`

### Modified Files
- `apps/IntelliFin.Communications/Models/NotificationModels.cs` - Add cost fields
- `libs/IntelliFin.Shared.DomainModels/Models/SmsBudgetModels.cs` - Budget entities

## Cost Tracking Models
```csharp
public class SmsCostRecord
{
    public Guid Id { get; set; }
    public Guid NotificationId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public decimal RawCost { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal ExchangeRate { get; set; }
    public decimal FinalCostZMW { get; set; }
    public string BranchId { get; set; } = string.Empty;
    public string? CostCenter { get; set; }
    public DateTime Timestamp { get; set; }
}

public class BudgetStatus
{
    public string BranchId { get; set; } = string.Empty;
    public DateTime Period { get; set; }
    public decimal BudgetAmount { get; set; }
    public decimal SpentAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public decimal PercentageUsed { get; set; }
    public bool IsOverBudget { get; set; }
    public int DaysRemaining { get; set; }
}

public class CostComparisonReport
{
    public DateTime Period { get; set; }
    public ProviderCostSummary AfricasTalkingCosts { get; set; } = new();
    public ProviderCostSummary LegacyCosts { get; set; } = new();
    public decimal TotalSavings { get; set; }
    public decimal SavingsPercentage { get; set; }
    public VolumeComparison VolumeComparison { get; set; } = new();
    public List<OptimizationRecommendation> Recommendations { get; set; } = new();
}
```

## API Endpoints
```csharp
[ApiController]
[Route("api/sms/costs")]
public class SmsCostController : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<List<SmsCostSummary>>> GetCostSummary(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] string? branchId = null)

    [HttpGet("budget-status/{branchId}")]
    public async Task<ActionResult<BudgetStatus>> GetBudgetStatus(string branchId)

    [HttpGet("provider-comparison")]
    public async Task<ActionResult<CostComparisonReport>> GetProviderComparison(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)

    [HttpGet("optimization-recommendations")]
    public async Task<ActionResult<List<OptimizationRecommendation>>> GetOptimizationRecommendations()
}
```

## Testing Requirements

### Unit Tests
- [ ] Cost calculation engine accuracy
- [ ] Budget monitoring and alert triggering
- [ ] Provider cost comparison logic
- [ ] Optimization recommendation generation
- [ ] Currency conversion and exchange rate handling

### Integration Tests
- [ ] End-to-end cost tracking from SMS sending to reporting
- [ ] Budget alert integration with notification system
- [ ] Cost data persistence and retrieval
- [ ] Financial report generation accuracy
- [ ] API endpoint functionality and security

## Dependencies
- Notification repository and models
- Budget management configuration
- Exchange rate service for currency conversion
- Reporting infrastructure
- Provider cost calculation services

## Success Criteria
- Accurate cost tracking within 1% margin of provider billing
- Budget alerts trigger within 5 minutes of threshold breach
- Cost comparison reports generate within 30 seconds
- Optimization recommendations provide measurable savings opportunities
- Financial reports meet accounting requirements

## Risk Mitigation
- **Cost Accuracy**: Regular reconciliation with provider billing
- **Budget Overruns**: Real-time monitoring and preventive controls
- **Exchange Rate Fluctuations**: Regular rate updates and historical tracking
- **Reporting Errors**: Data validation and audit trails
- **Performance Impact**: Efficient querying and caching strategies

## Definition of Done
- [ ] All acceptance criteria implemented and tested
- [ ] Unit test coverage ≥90% for cost tracking code
- [ ] Integration tests validate cost accuracy
- [ ] Financial reporting meets business requirements
- [ ] Performance testing confirms response times
- [ ] Documentation includes cost tracking guides
- [ ] Code review completed and approved