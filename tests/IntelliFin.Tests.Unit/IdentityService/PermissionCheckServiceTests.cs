using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using IntelliFin.IdentityService.Models;
using IntelliFin.IdentityService.Services;
using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.Shared.DomainModels.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace IntelliFin.Tests.Unit.IdentityService;

public class PermissionCheckServiceTests : IDisposable
{
    private readonly LmsDbContext _dbContext;
    private readonly Mock<IAuditService> _auditServiceMock = new();
    private readonly PermissionCheckService _service;

    public PermissionCheckServiceTests()
    {
        var options = new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new LmsDbContext(options);

        _service = new PermissionCheckService(
            _dbContext,
            _auditServiceMock.Object,
            Mock.Of<ILogger<PermissionCheckService>>());
    }

    [Fact]
    public async Task CheckPermissionAsync_WithMatchingContext_ReturnsAllowed()
    {
        var user = await SeedUserAsync();

        var response = await _service.CheckPermissionAsync(new PermissionCheckRequest
        {
            UserId = user.Id,
            Permission = "system:users_manage",
            Context = new PermissionContext(Guid.Parse(user.BranchId!), Guid.Parse(user.Metadata["tenantId"].ToString()!))
        });

        response.Allowed.Should().BeTrue();
        response.Reason.Should().Be("granted");
        _auditServiceMock.Verify(a => a.LogAsync(It.Is<AuditEvent>(e => e.Success), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CheckPermissionAsync_WithBranchMismatch_ReturnsDenied()
    {
        var user = await SeedUserAsync();
        var response = await _service.CheckPermissionAsync(new PermissionCheckRequest
        {
            UserId = user.Id,
            Permission = "system:users_manage",
            Context = new PermissionContext(Guid.NewGuid(), null)
        });

        response.Allowed.Should().BeFalse();
        response.Reason.Should().Be("branch_mismatch");
    }

    [Fact]
    public async Task CheckPermissionAsync_UserNotFound_ReturnsInvalid()
    {
        var response = await _service.CheckPermissionAsync(new PermissionCheckRequest
        {
            UserId = Guid.NewGuid().ToString(),
            Permission = "loans:view"
        });

        response.Allowed.Should().BeFalse();
        response.Reason.Should().Be("user_not_found");
    }

    private async Task<User> SeedUserAsync()
    {
        var tenantId = Guid.NewGuid().ToString();
        var branchId = Guid.NewGuid().ToString();

        var permission = new Permission
        {
            Id = Guid.NewGuid().ToString(),
            Name = "system:users_manage",
            IsActive = true
        };

        var role = new Role
        {
            Id = Guid.NewGuid().ToString(),
            Name = "admin",
            IsActive = true,
            RolePermissions = new List<RolePermission>
            {
                new()
                {
                    RoleId = Guid.NewGuid().ToString(),
                    Permission = permission,
                    IsActive = true
                }
            }
        };

        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = "branch-admin",
            Email = "admin@example.com",
            BranchId = branchId,
            Metadata = new Dictionary<string, object>
            {
                ["tenantId"] = tenantId
            },
            UserRoles = new List<UserRole>
            {
                new()
                {
                    UserId = Guid.NewGuid().ToString(),
                    RoleId = role.Id,
                    Role = role,
                    IsActive = true
                }
            }
        };

        user.UserRoles.First().UserId = user.Id;

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();
        return user;
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
