using IntelliFin.FinancialService.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Distributed;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Text.Json;

namespace IntelliFin.FinancialService.Services;

public interface IPaymentMonitoringService
{
    Task<PaymentPerformanceMetrics> GetPaymentPerformanceMetricsAsync(DateTime startDate, DateTime endDate);
    Task<GatewayPerformanceMetrics> GetGatewayPerformanceMetricsAsync(string gatewayName, DateTime startDate, DateTime endDate);
    Task RecordPaymentProcessingTimeAsync(string paymentId, string gateway, TimeSpan processingTime, bool success);
    Task<PaymentHealthStatus> GetPaymentSystemHealthAsync();
    Task<IEnumerable<PaymentAlert>> GetActivePaymentAlertsAsync();
    Task StartPerformanceMonitoringAsync();
    Task<PaymentTrendAnalysis> GetPaymentTrendAnalysisAsync(int daysBack = 30);
}

public class PaymentMonitoringService : IPaymentMonitoringService
{
    private readonly ILogger<PaymentMonitoringService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IDistributedCache _cache;
    
    // In-memory storage for quick metrics with Redis backup
    private readonly ConcurrentDictionary<string, PaymentMetricEntry> _recentMetrics = new();
    private readonly ConcurrentQueue<PaymentPerformanceEvent> _performanceEvents = new();
    private readonly ConcurrentDictionary<string, GatewayHealthInfo> _gatewayHealth = new();
    
    // Cache configuration
    private const int CacheExpirationMinutes = 30;
    private const string MetricsCacheKeyPrefix = "payment_metrics:";
    private const string PerformanceCacheKeyPrefix = "payment_performance:";
    private const string HealthCacheKeyPrefix = "payment_health:";

    public PaymentMonitoringService(
        ILogger<PaymentMonitoringService> logger, 
        IConfiguration configuration,
        IDistributedCache cache)
    {
        _logger = logger;
        _configuration = configuration;
        _cache = cache;
        
        // Initialize gateway health monitoring
        InitializeGatewayHealthMonitoring();
    }

    public async Task<PaymentPerformanceMetrics> GetPaymentPerformanceMetricsAsync(DateTime startDate, DateTime endDate)
    {
        _logger.LogInformation("Getting payment performance metrics from {StartDate} to {EndDate}", 
            startDate, endDate);

        try
        {
            // Filter events within date range
            var relevantEvents = _performanceEvents
                .Where(e => e.Timestamp >= startDate && e.Timestamp <= endDate)
                .ToList();

            var metrics = new PaymentPerformanceMetrics
            {
                StartDate = startDate,
                EndDate = endDate,
                TotalPayments = relevantEvents.Count,
                SuccessfulPayments = relevantEvents.Count(e => e.Success),
                FailedPayments = relevantEvents.Count(e => !e.Success),
                SuccessRate = CalculateSuccessRate(relevantEvents),
                AverageProcessingTime = CalculateAverageProcessingTime(relevantEvents),
                MedianProcessingTime = CalculateMedianProcessingTime(relevantEvents),
                P95ProcessingTime = CalculatePercentileProcessingTime(relevantEvents, 0.95),
                P99ProcessingTime = CalculatePercentileProcessingTime(relevantEvents, 0.99),
                MaxProcessingTime = relevantEvents.Any() ? relevantEvents.Max(e => e.ProcessingTime) : TimeSpan.Zero,
                MinProcessingTime = relevantEvents.Any() ? relevantEvents.Min(e => e.ProcessingTime) : TimeSpan.Zero,
                TotalProcessingTime = TimeSpan.FromMilliseconds(relevantEvents.Sum(e => e.ProcessingTime.TotalMilliseconds)),
                PaymentsByGateway = GroupPaymentsByGateway(relevantEvents),
                HourlyVolume = GroupPaymentsByHour(relevantEvents),
                PerformanceByHour = GetPerformanceByHour(relevantEvents)
            };

            // Calculate throughput (payments per minute)
            var durationMinutes = (endDate - startDate).TotalMinutes;
            metrics.ThroughputPerMinute = durationMinutes > 0 ? metrics.TotalPayments / durationMinutes : 0;

            await Task.Delay(50); // Simulate async operation

            _logger.LogInformation("Payment performance metrics calculated: Success Rate: {SuccessRate:F2}%, " +
                "Avg Processing Time: {AvgTime}ms, Throughput: {Throughput:F2}/min",
                metrics.SuccessRate, metrics.AverageProcessingTime.TotalMilliseconds, metrics.ThroughputPerMinute);

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get payment performance metrics");
            throw;
        }
    }

