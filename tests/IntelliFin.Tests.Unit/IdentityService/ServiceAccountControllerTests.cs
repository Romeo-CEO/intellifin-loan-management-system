using IntelliFin.IdentityService.Controllers;
using IntelliFin.IdentityService.Models;
using IntelliFin.IdentityService.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace IntelliFin.Tests.Unit.IdentityService;

public class ServiceAccountControllerTests
{
    private readonly Mock<IServiceTokenService> _serviceTokenServiceMock = new();
    private readonly Mock<ILogger<ServiceAccountController>> _loggerMock = new();
    private readonly ServiceAccountController _controller;

    public ServiceAccountControllerTests()
    {
        _controller = new ServiceAccountController(_serviceTokenServiceMock.Object, _loggerMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact]
    public async Task GenerateTokenAsync_WithValidRequest_ReturnsOk()
    {
        var response = new ServiceTokenResponse
        {
            AccessToken = "token",
            ExpiresIn = 3600,
            TokenType = "Bearer",
            Scope = "svc:read"
        };

        _serviceTokenServiceMock
            .Setup(x => x.GenerateTokenAsync(It.IsAny<ClientCredentialsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _controller.GenerateTokenAsync(new ClientCredentialsRequest
        {
            ClientId = "svc-123456",
            ClientSecret = new string('a', 48)
        }, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(response);
    }

    [Fact]
    public async Task GenerateTokenAsync_InvalidCredentials_ReturnsUnauthorized()
    {
        _serviceTokenServiceMock
            .Setup(x => x.GenerateTokenAsync(It.IsAny<ClientCredentialsRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException());

        var result = await _controller.GenerateTokenAsync(new ClientCredentialsRequest
        {
            ClientId = "svc-123456",
            ClientSecret = new string('a', 48)
        }, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task GenerateTokenAsync_KeycloakUnavailable_ReturnsBadGateway()
    {
        _serviceTokenServiceMock
            .Setup(x => x.GenerateTokenAsync(It.IsAny<ClientCredentialsRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeycloakTokenException(System.Net.HttpStatusCode.BadGateway));

        var result = await _controller.GenerateTokenAsync(new ClientCredentialsRequest
        {
            ClientId = "svc-123456",
            ClientSecret = new string('a', 48)
        }, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status502BadGateway);
    }

    [Fact]
    public async Task GenerateTokenAsync_InvalidConfiguration_ReturnsServiceUnavailable()
    {
        _serviceTokenServiceMock
            .Setup(x => x.GenerateTokenAsync(It.IsAny<ClientCredentialsRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("config"));

        var result = await _controller.GenerateTokenAsync(new ClientCredentialsRequest
        {
            ClientId = "svc-123456",
            ClientSecret = new string('a', 48)
        }, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }
}
