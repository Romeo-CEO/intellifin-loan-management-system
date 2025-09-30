using IntelliFin.Shared.DomainModels.Entities;
using IntelliFin.Shared.DomainModels.Repositories;
using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.Shared.Infrastructure.Messaging.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace IntelliFin.Communications.Services;

/// <summary>
/// Service responsible for routing events to appropriate consumers based on business rules
/// </summary>
public class EventRoutingService : IEventRoutingService
{
    private readonly LmsDbContext _dbContext;
    private readonly INotificationRepository _notificationRepository;
    private readonly ILogger<EventRoutingService> _logger;
    private readonly ConcurrentDictionary<string, List<EventRoute>> _eventRoutes;

    public EventRoutingService(
        LmsDbContext dbContext,
        INotificationRepository notificationRepository,
        ILogger<EventRoutingService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _eventRoutes = new ConcurrentDictionary<string, List<EventRoute>>();
    }

    /// <summary>
    /// Routes an event to appropriate consumers based on routing rules
    /// </summary>
    public async Task<RouteResult> RouteEventAsync(IBusinessEvent businessEvent)
    {
        _logger.LogInformation("Routing event {EventId} of type {EventType} from {SourceService}",
            businessEvent.EventId, businessEvent.EventType, businessEvent.SourceService);

        // Load routing rules for this event type
        var routes = await GetRoutesForEventTypeAsync(businessEvent.EventType);
        var routeResults = new List<RouteDestination>();

        foreach (var route in routes)
        {
            var shouldRoute = await EvaluateRoutingRuleAsync(businessEvent, route);
            if (shouldRoute)
            {
                routeResults.Add(route.Destination);
                _logger.LogInformation("Event {EventId} routed to consumer {ConsumerType}",
                    businessEvent.EventId, route.Destination.ConsumerType);
            }
        }

        var result = new RouteResult
        {
            BusinessEvent = businessEvent,
            Destinations = routeResults,
            RouteTimestamp = DateTime.UtcNow
        };

        // Log the routing result
        await LogEventRoutingAsync(result);

        return result;
    }

    /// <summary>
    /// Adds a new routing rule
    /// </summary>
    public async Task CreateRoutingRuleAsync(IntelliFin.Shared.DomainModels.Entities.EventRoutingRule rule)
    {
        var routingRule = new IntelliFin.Shared.DomainModels.Entities.EventRoutingRule
        {
            EventType = rule.EventType,
            ConsumerType = rule.ConsumerType,
            Priority = rule.Priority,
            Conditions = rule.Conditions,
            IsActive = rule.IsActive,
            Description = rule.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.EventRoutingRules.Add(routingRule);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Created routing rule {RuleId} for event type {EventType} to consumer {ConsumerType}",
            routingRule.Id, routingRule.EventType, routingRule.ConsumerType);

        // Refresh cached routes
        await RefreshRoutingCacheAsync();
    }

    /// <summary>
    /// Updates an existing routing rule
    /// </summary>
    public async Task UpdateRoutingRuleAsync(int ruleId, IntelliFin.Shared.DomainModels.Entities.EventRoutingRule rule)
    {
        var existingRule = await _dbContext.EventRoutingRules.FindAsync(ruleId);
        if (existingRule == null)
        {
            throw new InvalidOperationException($"Routing rule {ruleId} not found");
        }

        existingRule.EventType = rule.EventType;
        existingRule.ConsumerType = rule.ConsumerType;
        existingRule.Priority = rule.Priority;
        existingRule.Conditions = rule.Conditions;
        existingRule.IsActive = rule.IsActive;
        existingRule.Description = rule.Description;
        existingRule.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Updated routing rule {RuleId}", ruleId);

        // Refresh cached routes
        await RefreshRoutingCacheAsync();
    }

    /// <summary>
    /// Deletes a routing rule
    /// </summary>
    public async Task DeleteRoutingRuleAsync(int ruleId)
    {
        var rule = await _dbContext.EventRoutingRules.FindAsync(ruleId);
        if (rule == null)
        {
            throw new InvalidOperationException($"Routing rule {ruleId} not found");
        }

        _dbContext.EventRoutingRules.Remove(rule);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Deleted routing rule {RuleId}", ruleId);

        // Refresh cached routes
        await RefreshRoutingCacheAsync();
    }

