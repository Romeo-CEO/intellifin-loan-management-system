using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;

namespace IntelliFin.ApiGateway.Middleware;

public sealed class TraceContextMiddleware
{
    private const string TraceParentHeader = "traceparent";
    private const string TraceStateHeader = "tracestate";

    private readonly RequestDelegate _next;
    private readonly ILogger<TraceContextMiddleware> _logger;

    public TraceContextMiddleware(RequestDelegate next, ILogger<TraceContextMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var incomingTraceParent = context.Request.Headers[TraceParentHeader].FirstOrDefault();
        var httpActivityFeature = context.Features.Get<IHttpActivityFeature>();

        context.Response.OnStarting(() =>
        {
            var activity = Activity.Current ?? httpActivityFeature?.Activity;
            var traceParent = incomingTraceParent;

            if (string.IsNullOrWhiteSpace(traceParent))
            {
                if (activity is not null)
                {
                    traceParent = BuildTraceParent(activity);
                }
                else
                {
                    var traceId = ActivityTraceId.CreateRandom();
                    var spanId = ActivitySpanId.CreateRandom();
                    traceParent = $"00-{traceId}-{spanId}-01";
                }

                _logger.LogDebug("Generated traceparent header for request {Method} {Path}: {TraceParent}",
                    context.Request.Method,
                    context.Request.Path,
                    traceParent);
            }

            if (!context.Response.Headers.ContainsKey(TraceParentHeader) && !string.IsNullOrWhiteSpace(traceParent))
            {
                context.Response.Headers[TraceParentHeader] = traceParent;
            }

            if (activity is not null
                && !string.IsNullOrWhiteSpace(activity.TraceStateString)
                && !context.Response.Headers.ContainsKey(TraceStateHeader))
            {
                context.Response.Headers[TraceStateHeader] = activity.TraceStateString;
            }

            return Task.CompletedTask;
        });

        await _next(context);
    }

    private static string BuildTraceParent(Activity activity)
    {
        var traceId = activity.TraceId.ToString();
        var spanId = activity.SpanId.ToString();
        var traceFlags = activity.Recorded ? "01" : "00";
        return $"00-{traceId}-{spanId}-{traceFlags}";
    }
}
