using IntelliFin.FinancialService.Models;
using IntelliFin.Shared.DomainModels.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace IntelliFin.FinancialService.Services;

public class ComplianceMonitoringService : IComplianceMonitoringService
{
    private readonly LmsDbContext _dbContext;
    private readonly IBozComplianceService _bozComplianceService;
    private readonly IDistributedCache _cache;
    private readonly ILogger<ComplianceMonitoringService> _logger;
    private readonly IConfiguration _configuration;

    private const string ComplianceAlertsCachePrefix = "compliance_alerts:";
    private const string ComplianceRulesCachePrefix = "compliance_rules:";
    private const int DefaultCacheExpirationMinutes = 15;

    // In-memory storage for demo purposes - in production, use database
    private readonly Dictionary<string, ComplianceAlert> _alerts = new();
    private readonly Dictionary<string, ComplianceRule> _rules = new();
    private readonly List<ComplianceHistoryEntry> _history = new();

    public ComplianceMonitoringService(
        LmsDbContext dbContext,
        IBozComplianceService bozComplianceService,
        IDistributedCache cache,
        ILogger<ComplianceMonitoringService> logger,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _bozComplianceService = bozComplianceService;
        _cache = cache;
        _logger = logger;
        _configuration = configuration;
        
        InitializeDefaultRules();
    }

    public async Task<ComplianceMonitoringResult> MonitorComplianceAsync(string branchId, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger.LogInformation("Starting compliance monitoring for branch {BranchId}", branchId);

            var rules = await GetComplianceRulesAsync(cancellationToken: cancellationToken);
            var ruleResults = new List<ComplianceRuleResult>();
            var newAlerts = new List<ComplianceAlert>();

            foreach (var rule in rules.Where(r => r.IsEnabled))
            {
                try
                {
                    var ruleResult = await CheckComplianceRuleAsync(rule.Id, branchId, cancellationToken);
                    ruleResults.Add(ruleResult);

                    // Create alert if rule fails or warning
                    if (ruleResult.Status == ComplianceStatus.NonCompliant || ruleResult.Status == ComplianceStatus.Warning)
                    {
                        var alert = await CreateAlertFromRuleResult(ruleResult, branchId);
                        if (alert != null)
                        {
                            newAlerts.Add(alert);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking compliance rule {RuleId} for branch {BranchId}", rule.Id, branchId);
                    ruleResults.Add(new ComplianceRuleResult
                    {
                        RuleId = rule.Id,
                        RuleName = rule.Name,
                        Category = rule.Category,
                        Status = ComplianceStatus.Unknown,
                        Message = $"Error checking rule: {ex.Message}",
                        Severity = ComplianceSeverity.Medium
                    });
                }
            }

            var result = new ComplianceMonitoringResult
            {
                BranchId = branchId,
                TotalRulesChecked = ruleResults.Count,
                RulesPassed = ruleResults.Count(r => r.Status == ComplianceStatus.Compliant),
                RulesWarning = ruleResults.Count(r => r.Status == ComplianceStatus.Warning),
                RulesFailed = ruleResults.Count(r => r.Status == ComplianceStatus.NonCompliant),
                RuleResults = ruleResults,
                NewAlerts = newAlerts,
                MonitoringDuration = DateTime.UtcNow - startTime
            };

            // Determine overall status
            if (result.RulesFailed > 0)
                result.OverallStatus = ComplianceStatus.NonCompliant;
            else if (result.RulesWarning > 0)
                result.OverallStatus = ComplianceStatus.Warning;
            else
                result.OverallStatus = ComplianceStatus.Compliant;

            // Log monitoring history
            await LogComplianceHistoryAsync(branchId, "MONITORING_COMPLETED", $"Monitoring completed: {result.RulesPassed}/{result.TotalRulesChecked} rules passed", cancellationToken);

            _logger.LogInformation("Compliance monitoring completed for branch {BranchId}: {Status} ({Duration:N2}ms)", 
                branchId, result.OverallStatus, result.MonitoringDuration.TotalMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during compliance monitoring for branch {BranchId}", branchId);
            throw;
        }
    }

    public async Task<ComplianceRuleResult> CheckComplianceRuleAsync(string ruleId, string branchId, CancellationToken cancellationToken = default)
    {
        try
        {
            var rule = await GetRuleByIdAsync(ruleId);
            if (rule == null)
            {
                return new ComplianceRuleResult
                {
                    RuleId = ruleId,
                    Status = ComplianceStatus.Unknown,
                    Message = "Rule not found"
                };
            }

            // Delegate to specific compliance services based on category
            return rule.Category switch
            {
                ComplianceRuleCategory.CapitalAdequacy => await _bozComplianceService.CheckCapitalAdequacyRatioAsync(branchId, cancellationToken),
                ComplianceRuleCategory.LoanClassification => await _bozComplianceService.CheckLoanClassificationComplianceAsync(branchId, cancellationToken),
                ComplianceRuleCategory.Provisioning => await _bozComplianceService.CheckProvisionCoverageAsync(branchId, cancellationToken),
                ComplianceRuleCategory.LargeExposures => await _bozComplianceService.CheckLargeExposureLimitsAsync(branchId, cancellationToken),
                ComplianceRuleCategory.RegulatoryReporting => await _bozComplianceService.CheckReportingDeadlinesAsync(branchId, cancellationToken),
                ComplianceRuleCategory.LiquidityRatio => await _bozComplianceService.CheckLiquidityRatiosAsync(branchId, cancellationToken),
                _ => await CheckGenericRuleAsync(rule, branchId, cancellationToken)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking compliance rule {RuleId} for branch {BranchId}", ruleId, branchId);
            throw;
        }
    }

    public async Task<List<ComplianceAlert>> GetComplianceAlertsAsync(string branchId, ComplianceAlertStatus? status = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = $"{ComplianceAlertsCachePrefix}{branchId}:{status}";
            var cachedData = await _cache.GetStringAsync(cacheKey, cancellationToken);
            
            if (!string.IsNullOrEmpty(cachedData))
            {
                var cached = JsonSerializer.Deserialize<List<ComplianceAlert>>(cachedData);
                if (cached != null) return cached;
            }

            var alerts = _alerts.Values
                .Where(a => a.BranchId == branchId)
                .Where(a => !status.HasValue || a.Status == status.Value)
                .OrderByDescending(a => a.CreatedAt)
                .ToList();

            // Cache the result
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(DefaultCacheExpirationMinutes)
            };
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(alerts), cacheOptions, cancellationToken);

            return alerts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting compliance alerts for branch {BranchId}", branchId);
            throw;
        }
    }

