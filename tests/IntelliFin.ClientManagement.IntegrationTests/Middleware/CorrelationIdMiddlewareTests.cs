using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;

namespace IntelliFin.ClientManagement.IntegrationTests.Middleware;

/// <summary>
/// Integration tests for CorrelationIdMiddleware
/// </summary>
public class CorrelationIdMiddlewareTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public CorrelationIdMiddlewareTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Request_WithoutCorrelationId_Should_GenerateCorrelationId()
    {
        // Act
        var response = await _client.GetAsync("/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("X-Correlation-ID");
        
        var correlationId = response.Headers.GetValues("X-Correlation-ID").FirstOrDefault();
        correlationId.Should().NotBeNullOrEmpty();
        
        // Verify it's a valid GUID format
        Guid.TryParse(correlationId, out var guid).Should().BeTrue();
        guid.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Request_WithCorrelationId_Should_PreserveCorrelationId()
    {
        // Arrange
        var testCorrelationId = "test-correlation-123";
        _client.DefaultRequestHeaders.Add("X-Correlation-ID", testCorrelationId);

        // Act
        var response = await _client.GetAsync("/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Should().ContainKey("X-Correlation-ID");
        
        var correlationId = response.Headers.GetValues("X-Correlation-ID").FirstOrDefault();
        correlationId.Should().Be(testCorrelationId);
    }

    [Fact]
    public async Task MultipleRequests_Should_HaveDifferentCorrelationIds()
    {
        // Act
        var response1 = await _client.GetAsync("/");
        var response2 = await _client.GetAsync("/");

        // Assert
        var correlationId1 = response1.Headers.GetValues("X-Correlation-ID").FirstOrDefault();
        var correlationId2 = response2.Headers.GetValues("X-Correlation-ID").FirstOrDefault();

        correlationId1.Should().NotBeNullOrEmpty();
        correlationId2.Should().NotBeNullOrEmpty();
        correlationId1.Should().NotBe(correlationId2);
    }
}
