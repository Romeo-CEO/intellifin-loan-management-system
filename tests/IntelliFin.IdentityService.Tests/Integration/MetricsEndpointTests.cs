using IntelliFin.IdentityService.Tests.Common;
using System.Net;

namespace IntelliFin.IdentityService.Tests.Integration;

public class MetricsEndpointTests : IClassFixture<IdentityServiceWebApplicationFactory>
{
    private readonly IdentityServiceWebApplicationFactory _factory;

    public MetricsEndpointTests(IdentityServiceWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MetricsEndpoint_ReturnsPrometheusFormat()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/metrics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task MetricsEndpoint_ExposesServiceSpecificMetrics()
    {
        var client = _factory.CreateClient();

        // Trigger a request to ensure counters increment.
        await client.GetAsync("/");

        var response = await client.GetAsync("/metrics");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Contains("identity_service_startup_total", payload);
        Assert.Contains("identity_service_requests_total", payload);
    }
}
