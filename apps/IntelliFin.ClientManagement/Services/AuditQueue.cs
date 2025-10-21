using System.Threading.Channels;
using IntelliFin.Shared.Audit;

namespace IntelliFin.ClientManagement.Services;

public interface IAuditQueue
{
    ValueTask EnqueueAsync(AuditEventPayload payload, CancellationToken cancellationToken = default);
    bool TryRead(out AuditEventPayload? payload);
    int Count { get; }
}

public sealed class AuditQueue : IAuditQueue
{
    private readonly Channel<AuditEventPayload> _channel;

    public AuditQueue()
    {
        // Unbounded to avoid blocking business requests; rely on DLQ for backpressure handling
        _channel = Channel.CreateUnbounded<AuditEventPayload>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
    }

    public ValueTask EnqueueAsync(AuditEventPayload payload, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(payload, cancellationToken);
    }

    public bool TryRead(out AuditEventPayload? payload)
    {
        if (_channel.Reader.TryRead(out var item))
        {
            payload = item;
            return true;
        }

        payload = null;
        return false;
    }

    public ChannelReader<AuditEventPayload> Reader => _channel.Reader;

    public int Count => 0; // Not tracked for unbounded channel; batching drains until empty
}
