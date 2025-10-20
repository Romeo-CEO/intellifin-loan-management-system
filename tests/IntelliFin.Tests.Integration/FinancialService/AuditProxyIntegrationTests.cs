using System.Net;
using System.Net.Http.Json;
using IntelliFin.FinancialService.Clients;
using IntelliFin.FinancialService.Models.Audit;
using IntelliFin.Tests.Integration.FinancialService.Stubs;

namespace IntelliFin.Tests.Integration.FinancialService;

public class AuditProxyIntegrationTests
{
    private readonly TestAdminAuditClient _adminAuditClient = new();

    [Fact]
    public async Task GetEvents_ProxiesToAdminService()
    {
        using var factory = new FinancialServiceWebApplicationFactory(adminAuditClient: _adminAuditClient);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/audit/events?page=1&pageSize=10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<AuditEventPageResponse>();
        payload.Should().NotBeNull();
        payload!.Data.Should().HaveCount(1);
        payload.Data[0].Action.Should().Be("CollectionsPaymentRecorded");
    }

    [Fact]
    public async Task GetEvents_WhenAdminUnavailable_Returns503()
    {
        _adminAuditClient.ShouldThrow = true;
        using var factory = new FinancialServiceWebApplicationFactory(adminAuditClient: _adminAuditClient);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/audit/events");
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }
}
