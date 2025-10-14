using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace IntelliFin.Tests.Integration.AdminService;

public class CamundaWorkflowIntegrationTests : IClassFixture<AdminServiceWebApplicationFactory>
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerOptions.Default) { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly AdminServiceWebApplicationFactory _factory;

    public CamundaWorkflowIntegrationTests(AdminServiceWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ElevationRequest_ReturnsBadGateway_WhenCamundaFails()
    {
        _factory.CamundaHandler.Configure((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("{\"error\":\"camunda_down\"}", Encoding.UTF8, MediaTypeNames.Application.Json)
        }));

        using var client = _factory.CreateClient();
        var requestBody = JsonSerializer.Serialize(new
        {
            justification = "Need admin access for investigation",
            duration = 30,
            requestedRoles = new[] { "Support" }
        }, SerializerOptions);

        var response = await client.PostAsync("/api/admin/access/elevate", new StringContent(requestBody, Encoding.UTF8, MediaTypeNames.Application.Json));

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("workflowType").GetString().Should().Be("access_elevation");
        problem.RootElement.GetProperty("camundaStatus").GetInt32().Should().Be((int)HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task ElevationRequest_Succeeds_WhenCamundaRespondsSuccessfully()
    {
        _factory.CamundaHandler.Configure((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"processInstanceId\":\"proc-123\"}", Encoding.UTF8, MediaTypeNames.Application.Json)
        }));

        using var client = _factory.CreateClient();
        var requestBody = JsonSerializer.Serialize(new
        {
            justification = "Need admin access for release",
            duration = 45,
            requestedRoles = new[] { "ReleaseManager" }
        }, SerializerOptions);

        var response = await client.PostAsync("/api/admin/access/elevate", new StringContent(requestBody, Encoding.UTF8, MediaTypeNames.Application.Json));

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("proc-123");
    }
}