    public async Task<GatewayPerformanceMetrics> GetGatewayPerformanceMetricsAsync(string gatewayName, DateTime startDate, DateTime endDate)
    {
        _logger.LogInformation("Getting performance metrics for gateway {GatewayName}", gatewayName);

        try
        {
            var gatewayEvents = _performanceEvents
                .Where(e => e.Gateway.Equals(gatewayName, StringComparison.OrdinalIgnoreCase) &&
                           e.Timestamp >= startDate && e.Timestamp <= endDate)
                .ToList();

            var metrics = new GatewayPerformanceMetrics
            {
                GatewayName = gatewayName,
                StartDate = startDate,
                EndDate = endDate,
                TotalTransactions = gatewayEvents.Count,
                SuccessfulTransactions = gatewayEvents.Count(e => e.Success),
                FailedTransactions = gatewayEvents.Count(e => !e.Success),
                SuccessRate = CalculateSuccessRate(gatewayEvents),
                AverageResponseTime = CalculateAverageProcessingTime(gatewayEvents),
                MaxResponseTime = gatewayEvents.Any() ? gatewayEvents.Max(e => e.ProcessingTime) : TimeSpan.Zero,
                MinResponseTime = gatewayEvents.Any() ? gatewayEvents.Min(e => e.ProcessingTime) : TimeSpan.Zero,
                P95ResponseTime = CalculatePercentileProcessingTime(gatewayEvents, 0.95),
                ErrorTypes = GroupErrorTypes(gatewayEvents),
                Availability = CalculateAvailability(gatewayName, startDate, endDate)
            };

            await Task.Delay(30);

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get gateway performance metrics for {GatewayName}", gatewayName);
            throw;
        }
    }

    public async Task RecordPaymentProcessingTimeAsync(string paymentId, string gateway, TimeSpan processingTime, bool success)
    {
        try
        {
            var performanceEvent = new PaymentPerformanceEvent
            {
                PaymentId = paymentId,
                Gateway = gateway,
                ProcessingTime = processingTime,
                Success = success,
                Timestamp = DateTime.UtcNow
            };

            _performanceEvents.Enqueue(performanceEvent);

            // Keep only recent events to prevent memory issues (keep last 10000 events)
            while (_performanceEvents.Count > 10000)
            {
                _performanceEvents.TryDequeue(out _);
            }

            // Record in metrics cache
            var metricKey = $"{gateway}_{DateTime.UtcNow:yyyy-MM-dd-HH}";
            _recentMetrics.AddOrUpdate(metricKey, 
                new PaymentMetricEntry 
                { 
                    Gateway = gateway,
                    Count = 1,
                    SuccessCount = success ? 1 : 0,
                    TotalProcessingTime = processingTime,
                    Hour = DateTime.UtcNow.Hour
                },
                (key, existing) => 
                {
                    existing.Count++;
                    if (success) existing.SuccessCount++;
                    existing.TotalProcessingTime = existing.TotalProcessingTime.Add(processingTime);
                    return existing;
                });

            // Check for performance alerts
            await CheckPerformanceAlertsAsync(gateway, processingTime, success);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record payment processing time for {PaymentId}", paymentId);
        }
    }