    public async Task<ComplianceDashboardMetrics> GetComplianceDashboardAsync(string branchId, CancellationToken cancellationToken = default)
    {
        try
        {
            var alerts = await GetComplianceAlertsAsync(branchId, cancellationToken: cancellationToken);
            var activeAlerts = alerts.Where(a => a.Status == ComplianceAlertStatus.Active).ToList();
            
            var metrics = new ComplianceDashboardMetrics
            {
                BranchId = branchId,
                ActiveAlerts = activeAlerts.Count,
                CriticalAlerts = activeAlerts.Count(a => a.Severity == ComplianceSeverity.Critical),
                WarningAlerts = activeAlerts.Count(a => a.Severity == ComplianceSeverity.Medium || a.Severity == ComplianceSeverity.High),
                ResolvedAlertsToday = alerts.Count(a => a.Status == ComplianceAlertStatus.Resolved && 
                                                      a.ResolvedAt.HasValue && 
                                                      a.ResolvedAt.Value.Date == DateTime.Today),
                AlertsByCategory = alerts.GroupBy(a => a.Category).ToDictionary(g => g.Key, g => g.Count()),
                RecentAlerts = alerts.Take(10).ToList()
            };

            // Calculate compliance score (percentage of rules that are compliant)
            var totalRules = _rules.Count;
            var nonCompliantRules = activeAlerts.Select(a => a.RuleId).Distinct().Count();
            metrics.ComplianceScore = totalRules > 0 ? ((double)(totalRules - nonCompliantRules) / totalRules) * 100 : 100;

            // Set overall status based on critical alerts
            if (metrics.CriticalAlerts > 0)
                metrics.OverallStatus = ComplianceStatus.NonCompliant;
            else if (metrics.WarningAlerts > 0)
                metrics.OverallStatus = ComplianceStatus.Warning;
            else
                metrics.OverallStatus = ComplianceStatus.Compliant;

            // Generate trends (last 30 days)
            metrics.Trends = GenerateComplianceTrends(branchId, 30);

            // Add key metrics
            metrics.KeyMetrics = await GetKeyComplianceMetricsAsync(branchId, cancellationToken);

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting compliance dashboard for branch {BranchId}", branchId);
            throw;
        }
    }

