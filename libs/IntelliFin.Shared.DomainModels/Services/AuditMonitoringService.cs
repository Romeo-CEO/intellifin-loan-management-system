using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.Shared.DomainModels.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace IntelliFin.Shared.DomainModels.Services;

/// <summary>
/// Real-time audit monitoring and alerting service
/// </summary>
public class AuditMonitoringService : BackgroundService, IAuditMonitoringService
{
    private readonly LmsDbContext _dbContext;
    private readonly IDistributedCache _cache;
    private readonly ILogger<AuditMonitoringService> _logger;

    // In-memory storage for real-time monitoring
    private readonly ConcurrentDictionary<string, AuditMonitoringRule> _rules = new();
    private readonly ConcurrentDictionary<string, AuditMonitoringAlert> _activeAlerts = new();
    private readonly ConcurrentQueue<AuditEvent> _eventQueue = new();
    
    private readonly Timer _monitoringTimer;
    private readonly object _lockObject = new();

    private const string RulesCacheKey = "audit_monitoring_rules";
    private const string AlertsCachePrefix = "audit_alert:";
    private const int ProcessingIntervalSeconds = 5;

    public AuditMonitoringService(
        LmsDbContext dbContext,
        IDistributedCache cache,
        ILogger<AuditMonitoringService> logger)
    {
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;

        _monitoringTimer = new Timer(ProcessEventQueue, null, TimeSpan.Zero, TimeSpan.FromSeconds(ProcessingIntervalSeconds));
        
        // Initialize default monitoring rules
        InitializeDefaultRules();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Audit monitoring service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessMonitoringCycle(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in audit monitoring cycle");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        _logger.LogInformation("Audit monitoring service stopped");
    }

    public async Task StartMonitoringAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting audit monitoring");
        await LoadMonitoringRulesAsync(cancellationToken);
    }

    public async Task StopMonitoringAsync()
    {
        _logger.LogInformation("Stopping audit monitoring");
        await _monitoringTimer.DisposeAsync();
    }

