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

namespace IntelliFin.ClientManagement.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for client versioning API endpoints
/// </summary>
public class ClientVersioningControllerTests : IAsyncLifetime
{
    private const string TestSecretKey = "test-secret-key-at-least-32-characters-long-for-testing-purposes";
    private readonly MsSqlContainer _msSqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("YourStrong!Passw0rd")
        .Build();

    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        await _msSqlContainer.StartAsync();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<ClientManagementDbContext>));
                    if (descriptor != null)
                        services.Remove(descriptor);

                    services.AddDbContext<ClientManagementDbContext>(options =>
                    {
                        options.UseSqlServer(_msSqlContainer.GetConnectionString());
                    });

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

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ClientManagementDbContext>();
        await context.Database.MigrateAsync();

        _client = _factory.CreateClient();
        var token = GenerateJwtToken("test-user");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory != null)
            await _factory.DisposeAsync();
        await _msSqlContainer.DisposeAsync();
    }

    [Fact]
    public async Task UpdateClient_ShouldCreateVersionHistory()
    {
        // Arrange - Create client
        var createRequest = CreateValidClientRequest();
        var createResponse = await _client!.PostAsJsonAsync("/api/clients", createRequest);
        var client = await createResponse.Content.ReadFromJsonAsync<ClientResponse>();

        // Act - Update client
        var updateRequest = new UpdateClientRequest
        {
            FirstName = "Updated",
            LastName = "Name",
            MaritalStatus = "Married",
            PrimaryPhone = "+260971111111",
            PhysicalAddress = "New Address",
            City = "Ndola",
            Province = "Copperbelt"
        };
        await _client.PutAsJsonAsync($"/api/clients/{client!.Id}", updateRequest);

        // Assert - Check version history
        var versionsResponse = await _client.GetAsync($"/api/clients/{client.Id}/versions");
        versionsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var versions = await versionsResponse.Content.ReadFromJsonAsync<List<ClientVersionResponse>>();
        versions.Should().NotBeNull();
        versions!.Count.Should().Be(2); // Initial version + update version
        versions[0].VersionNumber.Should().Be(2); // Most recent first
        versions[1].VersionNumber.Should().Be(1);
    }

    [Fact]
    public async Task GET_Versions_ShouldReturnAllVersionsInDescendingOrder()
    {
        // Arrange - Create and update client multiple times
        var createRequest = CreateValidClientRequest();
        var createResponse = await _client!.PostAsJsonAsync("/api/clients", createRequest);
        var client = await createResponse.Content.ReadFromJsonAsync<ClientResponse>();

        // Update 3 times
        for (int i = 1; i <= 3; i++)
        {
            var updateRequest = new UpdateClientRequest
            {
                FirstName = $"Update{i}",
                LastName = "Test",
                MaritalStatus = "Single",
                PrimaryPhone = "+260971234567",
                PhysicalAddress = "Test Address",
                City = "Lusaka",
                Province = "Lusaka"
            };
            await _client.PutAsJsonAsync($"/api/clients/{client!.Id}", updateRequest);
        }

        // Act
        var response = await _client.GetAsync($"/api/clients/{client!.Id}/versions");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var versions = await response.Content.ReadFromJsonAsync<List<ClientVersionResponse>>();
        versions!.Count.Should().Be(4); // 1 initial + 3 updates
        versions[0].VersionNumber.Should().Be(4);
        versions[1].VersionNumber.Should().Be(3);
        versions[2].VersionNumber.Should().Be(2);
        versions[3].VersionNumber.Should().Be(1);
    }

    [Fact]
    public async Task GET_Versions_ByNumber_ShouldReturnSpecificVersion()
    {
        // Arrange
        var createRequest = CreateValidClientRequest();
        var createResponse = await _client!.PostAsJsonAsync("/api/clients", createRequest);
        var client = await createResponse.Content.ReadFromJsonAsync<ClientResponse>();

        // Update once
        var updateRequest = new UpdateClientRequest
        {
            FirstName = "Updated",
            LastName = "Name",
            MaritalStatus = "Single",
            PrimaryPhone = "+260971234567",
            PhysicalAddress = "Test Address",
            City = "Lusaka",
            Province = "Lusaka"
        };
        await _client.PutAsJsonAsync($"/api/clients/{client!.Id}", updateRequest);

        // Act - Get version 1
        var response = await _client.GetAsync($"/api/clients/{client.Id}/versions/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var version = await response.Content.ReadFromJsonAsync<ClientVersionResponse>();
        version!.VersionNumber.Should().Be(1);
        version.FirstName.Should().Be(createRequest.FirstName); // Original name
    }

    [Fact]
    public async Task GET_Versions_ByInvalidNumber_ShouldReturn404()
    {
        // Arrange
        var createRequest = CreateValidClientRequest();
        var createResponse = await _client!.PostAsJsonAsync("/api/clients", createRequest);
        var client = await createResponse.Content.ReadFromJsonAsync<ClientResponse>();

        // Act
        var response = await _client.GetAsync($"/api/clients/{client!.Id}/versions/999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GET_Versions_AtTimestamp_ShouldReturnVersionValidAtTime()
    {
        // Arrange - Create client
        var createRequest = CreateValidClientRequest();
        var createResponse = await _client!.PostAsJsonAsync("/api/clients", createRequest);
        var client = await createResponse.Content.ReadFromJsonAsync<ClientResponse>();
        var clientId = client!.Id;

        // Get initial version timestamp
        var initialVersionsResponse = await _client.GetAsync($"/api/clients/{clientId}/versions");
        var initialVersions = await initialVersionsResponse.Content.ReadFromJsonAsync<List<ClientVersionResponse>>();
        var version1Time = initialVersions![0].ValidFrom;

        // Wait and update
        await Task.Delay(500);
        var updateRequest = new UpdateClientRequest
        {
            FirstName = "Updated",
            LastName = "Name",
            MaritalStatus = "Single",
            PrimaryPhone = "+260971234567",
            PhysicalAddress = "Test Address",
            City = "Lusaka",
            Province = "Lusaka"
        };
        await _client.PutAsJsonAsync($"/api/clients/{clientId}", updateRequest);

        // Act - Query at version 1 time
        var timestampParam = version1Time.AddSeconds(1).ToString("O");
        var response = await _client.GetAsync($"/api/clients/{clientId}/versions/at/{timestampParam}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var version = await response.Content.ReadFromJsonAsync<ClientVersionResponse>();
        version!.VersionNumber.Should().Be(1);
        version.FirstName.Should().Be(createRequest.FirstName);
    }

    [Fact]
    public async Task GET_Versions_AtCurrentTime_ShouldReturnLatestVersion()
    {
        // Arrange
        var createRequest = CreateValidClientRequest();
        var createResponse = await _client!.PostAsJsonAsync("/api/clients", createRequest);
        var client = await createResponse.Content.ReadFromJsonAsync<ClientResponse>();

        // Update client
        var updateRequest = new UpdateClientRequest
        {
            FirstName = "Latest",
            LastName = "Version",
            MaritalStatus = "Single",
            PrimaryPhone = "+260971234567",
            PhysicalAddress = "Test Address",
            City = "Lusaka",
            Province = "Lusaka"
        };
        await _client!.PutAsJsonAsync($"/api/clients/{client!.Id}", updateRequest);

        // Act - Query at current time
        var currentTime = DateTime.UtcNow.ToString("O");
        var response = await _client.GetAsync($"/api/clients/{client.Id}/versions/at/{currentTime}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var version = await response.Content.ReadFromJsonAsync<ClientVersionResponse>();
        version!.IsCurrent.Should().BeTrue();
        version.FirstName.Should().Be("Latest");
    }

    [Fact]
    public async Task GET_Versions_WithInvalidTimestamp_ShouldReturn400()
    {
        // Arrange
        var createRequest = CreateValidClientRequest();
        var createResponse = await _client!.PostAsJsonAsync("/api/clients", createRequest);
        var client = await createResponse.Content.ReadFromJsonAsync<ClientResponse>();

        // Act
        var response = await _client.GetAsync($"/api/clients/{client!.Id}/versions/at/invalid-timestamp");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MultipleUpdates_ShouldMaintainConsistentVersionNumbers()
    {
        // Arrange
        var createRequest = CreateValidClientRequest();
        var createResponse = await _client!.PostAsJsonAsync("/api/clients", createRequest);
        var client = await createResponse.Content.ReadFromJsonAsync<ClientResponse>();

        // Act - Multiple sequential updates
        for (int i = 1; i <= 5; i++)
        {
            var updateRequest = new UpdateClientRequest
            {
                FirstName = $"Update{i}",
                LastName = "Test",
                MaritalStatus = "Single",
                PrimaryPhone = "+260971234567",
                PhysicalAddress = $"Address {i}",
                City = "Lusaka",
                Province = "Lusaka"
            };
            var updateResponse = await _client.PutAsJsonAsync($"/api/clients/{client!.Id}", updateRequest);
            updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Assert
        var versionsResponse = await _client.GetAsync($"/api/clients/{client!.Id}/versions");
        var versions = await versionsResponse.Content.ReadFromJsonAsync<List<ClientVersionResponse>>();
        
        versions!.Count.Should().Be(6); // 1 initial + 5 updates
        for (int i = 0; i < versions.Count; i++)
        {
            versions[i].VersionNumber.Should().Be(6 - i); // Descending order
        }
    }

    [Fact]
    public async Task VersionHistory_ShouldOnlyHaveOneCurrentVersion()
    {
        // Arrange
        var createRequest = CreateValidClientRequest();
        var createResponse = await _client!.PostAsJsonAsync("/api/clients", createRequest);
        var client = await createResponse.Content.ReadFromJsonAsync<ClientResponse>();

        // Update multiple times
        for (int i = 0; i < 3; i++)
        {
            var updateRequest = new UpdateClientRequest
            {
                FirstName = $"Update{i}",
                LastName = "Test",
                MaritalStatus = "Single",
                PrimaryPhone = "+260971234567",
                PhysicalAddress = "Test Address",
                City = "Lusaka",
                Province = "Lusaka"
            };
            await _client.PutAsJsonAsync($"/api/clients/{client!.Id}", updateRequest);
        }

        // Act
        var versionsResponse = await _client.GetAsync($"/api/clients/{client!.Id}/versions");
        var versions = await versionsResponse.Content.ReadFromJsonAsync<List<ClientVersionResponse>>();

        // Assert
        var currentVersions = versions!.Where(v => v.IsCurrent).ToList();
        currentVersions.Count.Should().Be(1);
        currentVersions[0].VersionNumber.Should().Be(versions.Max(v => v.VersionNumber));
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

    private string GenerateJwtToken(string username)
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
