using System;
using System.Threading;
using System.Threading.Tasks;
using IntelliFin.IdentityService.Controllers.Platform;
using IntelliFin.IdentityService.Models;
using IntelliFin.IdentityService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IntelliFin.IdentityService.Tests.Controllers;

public class PlatformTenantControllerTests
{
    [Fact]
    public async Task CreateTenant_Returns_Created_When_Success()
    {
        var mockService = new Mock<ITenantService>();
        var logger = new Mock<ILogger<PlatformTenantController>>().Object;

        var request = new TenantCreateRequest { Name = "Test", Code = "test" };
        var created = new TenantDto { TenantId = Guid.NewGuid(), Name = "Test", Code = "test" };

        mockService.Setup(s => s.CreateTenantAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(created);

        var controller = new PlatformTenantController(mockService.Object, logger as ILogger<PlatformTenantController>);

        var result = await controller.CreateTenant(request, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(created, createdResult.Value);
    }

    [Fact]
    public async Task CreateTenant_Returns_Conflict_On_Duplicate()
    {
        var mockService = new Mock<ITenantService>();
        var logger = new Mock<ILogger<PlatformTenantController>>().Object;

        var request = new TenantCreateRequest { Name = "Test", Code = "dup" };
        mockService.Setup(s => s.CreateTenantAsync(request, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Tenant with code 'dup' already exists."));

        var controller = new PlatformTenantController(mockService.Object, logger as ILogger<PlatformTenantController>);

        var result = await controller.CreateTenant(request, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var pd = Assert.IsType<Microsoft.AspNetCore.Mvc.ProblemDetails>(conflict.Value);
        Assert.Equal(409, pd.Status);
    }

    [Fact]
    public async Task AssignUser_Returns_Ok()
    {
        var mockService = new Mock<ITenantService>();
        var logger = new Mock<ILogger<PlatformTenantController>>().Object;

        var tenantId = Guid.NewGuid();
        var request = new UserAssignmentRequest { UserId = "u1", Role = "r" };

        mockService.Setup(s => s.AssignUserToTenantAsync(tenantId, request.UserId, request.Role, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var controller = new PlatformTenantController(mockService.Object, logger as ILogger<PlatformTenantController>);

        var result = await controller.AssignUser(tenantId, request, CancellationToken.None);

        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task RemoveUser_Returns_NoContent()
    {
        var mockService = new Mock<ITenantService>();
        var logger = new Mock<ILogger<PlatformTenantController>>().Object;

        var tenantId = Guid.NewGuid();
        var userId = "u1";

        mockService.Setup(s => s.RemoveUserFromTenantAsync(tenantId, userId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var controller = new PlatformTenantController(mockService.Object, logger as ILogger<PlatformTenantController>);

        var result = await controller.RemoveUser(tenantId, userId, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }
}