    public async Task<PaymentHealthStatus> GetPaymentSystemHealthAsync()
    {
        _logger.LogInformation("Getting payment system health status");

        try
        {
            var now = DateTime.UtcNow;
            var lastHour = now.AddHours(-1);

            var recentEvents = _performanceEvents
                .Where(e => e.Timestamp >= lastHour)
                .ToList();

            var healthStatus = new PaymentHealthStatus
            {
                CheckTime = now,
                OverallStatus = SystemHealthStatus.Healthy,
                GatewayStatuses = new List<GatewayHealthStatus>(),
                SystemMetrics = new PaymentSystemMetrics
                {
                    ActiveConnections = GetActiveConnectionCount(),
                    QueueLength = GetPaymentQueueLength(),
                    CpuUsage = GetCpuUsagePercentage(),
                    MemoryUsage = GetMemoryUsagePercentage(),
                    ResponseTime = CalculateAverageProcessingTime(recentEvents.TakeLast(100).ToList())
                }
            };

            // Check gateway health
            var gateways = new[] { "Tingg", "PMEC", "BankTransfer" };
            foreach (var gateway in gateways)
            {
                var gatewayEvents = recentEvents.Where(e => e.Gateway.Equals(gateway, StringComparison.OrdinalIgnoreCase)).ToList();
                var gatewayStatus = new GatewayHealthStatus
                {
                    GatewayName = gateway,
                    Status = DetermineGatewayStatus(gatewayEvents),
                    LastSuccessfulTransaction = GetLastSuccessfulTransaction(gateway),
                    SuccessRate = CalculateSuccessRate(gatewayEvents),
                    AverageResponseTime = CalculateAverageProcessingTime(gatewayEvents),
                    ErrorCount = gatewayEvents.Count(e => !e.Success)
                };

                healthStatus.GatewayStatuses.Add(gatewayStatus);

                // Update overall status based on gateway health
                if (gatewayStatus.Status == SystemHealthStatus.Critical)
                {
                    healthStatus.OverallStatus = SystemHealthStatus.Critical;
                }
                else if (gatewayStatus.Status == SystemHealthStatus.Warning && 
                         healthStatus.OverallStatus == SystemHealthStatus.Healthy)
                {
                    healthStatus.OverallStatus = SystemHealthStatus.Warning;
                }
            }

            await Task.Delay(20);

            return healthStatus;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get payment system health status");
            return new PaymentHealthStatus
            {
                CheckTime = DateTime.UtcNow,
                OverallStatus = SystemHealthStatus.Critical,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<IEnumerable<PaymentAlert>> GetActivePaymentAlertsAsync()
    {
        _logger.LogInformation("Getting active payment alerts");

        try
        {
            var alerts = new List<PaymentAlert>();

            // Check for high failure rate alerts
            var recentFailures = _performanceEvents
                .Where(e => e.Timestamp >= DateTime.UtcNow.AddMinutes(-15) && !e.Success)
                .ToList();

            if (recentFailures.Count >= _configuration.GetValue<int>("Monitoring:FailureThreshold", 5))
            {
                alerts.Add(new PaymentAlert
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = AlertType.HighFailureRate,
                    Severity = AlertSeverity.High,
                    Message = $"High payment failure rate detected: {recentFailures.Count} failures in last 15 minutes",
                    Timestamp = DateTime.UtcNow,
                    Component = "PaymentSystem"
                });
            }

            // Check for slow response time alerts
            var slowPayments = _performanceEvents
                .Where(e => e.Timestamp >= DateTime.UtcNow.AddMinutes(-10) && 
                           e.ProcessingTime > TimeSpan.FromSeconds(5))
                .ToList();

            if (slowPayments.Count >= _configuration.GetValue<int>("Monitoring:SlowResponseThreshold", 3))
            {
                alerts.Add(new PaymentAlert
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = AlertType.SlowResponseTime,
                    Severity = AlertSeverity.Medium,
                    Message = $"Slow payment processing detected: {slowPayments.Count} payments > 5 seconds",
                    Timestamp = DateTime.UtcNow,
                    Component = "PaymentSystem"
                });
            }

            await Task.Delay(10);

            return alerts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get active payment alerts");
            return new List<PaymentAlert>();
        }
    }

    public async Task StartPerformanceMonitoringAsync()
    {
        _logger.LogInformation("Starting payment performance monitoring");

        try
        {
            // Initialize gateway health tracking
            var gateways = new[] { "Tingg", "PMEC", "BankTransfer" };
            foreach (var gateway in gateways)
            {
                _gatewayHealth[gateway] = new GatewayHealthInfo
                {
                    GatewayName = gateway,
                    LastHealthCheck = DateTime.UtcNow,
                    IsHealthy = true
                };
            }

            // Start background monitoring tasks
            _ = Task.Run(async () => await MonitorGatewayHealthAsync());
            _ = Task.Run(async () => await CleanupOldMetricsAsync());

            await Task.CompletedTask;

            _logger.LogInformation("Payment performance monitoring started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start payment performance monitoring");
            throw;
        }
    }

    public async Task<PaymentTrendAnalysis> GetPaymentTrendAnalysisAsync(int daysBack = 30)
    {
        _logger.LogInformation("Getting payment trend analysis for the last {DaysBack} days", daysBack);

        try
        {
            var startDate = DateTime.UtcNow.AddDays(-daysBack);
            var endDate = DateTime.UtcNow;

            var events = _performanceEvents
                .Where(e => e.Timestamp >= startDate && e.Timestamp <= endDate)
                .ToList();

            var analysis = new PaymentTrendAnalysis
            {
                AnalysisPeriod = daysBack,
                StartDate = startDate,
                EndDate = endDate,
                DailyVolumes = GetDailyVolumes(events),
                DailySuccessRates = GetDailySuccessRates(events),
                DailyAverageResponseTimes = GetDailyAverageResponseTimes(events),
                GatewayTrends = GetGatewayTrends(events),
                PeakHours = GetPeakHours(events),
                WorstPerformingPeriods = GetWorstPerformingPeriods(events)
            };

            await Task.Delay(30);

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get payment trend analysis");
            throw;
        }
    }

    #region Private Helper Methods

    private double CalculateSuccessRate(List<PaymentPerformanceEvent> events)
    {
        if (!events.Any()) return 0;
        return (double)events.Count(e => e.Success) / events.Count * 100;
    }

    private TimeSpan CalculateAverageProcessingTime(List<PaymentPerformanceEvent> events)
    {
        if (!events.Any()) return TimeSpan.Zero;
        var totalMs = events.Average(e => e.ProcessingTime.TotalMilliseconds);
        return TimeSpan.FromMilliseconds(totalMs);
    }

    private TimeSpan CalculateMedianProcessingTime(List<PaymentPerformanceEvent> events)
    {
        if (!events.Any()) return TimeSpan.Zero;
        var sortedTimes = events.Select(e => e.ProcessingTime.TotalMilliseconds).OrderBy(x => x).ToList();
        var median = sortedTimes.Count % 2 == 0
            ? (sortedTimes[sortedTimes.Count / 2 - 1] + sortedTimes[sortedTimes.Count / 2]) / 2
            : sortedTimes[sortedTimes.Count / 2];
        return TimeSpan.FromMilliseconds(median);
    }

    private TimeSpan CalculatePercentileProcessingTime(List<PaymentPerformanceEvent> events, double percentile)
    {
        if (!events.Any()) return TimeSpan.Zero;
        var sortedTimes = events.Select(e => e.ProcessingTime.TotalMilliseconds).OrderBy(x => x).ToList();
        var index = (int)Math.Ceiling(percentile * sortedTimes.Count) - 1;
        index = Math.Max(0, Math.Min(index, sortedTimes.Count - 1));
        return TimeSpan.FromMilliseconds(sortedTimes[index]);
    }

    private Dictionary<string, int> GroupPaymentsByGateway(List<PaymentPerformanceEvent> events)
    {
        return events.GroupBy(e => e.Gateway)
                    .ToDictionary(g => g.Key, g => g.Count());
    }

    private Dictionary<int, int> GroupPaymentsByHour(List<PaymentPerformanceEvent> events)
    {
        return events.GroupBy(e => e.Timestamp.Hour)
                    .ToDictionary(g => g.Key, g => g.Count());
    }

    private Dictionary<int, double> GetPerformanceByHour(List<PaymentPerformanceEvent> events)
    {
        return events.GroupBy(e => e.Timestamp.Hour)
                    .ToDictionary(g => g.Key, g => CalculateSuccessRate(g.ToList()));
    }

    private Dictionary<string, int> GroupErrorTypes(List<PaymentPerformanceEvent> events)
    {
        // This would be more sophisticated in production
        return events.Where(e => !e.Success)
                    .GroupBy(e => e.Gateway)
                    .ToDictionary(g => $"{g.Key}_Error", g => g.Count());
    }

    private double CalculateAvailability(string gatewayName, DateTime startDate, DateTime endDate)
    {
        // Calculate uptime percentage based on health checks
        return 99.5; // Mock value
    }

    private async Task CheckPerformanceAlertsAsync(string gateway, TimeSpan processingTime, bool success)
    {
        // Check if processing time exceeds threshold
        var slowThreshold = TimeSpan.FromSeconds(_configuration.GetValue<double>("Monitoring:SlowTransactionThreshold", 5.0));
        if (processingTime > slowThreshold)
        {
            _logger.LogWarning("Slow payment processing detected for gateway {Gateway}: {ProcessingTime}ms", 
                gateway, processingTime.TotalMilliseconds);
        }

        await Task.CompletedTask;
    }

    private async Task MonitorGatewayHealthAsync()
    {
        while (true)
        {
            try
            {
                foreach (var gateway in _gatewayHealth.Keys)
                {
                    await UpdateGatewayHealthAsync(gateway);
                }

                await Task.Delay(TimeSpan.FromMinutes(1));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in gateway health monitoring");
                await Task.Delay(TimeSpan.FromMinutes(1));
            }
        }
    }

    private async Task CleanupOldMetricsAsync()
    {
        while (true)
        {
            try
            {
                // Clean up metrics older than 24 hours
                var cutoff = DateTime.UtcNow.AddHours(-24);
                var keysToRemove = _recentMetrics
                    .Where(kvp => kvp.Value.Timestamp < cutoff)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    _recentMetrics.TryRemove(key, out _);
                }

                await Task.Delay(TimeSpan.FromHours(1));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old metrics");
                await Task.Delay(TimeSpan.FromMinutes(10));
            }
        }
    }

