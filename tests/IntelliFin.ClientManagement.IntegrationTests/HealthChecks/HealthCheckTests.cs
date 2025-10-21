using IntelliFin.ClientManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using System.Net;
using Testcontainers.MsSql;

namespace IntelliFin.ClientManagement.IntegrationTests.HealthChecks;

/// <summary>
/// Integration tests for health check endpoints
/// </summary>
public class HealthCheckTests : IAsyncLifetime
{
    private readonly MsSqlContainer _msSqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("YourStrong!Passw0rd")
        .Build();

    private IHost? _host;

    public async Task InitializeAsync()
    {
        await _msSqlContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        await _msSqlContainer.DisposeAsync();
    }

    [Fact]
    public async Task HealthCheck_Database_Should_Return_Healthy_When_Connected()
    {
        // Arrange
        var connectionString = _msSqlContainer.GetConnectionString();
        _host = await CreateTestHost(connectionString);

        // Act
        var client = _host.GetTestClient();
        var response = await client.GetAsync("/health/db");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Healthy");
    }

    [Fact]
    public async Task HealthCheck_Database_Should_Return_Unhealthy_When_Disconnected()
    {
        // Arrange - Use invalid connection string
        var invalidConnectionString = "Server=localhost,9999;Database=Invalid;User Id=sa;Password=Invalid;TrustServerCertificate=true;Connection Timeout=2;";
        _host = await CreateTestHost(invalidConnectionString);

        // Act
        var client = _host.GetTestClient();
        var response = await client.GetAsync("/health/db");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task HealthCheck_General_Should_Return_OK()
    {
        // Arrange
        var connectionString = _msSqlContainer.GetConnectionString();
        _host = await CreateTestHost(connectionString);

        // Act
        var client = _host.GetTestClient();
        var response = await client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<IHost> CreateTestHost(string connectionString)
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddDbContext<ClientManagementDbContext>(options =>
                    {
                        options.UseSqlServer(connectionString);
                    });

                    services.AddHealthChecks()
                        .AddSqlServer(
                            connectionString: connectionString,
                            healthQuery: "SELECT 1",
                            name: "db",
                            failureStatus: HealthStatus.Unhealthy,
                            tags: new[] { "database", "sql" }
                        );
                });

                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapHealthChecks("/health");
                        endpoints.MapHealthChecks("/health/db", new HealthCheckOptions
                        {
                            Predicate = check => check.Tags.Contains("database")
                        });
                    });
                });
            });

        var host = await hostBuilder.StartAsync();
        
        // Apply migrations
        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClientManagementDbContext>();
        await dbContext.Database.MigrateAsync();
        
        return host;
    }
}