    /// <summary>
    /// Gets all routing rules with optional filtering
    /// </summary>
    public async Task<List<IntelliFin.Shared.DomainModels.Entities.EventRoutingRule>> GetRoutingRulesAsync(
        string? eventType = null,
        string? consumerType = null,
        bool? isActive = null)
    {
        var query = _dbContext.EventRoutingRules.AsQueryable();

        if (!string.IsNullOrEmpty(eventType))
        {
            query = query.Where(r => r.EventType == eventType);
        }

        if (!string.IsNullOrEmpty(consumerType))
        {
            query = query.Where(r => r.ConsumerType == consumerType);
        }

        if (isActive.HasValue)
        {
            query = query.Where(r => r.IsActive == isActive.Value);
        }

        return await query
            .OrderBy(r => r.Priority)
            .ThenBy(r => r.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Validates if a consumer is eligible to process an event
    /// </summary>
    public async Task<bool> CanConsumerProcessEventAsync(
        IBusinessEvent businessEvent,
        string consumerType)
    {
        var routes = await GetRoutesForEventTypeAsync(businessEvent.EventType);

        var route = routes.FirstOrDefault(r =>
            r.Destination.ConsumerType == consumerType && r.IsActive);

        if (route == null)
        {
            _logger.LogDebug("No active route found for event type {EventType} to consumer {ConsumerType}",
                businessEvent.EventType, consumerType);
            return false;
        }

        return await EvaluateRoutingRuleAsync(businessEvent, route);
    }

    /// <summary>
    /// Gets performance metrics for event routing
    /// </summary>
    public async Task<EventRoutingMetrics> GetMetricsAsync()
    {
        var rules = await _dbContext.EventRoutingRules.CountAsync();
        var processed = await _dbContext.EventProcessingStatus.ToListAsync();

        return new EventRoutingMetrics
        {
            TotalRoutingRules = rules,
            TotalEventsProcessed = processed.Count,
            SuccessfulRoutes = processed.Count(l => l.ProcessingResult == "Success"),
            FailedRoutes = processed.Count(l => l.ProcessingResult == "Failed"),
            LastMetricsUpdate = DateTime.UtcNow
        };
    }

    #region Private Methods

    private async Task<List<EventRoute>> GetRoutesForEventTypeAsync(string eventType)
    {
        // Check cache first
        if (_eventRoutes.TryGetValue(eventType, out var cachedRoutes))
        {
            return cachedRoutes;
        }

        // Load from database
        var rules = await _dbContext.EventRoutingRules
            .Where(r => r.EventType == eventType && r.IsActive)
            .OrderBy(r => r.Priority)
            .ToListAsync();

        var routes = rules.Select(r => new EventRoute
        {
            Id = r.Id,
            IsActive = r.IsActive,
            Priority = r.Priority,
            Conditions = r.Conditions,
            Destination = new RouteDestination
            {
                ConsumerType = r.ConsumerType,
                QueueName = GetQueueNameForConsumer(r.ConsumerType)
            }
        }).ToList();

        // Cache the result
        _eventRoutes[eventType] = routes;

        return routes;
    }

    private async Task<bool> EvaluateRoutingRuleAsync(
        IBusinessEvent businessEvent,
        EventRoute route)
    {
        try
        {
            // Always true if no conditions are specified
            if (string.IsNullOrEmpty(route.Conditions))
            {
                return true;
            }

            // Evaluate business rule conditions
            return await EvaluateBusinessConditionsAsync(businessEvent, route.Conditions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to evaluate routing rule for event {EventId}",
                businessEvent.EventId);
            return false;
        }
    }

    private async Task<bool> EvaluateBusinessConditionsAsync(
        IBusinessEvent businessEvent,
        string conditions)
    {
        // Minimal rule evaluation: supports expressions like
        // "Amount > 100000", "DaysOverdue >= 30", "NewStatus == 'Approved'"
        // Combine multiple conditions with 'AND' only for now.
        try
        {
            var type = businessEvent.GetType();
            var andParts = conditions.Split("AND", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in andParts)
            {
                var expr = part.Trim();

                string[] operators = new[] { ">=", "<=", "==", "!=", ">", "<" };
                string? op = operators.FirstOrDefault(o => expr.Contains(o));
                if (op == null) return false;

                var tokens = expr.Split(op, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length != 2) return false;

                var propName = tokens[0];
                var valueLiteral = tokens[1].Trim().Trim('\'', '"');

                var prop = type.GetProperty(propName);
                if (prop == null) return false;
                var leftVal = prop.GetValue(businessEvent);
                if (leftVal == null) return false;

                bool satisfied;
                if (leftVal is IComparable cmp)
                {
                    object rightVal;
                    if (leftVal is string)
                    {
                        rightVal = valueLiteral;
                    }
                    else if (leftVal is int)
                    {
                        rightVal = int.Parse(valueLiteral);
                    }
                    else if (leftVal is long)
                    {
                        rightVal = long.Parse(valueLiteral);
                    }
                    else if (leftVal is decimal)
                    {
                        rightVal = decimal.Parse(valueLiteral);
                    }
                    else if (leftVal is double)
                    {
                        rightVal = double.Parse(valueLiteral);
                    }
                    else if (leftVal is DateTime)
                    {
                        rightVal = DateTime.Parse(valueLiteral);
                    }
                    else
                    {
                        // Unsupported type
                        return false;
                    }

                    var compare = cmp.CompareTo(rightVal);
                    satisfied = op switch
                    {
                        ">" => compare > 0,
                        ">=" => compare >= 0,
                        "<" => compare < 0,
                        "<=" => compare <= 0,
                        "==" => compare == 0,
                        "!=" => compare != 0,
                        _ => false
                    };
                }
                else
                {
                    // Fallback to string equality
                    satisfied = op switch
                    {
                        "==" => string.Equals(leftVal.ToString(), valueLiteral, StringComparison.OrdinalIgnoreCase),
                        "!=" => !string.Equals(leftVal.ToString(), valueLiteral, StringComparison.OrdinalIgnoreCase),
                        _ => false
                    };
                }

                if (!satisfied) return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rule evaluation failed for event {EventId} with conditions {Conditions}",
                businessEvent.EventId, conditions);
            return false;
        }
    }

    private async Task LogEventRoutingAsync(RouteResult result)
    {
        try
        {
            var routingLog = new EventRoutingLog
            {
                EventId = result.BusinessEvent.EventId,
                EventType = result.BusinessEvent.EventType,
                SourceService = result.BusinessEvent.SourceService,
                Destinations = string.Join(",", result.Destinations.Select(d => d.ConsumerType)),
                RouteTimestamp = result.RouteTimestamp,
                Success = result.Destinations.Any()
            };

            // TODO: Save routing log to database when entity is available
            _logger.LogInformation("Event {EventId} routed to {Count} destinations: {Destinations}",
                result.BusinessEvent.EventId, result.Destinations.Count,
                string.Join(",", result.Destinations.Select(d => d.ConsumerType)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log event routing for event {EventId}",
                result.BusinessEvent.EventId);
        }
    }

    private async Task RefreshRoutingCacheAsync()
    {
        _eventRoutes.Clear();
        _logger.LogInformation("Refreshed event routing cache");
    }

    private string GetQueueNameForConsumer(string consumerType)
    {
        // Define queue names for each consumer type
        return consumerType switch
        {
            "LoanApplicationConsumer" => "loan-application-created",
            "LoanStatusConsumer" => "loan-status-changed",
            "PaymentReminderConsumer" => "payment-due-reminder",
            _ => $"temp-queue-{consumerType.ToLower().Replace("consumer", "")}"
        };
    }

    #endregion
}

/// <summary>
/// Represents an event routing rule
/// </summary>
public class EventRoutingRuleDto
{
    public int Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string ConsumerType { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string? Conditions { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Internal representation of an event route
/// </summary>
public class EventRoute
{
    public int Id { get; set; }
    public bool IsActive { get; set; }
    public int Priority { get; set; }
    public string? Conditions { get; set; }
    public RouteDestination Destination { get; set; } = new RouteDestination();
}

/// <summary>
/// Represents a routing destination
/// </summary>
public class RouteDestination
{
    public string ConsumerType { get; set; } = string.Empty;
    public string QueueName { get; set; } = string.Empty;
}

/// <summary>
/// Result of event routing
/// </summary>
public class RouteResult
{
    public IBusinessEvent BusinessEvent { get; set; } = default!;
    public List<RouteDestination> Destinations { get; set; } = new List<RouteDestination>();
    public DateTime RouteTimestamp { get; set; }
}

/// <summary>
/// Event routing metrics
/// </summary>
public class EventRoutingMetrics
{
    public int TotalRoutingRules { get; set; }
    public int TotalEventsProcessed { get; set; }
    public int SuccessfulRoutes { get; set; }
    public int FailedRoutes { get; set; }
    public DateTime LastMetricsUpdate { get; set; }
}

/// <summary>
/// Interface for event routing service
/// </summary>
public interface IEventRoutingService
{
    Task<RouteResult> RouteEventAsync(IBusinessEvent businessEvent);
    Task CreateRoutingRuleAsync(IntelliFin.Shared.DomainModels.Entities.EventRoutingRule rule);
    Task UpdateRoutingRuleAsync(int ruleId, IntelliFin.Shared.DomainModels.Entities.EventRoutingRule rule);
    Task DeleteRoutingRuleAsync(int ruleId);
    Task<List<IntelliFin.Shared.DomainModels.Entities.EventRoutingRule>> GetRoutingRulesAsync(string? eventType = null, string? consumerType = null, bool? isActive = null);
    Task<bool> CanConsumerProcessEventAsync(IBusinessEvent businessEvent, string consumerType);
    Task<EventRoutingMetrics> GetMetricsAsync();
}

/// <summary>
/// Event routing log for auditing
/// </summary>
public class EventRoutingLogDto
{
    public Guid EventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string SourceService { get; set; } = string.Empty;
    public string Destinations { get; set; } = string.Empty;
    public DateTime RouteTimestamp { get; set; }
    public bool Success { get; set; }
}
