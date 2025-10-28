namespace IntelliFin.TreasuryService.Contracts;

/// <summary>
/// Generic interface for handling domain events
/// </summary>
public interface IDomainEventHandler<in TEvent>
{
    /// <summary>
    /// Handle the domain event asynchronously
    /// </summary>
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}

