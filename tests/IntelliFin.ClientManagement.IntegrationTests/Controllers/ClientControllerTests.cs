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
/// Integration tests for ClientController endpoints
/// </summary>
public class ClientControllerTests : IAsyncLifetime
{
    private const string TestSecretKey = "test-secret-key-at-least-32-characters-long-for-testing-purposes";
    private readonly MsSqlContainer _msSqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("YourStrong!Passw0rd")
        .Build();

    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private string? _authToken;

    public async Task InitializeAsync()
    {
        await _msSqlContainer.StartAsync();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove existing DbContext registration
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<ClientManagementDbContext>));
                    if (descriptor != null)
                        services.Remove(descriptor);

                    // Add DbContext with test container connection string
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
    }

    [Fact]
    public async Task POST_Clients_WithValidData_Should_Return201Created()
    {
        // Arrange
        var request = CreateValidClientRequest();

        // Act
        var response = await _client!.PostAsJsonAsync("/api/clients", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var client = await response.Content.ReadFromJsonAsync<ClientResponse>();
        client.Should().NotBeNull();
        client!.FirstName.Should().Be("John");
        client.Nrc.Should().Be("123456/78/9");
    }

    [Fact]
    public async Task POST_Clients_WithInvalidNrc_Should_Return400BadRequest()
    {
        // Arrange
        var request = CreateValidClientRequest();
        request.Nrc = "INVALID"; // Invalid format

        // Act
        var response = await _client!.PostAsJsonAsync("/api/clients", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_Clients_WithDuplicateNrc_Should_Return409Conflict()
    {
        // Arrange
        var request = CreateValidClientRequest();
        await _client!.PostAsJsonAsync("/api/clients", request); // First creation

        // Act
        var response = await _client.PostAsJsonAsync("/api/clients", request); // Duplicate

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task POST_Clients_WithoutAuthentication_Should_Return401Unauthorized()
    {
        // Arrange
        var unauthenticatedClient = _factory!.CreateClient();
        var request = CreateValidClientRequest();

        // Act
        var response = await unauthenticatedClient.PostAsJsonAsync("/api/clients", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GET_Clients_ById_WhenExists_Should_Return200OK()
    {
        // Arrange
        var createRequest = CreateValidClientRequest();
        var createResponse = await _client!.PostAsJsonAsync("/api/clients", createRequest);
        var createdClient = await createResponse.Content.ReadFromJsonAsync<ClientResponse>();

        // Act
        var response = await _client.GetAsync($"/api/clients/{createdClient!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var client = await response.Content.ReadFromJsonAsync<ClientResponse>();
        client.Should().NotBeNull();
        client!.Id.Should().Be(createdClient.Id);
    }

    [Fact]
    public async Task GET_Clients_ById_WhenNotExists_Should_Return404NotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client!.GetAsync($"/api/clients/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GET_Clients_ByNrc_WhenExists_Should_Return200OK()
    {
        // Arrange
        var createRequest = CreateValidClientRequest();
        await _client!.PostAsJsonAsync("/api/clients", createRequest);

        // Act
        var response = await _client.GetAsync($"/api/clients/by-nrc/{createRequest.Nrc}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var client = await response.Content.ReadFromJsonAsync<ClientResponse>();
        client.Should().NotBeNull();
        client!.Nrc.Should().Be(createRequest.Nrc);
    }

    [Fact]
    public async Task GET_Clients_ByNrc_WhenNotExists_Should_Return404NotFound()
    {
        // Act
        var response = await _client!.GetAsync("/api/clients/by-nrc/999999/99/9");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PUT_Clients_WithValidData_Should_Return200OK()
    {
        // Arrange
        var createRequest = CreateValidClientRequest();
        var createResponse = await _client!.PostAsJsonAsync("/api/clients", createRequest);
        var createdClient = await createResponse.Content.ReadFromJsonAsync<ClientResponse>();

        var updateRequest = new UpdateClientRequest
        {
            FirstName = "UpdatedFirst",
            LastName = "UpdatedLast",
            MaritalStatus = "Married",
            PrimaryPhone = "+260971234567",
            PhysicalAddress = "Updated Address",
            City = "Kitwe",
            Province = "Copperbelt"
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/clients/{createdClient!.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedClient = await response.Content.ReadFromJsonAsync<ClientResponse>();
        updatedClient.Should().NotBeNull();
        updatedClient!.FirstName.Should().Be("UpdatedFirst");
        updatedClient.City.Should().Be("Kitwe");
    }

    [Fact]
    public async Task PUT_Clients_WhenNotExists_Should_Return404NotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var updateRequest = new UpdateClientRequest
        {
            FirstName = "Test",
            LastName = "User",
            MaritalStatus = "Single",
            PrimaryPhone = "+260971234567",
            PhysicalAddress = "Test Address",
            City = "Lusaka",
            Province = "Lusaka"
        };

        // Act
        var response = await _client!.PutAsJsonAsync($"/api/clients/{nonExistentId}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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
