using System.Threading.Channels;

namespace IntelliFin.IdentityService.Services;

/// <summary>
/// Interface for background message queue
/// </summary>
/// <typeparam name="T">Type of message to queue</typeparam>
public interface IBackgroundQueue<T>
{
    /// <summary>
    /// Enqueue a message for background processing
    /// </summary>
    ValueTask QueueAsync(T message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dequeue a message from the queue (blocking operation)
    /// </summary>
    ValueTask<T> DequeueAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// In-memory implementation of background queue using System.Threading.Channels
/// </summary>
/// <typeparam name="T">Type of message to queue</typeparam>
public class InMemoryBackgroundQueue<T> : IBackgroundQueue<T>
{
    private readonly Channel<T> _queue;
    private readonly ILogger<InMemoryBackgroundQueue<T>> _logger;

    public InMemoryBackgroundQueue(ILogger<InMemoryBackgroundQueue<T>> logger, int capacity = 100)
    {
        _logger = logger;
        
        // Create a bounded channel with specified capacity
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        
        _queue = Channel.CreateBounded<T>(options);
    }

    public async ValueTask QueueAsync(T message, CancellationToken cancellationToken = default)
    {
        if (message == null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        await _queue.Writer.WriteAsync(message, cancellationToken);
        
        _logger.LogDebug("Message queued: {MessageType}", typeof(T).Name);
    }

    public async ValueTask<T> DequeueAsync(CancellationToken cancellationToken = default)
    {
        var message = await _queue.Reader.ReadAsync(cancellationToken);
        
        _logger.LogDebug("Message dequeued: {MessageType}", typeof(T).Name);
        
        return message;
    }
}
