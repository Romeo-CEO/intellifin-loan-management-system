# Story 1.6: OpenTelemetry Shared Library and Instrumentation Bootstrap

### Metadata
- **ID**: 1.6 | **Points**: 8 | **Effort**: 5-7 days | **Priority**: P0
- **Dependencies**: None (new shared library)
- **Blocks**: 1.7, 1.8, 1.9, 1.17

### User Story
**As a** developer,  
**I want** OpenTelemetry SDK integrated into all microservices with basic instrumentation,  
**so that** we have distributed tracing, metrics, and logging foundation for observability.

### Acceptance Criteria
1. `IntelliFin.Shared.Observability` library created
2. `AddOpenTelemetryInstrumentation()` extension method configures OTLP exporters
3. Automatic instrumentation: ASP.NET Core HTTP, HttpClient, Entity Framework
4. W3C Trace Context propagation (HTTP headers + RabbitMQ properties)
5. All services updated with `AddOpenTelemetryInstrumentation()` in `Program.cs`
6. Service name, version, environment tagged in telemetry
7. Adaptive trace sampling (100% errors, 10% normal)

### Implementation
```csharp
// IntelliFin.Shared.Observability/OpenTelemetryExtensions.cs
public static IServiceCollection AddOpenTelemetryInstrumentation(
    this IServiceCollection services,
    IConfiguration configuration)
{
    var serviceName = configuration["ServiceName"] ?? "Unknown";
    var serviceVersion = configuration["ServiceVersion"] ?? "1.0.0";
    var otlpEndpoint = configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://otel-collector:4317";

    services.AddOpenTelemetry()
        .ConfigureResource(resource => resource
            .AddService(serviceName, serviceVersion: serviceVersion)
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = configuration["Environment"] ?? "Production",
                ["telemetry.sdk.name"] = "opentelemetry",
                ["telemetry.sdk.language"] = "dotnet"
            }))
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
                options.Filter = (httpContext) => !httpContext.Request.Path.StartsWithSegments("/health");
            })
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation(options =>
            {
                options.SetDbStatementForText = true;
                options.EnrichWithIDbCommand = (activity, command) =>
                {
                    activity.SetTag("db.connection_string", command.Connection.ConnectionString);
                };
            })
            .AddSource("IntelliFin.*")
            .SetSampler(new AdaptiveSampler())
            .AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint)))
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddProcessInstrumentation()
            .AddMeter("IntelliFin.*")
            .AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint)));

    return services;
}

// Adaptive Sampler
public class AdaptiveSampler : Sampler
{
    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        // Always sample errors
        if (samplingParameters.Tags.Any(t => t.Key == "error" && (bool)t.Value))
            return new SamplingResult(SamplingDecision.RecordAndSample);

        // Sample 10% of normal requests
        return new SamplingResult(
            Random.Shared.NextDouble() < 0.1
                ? SamplingDecision.RecordAndSample
                : SamplingDecision.Drop);
    }
}
```

### Implementation Notes

- The shared extension enforces W3C trace propagation for both HTTP headers and RabbitMQ message properties by registering a composite propagator that includes a custom `RabbitMqPropagator` adapter.
- Database command enrichment masks sensitive connection string fields before adding them to spans to avoid leaking credentials while still providing connection metadata for troubleshooting.
- Service metadata (`ServiceName`, `ServiceVersion`, `Environment`) is sourced from configuration with sensible fallbacks to assembly information and environment variables so telemetry remains tagged even when settings are omitted.
- Default OTLP endpoints (`http://otel-collector:4317`) are included in each microservice `appsettings.json`, allowing operators to override the collector target through configuration without code changes.
- All ASP.NET Core services reference the new shared library and call `AddOpenTelemetryInstrumentation(builder.Configuration)` during startup, ensuring consistent tracing and metrics registration across the fleet.

### Integration Verification
- **IV1**: Existing service functionality unaffected
- **IV2**: Correlation IDs automatically generated and propagated
- **IV3**: Performance overhead <5% (within NFR16 tolerance)
