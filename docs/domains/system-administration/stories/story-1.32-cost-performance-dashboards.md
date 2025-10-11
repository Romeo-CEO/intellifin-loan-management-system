# Story 1.32: Cost-Performance Monitoring Dashboards

## Story Metadata

| Field | Value |
|-------|-------|
| **Story ID** | 1.32 |
| **Epic** | System Administration Control Plane Enhancement |
| **Phase** | Phase 6: Advanced Observability |
| **Sprint** | Sprint 11-12 |
| **Story Points** | 10 |
| **Estimated Effort** | 7-10 days |
| **Priority** | P1 (High) |
| **Status** | 📋 Backlog |
| **Assigned To** | TBD |
| **Dependencies** | Grafana (Story 1.24), Cost Tracking (Story 1.30), OpenTelemetry (Story 1.29) |
| **Blocks** | Cost optimization initiatives, Resource right-sizing |

---

## User Story

**As a** CFO,  
**I want** dashboards showing infrastructure cost and performance metrics per service,  
**so that** I can optimize spending while maintaining SLA commitments.

---

## Business Value

Cost-Performance Monitoring provides critical financial and operational insights:

- **Financial Accountability**: Clear visibility into infrastructure spending per service
- **ROI Analysis**: Correlate costs with business value delivered
- **Cost Optimization**: Identify underutilized resources for right-sizing
- **SLA Compliance**: Ensure performance meets targets while controlling costs
- **Budget Planning**: Historical trends inform accurate forecasting
- **Chargeback**: Enable business unit cost allocation
- **Strategic Decisions**: Data-driven infrastructure investment choices

This story is **critical** for financial management and operational efficiency.

---

## Acceptance Criteria

### AC1: Cost-Performance Overview Dashboard
**Given** CFO needs cost and performance visibility  
**When** accessing the Cost-Performance Dashboard  
**Then**:
- Grafana dashboard `Cost-Performance-Overview.json` created
- Dashboard sections:
  - **Cost Metrics**: Total spend, per-service cost, projections
  - **Performance Metrics**: Latency, throughput, error rate
  - **Resource Utilization**: CPU, memory, storage efficiency
  - **Cost-Performance Ratio**: Cost per transaction, cost per user
  - **Optimization Recommendations**: Underutilized services
- Time range selector: Default 30 days, options for 7d, 30d, 90d, 6m, 1y
- Auto-refresh: Every 60 seconds
- Export: CSV download for finance reporting

### AC2: Per-Service Infrastructure Cost Panel
**Given** Cost accountability per service required  
**When** viewing per-service costs  
**Then**:
- Panel displays table with columns:
  - Service Name
  - Monthly Cost (current month to date)
  - Projected Month-End Cost (trend-based)
  - CPU Cost ($ per CPU-hour × usage)
  - Memory Cost ($ per GB-month × usage)
  - Storage Cost ($ per GB-month × usage)
  - Network Cost ($ per GB egress)
  - Month-over-Month Change (% increase/decrease)
- Cost calculation methodology:
  - Azure/AWS pricing: CPU $0.05/hour, Memory $0.01/GB-month, Storage $0.02/GB-month
  - Kubernetes metrics: resource requests × uptime × cost rate
- Top 10 most expensive services highlighted
- Drill-down: Click service → View resource breakdown

### AC3: Cost Per Transaction Panel
**Given** Business value correlation needed  
**When** calculating cost per transaction  
**Then**:
- Panel displays metrics:
  - **Loan Applications Processed**: Count for time range
  - **Total Infrastructure Cost**: Sum across all services
  - **Cost Per Loan Application**: Total cost / application count
  - **Cost Per Active User**: Total cost / monthly active users
  - **Cost Per API Request**: Total cost / total API requests
- Trend line showing cost efficiency over time
- Target thresholds:
  - Cost per loan application <$2 (green)
  - $2-$5 (yellow)
  - >$5 (red)
- Comparison with previous month

### AC4: Resource Utilization and Efficiency Panel
**Given** Resource optimization opportunities needed  
**When** analyzing resource utilization  
**Then**:
- Panel displays gauges:
  - **Average CPU Utilization**: Across all pods (target: 60-80%)
  - **Average Memory Utilization**: Across all pods (target: 60-80%)
  - **Pod Count**: Current vs. HPA max
  - **Storage Utilization**: Persistent volume usage
  - **Efficiency Score**: Weighted average (100 = optimal)
