using IntelliFin.ClientManagement.Domain.Events;
using IntelliFin.ClientManagement.EventHandlers;
using MassTransit;

namespace IntelliFin.ClientManagement.Consumers;

/// <summary>
/// MassTransit consumer for KYC-related domain events
/// Routes events to appropriate event handlers
/// </summary>
public class KycCompletedEventConsumer : IConsumer<KycCompletedEvent>
{
    private readonly IDomainEventHandler<KycCompletedEvent> _eventHandler;
    private readonly ILogger<KycCompletedEventConsumer> _logger;

    public KycCompletedEventConsumer(
        IDomainEventHandler<KycCompletedEvent> eventHandler,
        ILogger<KycCompletedEventConsumer> logger)
    {
        _eventHandler = eventHandler;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<KycCompletedEvent> context)
    {
        var evt = context.Message;

        _logger.LogInformation(
            "Consuming KycCompletedEvent: ClientId={ClientId}, MessageId={MessageId}, CorrelationId={CorrelationId}",
            evt.ClientId, context.MessageId, evt.CorrelationId);

        try
        {
            await _eventHandler.HandleAsync(evt, context.CancellationToken);

            _logger.LogInformation(
                "KycCompletedEvent processed successfully: ClientId={ClientId}",
                evt.ClientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing KycCompletedEvent: ClientId={ClientId}",
                evt.ClientId);

            // Re-throw to trigger MassTransit retry mechanism
            throw;
        }
    }
}

/// <summary>
/// MassTransit consumer for KYC rejection events
/// </summary>
public class KycRejectedEventConsumer : IConsumer<KycRejectedEvent>
{
    private readonly IDomainEventHandler<KycRejectedEvent> _eventHandler;
    private readonly ILogger<KycRejectedEventConsumer> _logger;

    public KycRejectedEventConsumer(
        IDomainEventHandler<KycRejectedEvent> eventHandler,
        ILogger<KycRejectedEventConsumer> logger)
    {
        _eventHandler = eventHandler;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<KycRejectedEvent> context)
    {
        var evt = context.Message;

        _logger.LogInformation(
            "Consuming KycRejectedEvent: ClientId={ClientId}, MessageId={MessageId}",
            evt.ClientId, context.MessageId);

        try
        {
            await _eventHandler.HandleAsync(evt, context.CancellationToken);

            _logger.LogInformation(
                "KycRejectedEvent processed successfully: ClientId={ClientId}",
                evt.ClientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing KycRejectedEvent: ClientId={ClientId}",
                evt.ClientId);

            throw;
        }
    }
}

/// <summary>
/// MassTransit consumer for EDD escalation events
/// </summary>
public class EddEscalatedEventConsumer : IConsumer<EddEscalatedEvent>
{
    private readonly IDomainEventHandler<EddEscalatedEvent> _eventHandler;
    private readonly ILogger<EddEscalatedEventConsumer> _logger;

    public EddEscalatedEventConsumer(
        IDomainEventHandler<EddEscalatedEvent> eventHandler,
        ILogger<EddEscalatedEventConsumer> logger)
    {
        _eventHandler = eventHandler;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<EddEscalatedEvent> context)
    {
        var evt = context.Message;

        _logger.LogInformation(
            "Consuming EddEscalatedEvent: ClientId={ClientId}, MessageId={MessageId}, Reason={Reason}",
            evt.ClientId, context.MessageId, evt.EddReason);

        try
        {
            await _eventHandler.HandleAsync(evt, context.CancellationToken);

            _logger.LogInformation(
                "EddEscalatedEvent processed successfully: ClientId={ClientId}",
                evt.ClientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing EddEscalatedEvent: ClientId={ClientId}",
                evt.ClientId);

            throw;
        }
    }
}

/// <summary>
/// MassTransit consumer for EDD approval events
/// </summary>
public class EddApprovedEventConsumer : IConsumer<EddApprovedEvent>
{
    private readonly IDomainEventHandler<EddApprovedEvent> _eventHandler;
    private readonly ILogger<EddApprovedEventConsumer> _logger;

    public EddApprovedEventConsumer(
        IDomainEventHandler<EddApprovedEvent> eventHandler,
        ILogger<EddApprovedEventConsumer> logger)
    {
        _eventHandler = eventHandler;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<EddApprovedEvent> context)
    {
        var evt = context.Message;

        _logger.LogInformation(
            "Consuming EddApprovedEvent: ClientId={ClientId}, MessageId={MessageId}",
            evt.ClientId, context.MessageId);

        try
        {
            await _eventHandler.HandleAsync(evt, context.CancellationToken);

            _logger.LogInformation(
                "EddApprovedEvent processed successfully: ClientId={ClientId}",
                evt.ClientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing EddApprovedEvent: ClientId={ClientId}",
                evt.ClientId);

            throw;
        }
    }
}

/// <summary>
/// MassTransit consumer for EDD rejection events
/// </summary>
public class EddRejectedEventConsumer : IConsumer<EddRejectedEvent>
{
    private readonly IDomainEventHandler<EddRejectedEvent> _eventHandler;
    private readonly ILogger<EddRejectedEventConsumer> _logger;

    public EddRejectedEventConsumer(
        IDomainEventHandler<EddRejectedEvent> eventHandler,
        ILogger<EddRejectedEventConsumer> logger)
    {
        _eventHandler = eventHandler;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<EddRejectedEvent> context)
    {
        var evt = context.Message;

        _logger.LogInformation(
            "Consuming EddRejectedEvent: ClientId={ClientId}, MessageId={MessageId}",
            evt.ClientId, context.MessageId);

        try
        {
            await _eventHandler.HandleAsync(evt, context.CancellationToken);

            _logger.LogInformation(
                "EddRejectedEvent processed successfully: ClientId={ClientId}",
                evt.ClientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing EddRejectedEvent: ClientId={ClientId}",
                evt.ClientId);

            throw;
        }
    }
}