    private SystemHealthStatus DetermineGatewayStatus(List<PaymentPerformanceEvent> gatewayEvents)
    {
        if (!gatewayEvents.Any()) return SystemHealthStatus.Warning;
        
        var successRate = CalculateSuccessRate(gatewayEvents);
        var avgResponseTime = CalculateAverageProcessingTime(gatewayEvents);

        if (successRate < 90 || avgResponseTime > TimeSpan.FromSeconds(10))
            return SystemHealthStatus.Critical;
        if (successRate < 95 || avgResponseTime > TimeSpan.FromSeconds(5))
            return SystemHealthStatus.Warning;
        
        return SystemHealthStatus.Healthy;
    }

    private DateTime? GetLastSuccessfulTransaction(string gateway)
    {
        return _performanceEvents
            .Where(e => e.Gateway == gateway && e.Success)
            .OrderByDescending(e => e.Timestamp)
            .FirstOrDefault()?.Timestamp;
    }

    private int GetActiveConnectionCount() => Random.Shared.Next(10, 50);
    private int GetPaymentQueueLength() => Random.Shared.Next(0, 25);
    private double GetCpuUsagePercentage() => Random.Shared.NextDouble() * 50 + 20;
    private double GetMemoryUsagePercentage() => Random.Shared.NextDouble() * 30 + 40;