- Color coding:
  - Green: 60-80% utilization (efficient)
  - Yellow: 40-60% or 80-90% (review)
  - Red: <40% (underutilized) or >90% (overutilized)
- Table listing underutilized services (avg CPU <30%) for right-sizing

### AC5: SLA Compliance and Performance Panel
**Given** Performance targets must be met  
**When** monitoring SLA compliance  
**Then**:
- Panel displays SLA metrics per service:
  - **P50 Latency**: Median response time
  - **P95 Latency**: 95th percentile (SLA target: <2s)
  - **P99 Latency**: 99th percentile (SLA target: <5s)
  - **Error Rate**: Percentage of failed requests (target: <1%)
  - **Uptime**: Percentage (target: 99.9%)
- SLA compliance indicator:
  - All green: 100% compliant
  - Any red: Violations highlighted
- Historical SLA compliance trend (last 90 days)

### AC6: Cost Anomaly Detection Panel
**Given** Unexpected cost spikes need detection  
**When** monitoring cost anomalies  
**Then**:
- Panel displays:
  - **Active Anomalies**: Services with >20% cost increase month-over-month
  - **Anomaly Severity**: High (>50% increase), Medium (20-50%), Low (<20%)
  - **Root Cause Hints**: "Increased pod count", "Storage growth", "Network spike"
  - **Estimated Impact**: Additional $ spend per month
- Drill-down: Click anomaly → View detailed metrics
- Alert annotation: Red banner for high-severity anomalies

### AC7: Forecasting and Optimization Recommendations
**Given** Future planning and optimization needed  
**When** viewing forecasts and recommendations  
**Then**:
- Forecasting panel displays:
  - **3-Month Cost Projection**: Trend-based forecast
  - **Confidence Interval**: Min/max range (95% confidence)
  - **Seasonality Adjustment**: Account for usage patterns
- Optimization recommendations table:
  - Service Name
  - Recommendation Type (Rightsizing, Reserved Instances, Spot VMs)
  - Current Cost
  - Potential Savings ($/month)
  - Implementation Effort (Low/Medium/High)
- Recommendations sorted by savings potential

### AC8: Cost Allocation Tags and Chargeback
**Given** Business unit chargeback required  
**When** allocating costs by tags  
**Then**:
- Panel displays cost by tag:
  - **By Domain**: Loan Origination, Collections, Disbursements
  - **By Environment**: Dev, Staging, Production
  - **By Team**: Engineering, Operations, Data Science
- Pie chart visualizing cost distribution
- Chargeback report: Downloadable CSV with per-team breakdown
- Tag coverage indicator: % of costs with proper tags

### AC9: Executive Summary and Alerts
**Given** Executives need high-level overview  
**When** accessing executive summary  
**Then**:
- Summary panel displays:
  - **Total Monthly Spend**: Single stat with trend arrow
  - **Budget Utilization**: Gauge showing % of budget used
  - **Cost Efficiency Score**: 0-100 (higher is better)
  - **Top Cost Driver**: Service consuming most resources
  - **Biggest Optimization Opportunity**: Service with highest savings potential
- Active alerts displayed:
  - Budget approaching (>80% utilized)
  - SLA violation active
  - Cost anomaly detected
- One-click navigation to detailed views

---

## Technical Implementation Details

### Architecture Reference

**PRD Sections**: Lines 1419-1447 (Story 1.31 in PRD, renumbered as 1.32)  
**Architecture Sections**: Section 13 (Cost Management), Section 9 (Observability), Section 4 (Performance)  
**Requirements**: NFR8 (API response <500ms), NFR9 (P95 latency <2s), NFR22 (Infrastructure cost tracking)

### Technology Stack

- **Dashboard Platform**: Grafana 10.x
- **Data Sources**: Prometheus (resource metrics), PostgreSQL (cost data)
- **Cost Model**: Custom calculation based on cloud provider pricing
- **Metrics**: Kubernetes metrics, OpenTelemetry traces
- **Forecasting**: ML.NET time series forecasting

### Grafana Dashboard JSON (Excerpt)

