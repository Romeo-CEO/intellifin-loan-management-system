using IntelliFin.IdentityService.Models;
using IntelliFin.IdentityService.Services;
using IntelliFin.Shared.DomainModels.Enums;
using Microsoft.Extensions.Logging;
using Xunit;

namespace IntelliFin.IdentityService.Tests.Unit.Services;

public class RoleServiceTests
{
    private readonly RoleService _roleService;
    private readonly Mock<ILogger<RoleService>> _loggerMock;

    public RoleServiceTests()
    {
        _loggerMock = new Mock<ILogger<RoleService>>();
        _roleService = new RoleService(_loggerMock.Object);
    }

    [Fact]
    public async Task GetRoleByIdAsync_ValidId_ReturnsRole()
    {
        // Arrange
        const string roleId = "1"; // CEO role from seeded data

        // Act
        var result = await _roleService.GetRoleByIdAsync(roleId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(roleId, result.Id);
        Assert.Equal("CEO", result.Name);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task GetRoleByIdAsync_InvalidId_ReturnsNull()
    {
        // Arrange
        const string invalidId = "invalid-id";

        // Act
        var result = await _roleService.GetRoleByIdAsync(invalidId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetRoleByNameAsync_ValidName_ReturnsRole()
    {
        // Arrange
        const string roleName = "Manager";

        // Act
        var result = await _roleService.GetRoleByNameAsync(roleName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(roleName, result.Name);
        Assert.Equal(RoleType.Organizational, result.Type);
    }

    [Fact]
    public async Task GetRoleByNameAsync_CaseInsensitive_ReturnsRole()
    {
        // Arrange
        const string roleName = "LOANOFFICER";

        // Act
        var result = await _roleService.GetRoleByNameAsync(roleName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("LoanOfficer", result.Name);
    }

    [Fact]
    public async Task GetAllRolesAsync_IncludeActiveOnly_ReturnsActiveRoles()
    {
        // Act
        var result = await _roleService.GetAllRolesAsync(includeInactive: false);

        // Assert
        Assert.NotNull(result);
        Assert.All(result, role => Assert.True(role.IsActive));
        Assert.True(result.Count() >= 5); // At least the seeded roles
    }

    [Fact]
    public async Task CreateRoleAsync_ValidRequest_CreatesRole()
    {
        // Arrange
        var request = new RoleRequest
        {
            Name = "TestRole",
            Description = "Test role for unit testing",
            Type = RoleType.Functional,
            IsActive = true
        };
        const string createdBy = "test-user";

        // Act
        var result = await _roleService.CreateRoleAsync(request, createdBy);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(request.Name, result.Name);
        Assert.Equal(request.Description, result.Description);
        Assert.Equal(request.Type, result.Type);
        Assert.Equal(createdBy, result.CreatedBy);
        Assert.True(result.CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public async Task CreateRoleAsync_DuplicateName_ThrowsException()
    {
        // Arrange
        var request = new RoleRequest
        {
            Name = "CEO", // Existing role name
            Description = "Duplicate role",
            Type = RoleType.Organizational
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _roleService.CreateRoleAsync(request, "test-user"));
    }

    [Fact]
    public async Task UpdateRoleAsync_ValidRequest_UpdatesRole()
    {
        // Arrange
        const string roleId = "3"; // LoanOfficer role
        var request = new RoleRequest
        {
            Name = "Senior Loan Officer",
            Description = "Updated description",
            Type = RoleType.Functional,
            IsActive = true
        };
        const string updatedBy = "test-user";

        // Act
        var result = await _roleService.UpdateRoleAsync(roleId, request, updatedBy);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(request.Name, result.Name);
        Assert.Equal(request.Description, result.Description);
        Assert.Equal(updatedBy, result.UpdatedBy);
        Assert.NotNull(result.UpdatedAt);
    }

    [Fact]
    public async Task UpdateRoleAsync_NonExistentRole_ThrowsKeyNotFoundException()
    {
        // Arrange
        const string invalidRoleId = "invalid-id";
        var request = new RoleRequest
        {
            Name = "Updated Role",
            Description = "Updated description",
            Type = RoleType.Functional
        };

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => 
            _roleService.UpdateRoleAsync(invalidRoleId, request, "test-user"));
    }

    [Fact]
    public async Task DeleteRoleAsync_SystemRole_ThrowsInvalidOperationException()
    {
        // Arrange
        const string systemRoleId = "1"; // CEO is a system role

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _roleService.DeleteRoleAsync(systemRoleId, "test-user"));
    }

    [Fact]
    public async Task ActivateRoleAsync_ValidRole_ActivatesRole()
    {
        // Arrange
        // First create a role to test activation
        var createRequest = new RoleRequest
        {
            Name = "TestActivationRole",
            Description = "Role for testing activation",
            Type = RoleType.Functional,
            IsActive = false
        };
        var createdRole = await _roleService.CreateRoleAsync(createRequest, "test-user");

        // Act
        var result = await _roleService.ActivateRoleAsync(createdRole.Id, "activator");

        // Assert
        Assert.True(result);
        
        var updatedRole = await _roleService.GetRoleByIdAsync(createdRole.Id);
        Assert.NotNull(updatedRole);
        Assert.True(updatedRole.IsActive);
        Assert.Equal("activator", updatedRole.UpdatedBy);
    }

    [Fact]
    public async Task DeactivateRoleAsync_SystemRole_ThrowsInvalidOperationException()
    {
        // Arrange
        const string systemRoleId = "1"; // CEO is a system role

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _roleService.DeactivateRoleAsync(systemRoleId, "test-user"));
    }

    [Fact]
    public async Task GetRoleHierarchyAsync_ValidRole_ReturnsHierarchy()
    {
        // Arrange
        const string roleId = "3"; // LoanOfficer role

        // Act
        var result = await _roleService.GetRoleHierarchyAsync(roleId);

        // Assert
        Assert.NotNull(result);
        var hierarchy = result.ToList();
        
        // Should include CEO -> Manager -> LoanOfficer hierarchy
        Assert.Contains(hierarchy, r => r.Name == "CEO");
        Assert.Contains(hierarchy, r => r.Name == "Manager");
        Assert.Contains(hierarchy, r => r.Name == "LoanOfficer");
        
        // Should be ordered by hierarchy level
        var ceoRole = hierarchy.First(r => r.Name == "CEO");
        var managerRole = hierarchy.First(r => r.Name == "Manager");
        var loanOfficerRole = hierarchy.First(r => r.Name == "LoanOfficer");
        
        Assert.True(ceoRole.Level < managerRole.Level);
        Assert.True(managerRole.Level <= loanOfficerRole.Level);
    }

    [Fact]
    public async Task HasRoleAsync_UserWithoutRoles_ReturnsFalse()
    {
        // Arrange
        const string userId = "test-user-id";
        const string roleName = "Manager";

        // Act
        var result = await _roleService.HasRoleAsync(userId, roleName);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("CEO")]
    [InlineData("Manager")]
    [InlineData("LoanOfficer")]
    [InlineData("ComplianceOfficer")]
    [InlineData("Admin")]
    public async Task GetRoleByNameAsync_SystemRoles_ReturnsExpectedRole(string roleName)
    {
        // Act
        var result = await _roleService.GetRoleByNameAsync(roleName);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(roleName, result.Name);
        Assert.True(result.IsSystemRole);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task GetAllRolesAsync_OrderedCorrectly_ReturnsOrderedRoles()
    {
        // Act
        var result = await _roleService.GetAllRolesAsync();

        // Assert
        var roles = result.ToList();
        
        // Should be ordered by level, then by name
        for (int i = 1; i < roles.Count; i++)
        {
            Assert.True(
                roles[i - 1].Level <= roles[i].Level,
                $"Role {roles[i - 1].Name} (Level {roles[i - 1].Level}) should come before {roles[i].Name} (Level {roles[i].Level})"
            );
        }
    }
}