    public Task ProcessEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        // Enqueue incoming event for background processing and return completed task to satisfy interface
        _eventQueue.Enqueue(auditEvent);
        _logger.LogTrace("Audit event queued for monitoring: {EventId}", auditEvent.Id);
        return Task.CompletedTask;
    }

    public Task<List<AuditMonitoringAlert>> GetActiveAlertsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_activeAlerts.Values
            .Where(alert => alert.Status == AuditMonitoringAlertStatus.Active)
            .OrderByDescending(alert => alert.TriggeredAt)
            .ToList());
    }

    public async Task AcknowledgeAlertAsync(string alertId, string acknowledgedBy, string? notes = null, CancellationToken cancellationToken = default)
    {
        if (_activeAlerts.TryGetValue(alertId, out var alert))
        {
            alert.Status = AuditMonitoringAlertStatus.Acknowledged;
            alert.AcknowledgedAt = DateTime.UtcNow;
            alert.AcknowledgedBy = acknowledgedBy;
            alert.AcknowledgementNotes = notes;

            // Cache the updated alert
            await CacheAlertAsync(alert, cancellationToken);

            _logger.LogInformation("Alert {AlertId} acknowledged by {User}", alertId, acknowledgedBy);
        }
    }

    public async Task ConfigureMonitoringRulesAsync(List<AuditMonitoringRule> rules, CancellationToken cancellationToken = default)
    {
        lock (_lockObject)
        {
            _rules.Clear();
            foreach (var rule in rules)
            {
                _rules.TryAdd(rule.Id, rule);
            }
        }

        // Cache the rules
        var rulesJson = JsonSerializer.Serialize(rules);
        await _cache.SetStringAsync(RulesCacheKey, rulesJson, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
        }, cancellationToken);

        _logger.LogInformation("Configured {RuleCount} audit monitoring rules", rules.Count);
    }

    public async Task<AuditMonitoringStatistics> GetMonitoringStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var todayAlerts = _activeAlerts.Values.Where(a => a.TriggeredAt >= today).ToList();

        var statistics = new AuditMonitoringStatistics
        {
            ActiveRules = _rules.Count,
            ActiveAlerts = _activeAlerts.Values.Count(a => a.Status == AuditMonitoringAlertStatus.Active),
            AcknowledgedAlerts = _activeAlerts.Values.Count(a => a.Status == AuditMonitoringAlertStatus.Acknowledged),
            TotalAlertsToday = todayAlerts.Count,
            AlertsByType = todayAlerts.GroupBy(a => a.Type).ToDictionary(g => g.Key, g => g.Count()),
            AlertsBySeverity = todayAlerts.GroupBy(a => a.Severity).ToDictionary(g => g.Key, g => g.Count()),
            TopTriggeredRules = _rules.Values
                .OrderByDescending(r => r.TriggerCount)
                .Take(5)
                .ToList(),
            AverageResponseTime = CalculateAverageResponseTime(todayAlerts),
            AlertTrend = await GenerateAlertTrendAsync(cancellationToken)
        };

        return statistics;
    }

    public async Task<AuditMonitoringTestResult> TestMonitoringAsync(CancellationToken cancellationToken = default)
    {
        var result = new AuditMonitoringTestResult();
        var errors = new List<string>();
        var warnings = new List<string>();

        try
        {
            // Test database connectivity
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            if (canConnect)
            {
                result.TestResults.Add("Database connectivity: OK");
            }
            else
            {
                errors.Add("Cannot connect to database");
            }

            // Test cache connectivity
            try
            {
                await _cache.GetStringAsync("test", cancellationToken);
                result.TestResults.Add("Cache connectivity: OK");
            }
            catch
            {
                warnings.Add("Cache connectivity issues detected");
            }

            // Test rules configuration
            if (_rules.Any())
            {
                result.TestResults.Add($"Monitoring rules loaded: {_rules.Count}");
            }
            else
            {
                warnings.Add("No monitoring rules configured");
            }

            // Test event processing
            var queueSize = _eventQueue.Count;
            result.TestResults.Add($"Event queue size: {queueSize}");
            if (queueSize > 1000)
            {
                warnings.Add("Event queue is growing large - check processing performance");
            }

            result.IsHealthy = errors.Count == 0;
            result.Errors = errors;
            result.Warnings = warnings;
            result.TestMetrics = new Dictionary<string, object>
            {
                ["ActiveRules"] = _rules.Count,
                ["ActiveAlerts"] = _activeAlerts.Count,
                ["QueueSize"] = queueSize,
                ["TestDuration"] = (DateTime.UtcNow - result.TestDate).TotalMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during monitoring test");
            errors.Add($"Test failed with error: {ex.Message}");
            result.IsHealthy = false;
        }

        return result;
    }

    #region Private Methods

    private async Task ProcessMonitoringCycle(CancellationToken cancellationToken)
    {
        // Clean up old alerts
        await CleanupOldAlertsAsync(cancellationToken);

        // Check for rule triggers
        await CheckRuleTriggersAsync(cancellationToken);

        // Update alert statistics
        await UpdateAlertStatisticsAsync(cancellationToken);
    }

    private void ProcessEventQueue(object? state)
    {
        try
        {
            var processedCount = 0;
            while (_eventQueue.TryDequeue(out var auditEvent) && processedCount < 100)
            {
                ProcessSingleEvent(auditEvent);
                processedCount++;
            }

            if (processedCount > 0)
            {
                _logger.LogTrace("Processed {Count} audit events from queue", processedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing audit event queue");
        }
    }

    private void ProcessSingleEvent(AuditEvent auditEvent)
    {
        try
        {
            var eventData = TryParseEventData(auditEvent.Data);
            
            foreach (var rule in _rules.Values.Where(r => r.IsEnabled))
            {
                if (ShouldTriggerRule(rule, auditEvent, eventData))
                {
                    TriggerAlert(rule, auditEvent, eventData);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing single audit event {EventId}", auditEvent.Id);
        }
    }

    private bool ShouldTriggerRule(AuditMonitoringRule rule, AuditEvent auditEvent, Dictionary<string, object>? eventData)
    {
        try
        {
            // Apply filters
            if (rule.CategoryFilter.HasValue)
            {
                var eventCategory = GetCategoryFromData(eventData);
                if (eventCategory != rule.CategoryFilter.Value)
                    return false;
            }

            if (!string.IsNullOrEmpty(rule.ActorFilter) && !auditEvent.Actor.Contains(rule.ActorFilter))
                return false;

            if (!string.IsNullOrEmpty(rule.ActionFilter) && !auditEvent.Action.Contains(rule.ActionFilter))
                return false;

            if (!string.IsNullOrEmpty(rule.EntityTypeFilter) && !auditEvent.EntityType.Contains(rule.EntityTypeFilter))
                return false;

            // Apply rule-specific logic
            return rule.Type switch
            {
                AuditMonitoringRuleType.VolumeThreshold => CheckVolumeThreshold(rule, auditEvent),
                AuditMonitoringRuleType.FailureRate => CheckFailureRate(rule, auditEvent, eventData),
                AuditMonitoringRuleType.SuspiciousPattern => CheckSuspiciousPattern(rule, auditEvent, eventData),
                AuditMonitoringRuleType.SecurityThreat => CheckSecurityThreat(rule, auditEvent, eventData),
                AuditMonitoringRuleType.ComplianceViolation => CheckComplianceViolation(rule, auditEvent, eventData),
                _ => false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking rule {RuleId} for event {EventId}", rule.Id, auditEvent.Id);
            return false;
        }
    }

    private bool CheckVolumeThreshold(AuditMonitoringRule rule, AuditEvent auditEvent)
    {
        // Count recent events matching the rule criteria
        var recentEvents = GetRecentEventsForRule(rule);
        return recentEvents.Count >= rule.Threshold;
    }

    private bool CheckFailureRate(AuditMonitoringRule rule, AuditEvent auditEvent, Dictionary<string, object>? eventData)
    {
        var isFailure = !GetSuccessFromData(eventData);
        if (!isFailure) return false;

        var recentEvents = GetRecentEventsForRule(rule);
        var failureCount = recentEvents.Count(e => !GetSuccessFromData(TryParseEventData(e.Data)));
        var failureRate = recentEvents.Count > 0 ? (double)failureCount / recentEvents.Count * 100 : 0;

        return failureRate > rule.Threshold;
    }

    private bool CheckSuspiciousPattern(AuditMonitoringRule rule, AuditEvent auditEvent, Dictionary<string, object>? eventData)
    {
        // Check for patterns like multiple failed login attempts, unusual access times, etc.
        var recentEvents = GetRecentEventsForRule(rule);
        
        // Example: Multiple events from same actor in short time
        var sameActorEvents = recentEvents.Where(e => e.Actor == auditEvent.Actor).Count();
        return sameActorEvents >= rule.Threshold;
    }

    private bool CheckSecurityThreat(AuditMonitoringRule rule, AuditEvent auditEvent, Dictionary<string, object>? eventData)
    {
        // Check for security-related events
        var securityActions = new[] { "unauthorized", "access_denied", "security_violation", "intrusion" };
        return securityActions.Any(action => auditEvent.Action.ToLower().Contains(action));
    }

    private bool CheckComplianceViolation(AuditMonitoringRule rule, AuditEvent auditEvent, Dictionary<string, object>? eventData)
    {
        // Check for compliance-related violations
        var complianceActions = new[] { "compliance_fail", "regulation_violation", "audit_fail" };
        return complianceActions.Any(action => auditEvent.Action.ToLower().Contains(action));
    }

    private void TriggerAlert(AuditMonitoringRule rule, AuditEvent auditEvent, Dictionary<string, object>? eventData)
    {
        var alertId = $"{rule.Id}_{DateTime.UtcNow:yyyyMMddHHmmss}";
        
        // Check if we already have a similar active alert to avoid spam
        var existingAlert = _activeAlerts.Values.FirstOrDefault(a => 
            a.RuleId == rule.Id && 
            a.Status == AuditMonitoringAlertStatus.Active &&
            (DateTime.UtcNow - a.TriggeredAt) < rule.TimeWindow);

        if (existingAlert != null)
        {
            // Update existing alert
            existingAlert.EventCount++;
            existingAlert.LastEventAt = auditEvent.OccurredAtUtc;
            existingAlert.RelatedEventIds.Add(auditEvent.Id.ToString());
            return;
        }

        var alert = new AuditMonitoringAlert
        {
            Id = alertId,
            RuleId = rule.Id,
            RuleName = rule.Name,
            Type = DetermineAlertType(rule, auditEvent),
            Severity = rule.AlertSeverity,
            Title = GenerateAlertTitle(rule, auditEvent),
            Description = GenerateAlertDescription(rule, auditEvent, eventData),
            Recommendation = GenerateRecommendation(rule, auditEvent),
            EventCount = 1,
            LastEventAt = auditEvent.OccurredAtUtc,
            RelatedEventIds = new List<string> { auditEvent.Id.ToString() },
            Data = new Dictionary<string, object>
            {
                ["RuleType"] = rule.Type.ToString(),
                ["TriggerEvent"] = new { auditEvent.Id, auditEvent.Actor, auditEvent.Action },
                ["ThresholdValue"] = rule.Threshold,
                ["TimeWindow"] = rule.TimeWindow.ToString()
            }
        };

        _activeAlerts.TryAdd(alertId, alert);
        
        // Update rule statistics
        rule.LastTriggered = DateTime.UtcNow;
        rule.TriggerCount++;

        _logger.LogWarning("Audit monitoring alert triggered: {AlertId} - {Title}", alertId, alert.Title);

        // Cache the alert for persistence
        _ = Task.Run(async () => await CacheAlertAsync(alert, CancellationToken.None));
    }

    private List<AuditEvent> GetRecentEventsForRule(AuditMonitoringRule rule)
    {
        // This is a simplified implementation - in production, you'd query the database
        // For now, we'll return an empty list as this requires database integration
        return new List<AuditEvent>();
    }

    private Dictionary<string, object>? TryParseEventData(string data)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(data);
        }
        catch
        {
            return null;
        }
    }

    private AuditEventCategory GetCategoryFromData(Dictionary<string, object>? data)
    {
        if (data?.TryGetValue("Category", out var categoryObj) == true &&
            categoryObj?.ToString() is string categoryStr &&
            Enum.TryParse<AuditEventCategory>(categoryStr, out var category))
        {
            return category;
        }
        return AuditEventCategory.SystemActivity;
    }

    private bool GetSuccessFromData(Dictionary<string, object>? data)
    {
        if (data?.TryGetValue("Success", out var successObj) == true)
        {
            return successObj?.ToString()?.ToLower() == "true";
        }
        return true;
    }

    private AuditMonitoringAlertType DetermineAlertType(AuditMonitoringRule rule, AuditEvent auditEvent)
    {
        return rule.Type switch
        {
            AuditMonitoringRuleType.VolumeThreshold => AuditMonitoringAlertType.HighVolumeActivity,
            AuditMonitoringRuleType.FailureRate => AuditMonitoringAlertType.FailedOperations,
            AuditMonitoringRuleType.SuspiciousPattern => AuditMonitoringAlertType.SuspiciousActivity,
            AuditMonitoringRuleType.SecurityThreat => AuditMonitoringAlertType.SecurityViolation,
            AuditMonitoringRuleType.ComplianceViolation => AuditMonitoringAlertType.ComplianceViolation,
            _ => AuditMonitoringAlertType.SystemAnomaly
        };
    }

    private string GenerateAlertTitle(AuditMonitoringRule rule, AuditEvent auditEvent)
    {
        return $"{rule.Name}: {auditEvent.Actor} - {auditEvent.Action}";
    }

    private string GenerateAlertDescription(AuditMonitoringRule rule, AuditEvent auditEvent, Dictionary<string, object>? eventData)
    {
        return $"Rule '{rule.Name}' triggered by event from {auditEvent.Actor} performing {auditEvent.Action} on {auditEvent.EntityType}:{auditEvent.EntityId} at {auditEvent.OccurredAtUtc:yyyy-MM-dd HH:mm:ss} UTC";
    }

    private string GenerateRecommendation(AuditMonitoringRule rule, AuditEvent auditEvent)
    {
        return rule.Type switch
        {
            AuditMonitoringRuleType.VolumeThreshold => "Review recent activity for unusual patterns and verify legitimacy of high-volume operations.",
            AuditMonitoringRuleType.FailureRate => "Investigate the cause of failed operations and implement corrective measures.",
            AuditMonitoringRuleType.SuspiciousPattern => "Review user behavior and verify the legitimacy of suspicious activities.",
            AuditMonitoringRuleType.SecurityThreat => "Immediately investigate potential security threat and implement protective measures.",
            AuditMonitoringRuleType.ComplianceViolation => "Review compliance requirements and ensure all processes adhere to regulations.",
            _ => "Review the alert details and take appropriate action based on your organization's policies."
        };
    }

    private async Task LoadMonitoringRulesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var cachedRules = await _cache.GetStringAsync(RulesCacheKey, cancellationToken);
            if (!string.IsNullOrEmpty(cachedRules))
            {
                var rules = JsonSerializer.Deserialize<List<AuditMonitoringRule>>(cachedRules);
                if (rules != null)
                {
                    await ConfigureMonitoringRulesAsync(rules, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading monitoring rules from cache");
        }
    }

    private async Task CacheAlertAsync(AuditMonitoringAlert alert, CancellationToken cancellationToken)
    {
        try
        {
            var alertJson = JsonSerializer.Serialize(alert);
            var cacheKey = $"{AlertsCachePrefix}{alert.Id}";
            await _cache.SetStringAsync(cacheKey, alertJson, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7)
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching alert {AlertId}", alert.Id);
        }
    }

    private Task CleanupOldAlertsAsync(CancellationToken cancellationToken)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-7);
        var oldAlerts = _activeAlerts.Values
            .Where(a => a.TriggeredAt < cutoffDate)
            .ToList();

        foreach (var alert in oldAlerts)
        {
            _activeAlerts.TryRemove(alert.Id, out _);
        }

        if (oldAlerts.Any())
        {
            _logger.LogDebug("Cleaned up {Count} old alerts", oldAlerts.Count);
        }
        return Task.CompletedTask;
    }

    private Task CheckRuleTriggersAsync(CancellationToken cancellationToken)
    {
        // Placeholder for periodic rule evaluation
        // In a full implementation, this would check database for patterns over time
        return Task.CompletedTask;
    }

    private Task UpdateAlertStatisticsAsync(CancellationToken cancellationToken)
    {
        // Update internal statistics for monitoring performance
        var activeAlertCount = _activeAlerts.Values.Count(a => a.Status == AuditMonitoringAlertStatus.Active);
        if (activeAlertCount > 100)
        {
            _logger.LogWarning("High number of active alerts: {Count}", activeAlertCount);
        }
        return Task.CompletedTask;
    }

    private double CalculateAverageResponseTime(List<AuditMonitoringAlert> alerts)
    {
        var acknowledgedAlerts = alerts.Where(a => a.AcknowledgedAt.HasValue).ToList();
        if (!acknowledgedAlerts.Any()) return 0;

        var totalResponseTime = acknowledgedAlerts
            .Sum(a => (a.AcknowledgedAt!.Value - a.TriggeredAt).TotalMinutes);

        return totalResponseTime / acknowledgedAlerts.Count;
    }

    private Task<List<AuditTrendPoint>> GenerateAlertTrendAsync(CancellationToken cancellationToken)
    {
        var trend = new List<AuditTrendPoint>();
        var endDate = DateTime.UtcNow.Date;
        
        for (int i = 6; i >= 0; i--)
        {
            var date = endDate.AddDays(-i);
            var dayAlerts = _activeAlerts.Values.Where(a => a.TriggeredAt.Date == date).Count();
            
            trend.Add(new AuditTrendPoint
            {
                Date = date,
                AlertCount = dayAlerts,
                EventCount = 0, // Would need database query for accurate count
                AverageResponseTime = 0 // Would need calculation from day's data
            });
        }

        return Task.FromResult(trend);
    }

    private void InitializeDefaultRules()
    {
        var defaultRules = new List<AuditMonitoringRule>
        {
            new AuditMonitoringRule
            {
                Id = "rule_high_volume_activity",
                Name = "High Volume Activity",
                Description = "Detects unusually high activity from a single user",
                Type = AuditMonitoringRuleType.VolumeThreshold,
                Threshold = 50,
                TimeWindow = TimeSpan.FromMinutes(15),
                AlertSeverity = AuditEventSeverity.Warning
            },
            new AuditMonitoringRule
            {
                Id = "rule_failed_operations",
                Name = "High Failure Rate",
                Description = "Detects high rate of failed operations",
                Type = AuditMonitoringRuleType.FailureRate,
                Threshold = 25, // 25% failure rate
                TimeWindow = TimeSpan.FromMinutes(10),
                AlertSeverity = AuditEventSeverity.Error
            },
            new AuditMonitoringRule
            {
                Id = "rule_security_violations",
                Name = "Security Violations",
                Description = "Detects security-related violations",
                Type = AuditMonitoringRuleType.SecurityThreat,
                CategoryFilter = AuditEventCategory.SecurityEvent,
                Threshold = 1,
                AlertSeverity = AuditEventSeverity.Critical
            }
        };

        foreach (var rule in defaultRules)
        {
            _rules.TryAdd(rule.Id, rule);
        }

        _logger.LogInformation("Initialized {Count} default monitoring rules", defaultRules.Count);
    }

    #endregion

    public override void Dispose()
    {
        _monitoringTimer?.Dispose();
        base.Dispose();
    }
}