```json
{
  "dashboard": {
    "title": "Cost-Performance Overview",
    "tags": ["cost", "performance", "finance", "sla"],
    "panels": [
      {
        "id": 1,
        "title": "Total Monthly Spend",
        "type": "stat",
        "targets": [{
          "expr": "sum(service_cost_usd{billing_period=\"$__interval\"})"
        }],
        "fieldConfig": {
          "defaults": {
            "unit": "currencyUSD",
            "thresholds": {
              "steps": [
                { "value": 0, "color": "green" },
                { "value": 10000, "color": "yellow" },
                { "value": 20000, "color": "red" }
              ]
            }
          }
        }
      },
      {
        "id": 2,
        "title": "Cost Per Service (Top 10)",
        "type": "table",
        "targets": [{
          "expr": "topk(10, sum by (service) (service_cost_usd))"
        }]
      },
      {
        "id": 3,
        "title": "Cost Per Transaction",
        "type": "stat",
        "targets": [{
          "expr": "sum(service_cost_usd) / sum(business_transactions_total)"
        }],
        "fieldConfig": {
          "defaults": {
            "unit": "currencyUSD",
            "decimals": 2
          }
        }
      },
      {
        "id": 4,
        "title": "Resource Utilization - CPU",
        "type": "gauge",
        "targets": [{
          "expr": "avg(rate(container_cpu_usage_seconds_total[5m])) * 100"
        }],
        "fieldConfig": {
          "defaults": {
            "unit": "percent",
            "thresholds": {
              "steps": [
                { "value": 0, "color": "red" },
                { "value": 40, "color": "yellow" },
                { "value": 60, "color": "green" },
                { "value": 80, "color": "yellow" },
                { "value": 90, "color": "red" }
              ]
            }
          }
        }
      },
      {
        "id": 5,
        "title": "SLA Compliance - P95 Latency",
        "type": "graph",
        "targets": [{
          "expr": "histogram_quantile(0.95, sum(rate(http_request_duration_seconds_bucket[5m])) by (le, service))"
        }]
      }
    ]
  }
}
```

### Cost Calculation Service

```csharp
// Services/CostPerformanceService.cs
namespace IntelliFin.Admin.Services
{
    public interface ICostPerformanceService
    {
        Task<ServiceCostBreakdown> GetServiceCostsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken);
        Task<CostPerTransactionMetrics> GetCostPerTransactionAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken);
        Task<ResourceUtilizationMetrics> GetResourceUtilizationAsync(CancellationToken cancellationToken);
        Task<List<OptimizationRecommendation>> GetOptimizationRecommendationsAsync(CancellationToken cancellationToken);
    }

    public class CostPerformanceService : ICostPerformanceService
    {
        private const decimal CPU_COST_PER_HOUR = 0.05m;
        private const decimal MEMORY_COST_PER_GB_MONTH = 0.01m;
        private const decimal STORAGE_COST_PER_GB_MONTH = 0.02m;
        private const decimal NETWORK_COST_PER_GB = 0.05m;

        private readonly IPrometheusQueryService _prometheusService;
        private readonly AdminDbContext _dbContext;
        private readonly ILogger<CostPerformanceService> _logger;

        public async Task<ServiceCostBreakdown> GetServiceCostsAsync(
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken)
        {
            var services = await _prometheusService.GetServiceNamesAsync(cancellationToken);
            var serviceCosts = new List<ServiceCost>();

            foreach (var service in services)
            {
                // Get CPU usage
                var cpuUsage = await _prometheusService.QueryAsync(
                    $"avg(rate(container_cpu_usage_seconds_total{{service=\"{service}\"}}[30d]))",
                    cancellationToken);

                // Get memory usage
                var memoryUsage = await _prometheusService.QueryAsync(
                    $"avg(container_memory_usage_bytes{{service=\"{service}\"}}[30d]) / 1024 / 1024 / 1024",
                    cancellationToken);

                // Get storage usage
                var storageUsage = await _prometheusService.QueryAsync(
                    $"sum(pvc_bytes{{service=\"{service}\"}}) / 1024 / 1024 / 1024",
                    cancellationToken);

                // Calculate costs
                var hoursInMonth = (endDate - startDate).TotalHours;
                var cpuCost = cpuUsage * (decimal)hoursInMonth * CPU_COST_PER_HOUR;
                var memoryCost = memoryUsage * MEMORY_COST_PER_GB_MONTH;
                var storageCost = storageUsage * STORAGE_COST_PER_GB_MONTH;

                serviceCosts.Add(new ServiceCost
                {
                    ServiceName = service,
                    CpuCost = cpuCost,
                    MemoryCost = memoryCost,
                    StorageCost = storageCost,
                    TotalCost = cpuCost + memoryCost + storageCost
                });
            }

            return new ServiceCostBreakdown
            {
                Services = serviceCosts,
                TotalCost = serviceCosts.Sum(s => s.TotalCost),
                StartDate = startDate,
                EndDate = endDate
            };
        }

        public async Task<CostPerTransactionMetrics> GetCostPerTransactionAsync(
            DateTime startDate,
            DateTime endDate,
            CancellationToken cancellationToken)
        {
            var totalCost = await _dbContext.CostRecords
                .Where(c => c.RecordDate >= startDate && c.RecordDate <= endDate)
                .SumAsync(c => c.CostUSD, cancellationToken);

            // Get transaction count from Prometheus
            var transactionCount = await _prometheusService.QueryAsync(
                $"sum(increase(business_transactions_total[{(endDate - startDate).TotalDays}d]))",
                cancellationToken);

            var costPerTransaction = transactionCount > 0 ? totalCost / transactionCount : 0;

            return new CostPerTransactionMetrics
            {
                TotalCost = totalCost,
                TransactionCount = (long)transactionCount,
                CostPerTransaction = costPerTransaction,
                Period = $"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}"
            };
        }
    }
}
```

