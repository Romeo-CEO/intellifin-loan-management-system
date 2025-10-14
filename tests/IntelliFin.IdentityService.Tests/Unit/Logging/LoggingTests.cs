using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Json;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace IntelliFin.IdentityService.Tests.Unit.Logging;

public class LoggingTests
{
    [Fact]
    public void Logging_OutputsStructuredJson()
    {
        var sink = new InMemorySink();
        var logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Information("Structured log test");

        Assert.Single(sink.Events);
        var entry = sink.Events.Single();

        using var json = JsonDocument.Parse(entry);
        Assert.Equal("Information", json.RootElement.GetProperty("Level").GetString());
    }

    [Fact]
    public void Logging_IncludesCorrelationId()
    {
        var sink = new InMemorySink();
        var logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Sink(sink)
            .CreateLogger();

        var correlationId = Guid.NewGuid().ToString();

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            logger.Information("Correlation ID test");
        }

        var entry = sink.Events.Single();
        using var json = JsonDocument.Parse(entry);
        Assert.Equal(correlationId, json.RootElement.GetProperty("CorrelationId").GetString());
    }

    private sealed class InMemorySink : ILogEventSink
    {
        public List<string> Events { get; } = new();

        public void Emit(LogEvent logEvent)
        {
            using var writer = new StringWriter();
            new JsonFormatter().Format(logEvent, writer);
            Events.Add(writer.ToString());
        }
    }
}
