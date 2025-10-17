using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace IntelliFin.Shared.Observability;

public static class OpenTelemetryExtensions
{
    private static readonly TextMapPropagator CompositePropagator = new CompositeTextMapPropagator(
        new TextMapPropagator[]
        {
            new TraceContextPropagator(),
            new BaggagePropagator(),
            new RabbitMqPropagator()
        });

    public static IServiceCollection AddOpenTelemetryInstrumentation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var entryAssembly = Assembly.GetEntryAssembly();
        var serviceName = configuration["ServiceName"] ?? entryAssembly?.GetName().Name ?? "Unknown";
        var serviceVersion = configuration["ServiceVersion"]
                             ?? entryAssembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                             ?? entryAssembly?.GetName().Version?.ToString()
                             ?? "1.0.0";
        var environment = configuration["Environment"]
                          ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                          ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                          ?? "Production";
        var otlpEndpoint = configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://otel-collector:4317";

        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;
        Sdk.SetDefaultTextMapPropagator(CompositePropagator);

        services.AddOptions<LoggerFactoryOptions>()
            .Configure(options =>
            {
                options.ActivityTrackingOptions = ActivityTrackingOptions.TraceId
                    | ActivityTrackingOptions.SpanId
                    | ActivityTrackingOptions.ParentId
                    | ActivityTrackingOptions.Baggage;
            });

        services.AddOpenTelemetry()
            .ConfigureResource(resourceBuilder => resourceBuilder
                .AddService(serviceName, serviceVersion: serviceVersion)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = environment,
                    ["service.instance.id"] = Environment.MachineName,
                    ["telemetry.sdk.name"] = "opentelemetry",
                    ["telemetry.sdk.language"] = "dotnet"
                }))
            .WithTracing(tracerProviderBuilder => tracerProviderBuilder
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.RecordException = true;
                    options.Filter = httpContext =>
                        !httpContext.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);
                })
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation(options =>
                {
                    options.SetDbStatementForText = true;
                    options.EnrichWithIDbCommand = (activity, command) =>
                    {
                        if (command.Connection is null)
                        {
                            return;
                        }

                        activity.SetTag("db.system", command.Connection.GetType().Name);
                        activity.SetTag("db.connection_string", SanitizeConnectionString(command.Connection.ConnectionString));
                    };
                })
                .AddSource("IntelliFin.*")
                .SetSampler(new AdaptiveSampler())
                .AddOtlpExporter(exporterOptions =>
                {
                    exporterOptions.Endpoint = new Uri(otlpEndpoint);
                }))
            .WithMetrics(meterProviderBuilder => meterProviderBuilder
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddProcessInstrumentation()
                .AddMeter("IntelliFin.*")
                .AddOtlpExporter(exporterOptions =>
                {
                    exporterOptions.Endpoint = new Uri(otlpEndpoint);
                }));

        return services;
    }

    private static string SanitizeConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return string.Empty;
        }

        try
        {
            var builder = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };

            var keysToScrub = new[] { "Password", "Pwd", "User ID", "UserID", "Uid" };
            foreach (var key in keysToScrub)
            {
                if (builder.ContainsKey(key))
                {
                    builder[key] = "***";
                }
            }

            return builder.ConnectionString;
        }
        catch (ArgumentException)
        {
            return connectionString;
        }
    }
}

public sealed class AdaptiveSampler : Sampler
{
    public new string Description => "IntelliFinAdaptiveSampler";

    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        if (samplingParameters.ParentContext.TraceFlags.HasFlag(ActivityTraceFlags.Recorded))
        {
            return new SamplingResult(SamplingDecision.RecordAndSample);
        }

        if (samplingParameters.Tags is not null)
        {
            foreach (var tag in samplingParameters.Tags)
            {
                if (IsErrorTag(tag))
                {
                    return new SamplingResult(SamplingDecision.RecordAndSample);
                }
            }
        }

