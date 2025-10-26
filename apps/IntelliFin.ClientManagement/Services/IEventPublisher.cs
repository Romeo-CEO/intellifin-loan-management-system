namespace IntelliFin.ClientManagement.Services;

/// <summary>
/// Service for publishing domain events
/// Abstracts MassTransit for easier testing
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publishes a domain event
    /// </summary>
    /// <typeparam name="TEvent">Type of event to publish</typeparam>
    /// <param name="domainEvent">The event to publish</param>
    /// <param name="routingKey">RabbitMQ routing key</param>
    /// <param name="correlationId">Correlation ID for tracking</param>
    Task PublishAsync<TEvent>(
        TEvent domainEvent,
        string routingKey,
        string? correlationId = null)
        where TEvent : class;
}
