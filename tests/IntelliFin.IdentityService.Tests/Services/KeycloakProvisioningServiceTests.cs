using IntelliFin.IdentityService.Models;
using IntelliFin.IdentityService.Services;
using IntelliFin.Shared.DomainModels.Entities;
using IntelliFin.Shared.DomainModels.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace IntelliFin.IdentityService.Tests.Services;

public class KeycloakProvisioningServiceTests
{
    private readonly Mock<IKeycloakAdminClient> _mockKeycloakAdmin;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<RoleManager<ApplicationRole>> _mockRoleManager;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<ILogger<KeycloakProvisioningService>> _mockLogger;
    private readonly KeycloakProvisioningService _service;

    public KeycloakProvisioningServiceTests()
    {
        _mockKeycloakAdmin = new Mock<IKeycloakAdminClient>();
        _mockUserManager = MockUserManager<ApplicationUser>();
        _mockRoleManager = MockRoleManager<ApplicationRole>();
        _mockUserRepository = new Mock<IUserRepository>();
        _mockLogger = new Mock<ILogger<KeycloakProvisioningService>>();

        _service = new KeycloakProvisioningService(
            _mockKeycloakAdmin.Object,
            _mockUserManager.Object,
            _mockRoleManager.Object,
            _mockUserRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task ProvisionUserAsync_UserNotExists_CreatesWithAttributes()
    {
        // Arrange
        var userId = "user-123";
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "john.doe",
            Email = "john.doe@test.com",
            FirstName = "John",
            LastName = "Doe",
            BranchId = "branch-456",
            BranchName = "Test Branch",
            BranchRegion = "Central"
        };

        var tenant = new Tenant
        {
            TenantId = Guid.NewGuid(),
            Name = "Test Tenant"
        };

        var tenantUser = new TenantUser
        {
            TenantId = tenant.TenantId,
            UserId = userId,
            Tenant = tenant
        };

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        _mockKeycloakAdmin.Setup(x => x.GetUserByEmailAsync(user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((KeycloakUserRepresentation?)null);

        _mockUserRepository.Setup(x => x.GetUserTenantsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { tenantUser });

        _mockUserManager.Setup(x => x.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "LoanOfficer" });

        var role = new ApplicationRole { Id = "role-1", Name = "LoanOfficer" };
        _mockRoleManager.Setup(x => x.FindByNameAsync("LoanOfficer"))
            .ReturnsAsync(role);

        _mockRoleManager.Setup(x => x.GetClaimsAsync(role))
            .ReturnsAsync(new List<Claim>
            {
                new Claim("permission", "loans:create"),
                new Claim("permission", "loans:view")
            });

        _mockKeycloakAdmin.Setup(x => x.CreateUserAsync(It.IsAny<KeycloakUserRepresentation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("keycloak-user-123");

        _mockKeycloakAdmin.Setup(x => x.SetTemporaryPasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockKeycloakAdmin.Setup(x => x.AssignRealmRoleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.ProvisionUserAsync(userId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("keycloak-user-123", result.KeycloakUserId);
        Assert.Equal(ProvisioningAction.Created, result.Action);

        _mockKeycloakAdmin.Verify(x => x.CreateUserAsync(
            It.Is<KeycloakUserRepresentation>(u =>
                u.Username == user.UserName &&
                u.Email == user.Email &&
                u.Attributes!["extUserId"][0] == userId &&
                u.Attributes["branchId"][0] == user.BranchId &&
                u.Attributes["branchName"][0] == user.BranchName &&
                u.Attributes["tenantId"][0] == tenant.TenantId.ToString() &&
                u.Attributes["permissions"].Contains("loans:create")),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockKeycloakAdmin.Verify(x => x.AssignRealmRoleAsync("keycloak-user-123", "LoanOfficer", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProvisionUserAsync_UserExists_UpdatesAttributesIdempotently()
    {
        // Arrange
        var userId = "user-123";
        var keycloakUserId = "keycloak-user-123";
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "john.doe",
            Email = "john.doe@test.com",
            FirstName = "John",
            LastName = "Doe"
        };

        var existingKeycloakUser = new KeycloakUserRepresentation
        {
            Id = keycloakUserId,
            Email = user.Email,
            Attributes = new Dictionary<string, string[]>
            {
                ["extUserId"] = new[] { userId }
            }
        };

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        _mockKeycloakAdmin.Setup(x => x.GetUserByEmailAsync(user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingKeycloakUser);

        _mockUserRepository.Setup(x => x.GetUserTenantsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TenantUser>());

        _mockUserManager.Setup(x => x.GetRolesAsync(user))
            .ReturnsAsync(new List<string>());

        _mockKeycloakAdmin.Setup(x => x.UpdateUserAsync(keycloakUserId, It.IsAny<KeycloakUserRepresentation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.ProvisionUserAsync(userId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(keycloakUserId, result.KeycloakUserId);
        Assert.Equal(ProvisioningAction.Skipped, result.Action);

        _mockKeycloakAdmin.Verify(x => x.CreateUserAsync(It.IsAny<KeycloakUserRepresentation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SyncUserAsync_UserExists_UpdatesUser()
    {
        // Arrange
        var userId = "user-123";
        var keycloakUserId = "keycloak-user-123";
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "john.doe",
            Email = "john.doe@test.com",
            FirstName = "John",
            LastName = "Doe"
        };

        var existingKeycloakUser = new KeycloakUserRepresentation
        {
            Id = keycloakUserId,
            Email = user.Email,
            Attributes = new Dictionary<string, string[]>
            {
                ["extUserId"] = new[] { userId }
            }
        };

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        _mockKeycloakAdmin.Setup(x => x.GetUserByEmailAsync(user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingKeycloakUser);

        _mockUserRepository.Setup(x => x.GetUserTenantsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TenantUser>());

        _mockUserManager.Setup(x => x.GetRolesAsync(user))
            .ReturnsAsync(new List<string>());

        _mockKeycloakAdmin.Setup(x => x.UpdateUserAsync(keycloakUserId, It.IsAny<KeycloakUserRepresentation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.SyncUserAsync(userId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(keycloakUserId, result.KeycloakUserId);
        Assert.Equal(ProvisioningAction.Updated, result.Action);

        _mockKeycloakAdmin.Verify(x => x.UpdateUserAsync(keycloakUserId, It.IsAny<KeycloakUserRepresentation>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProvisionAllUsersAsync_DryRun_ProducesDiffSummary()
    {
        // Arrange
        var users = new List<ApplicationUser>
        {
            new ApplicationUser { Id = "user-1", Email = "user1@test.com", UserName = "user1" },
            new ApplicationUser { Id = "user-2", Email = "user2@test.com", UserName = "user2" },
            new ApplicationUser { Id = "user-3", Email = "user3@test.com", UserName = "user3" }
        };

        _mockUserManager.Setup(x => x.Users)
            .Returns(users.AsQueryable());

        // User 1 exists in Keycloak
        _mockKeycloakAdmin.Setup(x => x.GetUserByEmailAsync("user1@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KeycloakUserRepresentation { Id = "kc-1" });

        // User 2 doesn't exist
        _mockKeycloakAdmin.Setup(x => x.GetUserByEmailAsync("user2@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((KeycloakUserRepresentation?)null);

        // User 3 doesn't exist
        _mockKeycloakAdmin.Setup(x => x.GetUserByEmailAsync("user3@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((KeycloakUserRepresentation?)null);

        // Act
        var result = await _service.ProvisionAllUsersAsync(dryRun: true);

        // Assert
        Assert.Equal(3, result.TotalUsers);
        Assert.Equal(1, result.SkippedProvisions); // User 1 exists
        Assert.Equal(2, result.PendingCreates);    // Users 2 & 3 need creation
        Assert.Equal(0, result.CreatedUsers);
        Assert.Equal(0, result.UpdatedUsers);
        Assert.Equal(0, result.FailedProvisions);

        // Verify no actual provisioning happened
        _mockKeycloakAdmin.Verify(x => x.CreateUserAsync(It.IsAny<KeycloakUserRepresentation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProvisionAllUsersAsync_ActualRun_CreatesAndUpdatesUsers()
    {
        // Arrange
        var users = new List<ApplicationUser>
        {
            new ApplicationUser { Id = "user-1", Email = "user1@test.com", UserName = "user1", FirstName = "User", LastName = "One" },
            new ApplicationUser { Id = "user-2", Email = "user2@test.com", UserName = "user2", FirstName = "User", LastName = "Two" }
        };

        _mockUserManager.Setup(x => x.Users)
            .Returns(users.AsQueryable());

        // User 1 doesn't exist - will be created
        _mockKeycloakAdmin.Setup(x => x.GetUserByEmailAsync("user1@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((KeycloakUserRepresentation?)null);

        // User 2 doesn't exist - will be created
        _mockKeycloakAdmin.Setup(x => x.GetUserByEmailAsync("user2@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((KeycloakUserRepresentation?)null);

        _mockUserRepository.Setup(x => x.GetUserTenantsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TenantUser>());

        _mockUserManager.Setup(x => x.GetRolesAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(new List<string>());

        _mockKeycloakAdmin.Setup(x => x.CreateUserAsync(It.IsAny<KeycloakUserRepresentation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((KeycloakUserRepresentation u, CancellationToken ct) => $"kc-{u.Email}");

        _mockKeycloakAdmin.Setup(x => x.SetTemporaryPasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.ProvisionAllUsersAsync(dryRun: false);

        // Assert
        Assert.Equal(2, result.TotalUsers);
        Assert.Equal(2, result.CreatedUsers);
        Assert.Equal(0, result.UpdatedUsers);
        Assert.Equal(0, result.SkippedProvisions);
        Assert.Equal(0, result.FailedProvisions);

        _mockKeycloakAdmin.Verify(x => x.CreateUserAsync(It.IsAny<KeycloakUserRepresentation>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ProvisionUserAsync_UserNotFound_ReturnsFailure()
    {
        // Arrange
        var userId = "nonexistent-user";

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _service.ProvisionUserAsync(userId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.ErrorMessage);
        Assert.Equal(ProvisioningAction.Failed, result.Action);
    }

    [Fact]
    public async Task ProvisionUserAsync_KeycloakCreateFails_ReturnsFailure()
    {
        // Arrange
        var userId = "user-123";
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "john.doe",
            Email = "john.doe@test.com",
            FirstName = "John",
            LastName = "Doe"
        };

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        _mockKeycloakAdmin.Setup(x => x.GetUserByEmailAsync(user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((KeycloakUserRepresentation?)null);

        _mockUserRepository.Setup(x => x.GetUserTenantsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TenantUser>());

        _mockUserManager.Setup(x => x.GetRolesAsync(user))
            .ReturnsAsync(new List<string>());

        // Keycloak creation fails
        _mockKeycloakAdmin.Setup(x => x.CreateUserAsync(It.IsAny<KeycloakUserRepresentation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Act
        var result = await _service.ProvisionUserAsync(userId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Failed to create", result.ErrorMessage);
    }

    [Fact]
    public async Task ProvisionUserAsync_MapsPermissionsFromRoleClaims()
    {
        // Arrange
        var userId = "user-123";
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "john.doe",
            Email = "john.doe@test.com",
            FirstName = "John",
            LastName = "Doe"
        };

        _mockUserManager.Setup(x => x.FindByIdAsync(userId))
            .ReturnsAsync(user);

        _mockKeycloakAdmin.Setup(x => x.GetUserByEmailAsync(user.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((KeycloakUserRepresentation?)null);

        _mockUserRepository.Setup(x => x.GetUserTenantsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<TenantUser>());

        _mockUserManager.Setup(x => x.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { "LoanOfficer", "Underwriter" });

        var loanOfficerRole = new ApplicationRole { Id = "role-1", Name = "LoanOfficer" };
        var underwriterRole = new ApplicationRole { Id = "role-2", Name = "Underwriter" };

        _mockRoleManager.Setup(x => x.FindByNameAsync("LoanOfficer"))
            .ReturnsAsync(loanOfficerRole);
        _mockRoleManager.Setup(x => x.FindByNameAsync("Underwriter"))
            .ReturnsAsync(underwriterRole);

        _mockRoleManager.Setup(x => x.GetClaimsAsync(loanOfficerRole))
            .ReturnsAsync(new List<Claim>
            {
                new Claim("permission", "loans:create"),
                new Claim("permission", "loans:view")
            });

        _mockRoleManager.Setup(x => x.GetClaimsAsync(underwriterRole))
            .ReturnsAsync(new List<Claim>
            {
                new Claim("permission", "loans:approve"),
                new Claim("permission", "loans:view") // Duplicate - should be deduped
            });

        _mockKeycloakAdmin.Setup(x => x.CreateUserAsync(It.IsAny<KeycloakUserRepresentation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("keycloak-user-123");

        _mockKeycloakAdmin.Setup(x => x.SetTemporaryPasswordAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockKeycloakAdmin.Setup(x => x.AssignRealmRoleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.ProvisionUserAsync(userId);

        // Assert
        Assert.True(result.Success);

        _mockKeycloakAdmin.Verify(x => x.CreateUserAsync(
            It.Is<KeycloakUserRepresentation>(u =>
                u.Attributes!["permissions"].Length == 3 && // loans:create, loans:view, loans:approve (deduplicated)
                u.Attributes["permissions"].Contains("loans:create") &&
                u.Attributes["permissions"].Contains("loans:view") &&
                u.Attributes["permissions"].Contains("loans:approve")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // Helper methods to mock UserManager and RoleManager
    private static Mock<UserManager<TUser>> MockUserManager<TUser>() where TUser : class
    {
        var store = new Mock<IUserStore<TUser>>();
        var mock = new Mock<UserManager<TUser>>(store.Object, null, null, null, null, null, null, null, null);
        return mock;
    }

    private static Mock<RoleManager<TRole>> MockRoleManager<TRole>() where TRole : class
    {
        var store = new Mock<IRoleStore<TRole>>();
        var mock = new Mock<RoleManager<TRole>>(store.Object, null, null, null, null);
        return mock;
    }
}
