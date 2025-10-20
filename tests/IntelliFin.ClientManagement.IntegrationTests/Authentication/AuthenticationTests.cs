using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;

namespace IntelliFin.ClientManagement.IntegrationTests.Authentication;

/// <summary>
/// Integration tests for JWT authentication
/// </summary>
public class AuthenticationTests : IAsyncLifetime
{
    private const string TestSecretKey = "test-secret-key-at-least-32-characters-long-for-testing-purposes";
    private const string TestIssuer = "test-issuer";
    private const string TestAudience = "test-audience";

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
    public async Task ProtectedEndpoint_WithoutToken_Should_Return401Unauthorized()
    {
        // Arrange
        _host = await CreateTestHostWithAuth();
        var client = _host.GetTestClient();

        // Act
        var response = await client.GetAsync("/api/protected");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithValidToken_Should_Return200OK()
    {
        // Arrange
        _host = await CreateTestHostWithAuth();
        var client = _host.GetTestClient();
        
        var token = GenerateJwtToken("testuser", new[] { "kyc-officer" });
        client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/api/protected");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithInvalidToken_Should_Return401Unauthorized()
    {
        // Arrange
        _host = await CreateTestHostWithAuth();
        var client = _host.GetTestClient();
        
        client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "invalid-token");

        // Act
        var response = await client.GetAsync("/api/protected");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithExpiredToken_Should_Return401Unauthorized()
    {
        // Arrange
        _host = await CreateTestHostWithAuth();
        var client = _host.GetTestClient();
        
        var token = GenerateJwtToken("testuser", new[] { "kyc-officer" }, expiresInMinutes: -10);
        client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/api/protected");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<IHost> CreateTestHostWithAuth()
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Authentication:SecretKey"] = TestSecretKey,
                        ["Authentication:Issuer"] = TestIssuer,
                        ["Authentication:Audience"] = TestAudience,
                        ["Authentication:ValidateLifetime"] = "true"
                    });
                });
                
                webHost.ConfigureServices(services =>
                {
                    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                        .AddJwtBearer(options =>
                        {
                            options.TokenValidationParameters = new TokenValidationParameters
                            {
                                ValidateIssuer = true,
                                ValidateAudience = true,
                                ValidateLifetime = true,
                                ValidateIssuerSigningKey = true,
                                ValidIssuer = TestIssuer,
                                ValidAudience = TestAudience,
                                IssuerSigningKey = new SymmetricSecurityKey(
                                    Encoding.UTF8.GetBytes(TestSecretKey))
                            };
                        });

                    services.AddAuthorization();
                    services.AddRouting();
                });

                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/api/protected", () => Results.Ok(new { message = "Success" }))
                            .RequireAuthorization();
                    });
                });
            });

        return await hostBuilder.StartAsync();
    }

    private string GenerateJwtToken(string username, string[] roles, int expiresInMinutes = 15)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSecretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresInMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
