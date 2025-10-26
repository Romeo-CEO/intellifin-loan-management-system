namespace IntelliFin.ClientManagement.EventHandlers;

/// <summary>
/// Base interface for domain event handlers
/// </summary>
/// <typeparam name="TEvent">Type of domain event to handle</typeparam>
public interface IDomainEventHandler<in TEvent> where TEvent : class
{
    /// <summary>
    /// Handles the domain event
    /// </summary>
    /// <param name="domainEvent">The event to handle</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken = default);
}
