using MassTransit;

namespace IntelliFin.ClientManagement.Consumers;

/// <summary>
/// Consumer for processing messages from the Dead Letter Queue
/// Handles failed notification events for manual review and retry
/// </summary>
public class DeadLetterQueueConsumer : IConsumer<object>
{
    private readonly ILogger<DeadLetterQueueConsumer> _logger;

    public DeadLetterQueueConsumer(ILogger<DeadLetterQueueConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<object> context)
    {
        var message = context.Message;
        var messageType = message.GetType().Name;

        _logger.LogWarning(
            "Processing message from DLQ: MessageId={MessageId}, MessageType={MessageType}, " +
            "RedeliveryCount={RedeliveryCount}",
            context.MessageId,
            messageType,
            context.GetRedeliveryCount());

        try
        {
            // Log detailed error information
            _logger.LogError(
                "DLQ Message Details: {@Message}, Headers={@Headers}, " +
                "ExceptionInfo={ExceptionInfo}",
                message,
                context.Headers,
                context.Headers.Get<string>("MT-Fault-Message"));

            // In production, this could:
            // 1. Store to database for manual review
            // 2. Send alert to operations team
            // 3. Attempt limited retry with different strategy
            // 4. Forward to external monitoring system

            // For now, just acknowledge to remove from DLQ
            await Task.CompletedTask;

            _logger.LogInformation(
                "DLQ message processed and acknowledged: MessageId={MessageId}",
                context.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing DLQ message: MessageId={MessageId}",
                context.MessageId);

            // Don't throw - would cause infinite loop
            // Message stays in DLQ for manual intervention
        }
    }
}