---

## Integration Verification

### IV1: Cost Calculation Accuracy
**Verification Steps**:
1. Compare calculated costs with actual cloud bills
2. Validate within 10% accuracy
3. Verify cost allocation per service
4. Test cost projections against actuals

**Success Criteria**:
- Cost estimates within 10% of actual
- All services represented
- Cost allocation adds up to total

### IV2: Performance Correlation
**Verification Steps**:
1. Cross-validate latency metrics with Jaeger traces
2. Compare resource utilization with Kubernetes metrics
3. Verify SLA compliance calculations
4. Test alert thresholds

**Success Criteria**:
- Metrics match source data
- SLA compliance accurate
- Alerts trigger correctly

### IV3: Optimization Recommendations
**Verification Steps**:
1. Identify intentionally over-provisioned test service
2. Verify recommendation generated
3. Calculate expected savings
4. Validate effort assessment

**Success Criteria**:
- Recommendations generated for underutilized services
- Savings calculations reasonable
- Effort estimates accurate

---

## Testing Strategy

### Unit Tests

```csharp
[Fact]
public async Task CalculateServiceCosts_ReturnsAccurateCosts()
{
    var service = CreateCostPerformanceService();
    var startDate = new DateTime(2025, 1, 1);
    var endDate = new DateTime(2025, 1, 31);

    var costs = await service.GetServiceCostsAsync(startDate, endDate, CancellationToken.None);

    Assert.NotNull(costs);
    Assert.True(costs.TotalCost > 0);
    Assert.All(costs.Services, s => Assert.True(s.TotalCost >= 0));
}
```

---

## Risks and Mitigation

| Risk | Impact | Probability | Mitigation |
|------|---------|-------------|------------|
| Cost model inaccuracy | Incorrect financial decisions | Medium | Regular validation against actual bills. Quarterly model updates. |
| Metric data gaps | Incomplete cost picture | Low | Comprehensive monitoring coverage. Alert on missing data. |
| Performance overhead | Expensive queries slow dashboard | Medium | Optimize Prometheus queries. Implement caching. Recording rules. |

---

## Definition of Done

- [ ] Grafana dashboard created and deployed
- [ ] All cost calculation formulas implemented
- [ ] Performance metrics integrated
- [ ] Optimization recommendations engine working
- [ ] CSV export functionality tested
- [ ] Dashboard performance <5s load time
- [ ] Integration tests: Cost accuracy, recommendations
- [ ] Documentation: Cost model, metric definitions
- [ ] User training: CFO and finance team trained

---

## Related Documentation

### PRD References
- **Lines 1419-1447**: Story 1.31 detailed requirements (renumbered as 1.32)
- **NFR8**: API response time <500ms
- **NFR9**: P95 latency <2s
- **NFR22**: Infrastructure cost tracking

---

**Story Created**: 2025-10-11  
**Last Updated**: 2025-10-11  
**Next Story**: [Story 1.33: Automated Alerting and Incident Response](./story-1.33-automated-alerting.md)
