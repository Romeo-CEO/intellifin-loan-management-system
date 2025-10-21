using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Text.Json;

namespace IntelliFin.ClientManagement.IntegrationTests.Middleware;

/// <summary>
/// Integration tests for GlobalExceptionHandlerMiddleware
/// </summary>
public class GlobalExceptionHandlerTests : IAsyncLifetime
{
    private IHost? _host;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }

    [Fact]
    public async Task UnhandledException_Should_Return500WithErrorResponse()
    {
        // Arrange
        _host = await CreateTestHostWithExceptionEndpoint();
        var client = _host.GetTestClient();

        // Act
        var response = await client.GetAsync("/test/throw");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        
        var content = await response.Content.ReadAsStringAsync();
        var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        errorResponse.Should().NotBeNull();
        errorResponse!.Error.Should().NotBeNullOrEmpty();
        errorResponse.CorrelationId.Should().NotBeNullOrEmpty();
        errorResponse.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        errorResponse.Path.Should().Be("/test/throw");
    }

    [Fact]
    public async Task ArgumentException_Should_Return400BadRequest()
    {
        // Arrange
        _host = await CreateTestHostWithExceptionEndpoint(throwArgumentException: true);
        var client = _host.GetTestClient();

        // Act
        var response = await client.GetAsync("/test/throw");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("error");
        content.Should().Contain("correlationId");
    }

    private async Task<IHost> CreateTestHostWithExceptionEndpoint(bool throwArgumentException = false)
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddHttpContextAccessor();
                    services.AddRouting();
                });

                webHost.Configure(app =>
                {
                    app.UseMiddleware<IntelliFin.ClientManagement.Middleware.CorrelationIdMiddleware>();
                    app.UseMiddleware<IntelliFin.ClientManagement.Middleware.GlobalExceptionHandlerMiddleware>();
                    
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/test/throw", () =>
                        {
                            if (throwArgumentException)
                                throw new ArgumentException("Test argument exception");
                            throw new Exception("Test exception");
                        });
                    });
                });
            });

        return await hostBuilder.StartAsync();
    }

    private class ErrorResponse
    {
        public string Error { get; set; } = string.Empty;
        public string CorrelationId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Path { get; set; } = string.Empty;
        public string? Details { get; set; }
    }
}
