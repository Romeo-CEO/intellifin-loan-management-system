using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace IntelliFin.Tests.Integration.ApiGateway;

public class ApiGatewayAuthenticationTests : IClassFixture<ApiGatewayWebApplicationFactory>
{
    private readonly ApiGatewayWebApplicationFactory _factory;

    public ApiGatewayAuthenticationTests(ApiGatewayWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LegacyToken_IsRejected()
    {
        _factory.Forwarder.Reset();
        using var client = _factory.CreateClient();
        var correlationId = Guid.NewGuid().ToString();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/ping");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.CreateLegacyToken());
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.Should().NotBeEmpty();
        _factory.Forwarder.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task KeycloakToken_Succeeds_And_Propagates_Correlation_Header()
    {
        _factory.Forwarder.Reset();
        using var client = _factory.CreateClient();
        var correlationId = Guid.NewGuid().ToString();

        var claims = new[]
        {
            new Claim("branchId", "123"),
            new Claim("branchName", "Head Office"),
            new Claim("branchRegion", "Lusaka")
        };

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/ping");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.CreateKeycloakToken(claims));
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("traceparent").Should().BeTrue();

        var forwarded = _factory.Forwarder.SingleRequest;
        forwarded.Should().NotBeNull();
        forwarded!.CorrelationId.Should().Be(correlationId);
        forwarded.BranchId.Should().Be("123");
        forwarded.BranchName.Should().Be("Head Office");
        forwarded.BranchRegion.Should().Be("Lusaka");
        forwarded.TokenType.Should().Be("Keycloak");
        forwarded.Headers.Keys.Should().Contain("Authorization");
        forwarded.Headers.Values.Should().Contain(headers => headers.Contains("Bearer"));
    }
}
