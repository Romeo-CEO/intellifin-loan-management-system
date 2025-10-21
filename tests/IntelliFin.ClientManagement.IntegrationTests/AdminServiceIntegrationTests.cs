using IntelliFin.ClientManagement.Controllers.DTOs;
using IntelliFin.ClientManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Testcontainers.MsSql;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace IntelliFin.ClientManagement.IntegrationTests;

public class AdminServiceIntegrationTests : IAsyncLifetime
{
    private const string TestSecretKey = "test-secret-key-at-least-32-characters-long-for-testing-purposes";

    private readonly MsSqlContainer _msSqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("YourStrong!Passw0rd")
        .Build();

    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private string? _authToken;
    private WireMockServer? _wireMock;

    public async Task InitializeAsync()
    {
        await _msSqlContainer.StartAsync();

        _wireMock = WireMockServer.Start(port: 5001);
        _wireMock.Given(Request.Create().WithPath("/api/admin/audit/events/batch").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(202));
        _wireMock.Given(Request.Create().WithPath("/health/ready").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200));

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Override DbContext for tests
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<ClientManagementDbContext>));
                    if (descriptor != null)
                        services.Remove(descriptor);

                    services.AddDbContext<ClientManagementDbContext>(options =>
                    {
                        options.UseSqlServer(_msSqlContainer.GetConnectionString());
                    });

                    // Override authentication configuration
                    services.PostConfigure<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>(
                        Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme,
                        options =>
                        {
                            options.TokenValidationParameters = new TokenValidationParameters
                            {
                                ValidateIssuer = true,
                                ValidateAudience = true,
                                ValidateLifetime = true,
                                ValidateIssuerSigningKey = true,
                                ValidIssuer = "test-issuer",
                                ValidAudience = "test-audience",
                                IssuerSigningKey = new SymmetricSecurityKey(
                                    Encoding.UTF8.GetBytes(TestSecretKey))
                            };
                        });
                });
            });

        // Apply migrations
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ClientManagementDbContext>();
        await context.Database.MigrateAsync();

        _client = _factory.CreateClient();
        _authToken = GenerateJwtToken("test-user");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory != null)
            await _factory.DisposeAsync();
        await _msSqlContainer.DisposeAsync();
        _wireMock?.Stop();
    }

    [Fact]
    public async Task CreateClient_Should_Enqueue_AuditEvent_And_Batch_Send()
    {
        var request = CreateValidClientRequest();
        var response = await _client!.PostAsJsonAsync("/api/clients", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // allow batching service to tick (flush interval is 5s; wait a bit more)
        await Task.Delay(TimeSpan.FromSeconds(6));

        var calls = _wireMock!.LogEntries.Count(l => l.RequestMessage.Path == "/api/admin/audit/events/batch" && l.RequestMessage.Method == "POST");
        calls.Should().BeGreaterThan(0);
    }

    private static CreateClientRequest CreateValidClientRequest(string nrc = "123456/78/9")
    {
        return new CreateClientRequest
        {
            Nrc = nrc,
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
            Gender = "M",
            MaritalStatus = "Single",
            Nationality = "Zambian",
            PrimaryPhone = "+260977123456",
            PhysicalAddress = "123 Main Street, Woodlands",
            City = "Lusaka",
            Province = "Lusaka",
            BranchId = Guid.NewGuid()
        };
    }

    private static string GenerateJwtToken(string username)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSecretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "kyc-officer")
        };

        var token = new JwtSecurityToken(
            issuer: "test-issuer",
            audience: "test-audience",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
