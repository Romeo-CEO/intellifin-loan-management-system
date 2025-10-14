using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Specialized;
using IntelliFin.AdminService.ExceptionHandling;
using IntelliFin.AdminService.Models;
using IntelliFin.AdminService.Options;
using IntelliFin.AdminService.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IntelliFin.Tests.Unit.AdminService;

public class CamundaWorkflowServiceTests
{
    [Fact]
    public async Task StartElevationWorkflowAsync_ReturnsNull_WhenFailOpenEnabled()
    {
        var handler = new StubHandler(_ => throw new InvalidOperationException("Camunda should not be invoked"));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://camunda.test/engine-rest/") };
        var options = Options.Create(new CamundaOptions { FailOpen = true, BaseUrl = "https://camunda.test/engine-rest/" });
        var incidentOptions = Options.Create(new IncidentResponseOptions());
        var service = new CamundaWorkflowService(httpClient, new StaticOptionsMonitor(options.Value), new StaticOptionsMonitor(incidentOptions.Value), NullLogger<CamundaWorkflowService>.Instance);

        var result = await service.StartElevationWorkflowAsync(new ElevationRequest { ElevationId = Guid.NewGuid(), UserId = "user", UserName = "user", ManagerId = "manager", Justification = "Need access", RequestedDuration = 30 }, new[] { "Role" }, CancellationToken.None);

        result.Should().BeNull();
        handler.InvocationCount.Should().Be(0);
    }

    [Fact]
    public async Task StartElevationWorkflowAsync_ThrowsCamundaWorkflowException_OnFailure()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = JsonContent.Create(new { error = "failed" })
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://camunda.test/engine-rest/") };
        var options = Options.Create(new CamundaOptions
        {
            BaseUrl = "https://camunda.test/engine-rest/",
            FailOpen = false
        });

        var incidentOptions = Options.Create(new IncidentResponseOptions());
        var service = new CamundaWorkflowService(httpClient, new StaticOptionsMonitor(options.Value), new StaticOptionsMonitor(incidentOptions.Value), NullLogger<CamundaWorkflowService>.Instance);

        await FluentActions.Invoking(() => service.StartElevationWorkflowAsync(new ElevationRequest
            {
                ElevationId = Guid.NewGuid(),
                UserId = "user",
                UserName = "user",
                ManagerId = "manager",
                Justification = "Need access",
                RequestedDuration = 15
            },
            new[] { "Role" },
            CancellationToken.None))
            .Should().ThrowAsync<CamundaWorkflowException>()
            .Where(ex => ex.StatusCode == HttpStatusCode.BadGateway);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _factory;

        public int InvocationCount { get; private set; }

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> factory)
        {
            _factory = factory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            InvocationCount++;
            return Task.FromResult(_factory(request));
        }
    }

    private sealed class StaticOptionsMonitor<TOptions> : IOptionsMonitor<TOptions> where TOptions : class, new()
    {
        private readonly TOptions _value;

        public StaticOptionsMonitor(TOptions value)
        {
            _value = value;
        }

        public TOptions CurrentValue => _value;

        public TOptions Get(string? name) => _value;

        public IDisposable? OnChange(Action<TOptions, string> listener) => null;
    }
}
