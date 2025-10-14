using System.Net;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Specialized;
using IntelliFin.AdminService.Options;
using IntelliFin.AdminService.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IntelliFin.Tests.Unit.AdminService;

public class CamundaTokenProviderTests
{
    [Fact]
    public async Task GetAccessTokenAsync_CachesToken_UntilBufferExpires()
    {
        var handler = new CountingHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { access_token = "token-1", expires_in = 2 }))
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://keycloak.test/token") };
        var options = CreateOptions(bufferSeconds: 1);
        var provider = new CamundaTokenProvider(httpClient, options, NullLogger<CamundaTokenProvider>.Instance);

        var first = await provider.GetAccessTokenAsync(CancellationToken.None);
        var second = await provider.GetAccessTokenAsync(CancellationToken.None);

        first.Should().Be("token-1");
        second.Should().Be("token-1");
        handler.RequestCount.Should().Be(1);

        await Task.Delay(TimeSpan.FromMilliseconds(1500));

        handler.ResponseFactory = () => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { access_token = "token-2", expires_in = 120 }))
        };

        var refreshed = await provider.GetAccessTokenAsync(CancellationToken.None);
        refreshed.Should().Be("token-2");
        handler.RequestCount.Should().Be(2);
    }

    [Fact]
    public async Task GetAccessTokenAsync_Throws_OnFailure()
    {
        var handler = new CountingHandler(() => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"error\":\"invalid_client\"}")
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://keycloak.test/token") };
        var options = CreateOptions();
        var provider = new CamundaTokenProvider(httpClient, options, NullLogger<CamundaTokenProvider>.Instance);

        await FluentActions.Invoking(() => provider.GetAccessTokenAsync(CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Failed to acquire Camunda token*");

        handler.RequestCount.Should().Be(1);
    }

    private static IOptionsMonitor<CamundaOptions> CreateOptions(int bufferSeconds = 60)
    {
        var options = Options.Create(new CamundaOptions
        {
            ClientId = "admin-service",
            ClientSecret = "secret",
            BaseUrl = "https://camunda.test/engine-rest/",
            TokenEndpoint = "https://keycloak.test/token",
            TokenRefreshBufferSeconds = bufferSeconds
        });

        return new StaticOptionsMonitor(options.Value);
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public Func<HttpResponseMessage> ResponseFactory;
        public int RequestCount { get; private set; }

        public CountingHandler(Func<HttpResponseMessage> responseFactory)
        {
            ResponseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(ResponseFactory());
        }
    }

    private sealed class StaticOptionsMonitor : IOptionsMonitor<CamundaOptions>
    {
        private CamundaOptions _currentValue;

        public StaticOptionsMonitor(CamundaOptions options)
        {
            _currentValue = options;
        }

        public CamundaOptions CurrentValue => _currentValue;

        public CamundaOptions Get(string? name) => _currentValue;

        public IDisposable? OnChange(Action<CamundaOptions, string> listener) => null;
    }
}
