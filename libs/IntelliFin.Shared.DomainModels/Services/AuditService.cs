using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.Shared.DomainModels.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace IntelliFin.Shared.DomainModels.Services;

/// <summary>
/// Comprehensive audit trail service implementation
/// </summary>
public class AuditService : IAuditService
{
    private readonly LmsDbContext _dbContext;
    private readonly IDistributedCache _cache;
    private readonly ILogger<AuditService> _logger;
    
    private const string AuditCachePrefix = "audit:";
    private const int DefaultCacheExpirationMinutes = 30;

    public AuditService(
        LmsDbContext dbContext,
        IDistributedCache cache,
        ILogger<AuditService> logger)
    {
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;
    }

    public async Task LogEventAsync(string actor, string action, string entityType, string entityId, object? data = null, CancellationToken cancellationToken = default)
    {
        var context = new AuditEventContext
        {
            Actor = actor,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Data = data != null ? JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(data)) ?? new() : new(),
            Category = DetermineCategory(action, entityType),
            Source = "IntelliFin.LoanManagementSystem"
        };

        await LogEventAsync(context, cancellationToken);
    }

    public async Task LogEventAsync(AuditEventContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var auditEvent = new AuditEvent
            {
                Id = Guid.NewGuid(),
                Actor = context.Actor,
                Action = context.Action,
                EntityType = context.EntityType,
                EntityId = context.EntityId,
                OccurredAtUtc = context.OccurredAt ?? DateTime.UtcNow,
                Data = JsonSerializer.Serialize(new
                {
                    IpAddress = context.IpAddress,
                    UserAgent = context.UserAgent,
                    SessionId = context.SessionId,
                    Source = context.Source,
                    Category = context.Category.ToString(),
                    Severity = context.Severity.ToString(),
                    Success = context.Success,
                    ErrorMessage = context.ErrorMessage,
                    Data = context.Data
                })
            };

            _dbContext.AuditEvents.Add(auditEvent);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Cache recent events for quick access
            await CacheRecentEventAsync(auditEvent, cancellationToken);

            _logger.LogDebug("Audit event logged: {Actor} {Action} on {EntityType}:{EntityId}", 
                context.Actor, context.Action, context.EntityType, context.EntityId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log audit event for {Actor} {Action}", context.Actor, context.Action);
            // Don't throw - audit logging should not break the main flow
        }
    }

    public async Task<AuditQueryResult> QueryEventsAsync(AuditQuery query, CancellationToken cancellationToken = default)
    {
        try
        {
            var dbQuery = _dbContext.AuditEvents.AsQueryable();

            // Apply filters
            if (query.StartDate.HasValue)
                dbQuery = dbQuery.Where(e => e.OccurredAtUtc >= query.StartDate.Value);

            if (query.EndDate.HasValue)
                dbQuery = dbQuery.Where(e => e.OccurredAtUtc <= query.EndDate.Value);

            if (!string.IsNullOrEmpty(query.Actor))
                dbQuery = dbQuery.Where(e => e.Actor.Contains(query.Actor));

            if (!string.IsNullOrEmpty(query.Action))
                dbQuery = dbQuery.Where(e => e.Action.Contains(query.Action));

            if (!string.IsNullOrEmpty(query.EntityType))
                dbQuery = dbQuery.Where(e => e.EntityType.Contains(query.EntityType));

            if (!string.IsNullOrEmpty(query.EntityId))
                dbQuery = dbQuery.Where(e => e.EntityId == query.EntityId);

            if (query.Category.HasValue)
                dbQuery = dbQuery.Where(e => e.Data.Contains(query.Category.ToString()!));

            if (!string.IsNullOrEmpty(query.SearchText))
            {
                var searchLower = query.SearchText.ToLower();
                dbQuery = dbQuery.Where(e => 
                    e.Actor.ToLower().Contains(searchLower) ||
                    e.Action.ToLower().Contains(searchLower) ||
                    e.EntityType.ToLower().Contains(searchLower) ||
                    e.Data.ToLower().Contains(searchLower));
            }

            // Get total count
            var totalCount = await dbQuery.CountAsync(cancellationToken);

            // Apply sorting
            dbQuery = query.SortDescending
                ? dbQuery.OrderByDescending(e => EF.Property<DateTime>(e, query.SortBy))
                : dbQuery.OrderBy(e => EF.Property<DateTime>(e, query.SortBy));

            // Apply pagination
            var events = await dbQuery
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);

            return new AuditQueryResult
            {
                Events = events,
                TotalCount = totalCount,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query audit events");
            throw;
        }
    }

    public async Task<AuditReport> GenerateReportAsync(AuditReportRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating audit report for period {StartDate} to {EndDate}", 
                request.StartDate, request.EndDate);

            // Query events for the report period
            var query = new AuditQuery
            {
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                PageSize = int.MaxValue // Get all events for report
            };

            var queryResult = await QueryEventsAsync(query, cancellationToken);
            var statistics = await GenerateStatisticsAsync(queryResult.Events);

            // Generate report content based on format
            var content = request.Format switch
            {
                AuditReportFormat.Pdf => await GeneratePdfReportAsync(queryResult.Events, statistics, request),
                AuditReportFormat.Excel => await GenerateExcelReportAsync(queryResult.Events, statistics, request),
                AuditReportFormat.Json => await GenerateJsonReportAsync(queryResult.Events, statistics, request),
                AuditReportFormat.Csv => await GenerateCsvReportAsync(queryResult.Events, statistics, request),
                AuditReportFormat.Html => await GenerateHtmlReportAsync(queryResult.Events, statistics, request),
                _ => throw new ArgumentException($"Unsupported report format: {request.Format}")
            };

            return new AuditReport
            {
                Title = request.Title ?? $"Audit Report - {request.ReportType}",
                Description = request.Description ?? $"Audit report for period {request.StartDate:yyyy-MM-dd} to {request.EndDate:yyyy-MM-dd}",
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                ReportType = request.ReportType,
                Format = request.Format,
                Content = content,
                Statistics = statistics,
                SampleEvents = queryResult.Events.Take(10).ToList(),
                Metadata = new Dictionary<string, object>
                {
                    ["GeneratedBy"] = "IntelliFin Audit Service",
                    ["TotalEvents"] = queryResult.TotalCount,
                    ["ReportSize"] = content.Length,
                    ["GenerationTimeMs"] = DateTime.UtcNow.Subtract(DateTime.UtcNow).TotalMilliseconds
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate audit report");
            throw;
        }
    }

    public async Task<AuditStatistics> GetStatisticsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = new AuditQuery
            {
                StartDate = startDate,
                EndDate = endDate,
                PageSize = int.MaxValue
            };

            var queryResult = await QueryEventsAsync(query, cancellationToken);
            return await GenerateStatisticsAsync(queryResult.Events);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate audit statistics");
            throw;
        }
    }

    public async Task<AuditIntegrityResult> VerifyIntegrityAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        try
        {
            var events = await _dbContext.AuditEvents
                .Where(e => e.OccurredAtUtc >= startDate && e.OccurredAtUtc <= endDate)
                .OrderBy(e => e.OccurredAtUtc)
                .ToListAsync(cancellationToken);

            var violations = new List<string>();
            var verifiedCount = 0;

            // Check for data integrity issues
            foreach (var auditEvent in events)
            {
                if (await IsEventIntact(auditEvent))
                {
                    verifiedCount++;
                }
                else
                {
                    violations.Add($"Event {auditEvent.Id} failed integrity check");
                }
            }

            // Check for sequence gaps
            var sequenceGaps = DetectSequenceGaps(events);
            violations.AddRange(sequenceGaps);

            // Generate verification hash
            var verificationHash = GenerateVerificationHash(events);

            return new AuditIntegrityResult
            {
                IsIntact = violations.Count == 0,
                TotalRecords = events.Count,
                VerifiedRecords = verifiedCount,
                IntegrityViolations = violations,
                VerificationHash = verificationHash
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify audit trail integrity");
            throw;
        }
    }

    #region Private Helper Methods

    private AuditEventCategory DetermineCategory(string action, string entityType)
    {
        return action.ToLower() switch
        {
            var a when a.Contains("login") || a.Contains("logout") => AuditEventCategory.Authentication,
            var a when a.Contains("authorize") || a.Contains("permission") => AuditEventCategory.Authorization,
            var a when a.Contains("create") || a.Contains("update") || a.Contains("delete") => AuditEventCategory.DataModification,
            var a when a.Contains("view") || a.Contains("read") || a.Contains("query") => AuditEventCategory.DataAccess,
            var a when entityType.ToLower().Contains("loan") => AuditEventCategory.LoanActivity,
            var a when entityType.ToLower().Contains("client") => AuditEventCategory.ClientActivity,
            var a when entityType.ToLower().Contains("payment") || entityType.ToLower().Contains("transaction") => AuditEventCategory.FinancialTransaction,
            var a when a.Contains("report") => AuditEventCategory.ReportGeneration,
            var a when a.Contains("config") => AuditEventCategory.ConfigurationChange,
            var a when a.Contains("error") => AuditEventCategory.ErrorEvent,
            _ => AuditEventCategory.SystemActivity
        };
    }

    private async Task CacheRecentEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        try
        {
            var cacheKey = $"{AuditCachePrefix}recent:{auditEvent.Actor}";
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(DefaultCacheExpirationMinutes)
            };

            var eventJson = JsonSerializer.Serialize(auditEvent);
            await _cache.SetStringAsync(cacheKey, eventJson, cacheOptions, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache recent audit event");
        }
    }

    private async Task<AuditStatistics> GenerateStatisticsAsync(List<AuditEvent> events)
    {
        var statistics = new AuditStatistics
        {
            TotalEvents = events.Count
        };

        if (!events.Any())
            return statistics;

        // Parse data for detailed statistics
        var parsedEvents = events.Select(e => new
        {
            Event = e,
            ParsedData = TryParseEventData(e.Data)
        }).ToList();

        // Events by category
        statistics.EventsByCategory = parsedEvents
            .GroupBy(e => GetCategoryFromData(e.ParsedData))
            .ToDictionary(g => g.Key, g => g.Count());

        // Events by severity
        statistics.EventsBySeverity = parsedEvents
            .GroupBy(e => GetSeverityFromData(e.ParsedData))
            .ToDictionary(g => g.Key, g => g.Count());

        // Events by action
        statistics.EventsByAction = parsedEvents
            .GroupBy(e => e.Event.Action)
            .ToDictionary(g => g.Key, g => g.Count());

        // Events by actor
        statistics.EventsByActor = parsedEvents
            .GroupBy(e => e.Event.Actor)
            .ToDictionary(g => g.Key, g => g.Count());

        // Events by entity type
        statistics.EventsByEntityType = parsedEvents
            .GroupBy(e => e.Event.EntityType)
            .ToDictionary(g => g.Key, g => g.Count());

        // Events by day
        statistics.EventsByDay = parsedEvents
            .GroupBy(e => e.Event.OccurredAtUtc.Date)
            .ToDictionary(g => g.Key, g => g.Count());

        // Events by hour
        statistics.EventsByHour = parsedEvents
            .GroupBy(e => e.Event.OccurredAtUtc.Hour)
            .ToDictionary(g => g.Key, g => g.Count());

        // Success/failure statistics
        var successEvents = parsedEvents.Where(e => GetSuccessFromData(e.ParsedData)).ToList();
        statistics.SuccessfulEvents = successEvents.Count;
        statistics.FailedEvents = statistics.TotalEvents - statistics.SuccessfulEvents;
        statistics.SuccessRate = statistics.TotalEvents > 0 ? 
            (double)statistics.SuccessfulEvents / statistics.TotalEvents * 100 : 0;

        // Top actors and actions
        statistics.TopActors = statistics.EventsByActor
            .OrderByDescending(kvp => kvp.Value)
            .Take(10)
            .Select(kvp => kvp.Key)
            .ToList();

        statistics.TopActions = statistics.EventsByAction
            .OrderByDescending(kvp => kvp.Value)
            .Take(10)
            .Select(kvp => kvp.Key)
            .ToList();

        // Detect anomalies
        statistics.Anomalies = await DetectAnomaliesAsync(parsedEvents.Cast<dynamic>().ToList());

        return statistics;
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

    private AuditEventSeverity GetSeverityFromData(Dictionary<string, object>? data)
    {
        if (data?.TryGetValue("Severity", out var severityObj) == true &&
            severityObj?.ToString() is string severityStr &&
            Enum.TryParse<AuditEventSeverity>(severityStr, out var severity))
        {
            return severity;
        }
        return AuditEventSeverity.Information;
    }

    private bool GetSuccessFromData(Dictionary<string, object>? data)
    {
        if (data?.TryGetValue("Success", out var successObj) == true)
        {
            return successObj?.ToString()?.ToLower() == "true";
        }
        return true; // Default to success if not specified
    }

    private async Task<List<AuditAnomaly>> DetectAnomaliesAsync(List<dynamic> parsedEvents)
    {
        var anomalies = new List<AuditAnomaly>();

        // Detect unusual activity patterns
        var eventsByHour = parsedEvents
            .GroupBy(e => e.Event.OccurredAtUtc.Hour)
            .ToDictionary(g => g.Key, g => g.Count());

        // Find hours with unusually high activity
        var avgEventsPerHour = eventsByHour.Values.Any() ? eventsByHour.Values.Average() : 0;
        var threshold = avgEventsPerHour * 3; // 3x average

        foreach (var kvp in eventsByHour.Where(kvp => kvp.Value > threshold))
        {
            anomalies.Add(new AuditAnomaly
            {
                Type = "HighActivityHour",
                Description = $"Unusually high activity detected at hour {kvp.Key} with {kvp.Value} events",
                Severity = AuditEventSeverity.Warning,
                Data = new Dictionary<string, object>
                {
                    ["Hour"] = kvp.Key,
                    ["EventCount"] = kvp.Value,
                    ["Threshold"] = threshold
                }
            });
        }

        return anomalies;
    }

    private async Task<bool> IsEventIntact(AuditEvent auditEvent)
    {
        // Basic integrity checks
        if (string.IsNullOrEmpty(auditEvent.Actor) || 
            string.IsNullOrEmpty(auditEvent.Action) ||
            string.IsNullOrEmpty(auditEvent.EntityType))
        {
            return false;
        }

        // Validate data format
        try
        {
            JsonSerializer.Deserialize<Dictionary<string, object>>(auditEvent.Data);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private List<string> DetectSequenceGaps(List<AuditEvent> events)
    {
        var violations = new List<string>();
        
        // Check for suspicious time gaps
        for (int i = 1; i < events.Count; i++)
        {
            var timeDiff = events[i].OccurredAtUtc - events[i - 1].OccurredAtUtc;
            if (timeDiff < TimeSpan.Zero)
            {
                violations.Add($"Time sequence violation: Event {events[i].Id} occurred before {events[i - 1].Id}");
            }
        }

        return violations;
    }

    private string GenerateVerificationHash(List<AuditEvent> events)
    {
        var combined = string.Join("", events.Select(e => $"{e.Id}{e.OccurredAtUtc:O}{e.Actor}{e.Action}"));
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
        return Convert.ToBase64String(hashBytes);
    }

    private async Task<byte[]> GeneratePdfReportAsync(List<AuditEvent> events, AuditStatistics statistics, AuditReportRequest request)
    {
        // Placeholder for PDF generation - would use a library like iTextSharp or PdfSharp
        var content = $"Audit Report - {request.ReportType}\nPeriod: {request.StartDate:yyyy-MM-dd} to {request.EndDate:yyyy-MM-dd}\nTotal Events: {statistics.TotalEvents}";
        return Encoding.UTF8.GetBytes(content);
    }

    private async Task<byte[]> GenerateExcelReportAsync(List<AuditEvent> events, AuditStatistics statistics, AuditReportRequest request)
    {
        // Placeholder for Excel generation - would use a library like EPPlus or OpenXML
        var content = $"Audit Report - {request.ReportType}\nPeriod: {request.StartDate:yyyy-MM-dd} to {request.EndDate:yyyy-MM-dd}\nTotal Events: {statistics.TotalEvents}";
        return Encoding.UTF8.GetBytes(content);
    }

    private async Task<byte[]> GenerateJsonReportAsync(List<AuditEvent> events, AuditStatistics statistics, AuditReportRequest request)
    {
        var report = new
        {
            Title = request.Title ?? $"Audit Report - {request.ReportType}",
            Period = new { Start = request.StartDate, End = request.EndDate },
            Statistics = statistics,
            Events = events.Take(100) // Limit events in JSON report
        };
        return JsonSerializer.SerializeToUtf8Bytes(report);
    }

    private async Task<byte[]> GenerateCsvReportAsync(List<AuditEvent> events, AuditStatistics statistics, AuditReportRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Id,Actor,Action,EntityType,EntityId,OccurredAt,Data");
        
        foreach (var evt in events)
        {
            sb.AppendLine($"{evt.Id},{evt.Actor},{evt.Action},{evt.EntityType},{evt.EntityId},{evt.OccurredAtUtc:yyyy-MM-dd HH:mm:ss},\"{evt.Data.Replace("\"", "\"\"")}\"");
        }
        
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private async Task<byte[]> GenerateHtmlReportAsync(List<AuditEvent> events, AuditStatistics statistics, AuditReportRequest request)
    {
        var html = $@"
<!DOCTYPE html>
<html>
<head>
    <title>Audit Report - {request.ReportType}</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 20px; }}
        table {{ border-collapse: collapse; width: 100%; }}
        th, td {{ border: 1px solid #ddd; padding: 8px; text-align: left; }}
        th {{ background-color: #f2f2f2; }}
        .stats {{ background-color: #f9f9f9; padding: 15px; margin-bottom: 20px; border-radius: 5px; }}
    </style>
</head>
<body>
    <h1>Audit Report - {request.ReportType}</h1>
    <div class='stats'>
        <h2>Statistics</h2>
        <p><strong>Period:</strong> {request.StartDate:yyyy-MM-dd} to {request.EndDate:yyyy-MM-dd}</p>
        <p><strong>Total Events:</strong> {statistics.TotalEvents}</p>
        <p><strong>Success Rate:</strong> {statistics.SuccessRate:F2}%</p>
    </div>
    <h2>Sample Events</h2>
    <table>
        <thead>
            <tr><th>Time</th><th>Actor</th><th>Action</th><th>Entity</th><th>ID</th></tr>
        </thead>
        <tbody>
            {string.Join("", events.Take(20).Select(e => $"<tr><td>{e.OccurredAtUtc:yyyy-MM-dd HH:mm:ss}</td><td>{e.Actor}</td><td>{e.Action}</td><td>{e.EntityType}</td><td>{e.EntityId}</td></tr>"))}
        </tbody>
    </table>
</body>
</html>";
        return Encoding.UTF8.GetBytes(html);
    }

    #endregion
}