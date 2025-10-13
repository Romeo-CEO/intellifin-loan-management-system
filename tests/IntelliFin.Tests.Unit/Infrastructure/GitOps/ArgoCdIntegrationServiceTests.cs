using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using IntelliFin.AdminService.Options;
using IntelliFin.AdminService.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IntelliFin.Tests.Unit.Infrastructure.GitOps;

public class ArgoCdIntegrationServiceTests
{
    [Fact]
    public async Task GetApplicationsAsync_MapsResponsePayload()
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            request.Method.Should().Be(HttpMethod.Get);
            request.RequestUri.Should().NotBeNull();
            request.Headers.Authorization.Should().NotBeNull();
            request.Headers.Authorization!.Parameter.Should().Be("token-value");

            var payload = new
            {
                items = new[]
                {
                    new
                    {
                        metadata = new { name = "identity-service", @namespace = "argocd" },
                        spec = new
                        {
                            project = "intellifin-production",
                            destination = new { server = "https://kubernetes.default.svc" }
                        },
                        status = new
                        {
                            sync = new { status = "Synced", revision = "abc123" },
                            health = new { status = "Healthy" },
                            operationState = new { finishedAt = DateTimeOffset.Parse("2025-01-02T03:04:05Z") }
                        }
                    }
                }
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient);

        var result = await service.GetApplicationsAsync(CancellationToken.None);

        result.Should().ContainSingle();
        var application = result.Single();
        application.Name.Should().Be("identity-service");
        application.Project.Should().Be("intellifin-production");
        application.SyncStatus.Should().Be("Synced");
        application.HealthStatus.Should().Be("Healthy");
        application.Revision.Should().Be("abc123");
        application.LastSyncedAt.Should().Be(DateTimeOffset.Parse("2025-01-02T03:04:05Z"));
    }

    [Fact]
    public async Task TriggerSyncAsync_SendsPostRequest()
    {
        HttpRequestMessage? captured = null;
        var handler = new FakeHttpMessageHandler(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient);

        await service.TriggerSyncAsync("identity-service", new ArgoCdSyncRequestParameters(true, false, 3), CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.AbsolutePath.Should().Contain("/api/v1/applications/identity-service/sync");
        var body = await captured.Content!.ReadAsStringAsync();
        body.Should().Contain("\"prune\":true");
        body.Should().Contain("\"limit\":3");
    }

    private static IArgoCdIntegrationService CreateService(HttpClient httpClient)
    {
        var options = new TestOptionsMonitor<ArgoCdOptions>(new ArgoCdOptions
        {
            Url = "https://argocd.example.com",
            Token = "token-value",
            TimeoutSeconds = 45
        });

        return new ArgoCdIntegrationService(httpClient, options, NullLogger<ArgoCdIntegrationService>.Instance);
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request));
    }

    private sealed class TestOptionsMonitor<T> : IOptionsMonitor<T> where T : class, new()
    {
        private T _currentValue;

        public TestOptionsMonitor(T value)
        {
            _currentValue = value;
        }

        public T CurrentValue => _currentValue;

        public T Get(string? name) => _currentValue;

        public IDisposable OnChange(Action<T, string?> listener)
        {
            return new NoopDisposable();
        }

        private sealed class NoopDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
