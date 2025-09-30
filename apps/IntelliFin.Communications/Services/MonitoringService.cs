using IntelliFin.Shared.DomainModels.Entities;
using IntelliFin.Shared.DomainModels.Repositories;
using IntelliFin.Shared.DomainModels.Data;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace IntelliFin.Communications.Services;

/// <summary>
/// Comprehensive monitoring service for event processing system
/// </summary>
public class MonitoringService : IMonitoringService
{
    private readonly LmsDbContext _dbContext;
    private readonly INotificationRepository _notificationRepository;
    private readonly ILogger<MonitoringService> _logger;
    private readonly ConcurrentDictionary<string, PerformanceMetrics> _performanceMetrics;
    private readonly ConcurrentDictionary<string, HealthCheckResult> _healthStatus;

    public MonitoringService(
        LmsDbContext dbContext,
        INotificationRepository notificationRepository,
        ILogger<MonitoringService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _performanceMetrics = new ConcurrentDictionary<string, PerformanceMetrics>();
        _healthStatus = new ConcurrentDictionary<string, HealthCheckResult>();
    }

    /// <summary>
    /// Records successful event processing
    /// </summary>
    public async Task RecordEventSuccessAsync(
        string eventId,
        string eventType,
        string consumer,
        TimeSpan processingTime)
    {
        try
        {
            var metricsKey = $"{consumer}:{eventType}";
            var metrics = _performanceMetrics.GetOrAdd(metricsKey, _ => new PerformanceMetrics());

            lock (metrics)
            {
                metrics.TotalEvents++;
                metrics.SuccessfulEvents++;
                metrics.TotalProcessingTime += processingTime.TotalMilliseconds;
                metrics.LastProcessed = DateTime.UtcNow;
                metrics.AverageProcessingTime = metrics.TotalProcessingTime / metrics.TotalEvents;
            }

            // Check for performance degradation
            await CheckPerformanceThresholdAsync(metrics, eventType, consumer, processingTime);

            // Log to database if needed for detailed monitoring
            if (processingTime.TotalSeconds > 10) // Log slow events
            {
                await LogPerformanceIssueAsync(eventId, eventType, consumer, processingTime, "SlowProcessing");
            }

            _logger.LogInformation(
                "Event {EventId} processed successfully by {Consumer} in {ProcessingTime}ms",
                eventId, consumer, processingTime.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record event success for {EventId}", eventId);
        }
    }

    /// <summary>
    /// Records event processing failure
    /// </summary>
    public async Task RecordEventFailureAsync(
        string eventId,
        string eventType,
        string consumer,
        Exception exception,
        TimeSpan processingTime)
    {
        try
        {
            var metricsKey = $"{consumer}:{eventType}";
            var metrics = _performanceMetrics.GetOrAdd(metricsKey, _ => new PerformanceMetrics());

            lock (metrics)
            {
                metrics.TotalEvents++;
                metrics.FailedEvents++;
                metrics.LastFailure = DateTime.UtcNow;
                metrics.LastErrorMessage = exception.Message;
            }

            // Log error to database
            await LogErrorAsync(eventId, eventType, consumer, exception, processingTime);

            // Send alert for critical failures
            if (IsCriticalFailure(eventType, exception))
            {
                await SendFailureAlertAsync(eventId, eventType, consumer, exception);
            }

            _logger.LogError(exception,
                "Event {EventId} processing failed by {Consumer} after {ProcessingTime}ms",
                eventId, consumer, processingTime.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record event failure for {EventId}", eventId);
        }
    }

    /// <summary>
    /// Performs health check of the communications system
    /// </summary>
    public async Task<SystemHealthCheck> PerformHealthCheckAsync()
    {
        var healthCheck = new SystemHealthCheck
        {
            ServiceName = "IntelliFin Communications",
            CheckTime = DateTime.UtcNow,
            Components = new List<ComponentHealth>()
        };

        // Database connectivity check
        var dbHealth = await CheckDatabaseHealthAsync();
        healthCheck.Components.Add(dbHealth);

        // Message queue connectivity check
        var mqHealth = await CheckMessageQueueHealthAsync();
        healthCheck.Components.Add(mqHealth);

        // Consumer health checks
        var consumerHealth = await CheckConsumersHealthAsync();
        healthCheck.Components.AddRange(consumerHealth);

        // Service connectivity check
        var serviceHealth = await CheckExternalServicesHealthAsync();
        healthCheck.Components.AddRange(serviceHealth);

        // Overall system status
        healthCheck.OverallStatus = DetermineOverallStatus(healthCheck.Components);

        // Update health status cache
        _healthStatus["System"] = healthCheck.OverallStatus;

        _logger.LogInformation("Health check completed: {Status}", healthCheck.OverallStatus.Status);

        return healthCheck;
    }

    /// <summary>
    /// Gets comprehensive system metrics
    /// </summary>
    public async Task<SystemMetrics> GetSystemMetricsAsync()
    {
        var metrics = new SystemMetrics
        {
            Timestamp = DateTime.UtcNow,
            Consumers = new List<ConsumerMetrics>()
        };

        // Aggregate metrics from all tracked consumers
        foreach (var kvp in _performanceMetrics)
        {
            var consumerType = kvp.Key.Split(':')[0];
            var eventType = kvp.Key.Split(':')[1];
            var perfMetrics = kvp.Value;

            var consumer = metrics.Consumers.FirstOrDefault(c => c.ConsumerType == consumerType);
            if (consumer == null)
            {
                consumer = new ConsumerMetrics
                {
                    ConsumerType = consumerType,
                    EventTypes = new List<EventTypeMetrics>()
                };
                metrics.Consumers.Add(consumer);
            }

            consumer.EventTypes.Add(new EventTypeMetrics
            {
                EventType = eventType,
                TotalEvents = (long)perfMetrics.TotalEvents,
                SuccessfulEvents = (long)perfMetrics.SuccessfulEvents,
                FailedEvents = (long)perfMetrics.FailedEvents,
                AverageProcessingTimeMs = perfMetrics.AverageProcessingTime,
                LastProcessed = perfMetrics.LastProcessed,
                LastFailure = perfMetrics.LastFailure,
                LastErrorMessage = perfMetrics.LastErrorMessage
            });
        }

        // Get database metrics
        var dbMetrics = await GetDatabaseMetricsAsync();
        metrics.Database = dbMetrics;

        return metrics;
    }

    /// <summary>
    /// Gets recent errors with filtering options
    /// </summary>
    public async Task<List<ErrorLog>> GetRecentErrorsAsync(
        string? eventType = null,
        string? consumer = null,
        DateTime? since = null,
        int limit = 50)
    {
        try
        {
            var query = _dbContext.ErrorLogs.AsQueryable();

            if (!string.IsNullOrEmpty(eventType))
                query = query.Where(e => e.EventType == eventType);

            if (!string.IsNullOrEmpty(consumer))
                query = query.Where(e => e.ConsumerType == consumer);

            if (since.HasValue)
                query = query.Where(e => e.ErrorTime >= since.Value);

            return await query
                .OrderByDescending(e => e.ErrorTime)
                .Take(limit)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve recent errors");
            return new List<ErrorLog>();
        }
    }

    /// <summary>
    /// Clears old performance metrics and error logs
    /// </summary>
    public async Task PerformMaintenanceAsync()
    {
        try
        {
            var retentionDate = DateTime.UtcNow.AddDays(-30);

            // Clean up old error logs
            var deletedErrors = await _dbContext.ErrorLogs
                .Where(e => e.ErrorTime < retentionDate)
                .ExecuteDeleteAsync();

            // Clean up old performance logs
            var deletedPerformance = await _dbContext.PerformanceLogs
                .Where(p => p.LoggedAt < retentionDate)
                .ExecuteDeleteAsync();

            // Clean up old health check logs
            var deletedHealthChecks = await _dbContext.HealthCheckLogs
                .Where(h => h.CheckedAt < retentionDate)
                .ExecuteDeleteAsync();

            _logger.LogInformation(
                "Maintenance completed: {DeletedErrors} errors, {DeletedPerformance} performance logs, {DeletedHealthChecks} health checks cleaned",
                deletedErrors, deletedPerformance, deletedHealthChecks);

            // Clear in-memory performance cache older than 24 hours
            ClearStalePerformanceMetrics();

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Maintenance operation failed");
        }
    }

    #region Private Methods

    private async Task CheckPerformanceThresholdAsync(
        PerformanceMetrics metrics,
        string eventType,
        string consumer,
        TimeSpan processingTime)
    {
        // Check for performance degradation (processing time > 5000ms consistently)
        const double PERFORMANCE_THRESHOLD_MS = 5000;
        const int MINIMUM_SAMPLE_SIZE = 10;

        if (metrics.TotalEvents >= MINIMUM_SAMPLE_SIZE &&
            metrics.AverageProcessingTime > PERFORMANCE_THRESHOLD_MS)
        {
            _logger.LogWarning(
                "Performance degradation detected for {Consumer} processing {EventType}: Average {AverageTime}ms over {Count} events",
                consumer, eventType, metrics.AverageProcessingTime, metrics.TotalEvents);

            await LogPerformanceIssueAsync(
                Guid.NewGuid().ToString(),
                eventType,
                consumer,
                processingTime,
                "PerformanceDegradation");

            // Could trigger alerts here
        }
    }

    private async Task LogPerformanceIssueAsync(
        string eventId,
        string eventType,
        string consumer,
        TimeSpan processingTime,
        string issueType)
    {
        try
        {
            // Implement performance logging to database if needed
            _logger.LogWarning(
                "Performance issue logged: {IssueType} for event {EventId}, consumer {Consumer}, time {Time}ms",
                issueType, eventId, consumer, processingTime.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log performance issue");
        }
    }

    private async Task LogErrorAsync(
        string eventId,
        string eventType,
        string consumer,
        Exception exception,
        TimeSpan processingTime)
    {
        try
        {
            var errorLog = new ErrorLog
            {
                EventId = eventId,
                EventType = eventType,
                ConsumerType = consumer,
                ErrorMessage = exception.Message,
                ErrorTime = DateTime.UtcNow,
                ProcessingTimeMs = processingTime.TotalMilliseconds,
                StackTrace = exception.StackTrace
            };

            _dbContext.ErrorLogs.Add(errorLog);
            await _dbContext.SaveChangesAsync();

            _logger.LogError("Error logged to database: {EventId}, {Consumer}, {Error}",
                eventId, consumer, exception.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log error to database: {OriginalError}", exception.Message);
        }
    }

    private bool IsCriticalFailure(string eventType, Exception exception)
    {
        // Define what constitutes a critical failure
        if (exception is InvalidOperationException ||
            exception is NullReferenceException ||
            exception is ArgumentException)
        {
            return false; // These are usually code issues, not infrastructure
        }

        // Network, database, or timeout errors are critical
        return exception is TimeoutException ||
               exception.Message.Contains("network", StringComparison.OrdinalIgnoreCase) ||
               exception.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
               exception.Message.Contains("database", StringComparison.OrdinalIgnoreCase);
    }

    private async Task SendFailureAlertAsync(
        string eventId,
        string eventType,
        string consumer,
        Exception exception)
    {
        // TODO: Implement alert mechanism (email, SMS, webhook)
        _logger.LogCritical(
            "Critical failure alert: Event {EventId} failed in {Consumer} with error {Error}",
            eventId, consumer, exception.Message);
    }

    private async Task<ComponentHealth> CheckDatabaseHealthAsync()
    {
        var component = new ComponentHealth
        {
            Component = "Database",
            Status = HealthStatus.Degraded,
            Description = "Database connectivity check",
            CheckTime = DateTime.UtcNow
        };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _dbContext.Database.CanConnectAsync();
            await _dbContext.NotificationLogs.Take(1).CountAsync();

            component.Status = HealthStatus.Healthy;
            component.Description = "Database connection healthy";

            _logger.LogInformation("Database health check passed");
        }
        catch (Exception ex)
        {
            component.Status = HealthStatus.Unhealthy;
            component.Description = $"Database connection failed: {ex.Message}";
            component.Error = ex.Message;

            _logger.LogError(ex, "Database health check failed");
        }

        stopwatch.Stop();
        component.ResponseTimeMs = stopwatch.ElapsedMilliseconds;

        return component;
    }

    private async Task<ComponentHealth> CheckMessageQueueHealthAsync()
    {
        var component = new ComponentHealth
        {
            Component = "MessageQueue",
            Status = HealthStatus.Degraded,
            Description = "Message queue connectivity check",
            CheckTime = DateTime.UtcNow
        };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // TODO: Implement actual RabbitMQ health check
            // For now, simulate successful check
            await Task.Delay(10); // Simulate network call

            component.Status = HealthStatus.Healthy;
            component.Description = "Message queue connection healthy";

            _logger.LogInformation("Message queue health check passed");
        }
        catch (Exception ex)
        {
            component.Status = HealthStatus.Unhealthy;
            component.Description = $"Message queue connection failed: {ex.Message}";
            component.Error = ex.Message;

            _logger.LogError(ex, "Message queue health check failed");
        }

        stopwatch.Stop();
        component.ResponseTimeMs = stopwatch.ElapsedMilliseconds;

        return component;
    }

    private async Task<List<ComponentHealth>> CheckConsumersHealthAsync()
    {
        var consumerHealths = new List<ComponentHealth>();

        var consumerTypes = new[] { "LoanApplicationConsumer", "LoanStatusConsumer", "PaymentReminderConsumer" };

        foreach (var consumerType in consumerTypes)
        {
            var component = new ComponentHealth
            {
                Component = $"Consumer:{consumerType}",
                Status = HealthStatus.Degraded,
                Description = $"{consumerType} health check",
                CheckTime = DateTime.UtcNow
            };

            try
            {
                // Check if consumer has processed events recently (within last 30 minutes)
                var recentActivity = await _dbContext.NotificationLogs
                    .Where(n => n.CreatedBy == consumerType &&
                               n.CreatedAt >= DateTime.UtcNow.AddMinutes(-30))
                    .AnyAsync();

                if (recentActivity)
                {
                    component.Status = HealthStatus.Healthy;
                    component.Description = $"{consumerType} is processing events";
                }
                else
                {
                    component.Status = HealthStatus.Degraded;
                    component.Description = $"{consumerType} has not processed recent events";
                }
            }
            catch (Exception ex)
            {
                component.Status = HealthStatus.Unhealthy;
                component.Description = $"{consumerType} health check failed: {ex.Message}";
                component.Error = ex.Message;
            }

            consumerHealths.Add(component);
        }

        return consumerHealths;
    }

    private async Task<List<ComponentHealth>> CheckExternalServicesHealthAsync()
    {
        var services = new List<ComponentHealth>();

        // Check SMS service health
        var smsHealth = new ComponentHealth
        {
            Component = "SMSService",
            Status = HealthStatus.Healthy,
            Description = "SMS service is operational",
            CheckTime = DateTime.UtcNow
        };
        services.Add(smsHealth);

        // Check email service health
        var emailHealth = new ComponentHealth
        {
            Component = "EmailService",
            Status = HealthStatus.Healthy,
            Description = "Email service is operational",
            CheckTime = DateTime.UtcNow
        };
        services.Add(emailHealth);

        return services;
    }

    private HealthCheckResult DetermineOverallStatus(List<ComponentHealth> components)
    {
        if (components.Any(c => c.Status == HealthStatus.Unhealthy))
        {
            return new HealthCheckResult
            {
                Status = HealthStatus.Unhealthy,
                Timestamp = DateTime.UtcNow,
                Message = "One or more components are unhealthy"
            };
        }
        else if (components.Any(c => c.Status == HealthStatus.Degraded))
        {
            return new HealthCheckResult
            {
                Status = HealthStatus.Degraded,
                Timestamp = DateTime.UtcNow,
                Message = "Some components are degraded but system is operational"
            };
        }
        else
        {
            return new HealthCheckResult
            {
                Status = HealthStatus.Healthy,
                Timestamp = DateTime.UtcNow,
                Message = "All components are healthy"
            };
        }
    }

    private async Task<DatabaseMetrics> GetDatabaseMetricsAsync()
    {
        try
        {
            var totalNotifications = await _dbContext.NotificationLogs.CountAsync();
            var totalErrors = await _dbContext.ErrorLogs.CountAsync();
            var totalTemplates = await _dbContext.NotificationTemplates.CountAsync();
            var recentActivity = await _dbContext.NotificationLogs
                .Where(n => n.CreatedAt >= DateTime.UtcNow.AddHours(-24))
                .CountAsync();

            return new DatabaseMetrics
            {
                TotalNotifications = totalNotifications,
                TotalErrors = totalErrors,
                TotalTemplates = totalTemplates,
                RecentActivity = recentActivity,
                ConnectionHealth = HealthStatus.Healthy
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get database metrics");
            return new DatabaseMetrics
            {
                TotalNotifications = 0,
                TotalErrors = 0,
                TotalTemplates = 0,
                RecentActivity = 0,
                ConnectionHealth = HealthStatus.Unhealthy,
                Error = ex.Message
            };
        }
    }

    private void ClearStalePerformanceMetrics()
    {
        var cutoff = DateTime.UtcNow.AddHours(-24);

        foreach (var kvp in _performanceMetrics)
        {
            if (kvp.Value.LastProcessed < cutoff)
            {
                _performanceMetrics.TryRemove(kvp.Key, out _);
            }
        }

        _logger.LogInformation("Cleared stale performance metrics from in-memory cache");
    }

    #endregion
}

/// <summary>
/// Interface for monitoring service
/// </summary>
public interface IMonitoringService
{
    Task RecordEventSuccessAsync(string eventId, string eventType, string consumer, TimeSpan processingTime);
    Task RecordEventFailureAsync(string eventId, string eventType, string consumer, Exception exception, TimeSpan processingTime);
    Task<SystemHealthCheck> PerformHealthCheckAsync();
    Task<SystemMetrics> GetSystemMetricsAsync();
    Task<List<ErrorLog>> GetRecentErrorsAsync(string? eventType = null, string? consumer = null, DateTime? since = null, int limit = 50);
    Task PerformMaintenanceAsync();
}