    public async Task AcknowledgeAlertAsync(string alertId, string acknowledgedBy, string? notes = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_alerts.TryGetValue(alertId, out var alert))
            {
                alert.Status = ComplianceAlertStatus.Acknowledged;
                alert.AcknowledgedAt = DateTime.UtcNow;
                alert.AcknowledgedBy = acknowledgedBy;
                alert.AcknowledgementNotes = notes;

                await InvalidateAlertsCache(alert.BranchId);
                await LogComplianceHistoryAsync(alert.BranchId, "ALERT_ACKNOWLEDGED", $"Alert {alertId} acknowledged by {acknowledgedBy}", cancellationToken);

                _logger.LogInformation("Compliance alert {AlertId} acknowledged by {User}", alertId, acknowledgedBy);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acknowledging compliance alert {AlertId}", alertId);
            throw;
        }
    }

    public async Task ResolveAlertAsync(string alertId, string resolvedBy, string resolutionNotes, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_alerts.TryGetValue(alertId, out var alert))
            {
                alert.Status = ComplianceAlertStatus.Resolved;
                alert.ResolvedAt = DateTime.UtcNow;
                alert.ResolvedBy = resolvedBy;
                alert.ResolutionNotes = resolutionNotes;

                await InvalidateAlertsCache(alert.BranchId);
                await LogComplianceHistoryAsync(alert.BranchId, "ALERT_RESOLVED", $"Alert {alertId} resolved by {resolvedBy}: {resolutionNotes}", cancellationToken);

                _logger.LogInformation("Compliance alert {AlertId} resolved by {User}", alertId, resolvedBy);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving compliance alert {AlertId}", alertId);
            throw;
        }
    }

    public async Task<string> CreateManualAlertAsync(CreateComplianceAlertRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var alert = new ComplianceAlert
            {
                Id = Guid.NewGuid().ToString(),
                BranchId = request.BranchId,
                RuleId = "MANUAL",
                RuleName = "Manual Alert",
                Category = request.Category,
                Severity = request.Severity,
                Status = ComplianceAlertStatus.Active,
                Title = request.Title,
                Description = request.Description,
                RecommendedAction = request.RecommendedAction,
                Data = request.Data,
                DueDate = request.DueDate,
                IsRegulatory = request.IsRegulatory
            };

            _alerts[alert.Id] = alert;
            await InvalidateAlertsCache(request.BranchId);
            await LogComplianceHistoryAsync(request.BranchId, "MANUAL_ALERT_CREATED", $"Manual alert created: {request.Title}", cancellationToken);

            _logger.LogInformation("Manual compliance alert created: {AlertId} for branch {BranchId}", alert.Id, request.BranchId);

            return alert.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating manual compliance alert for branch {BranchId}", request.BranchId);
            throw;
        }
    }

    public async Task<List<ComplianceRule>> GetComplianceRulesAsync(ComplianceRuleCategory? category = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = $"{ComplianceRulesCachePrefix}{category}";
            var cachedData = await _cache.GetStringAsync(cacheKey, cancellationToken);
            
            if (!string.IsNullOrEmpty(cachedData))
            {
                var cached = JsonSerializer.Deserialize<List<ComplianceRule>>(cachedData);
                if (cached != null) return cached;
            }

            var rules = _rules.Values
                .Where(r => !category.HasValue || r.Category == category.Value)
                .ToList();

            // Cache the result
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) // Rules change less frequently
            };
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(rules), cacheOptions, cancellationToken);

            return rules;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting compliance rules");
            throw;
        }
    }

    public async Task UpdateComplianceRuleAsync(string ruleId, UpdateComplianceRuleRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_rules.TryGetValue(ruleId, out var rule))
            {
                if (request.Name != null) rule.Name = request.Name;
                if (request.Description != null) rule.Description = request.Description;
                if (request.IsEnabled.HasValue) rule.IsEnabled = request.IsEnabled.Value;
                if (request.WarningThreshold.HasValue) rule.WarningThreshold = request.WarningThreshold.Value;
                if (request.CriticalThreshold.HasValue) rule.CriticalThreshold = request.CriticalThreshold.Value;
                if (request.CheckFrequency.HasValue) rule.CheckFrequency = request.CheckFrequency.Value;
                if (request.Configuration != null) rule.Configuration = request.Configuration;

                rule.LastModified = DateTime.UtcNow;

                // Invalidate cache
                await _cache.RemoveAsync($"{ComplianceRulesCachePrefix}", cancellationToken);
                foreach (var category in Enum.GetValues<ComplianceRuleCategory>())
                {
                    await _cache.RemoveAsync($"{ComplianceRulesCachePrefix}{category}", cancellationToken);
                }

                _logger.LogInformation("Compliance rule {RuleId} updated", ruleId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating compliance rule {RuleId}", ruleId);
            throw;
        }
    }

    public async Task<ComplianceReport> GenerateComplianceReportAsync(DateTime startDate, DateTime endDate, string branchId, CancellationToken cancellationToken = default)
    {
        try
        {
            var alerts = await GetComplianceAlertsAsync(branchId, cancellationToken: cancellationToken);
            var periodAlerts = alerts.Where(a => a.CreatedAt >= startDate && a.CreatedAt <= endDate).ToList();

            var report = new ComplianceReport
            {
                Id = Guid.NewGuid().ToString(),
                BranchId = branchId,
                StartDate = startDate,
                EndDate = endDate,
                TotalAlertsGenerated = periodAlerts.Count,
                AlertsResolved = periodAlerts.Count(a => a.Status == ComplianceAlertStatus.Resolved),
                CriticalAlerts = periodAlerts.Where(a => a.Severity == ComplianceSeverity.Critical).ToList()
            };

            // Calculate category metrics
            foreach (var category in Enum.GetValues<ComplianceRuleCategory>())
            {
                var categoryAlerts = periodAlerts.Where(a => a.Category == category).ToList();
                report.CategoryMetrics[category] = new ComplianceMetrics
                {
                    TotalAlerts = categoryAlerts.Count,
                    ResolvedAlerts = categoryAlerts.Count(a => a.Status == ComplianceAlertStatus.Resolved),
                    PendingAlerts = categoryAlerts.Count(a => a.Status != ComplianceAlertStatus.Resolved),
                    AverageResolutionTime = CalculateAverageResolutionTime(categoryAlerts),
                    CompliancePercentage = CalculateCompliancePercentage(categoryAlerts)
                };
            }

            // Set overall status and score
            report.ComplianceScore = report.CategoryMetrics.Values.Any() 
                ? report.CategoryMetrics.Values.Average(m => m.CompliancePercentage)
                : 100;

            if (report.CriticalAlerts.Any())
                report.OverallStatus = ComplianceStatus.NonCompliant;
            else if (report.ComplianceScore < 90)
                report.OverallStatus = ComplianceStatus.Warning;
            else
                report.OverallStatus = ComplianceStatus.Compliant;

            // Generate recommendations
            report.Recommendations = GenerateRecommendations(report);

            _logger.LogInformation("Compliance report generated for branch {BranchId} ({StartDate} to {EndDate})", 
                branchId, startDate, endDate);

            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating compliance report for branch {BranchId}", branchId);
            throw;
        }
    }

    public async Task ScheduleComplianceMonitoringAsync(string branchId, ComplianceMonitoringSchedule schedule, CancellationToken cancellationToken = default)
    {
        try
        {
            // This would integrate with a job scheduler like Hangfire
            schedule.Id = Guid.NewGuid().ToString();
            schedule.NextRun = DateTime.UtcNow.Add(schedule.Frequency);

            // Store schedule (in production, use database)
            _logger.LogInformation("Compliance monitoring scheduled for branch {BranchId} with frequency {Frequency}", 
                branchId, schedule.Frequency);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling compliance monitoring for branch {BranchId}", branchId);
            throw;
        }
    }

    public async Task<List<ComplianceHistoryEntry>> GetComplianceHistoryAsync(string branchId, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var history = _history
                .Where(h => h.BranchId == branchId)
                .Where(h => !startDate.HasValue || h.Date >= startDate.Value)
                .Where(h => !endDate.HasValue || h.Date <= endDate.Value)
                .OrderByDescending(h => h.Date)
                .ToList();

            return history;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting compliance history for branch {BranchId}", branchId);
            throw;
        }
    }

    private async Task<ComplianceRuleResult> CheckGenericRuleAsync(ComplianceRule rule, string branchId, CancellationToken cancellationToken)
    {
        // Generic rule checking logic - would implement based on rule configuration
        return new ComplianceRuleResult
        {
            RuleId = rule.Id,
            RuleName = rule.Name,
            Category = rule.Category,
            Status = ComplianceStatus.Compliant,
            Message = "Generic rule check passed",
            Severity = rule.DefaultSeverity
        };
    }

    private async Task<ComplianceAlert?> CreateAlertFromRuleResult(ComplianceRuleResult result, string branchId)
    {
        // Check if alert already exists for this rule
        var existingAlert = _alerts.Values.FirstOrDefault(a => 
            a.BranchId == branchId && 
            a.RuleId == result.RuleId && 
            a.Status == ComplianceAlertStatus.Active);

        if (existingAlert != null)
        {
            return null; // Don't create duplicate alerts
        }

        var alert = new ComplianceAlert
        {
            Id = Guid.NewGuid().ToString(),
            BranchId = branchId,
            RuleId = result.RuleId,
            RuleName = result.RuleName,
            Category = result.Category,
            Severity = result.Severity,
            Status = ComplianceAlertStatus.Active,
            Title = $"Compliance Alert: {result.RuleName}",
            Description = result.Message ?? "Compliance rule failed",
            Data = result.Metrics,
            IsRegulatory = IsRegulatoryCategory(result.Category)
        };

        _alerts[alert.Id] = alert;
        await InvalidateAlertsCache(branchId);

        return alert;
    }

    private async Task<ComplianceRule?> GetRuleByIdAsync(string ruleId)
    {
        return _rules.TryGetValue(ruleId, out var rule) ? rule : null;
    }

    private async Task InvalidateAlertsCache(string branchId)
    {
        foreach (var status in Enum.GetValues<ComplianceAlertStatus>())
        {
            await _cache.RemoveAsync($"{ComplianceAlertsCachePrefix}{branchId}:{status}");
        }
        await _cache.RemoveAsync($"{ComplianceAlertsCachePrefix}{branchId}:");
    }

    private async Task LogComplianceHistoryAsync(string branchId, string action, string details, CancellationToken cancellationToken)
    {
        var entry = new ComplianceHistoryEntry
        {
            Id = Guid.NewGuid().ToString(),
            BranchId = branchId,
            Date = DateTime.UtcNow,
            Action = action,
            Details = details
        };

        _history.Add(entry);
        await Task.CompletedTask;
    }

    private List<ComplianceTrend> GenerateComplianceTrends(string branchId, int days)
    {
        var trends = new List<ComplianceTrend>();
        var startDate = DateTime.Today.AddDays(-days);

        for (int i = 0; i < days; i++)
        {
            var date = startDate.AddDays(i);
            var dayAlerts = _alerts.Values
                .Where(a => a.BranchId == branchId && a.CreatedAt.Date == date)
                .ToList();

            foreach (var category in Enum.GetValues<ComplianceRuleCategory>())
            {
                var categoryAlerts = dayAlerts.Where(a => a.Category == category).ToList();
                trends.Add(new ComplianceTrend
                {
                    Date = date,
                    Category = category,
                    AlertCount = categoryAlerts.Count,
                    ComplianceScore = CalculateCompliancePercentage(categoryAlerts)
                });
            }
        }

        return trends;
    }

    private async Task<Dictionary<string, object>> GetKeyComplianceMetricsAsync(string branchId, CancellationToken cancellationToken)
    {
        return new Dictionary<string, object>
        {
            ["averageResolutionTime"] = CalculateAverageResolutionTime(_alerts.Values.Where(a => a.BranchId == branchId).ToList()),
            ["escalationRate"] = CalculateEscalationRate(branchId),
            ["complianceScore"] = await CalculateOverallComplianceScoreAsync(branchId),
            ["criticalAlertsLast30Days"] = _alerts.Values.Count(a => a.BranchId == branchId && 
                a.Severity == ComplianceSeverity.Critical && 
                a.CreatedAt >= DateTime.UtcNow.AddDays(-30))
        };
    }

    private double CalculateAverageResolutionTime(List<ComplianceAlert> alerts)
    {
        var resolvedAlerts = alerts.Where(a => a.ResolvedAt.HasValue).ToList();
        if (!resolvedAlerts.Any()) return 0;

        var totalHours = resolvedAlerts.Sum(a => (a.ResolvedAt!.Value - a.CreatedAt).TotalHours);
        return totalHours / resolvedAlerts.Count;
    }

    private double CalculateCompliancePercentage(List<ComplianceAlert> alerts)
    {
        if (!alerts.Any()) return 100;
        var resolvedAlerts = alerts.Count(a => a.Status == ComplianceAlertStatus.Resolved);
        return ((double)resolvedAlerts / alerts.Count) * 100;
    }

    private double CalculateEscalationRate(string branchId)
    {
        var branchAlerts = _alerts.Values.Where(a => a.BranchId == branchId).ToList();
        if (!branchAlerts.Any()) return 0;
        
        var escalatedAlerts = branchAlerts.Count(a => a.Status == ComplianceAlertStatus.Escalated);
        return ((double)escalatedAlerts / branchAlerts.Count) * 100;
    }

    private async Task<double> CalculateOverallComplianceScoreAsync(string branchId)
    {
        var totalRules = _rules.Count;
        var failedRules = _alerts.Values.Count(a => a.BranchId == branchId && a.Status == ComplianceAlertStatus.Active);
        return totalRules > 0 ? ((double)(totalRules - failedRules) / totalRules) * 100 : 100;
    }

    private List<string> GenerateRecommendations(ComplianceReport report)
    {
        var recommendations = new List<string>();

        if (report.CriticalAlerts.Any())
        {
            recommendations.Add("Address critical compliance alerts immediately to avoid regulatory penalties");
        }

        if (report.ComplianceScore < 80)
        {
            recommendations.Add("Implement additional compliance monitoring procedures to improve overall score");
        }

        var highVolumeCategories = report.CategoryMetrics
            .Where(kvp => kvp.Value.TotalAlerts > 5)
            .OrderByDescending(kvp => kvp.Value.TotalAlerts)
            .Take(3);

        foreach (var category in highVolumeCategories)
        {
            recommendations.Add($"Focus on improving {category.Key} compliance - highest alert volume category");
        }

        return recommendations;
    }

    private bool IsRegulatoryCategory(ComplianceRuleCategory category)
    {
        return category switch
        {
            ComplianceRuleCategory.CapitalAdequacy => true,
            ComplianceRuleCategory.LoanClassification => true,
            ComplianceRuleCategory.Provisioning => true,
            ComplianceRuleCategory.LargeExposures => true,
            ComplianceRuleCategory.LiquidityRatio => true,
            ComplianceRuleCategory.RegulatoryReporting => true,
            _ => false
        };
    }

    private void InitializeDefaultRules()
    {
        var defaultRules = new[]
        {
            new ComplianceRule
            {
                Id = "CAR_MINIMUM",
                Name = "Minimum Capital Adequacy Ratio",
                Description = "Ensures CAR is above BoZ minimum requirement of 10%",
                Category = ComplianceRuleCategory.CapitalAdequacy,
                DefaultSeverity = ComplianceSeverity.Critical,
                WarningThreshold = 12.0,
                CriticalThreshold = 10.0,
                ThresholdUnit = "percentage",
                CheckFrequency = TimeSpan.FromDays(1)
            },
            new ComplianceRule
            {
                Id = "NPL_CLASSIFICATION",
                Name = "Non-Performing Loan Classification",
                Description = "Ensures loans are properly classified according to BoZ guidelines",
                Category = ComplianceRuleCategory.LoanClassification,
                DefaultSeverity = ComplianceSeverity.High,
                CheckFrequency = TimeSpan.FromDays(1)
            },
            new ComplianceRule
            {
                Id = "PROVISION_COVERAGE",
                Name = "Provision Coverage Ratio",
                Description = "Ensures adequate provisions are maintained for loan losses",
                Category = ComplianceRuleCategory.Provisioning,
                DefaultSeverity = ComplianceSeverity.High,
                WarningThreshold = 80.0,
                CriticalThreshold = 70.0,
                ThresholdUnit = "percentage",
                CheckFrequency = TimeSpan.FromDays(1)
            },
            new ComplianceRule
            {
                Id = "LARGE_EXPOSURE_LIMIT",
                Name = "Large Exposure Limits",
                Description = "Monitors compliance with large exposure limits (25% of capital)",
                Category = ComplianceRuleCategory.LargeExposures,
                DefaultSeverity = ComplianceSeverity.Critical,
                CriticalThreshold = 25.0,
                ThresholdUnit = "percentage",
                CheckFrequency = TimeSpan.FromDays(1)
            },
            new ComplianceRule
            {
                Id = "REPORTING_DEADLINES",
                Name = "Regulatory Reporting Deadlines",
                Description = "Ensures all BoZ reports are submitted on time",
                Category = ComplianceRuleCategory.RegulatoryReporting,
                DefaultSeverity = ComplianceSeverity.High,
                CheckFrequency = TimeSpan.FromDays(1)
            }
        };

        foreach (var rule in defaultRules)
        {
            _rules[rule.Id] = rule;
        }
    }
}