    private Dictionary<DateTime, int> GetDailyVolumes(List<PaymentPerformanceEvent> events)
    {
        return events.GroupBy(e => e.Timestamp.Date)
                    .ToDictionary(g => g.Key, g => g.Count());
    }

    private Dictionary<DateTime, double> GetDailySuccessRates(List<PaymentPerformanceEvent> events)
    {
        return events.GroupBy(e => e.Timestamp.Date)
                    .ToDictionary(g => g.Key, g => CalculateSuccessRate(g.ToList()));
    }

    private Dictionary<DateTime, TimeSpan> GetDailyAverageResponseTimes(List<PaymentPerformanceEvent> events)
    {
        return events.GroupBy(e => e.Timestamp.Date)
                    .ToDictionary(g => g.Key, g => CalculateAverageProcessingTime(g.ToList()));
    }

    private Dictionary<string, Dictionary<DateTime, double>> GetGatewayTrends(List<PaymentPerformanceEvent> events)
    {
        return events.GroupBy(e => e.Gateway)
                    .ToDictionary(
                        gatewayGroup => gatewayGroup.Key,
                        gatewayGroup => gatewayGroup.GroupBy(e => e.Timestamp.Date)
                                                   .ToDictionary(g => g.Key, g => CalculateSuccessRate(g.ToList())));
    }

