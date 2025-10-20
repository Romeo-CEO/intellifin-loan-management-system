using FluentAssertions;
using IntelliFin.IdentityService.Controllers;
using IntelliFin.IdentityService.Models;
using IntelliFin.IdentityService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace IntelliFin.Tests.Unit.IdentityService;

public class AuthorizationControllerTests
{
    private readonly Mock<ITokenIntrospectionService> _introspectionServiceMock = new();
    private readonly Mock<IPermissionCheckService> _permissionCheckServiceMock = new();
    private readonly AuthorizationController _controller;

    public AuthorizationControllerTests()
    {
        _controller = new AuthorizationController(
            _introspectionServiceMock.Object,
            _permissionCheckServiceMock.Object,
            Mock.Of<ILogger<AuthorizationController>>());
    }

    [Fact]
    public async Task IntrospectAsync_WithValidRequest_ReturnsOk()
    {
        var response = new IntrospectionResponse { Active = true, Subject = "abc" };
        _introspectionServiceMock
            .Setup(s => s.IntrospectAsync(It.IsAny<IntrospectionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _controller.IntrospectAsync(new IntrospectionRequest { Token = "token" }) as OkObjectResult;

        result.Should().NotBeNull();
        result!.StatusCode.Should().Be(200);
        result.Value.Should().BeEquivalentTo(response);
    }

    [Fact]
    public async Task IntrospectAsync_UnknownIssuer_ReturnsBadRequest()
    {
        _introspectionServiceMock
            .Setup(s => s.IntrospectAsync(It.IsAny<IntrospectionRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnknownIssuerException("https://unknown"));

        var result = await _controller.IntrospectAsync(new IntrospectionRequest { Token = "token" });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CheckPermissionAsync_ReturnsOk()
    {
        var response = new PermissionCheckResponse { Allowed = true, Reason = "granted" };
        _permissionCheckServiceMock
            .Setup(s => s.CheckPermissionAsync(It.IsAny<PermissionCheckRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _controller.CheckPermissionAsync(new PermissionCheckRequest { UserId = "user", Permission = "perm" }) as OkObjectResult;

        result.Should().NotBeNull();
        result!.Value.Should().BeEquivalentTo(response);
    }
}
