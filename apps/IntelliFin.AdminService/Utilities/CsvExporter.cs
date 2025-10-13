using System.Text;
using IntelliFin.AdminService.Models;

namespace IntelliFin.AdminService.Utilities;

public static class CsvExporter
{
    public static byte[] Export(IEnumerable<AuditEvent> events)
    {
        var builder = new StringBuilder();
        builder.AppendLine("EventId,Timestamp,Actor,Action,EntityType,EntityId,CorrelationId,IpAddress,UserAgent,MigrationSource,EventData");

        foreach (var evt in events)
        {
            builder.AppendLine(string.Join(',',
                Escape(evt.EventId.ToString()),
                Escape(evt.Timestamp.ToString("O")),
                Escape(evt.Actor),
                Escape(evt.Action),
                Escape(evt.EntityType),
                Escape(evt.EntityId),
                Escape(evt.CorrelationId),
                Escape(evt.IpAddress),
                Escape(evt.UserAgent),
                Escape(evt.MigrationSource),
                Escape(evt.EventData)));
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var needsQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n');
        var sanitized = value.Replace("\"", "\"\"");

        return needsQuotes ? $"\"{sanitized}\"" : sanitized;
    }
}
