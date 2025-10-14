using IntelliFin.IdentityService.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Net;

namespace IntelliFin.IdentityService.Tests.Integration;

public class HealthCheckTests : IClassFixture<IdentityServiceWebApplicationFactory>
{
    private readonly IdentityServiceWebApplicationFactory _factory;

    public HealthCheckTests(IdentityServiceWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LivenessEndpoint_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReadinessEndpoint_ReturnsOk_WhenHealthChecksAreHealthy()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddHealthChecks()
                    .AddCheck("test-ready", () => HealthCheckResult.Healthy(), new[] { "ready" });
            });
        });

        var client = factory.CreateClient();
        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ReadinessEndpoint_ReturnsServiceUnavailable_WhenHealthChecksFail()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddHealthChecks()
                    .AddCheck("test-ready", () => HealthCheckResult.Unhealthy(), new[] { "ready" });
            });
        });

        var client = factory.CreateClient();
        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