    private List<int> GetPeakHours(List<PaymentPerformanceEvent> events)
    {
        return events.GroupBy(e => e.Timestamp.Hour)
                    .OrderByDescending(g => g.Count())
                    .Take(3)
                    .Select(g => g.Key)
                    .ToList();
    }

    private List<DateTime> GetWorstPerformingPeriods(List<PaymentPerformanceEvent> events)
    {
        return events.GroupBy(e => e.Timestamp.Date)
                    .Where(g => CalculateSuccessRate(g.ToList()) < 90)
                    .Select(g => g.Key)
                    .OrderBy(date => date)
                    .ToList();
    }

    private void InitializeGatewayHealthMonitoring()
    {
        _logger.LogInformation("Initializing gateway health monitoring");
        
        // Initialize known gateways
        var gateways = _configuration.GetSection("PaymentGateways:Enabled").Get<string[]>() 
                      ?? new[] { "Tingg", "PMEC", "BankTransfer" };
        
        foreach (var gateway in gateways)
        {
            _gatewayHealth[gateway] = new GatewayHealthInfo
            {
                GatewayName = gateway,
                LastHealthCheck = DateTime.UtcNow,
                IsHealthy = true,
                ResponseTimeMs = 0,
                ErrorRate = 0
            };
        }
    }

    private async Task UpdateGatewayHealthAsync(string gatewayName)
    {
        try
        {
            var recentEvents = _performanceEvents
                .Where(e => e.Gateway.Equals(gatewayName, StringComparison.OrdinalIgnoreCase) 
                           && e.Timestamp > DateTime.UtcNow.AddMinutes(-10))
                .ToList();

            if (recentEvents.Any() && _gatewayHealth.ContainsKey(gatewayName))
            {
                var successRate = CalculateSuccessRate(recentEvents);
                var avgResponseTime = CalculateAverageProcessingTime(recentEvents);
                
                _gatewayHealth[gatewayName] = new GatewayHealthInfo
                {
                    GatewayName = gatewayName,
                    LastHealthCheck = DateTime.UtcNow,
                    IsHealthy = successRate >= 95 && avgResponseTime < TimeSpan.FromSeconds(5),
                    ResponseTimeMs = (int)avgResponseTime.TotalMilliseconds,
                    ErrorRate = 100 - successRate
                };

                // Cache health status
                var cacheKey = $"{HealthCacheKeyPrefix}{gatewayName}";
                await SetCacheAsync(cacheKey, _gatewayHealth[gatewayName]);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating health for gateway {GatewayName}", gatewayName);
        }
    }

    #region Cache Helper Methods

    private async Task<T?> GetFromCacheAsync<T>(string cacheKey) where T : class
    {
        try
        {
            var cachedValue = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cachedValue))
            {
                return JsonSerializer.Deserialize<T>(cachedValue);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving from cache with key: {CacheKey}", cacheKey);
        }
        
        return null;
    }

    private async Task SetCacheAsync<T>(string cacheKey, T value) where T : class
    {
        try
        {
            var serializedValue = JsonSerializer.Serialize(value);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheExpirationMinutes)
            };
            
            await _cache.SetStringAsync(cacheKey, serializedValue, options);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error setting cache with key: {CacheKey}", cacheKey);
        }
    }

    #endregion

    #endregion
}

// Supporting classes for monitoring
public class PaymentPerformanceEvent
{
    public string PaymentId { get; set; } = string.Empty;
    public string Gateway { get; set; } = string.Empty;
    public TimeSpan ProcessingTime { get; set; }
    public bool Success { get; set; }
    public DateTime Timestamp { get; set; }
}