        return Random.Shared.NextDouble() < 0.1
            ? new SamplingResult(SamplingDecision.RecordAndSample)
            : new SamplingResult(SamplingDecision.Drop);
    }

    private static bool IsErrorTag(KeyValuePair<string, object?> tag)
    {
        if (string.Equals(tag.Key, "error", StringComparison.OrdinalIgnoreCase))
        {
            return tag.Value switch
            {
                bool boolValue => boolValue,
                string stringValue when bool.TryParse(stringValue, out var parsed) => parsed,
                _ => false
            };
        }

        if (string.Equals(tag.Key, "otel.status_code", StringComparison.OrdinalIgnoreCase))
        {
            return tag.Value is string status && string.Equals(status, "ERROR", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}

public sealed class RabbitMqPropagator : TextMapPropagator
{
    private static readonly TextMapPropagator TraceContext = new TraceContextPropagator();
    private static readonly TextMapPropagator Baggage = new BaggagePropagator();
    private static readonly ISet<string> PropagatorFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "traceparent",
        "tracestate",
        "baggage"
    };

    public override ISet<string> Fields => PropagatorFields;

    public override PropagationContext Extract<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>?> getter)
    {
        ArgumentNullException.ThrowIfNull(getter);

        var effectiveGetter = WrapGetter(carrier, getter);
        var propagationContext = TraceContext.Extract(context, carrier, effectiveGetter);
        propagationContext = Baggage.Extract(propagationContext, carrier, effectiveGetter);
        return propagationContext;
    }

    public override void Inject<T>(PropagationContext context, T carrier, Action<T, string, string> setter)
    {
        ArgumentNullException.ThrowIfNull(setter);

        var effectiveSetter = WrapSetter(carrier, setter);
        TraceContext.Inject(context, carrier, effectiveSetter);
        Baggage.Inject(context, carrier, effectiveSetter);
    }

    private static Func<T, string, IEnumerable<string>> WrapGetter<T>(T carrier, Func<T, string, IEnumerable<string>> getter)
    {
        if (carrier is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            return (_, key) => TryReadValues(readOnlyDictionary, key);
        }

        if (carrier is IDictionary dictionary)
        {
            return (_, key) => TryReadValues(dictionary, key);
        }

        return getter;
    }

    private static Action<T, string, string> WrapSetter<T>(T carrier, Action<T, string, string> setter)
    {
        if (carrier is IDictionary dictionary)
        {
            return (_, key, value) => dictionary[key] = value;
        }

        return setter;
    }

    private static IEnumerable<string> TryReadValues(IDictionary dictionary, string key)
    {
        foreach (DictionaryEntry entry in dictionary)
        {
            if (entry.Key is not string headerKey)
            {
                continue;
            }

            if (!string.Equals(headerKey, key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return ConvertValue(entry.Value);
        }

        return Array.Empty<string>();
    }

    private static IEnumerable<string> TryReadValues(IReadOnlyDictionary<string, object?> dictionary, string key)
    {
        if (dictionary.TryGetValue(key, out var value) && value is not null)
        {
            return ConvertValue(value);
        }

        foreach (var pair in dictionary)
        {
            if (!string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return ConvertValue(pair.Value);
        }

        return Array.Empty<string>();
    }

    private static IEnumerable<string> ConvertValue(object? value)
    {
        if (value is null)
        {
            return Array.Empty<string>();
        }

        if (value is string stringValue)
        {
            return new[] { stringValue };
        }

        if (value is byte[] byteValue)
        {
            return new[] { Encoding.UTF8.GetString(byteValue) };
        }

        if (value is IEnumerable enumerable)
        {
            var results = new List<string>();
            foreach (var item in enumerable)
            {
                if (item is string str)
                {
                    results.Add(str);
                }
                else if (item is byte[] bytes)
                {
                    results.Add(Encoding.UTF8.GetString(bytes));
                }
            }

            if (results.Count > 0)
            {
                return results;
            }
        }

        return Array.Empty<string>();
    }
}
