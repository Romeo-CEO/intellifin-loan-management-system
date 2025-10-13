using System;
using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace IntelliFin.Shared.Observability;

public sealed class TraceContextEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (logEvent is null)
        {
            throw new ArgumentNullException(nameof(logEvent));
        }

        if (propertyFactory is null)
        {
            throw new ArgumentNullException(nameof(propertyFactory));
        }

        var activity = Activity.Current;
        if (activity is null || activity.TraceId == default)
        {
            return;
        }

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceId", activity.TraceId.ToString()));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SpanId", activity.SpanId.ToString()));

        if (activity.ParentSpanId != default)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ParentSpanId", activity.ParentSpanId.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(activity.TraceStateString))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceState", activity.TraceStateString));
        }
    }
}
