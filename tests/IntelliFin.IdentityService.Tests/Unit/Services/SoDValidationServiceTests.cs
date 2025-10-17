using IntelliFin.IdentityService.Models;
using System;
using System.Linq;
using IntelliFin.IdentityService.Services;
using System.Threading;
using System.Threading.Tasks;
using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.Shared.DomainModels.Entities;
using IntelliFin.Shared.DomainModels.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IntelliFin.IdentityService.Tests.Unit.Services;

public class SoDValidationServiceTests
{
    [Fact]
    public async Task ValidateRoleAssignmentAsync_StrictConflict_BlocksAssignment()
    {
        var auditMock = new Mock<IAuditService>();
        using var context = CreateContext();
        await SeedUserWithRolesAsync(context);
        await SeedSoDRuleAsync(context, SoDEnforcementLevel.Strict);

        var service = new SoDValidationService(context, auditMock.Object, Mock.Of<ILogger<SoDValidationService>>());

        var result = await service.ValidateRoleAssignmentAsync("user-1", "role-approver", CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.True(result.HasConflicts);
        Assert.Single(result.Conflicts);
        Assert.Equal(SoDEnforcementLevel.Strict, result.Conflicts[0].Enforcement);
        auditMock.Verify(a => a.LogAsync(It.IsAny<AuditEvent>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce());
    }

    [Fact]
    public async Task ValidateRoleAssignmentAsync_Warning_ProvidesWarnings()
    {
        var auditMock = new Mock<IAuditService>();
        using var context = CreateContext();
        await SeedUserWithRolesAsync(context);
        await SeedSoDRuleAsync(context, SoDEnforcementLevel.Warning);

        var service = new SoDValidationService(context, auditMock.Object, Mock.Of<ILogger<SoDValidationService>>());

        var result = await service.ValidateRoleAssignmentAsync("user-1", "role-approver", CancellationToken.None);

        Assert.True(result.IsAllowed);
        Assert.True(result.HasWarnings);
        Assert.Single(result.Conflicts);
        auditMock.Verify(a => a.LogAsync(It.Is<AuditEvent>(e => e.Action == "SoDViolation"), It.IsAny<CancellationToken>()), Times.AtLeastOnce());
    }

    [Fact]
    public async Task DetectViolationsAsync_ReturnsActiveViolations()
    {
        var auditMock = new Mock<IAuditService>();
        using var context = CreateContext();
        await SeedUserWithRolesAsync(context, includeApproverRole: true);
        await SeedSoDRuleAsync(context, SoDEnforcementLevel.Strict);

        var service = new SoDValidationService(context, auditMock.Object, Mock.Of<ILogger<SoDValidationService>>());

        var report = await service.DetectViolationsAsync(CancellationToken.None);

        Assert.Equal(1, report.TotalViolations);
        var violation = Assert.Single(report.Violations);
        Assert.Equal("user-1", violation.UserId);
        Assert.Contains("perm.accounts.view", violation.ConflictingPermissions);
        Assert.Contains("perm.accounts.approve", violation.ConflictingPermissions);
        auditMock.Verify(a => a.LogAsync(It.IsAny<AuditEvent>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce());
    }

    private static LmsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new LmsDbContext(options);
    }

    private static async Task SeedUserWithRolesAsync(LmsDbContext context, bool includeApproverRole = false)
    {
        var permissionView = new Permission
        {
            Id = "perm-view",
            Name = "perm.accounts.view",
            Description = "View accounts",
            IsActive = true
        };

        var permissionApprove = new Permission
        {
            Id = "perm-approve",
            Name = "perm.accounts.approve",
            Description = "Approve accounts",
            IsActive = true
        };

        var reviewerRole = new Role
        {
            Id = "role-reviewer",
            Name = "Reviewer",
            IsActive = true,
            RolePermissions =
            {
                new RolePermission
                {
                    RoleId = "role-reviewer",
                    PermissionId = permissionView.Id,
                    Permission = permissionView,
                    IsActive = true
                }
            }
        };

        var approverRole = new Role
        {
            Id = "role-approver",
            Name = "Approver",
            IsActive = true,
            RolePermissions =
            {
                new RolePermission
                {
                    RoleId = "role-approver",
                    PermissionId = permissionApprove.Id,
                    Permission = permissionApprove,
                    IsActive = true
                }
            }
        };

        context.Permissions.AddRange(permissionView, permissionApprove);
        context.Roles.AddRange(reviewerRole, approverRole);
        context.RolePermissions.AddRange(reviewerRole.RolePermissions.Concat(approverRole.RolePermissions));

        var user = new User
        {
            Id = "user-1",
            Username = "user1",
            Email = "user1@example.com",
            PasswordHash = "hash",
            IsActive = true
        };

        context.Users.Add(user);
        context.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = reviewerRole.Id,
            Role = reviewerRole,
            AssignedBy = "system",
            IsActive = true
        });

        if (includeApproverRole)
        {
            context.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = approverRole.Id,
                Role = approverRole,
                AssignedBy = "system",
                IsActive = true
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedSoDRuleAsync(LmsDbContext context, SoDEnforcementLevel enforcement)
    {
        var rule = new SoDRule
        {
            Id = Guid.NewGuid(),
            Name = "Accounts reviewer vs approver",
            Enforcement = enforcement,
            CreatedBy = "system",
            ConflictingPermissions = new[]
            {
                "perm.accounts.view",
                "perm.accounts.approve"
            }
        };

        context.SoDRules.Add(rule);
        await context.SaveChangesAsync();
    }
}
