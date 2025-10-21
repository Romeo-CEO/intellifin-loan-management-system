using System.Text.Json;
using IntelliFin.Shared.Audit;

namespace IntelliFin.ClientManagement.Integration;

public static class AuditEventDeadLetterQueue
{
    public static async Task WriteAsync(IEnumerable<AuditEventPayload> events, string path, Exception reason, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        await using var writer = new StreamWriter(stream);

        var now = DateTime.UtcNow;
        foreach (var evt in events)
        {
            var record = new
            {
                auditEvent = evt,
                failureReason = reason.Message,
                retryCount = 3,
                lastAttemptAt = now,
                addedToDlqAt = now
            };
            var json = JsonSerializer.Serialize(record);
            await writer.WriteLineAsync(json.AsMemory(), cancellationToken);
        }
    }
}
