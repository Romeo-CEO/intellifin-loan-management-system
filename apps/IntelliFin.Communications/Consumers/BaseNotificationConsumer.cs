using IntelliFin.Shared.DomainModels.Entities;
using IntelliFin.Shared.DomainModels.Repositories;
using IntelliFin.Shared.Infrastructure.Messaging.Contracts;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace IntelliFin.Communications.Consumers;

public abstract class BaseNotificationConsumer<TEvent> : IConsumer<TEvent>
    where TEvent : class, IBusinessEvent
{
    protected readonly INotificationRepository _notificationRepository;
    protected readonly ILogger<BaseNotificationConsumer<TEvent>> _logger;

    protected BaseNotificationConsumer(
        INotificationRepository notificationRepository,
        ILogger<BaseNotificationConsumer<TEvent>> logger)
    {
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Consume(ConsumeContext<TEvent> context)
    {
        var eventData = context.Message;
        var eventId = eventData.EventId;
        var eventType = typeof(TEvent).Name;

        _logger.LogInformation(
            "Processing event {EventId} of type {EventType}",
            eventId, eventType);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Check idempotency - ensure same event isn't processed twice
            if (await _notificationRepository.IsEventProcessedAsync(eventId))
            {
                _logger.LogInformation("Event {EventId} already processed, skipping", eventId);
                return;
            }

            // Process the business event
            await ProcessEventAsync(eventData, context);

            // Mark event as successfully processed
            await _notificationRepository.MarkEventProcessedAsync(
                eventId, eventType, success: true);

            stopwatch.Stop();
            _logger.LogInformation(
                "Successfully processed event {EventId} in {ElapsedMs}ms",
                eventId, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex,
                "Failed to process event {EventId} after {ElapsedMs}ms: {Error}",
                eventId, stopwatch.ElapsedMilliseconds, ex.Message);

            // Mark event as failed
            await _notificationRepository.MarkEventProcessedAsync(
                eventId, eventType, success: false, error: ex.Message);

            // Re-throw to trigger MassTransit's retry mechanism
            throw;
        }
    }

    /// <summary>
    /// Abstract method to be implemented by concrete consumers for business logic
    /// </summary>
    protected abstract Task ProcessEventAsync(TEvent eventData, ConsumeContext<TEvent> context);

    /// <summary>
    /// Helper method to create notification requests with common properties
    /// </summary>
    protected List<NotificationRequest> BuildNotificationRequests(
        Guid eventId,
        IEnumerable<NotificationTarget> targets,
        string templateCategory)
    {
        var requests = new List<NotificationRequest>();

        foreach (var target in targets)
        {
            var request = new NotificationRequest
            {
                EventId = eventId,
                RecipientId = target.RecipientId,
                RecipientType = target.RecipientType,
                Channel = GetChannelForTarget(target),
                TemplateCategory = templateCategory,
                Priority = target.Priority,
                PersonalizationContext = target.PersonalizationData,
                BranchId = target.BranchId
            };

            if (target.TemplateName != null)
                request.TemplateName = target.TemplateName;

            requests.Add(request);
        }

        return requests;
    }

    /// <summary>
    /// Determines the best channel for a given notification target
    /// </summary>
    protected virtual string GetChannelForTarget(NotificationTarget target)
    {
        // Default implementation - can be overridden by specific consumers
        if (target.PreferredChannel != null)
            return target.PreferredChannel;

        // Fallback to SMS for customers, InApp for staff
        return target.RecipientType switch
        {
            "Customer" => "SMS",
            "LoanOfficer" or "BranchManager" => "InApp",
            _ => "SMS"
        };
    }

    /// <summary>
    /// Gets the branch ID for a notification (default implementation)
    /// </summary>
    protected virtual int GetBranchId(TEvent eventData)
    {
        // This will be overridden by subclasses with event-specific logic
        // For now, return default branch
        return 1;
    }

    /// <summary>
    /// Gets the created by user (default implementation)
    /// </summary>
    protected virtual string GetCreatedBy()
    {
        // This can be enhanced with actual user context
        return "System";
    }

    /// <summary>
    /// Creates a notification log entry with proper metadata
    /// </summary>
    protected async Task<NotificationLog> CreateNotificationLogAsync(
        NotificationRequest request,
        string content)
    {
        var log = new NotificationLog
        {
            EventId = request.EventId,
            RecipientId = request.RecipientId,
            RecipientType = request.RecipientType,
            Channel = request.Channel,
            TemplateId = await GetTemplateIdAsync(request.TemplateName),
            Subject = request.Subject,
            Content = content,
            PersonalizationData = request.PersonalizationContext != null
                ? System.Text.Json.JsonSerializer.Serialize(request.PersonalizationContext)
                : null,
            Status = NotificationStatus.Pending,
            BranchId = request.BranchId ?? GetBranchId(default!),
            CreatedBy = GetCreatedBy()
        };

        // This method signature might need adjustment based on repository interface
        // For now, we'll create the entity and let subclasses handle persistence
        return log;
    }

    /// <summary>
    /// Gets template ID by name (can be cached in future)
    /// </summary>
    protected virtual async Task<int?> GetTemplateIdAsync(string? templateName)
    {
        // TODO: Implement template lookup by name
        // For now, return null - templates can be resolved later
        return null;
    }
}

/// <summary>
/// Represents a target for notification delivery
/// </summary>
public class NotificationTarget
{
    public string RecipientId { get; set; } = string.Empty;
    public string RecipientType { get; set; } = string.Empty;
    public string? PreferredChannel { get; set; }
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
    public object? PersonalizationData { get; set; }
    public string? TemplateName { get; set; }
    public int? BranchId { get; set; }
}

/// <summary>
/// Represents a request to send a notification
/// </summary>
public class NotificationRequest
{
    public Guid EventId { get; set; }
    public string RecipientId { get; set; } = string.Empty;
    public string RecipientType { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string TemplateCategory { get; set; } = string.Empty;
    public string? TemplateName { get; set; }
    public string? Subject { get; set; }
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
    public object? PersonalizationContext { get; set; }
    public int? BranchId { get; set; }
}

/// <summary>
/// Notification priority levels
/// </summary>
public enum NotificationPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}
