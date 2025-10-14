using System.Net.Http;
using IntelliFin.Shared.Audit;

namespace IntelliFin.Tests.Integration.FinancialService.Stubs;

internal sealed class TestAuditClient : IAuditClient
{
    private readonly List<AuditEventPayload> _events = new();
    private readonly bool _shouldThrow;

    public TestAuditClient(bool shouldThrow = false)
    {
        _shouldThrow = shouldThrow;
    }

    public IReadOnlyList<AuditEventPayload> Events => _events;

    public Task LogEventAsync(AuditEventPayload payload, CancellationToken cancellationToken = default)
    {
        if (_shouldThrow)
        {
            throw new HttpRequestException("admin-service-unavailable");
        }

        _events.Add(payload);
        return Task.CompletedTask;
    }

    public Task LogEventsBatchAsync(IEnumerable<AuditEventPayload> payloads, CancellationToken cancellationToken = default)
    {
        if (_shouldThrow)
        {
            throw new HttpRequestException("admin-service-unavailable");
        }

        _events.AddRange(payloads);
        return Task.CompletedTask;
    }
}