public class PaymentMetricEntry
{
    public string Gateway { get; set; } = string.Empty;
    public int Count { get; set; }
    public int SuccessCount { get; set; }
    public TimeSpan TotalProcessingTime { get; set; }
    public int Hour { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class GatewayHealthInfo
{
    public string GatewayName { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public DateTime LastHealthCheck { get; set; }
    public string? LastError { get; set; }
    public int ResponseTimeMs { get; set; }
    public double ErrorRate { get; set; }
}

// Additional models would be defined in PaymentModels.cs or separate files
public class PaymentPerformanceMetrics
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalPayments { get; set; }
    public int SuccessfulPayments { get; set; }
    public int FailedPayments { get; set; }
    public double SuccessRate { get; set; }
    public TimeSpan AverageProcessingTime { get; set; }
    public TimeSpan MedianProcessingTime { get; set; }
    public TimeSpan P95ProcessingTime { get; set; }
    public TimeSpan P99ProcessingTime { get; set; }
    public TimeSpan MaxProcessingTime { get; set; }
    public TimeSpan MinProcessingTime { get; set; }
    public TimeSpan TotalProcessingTime { get; set; }
    public double ThroughputPerMinute { get; set; }
    public Dictionary<string, int> PaymentsByGateway { get; set; } = new();
    public Dictionary<int, int> HourlyVolume { get; set; } = new();
    public Dictionary<int, double> PerformanceByHour { get; set; } = new();
}

public class GatewayPerformanceMetrics
{
    public string GatewayName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalTransactions { get; set; }
    public int SuccessfulTransactions { get; set; }
    public int FailedTransactions { get; set; }
    public double SuccessRate { get; set; }
    public TimeSpan AverageResponseTime { get; set; }
    public TimeSpan MaxResponseTime { get; set; }
    public TimeSpan MinResponseTime { get; set; }
    public TimeSpan P95ResponseTime { get; set; }
    public Dictionary<string, int> ErrorTypes { get; set; } = new();
    public double Availability { get; set; }
}

public class PaymentHealthStatus
{
    public DateTime CheckTime { get; set; }
    public SystemHealthStatus OverallStatus { get; set; }
    public List<GatewayHealthStatus> GatewayStatuses { get; set; } = new();
    public PaymentSystemMetrics SystemMetrics { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

public class GatewayHealthStatus
{
    public string GatewayName { get; set; } = string.Empty;
    public SystemHealthStatus Status { get; set; }
    public DateTime? LastSuccessfulTransaction { get; set; }
    public double SuccessRate { get; set; }
    public TimeSpan AverageResponseTime { get; set; }
    public int ErrorCount { get; set; }
    public string? StatusMessage { get; set; }
}

public class PaymentSystemMetrics
{
    public int ActiveConnections { get; set; }
    public int QueueLength { get; set; }
    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
    public TimeSpan ResponseTime { get; set; }
}

public class PaymentAlert
{
    public string Id { get; set; } = string.Empty;
    public AlertType Type { get; set; }
    public AlertSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Component { get; set; } = string.Empty;
    public bool IsAcknowledged { get; set; }
}

public class PaymentTrendAnalysis
{
    public int AnalysisPeriod { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public Dictionary<DateTime, int> DailyVolumes { get; set; } = new();
    public Dictionary<DateTime, double> DailySuccessRates { get; set; } = new();
    public Dictionary<DateTime, TimeSpan> DailyAverageResponseTimes { get; set; } = new();
    public Dictionary<string, Dictionary<DateTime, double>> GatewayTrends { get; set; } = new();
    public List<int> PeakHours { get; set; } = new();
    public List<DateTime> WorstPerformingPeriods { get; set; } = new();
}

public enum SystemHealthStatus
{
    Healthy,
    Warning,
    Critical,
    Unknown
}

public enum AlertType
{
    HighFailureRate,
    SlowResponseTime,
    GatewayDown,
    HighVolume,
    SecurityAlert
}

public enum AlertSeverity
{
    Low,
    Medium,
    High,
    Critical
}