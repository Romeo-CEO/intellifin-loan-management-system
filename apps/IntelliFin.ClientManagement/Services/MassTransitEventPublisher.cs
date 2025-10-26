using IntelliFin.ClientManagement.Extensions;
using MassTransit;

namespace IntelliFin.ClientManagement.Services;

/// <summary>
/// MassTransit implementation of event publisher
/// Publishes events to RabbitMQ
/// </summary>
public class MassTransitEventPublisher : IEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<MassTransitEventPublisher> _logger;

    public MassTransitEventPublisher(
        IPublishEndpoint publishEndpoint,
        ILogger<MassTransitEventPublisher> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(
        TEvent domainEvent,
        string routingKey,
        string? correlationId = null)
        where TEvent : class
    {
        try
        {
            _logger.LogInformation(
                "Publishing event {EventType} with routing key {RoutingKey}, CorrelationId={CorrelationId}",
                typeof(TEvent).Name, routingKey, correlationId);

            await _publishEndpoint.PublishDomainEventAsync(
                domainEvent,
                routingKey,
                correlationId);

            _logger.LogDebug(
                "Event published successfully: {EventType}",
                typeof(TEvent).Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error publishing event {EventType} with routing key {RoutingKey}",
                typeof(TEvent).Name, routingKey);

            throw;
        }
    }
}

/// <summary>
/// In-memory event publisher for testing and when MassTransit is disabled
/// Invokes event handlers directly
/// </summary>
public class InMemoryEventPublisher : IEventPublisher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InMemoryEventPublisher> _logger;

    public InMemoryEventPublisher(
        IServiceProvider serviceProvider,
        ILogger<InMemoryEventPublisher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(
        TEvent domainEvent,
        string routingKey,
        string? correlationId = null)
        where TEvent : class
    {
        _logger.LogInformation(
            "Publishing event {EventType} (in-memory) with routing key {RoutingKey}",
            typeof(TEvent).Name, routingKey);

        // For in-memory, we just invoke handlers directly via fire-and-forget
        // This maintains the same behavior as Phase 1.14a
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var handler = scope.ServiceProvider.GetService<EventHandlers.IDomainEventHandler<TEvent>>();
                
                if (handler != null)
                {
                    await handler.HandleAsync(domainEvent);
                }
                else
                {
                    _logger.LogWarning("No handler registered for event type {EventType}", typeof(TEvent).Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling event {EventType} in-memory", typeof(TEvent).Name);
            }
        });

        await Task.CompletedTask;
    }
}
