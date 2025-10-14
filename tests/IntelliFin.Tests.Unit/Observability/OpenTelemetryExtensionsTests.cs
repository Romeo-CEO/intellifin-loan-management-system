using System.Collections.Generic;
using IntelliFin.Shared.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Logs;

namespace IntelliFin.Tests.Unit.Observability;

public class OpenTelemetryExtensionsTests
{
    [Fact]
    public void AddOpenTelemetryInstrumentation_ConfiguresLoggingPipeline()
    {
        var configurationValues = new Dictionary<string, string?>
        {
            ["ServiceName"] = "IntelliFin.Test",
            ["ServiceVersion"] = "1.0-test",
            ["Environment"] = "Test",
            ["OpenTelemetry:OtlpEndpoint"] = "http://collector:4317",
            ["OpenTelemetry:Logs:OtlpEndpoint"] = "http://collector:4317",
            ["OpenTelemetry:Logs:Headers:X-Scope-OrgID"] = "test-tenant"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        services.AddOpenTelemetryInstrumentation(configuration);

        using var provider = services.BuildServiceProvider();

        var loggerOptions = provider.GetRequiredService<IOptions<LoggerFactoryOptions>>().Value;
        loggerOptions.ActivityTrackingOptions.HasFlag(ActivityTrackingOptions.TraceId).Should().BeTrue();
        loggerOptions.ActivityTrackingOptions.HasFlag(ActivityTrackingOptions.SpanId).Should().BeTrue();

        var otelOptions = provider.GetRequiredService<IOptionsMonitor<OpenTelemetryLoggerOptions>>().Get(string.Empty);
        otelOptions.IncludeScopes.Should().BeTrue();
        otelOptions.IncludeFormattedMessage.Should().BeTrue();
        otelOptions.ParseStateValues.Should().BeTrue();
    }
}
