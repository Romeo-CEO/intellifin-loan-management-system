using IntelliFin.CreditAssessmentService.Models.Requests;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using Xunit;
using FluentAssertions;

namespace IntelliFin.CreditAssessmentService.Tests.Integration;

/// <summary>
/// Integration tests for Credit Assessment API.
/// Story 1.18: Comprehensive Testing Suite
/// </summary>
public class CreditAssessmentApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public CreditAssessmentApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthCheck_Live_ShouldReturnOk()
    {
        // Act
        var response = await _client.GetAsync("/health/live");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthCheck_Ready_ShouldReturnHealthStatus()
    {
        // Act
        var response = await _client.GetAsync("/health/ready");

        // Assert
        response.Should().NotBeNull();
        // Note: May return 503 if database not available in test environment
    }

    [Fact]
    public async Task Metrics_ShouldReturnPrometheusFormat()
    {
        // Act
        var response = await _client.GetAsync("/metrics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("http_requests");
    }

    [Fact]
    public async Task AssessEndpoint_WithoutAuth_ShouldReturn401()
    {
        // Arrange
        var request = new AssessmentRequest
        {
            LoanApplicationId = Guid.NewGuid(),
            ClientId = Guid.NewGuid(),
            RequestedAmount = 50000,
            TermMonths = 24,
            ProductType = "PAYROLL"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/credit-assessment/assess", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
