using IntelliFin.IdentityService.Configuration;
using IntelliFin.IdentityService.Models;
using IntelliFin.IdentityService.Services;
using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.Shared.DomainModels.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

using AuditEvent = IntelliFin.IdentityService.Models.AuditEvent;
namespace IntelliFin.IdentityService.Tests.Services;

public class TenantServiceTests : IDisposable
{
    private readonly LmsDbContext _dbContext;
    private readonly Mock<IAuditService> _mockAuditService;
    private readonly Mock<IBackgroundQueue<ProvisionCommand>> _mockProvisioningQueue;
    private readonly Mock<ILogger<TenantService>> _mockLogger;
    private readonly Mock<IOptions<FeatureFlags>> _mockFeatureFlags;
    private readonly TenantService _service;

    public TenantServiceTests()
    {
        // Create in-memory database
        var options = new DbContextOptionsBuilder<LmsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new LmsDbContext(options);

        _mockAuditService = new Mock<IAuditService>();
        _mockProvisioningQueue = new Mock<IBackgroundQueue<ProvisionCommand>>();
        _mockLogger = new Mock<ILogger<TenantService>>();
        _mockFeatureFlags = new Mock<IOptions<FeatureFlags>>();

        // Default feature flags
        _mockFeatureFlags.Setup(x => x.Value).Returns(new FeatureFlags
        {
            EnableUserProvisioning = false
        });

        _service = new TenantService(
            _dbContext,
            _mockAuditService.Object,
            _mockLogger.Object,
            _mockFeatureFlags.Object,
            _mockProvisioningQueue.Object);
    }

    [Fact]
    public async Task CreateTenantAsync_UniqueCode_Succeeds()
    {
        // Arrange
        var request = new TenantCreateRequest
        {
            Name = "Test Tenant",
            Code = "test-tenant",
            Settings = "{\"key\":\"value\"}"
        };

        // Act
        var result = await _service.CreateTenantAsync(request);

        // Assert
        Assert.NotEqual(Guid.Empty, result.TenantId);
        Assert.Equal("Test Tenant", result.Name);
        Assert.Equal("test-tenant", result.Code);
        Assert.True(result.IsActive);
        Assert.Equal("{\"key\":\"value\"}", result.Settings);

        // Verify tenant was added to database
        var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Code == "test-tenant");
        Assert.NotNull(tenant);
        Assert.Equal("Test Tenant", tenant.Name);

        // Verify audit event was logged
        _mockAuditService.Verify(x => x.LogAsync(
            It.Is<AuditEvent>(e => e.Action == "TenantCreated" && e.Entity == "Tenant"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateTenantAsync_DuplicateCode_ThrowsInvalidOperationException()
    {
        // Arrange
        var existingTenant = new Tenant
        {
            TenantId = Guid.NewGuid(),
            Name = "Existing Tenant",
            Code = "duplicate-code",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Tenants.Add(existingTenant);
        await _dbContext.SaveChangesAsync();

        var request = new TenantCreateRequest
        {
            Name = "New Tenant",
            Code = "duplicate-code"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateTenantAsync(request));

        Assert.Contains("already exists", exception.Message);

        // Verify no audit event was logged
        _mockAuditService.Verify(x => x.LogAsync(
            It.IsAny<AuditEvent>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AssignUserToTenantAsync_NewAssignment_Succeeds()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            TenantId = tenantId,
            Name = "Test Tenant",
            Code = "test-tenant",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var userId = "user-123";
        var role = "Admin";

        // Act
        await _service.AssignUserToTenantAsync(tenantId, userId, role);

        // Assert
        var membership = await _dbContext.TenantUsers
            .FirstOrDefaultAsync(tu => tu.TenantId == tenantId && tu.UserId == userId);

        Assert.NotNull(membership);
        Assert.Equal(role, membership.Role);
        Assert.Equal("system", membership.AssignedBy);

        // Verify audit event was logged
        _mockAuditService.Verify(x => x.LogAsync(
            It.Is<AuditEvent>(e => e.Action == "UserAssigned" && e.Entity == "TenantUser"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AssignUserToTenantAsync_ExistingMembership_UpdatesIdempotently()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            TenantId = tenantId,
            Name = "Test Tenant",
            Code = "test-tenant",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Tenants.Add(tenant);

        var userId = "user-123";
        var existingMembership = new TenantUser
        {
            TenantId = tenantId,
            UserId = userId,
            Role = "User",
            AssignedAt = DateTime.UtcNow.AddDays(-1),
            AssignedBy = "admin"
        };
        _dbContext.TenantUsers.Add(existingMembership);
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.AssignUserToTenantAsync(tenantId, userId, "Admin");

        // Assert
        var membership = await _dbContext.TenantUsers
            .FirstOrDefaultAsync(tu => tu.TenantId == tenantId && tu.UserId == userId);

        Assert.NotNull(membership);
        Assert.Equal("Admin", membership.Role); // Updated
        Assert.Equal("system", membership.AssignedBy);

        // Verify only one membership exists
        var count = await _dbContext.TenantUsers
            .CountAsync(tu => tu.TenantId == tenantId && tu.UserId == userId);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task AssignUserToTenantAsync_TenantNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var nonExistentTenantId = Guid.NewGuid();
        var userId = "user-123";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.AssignUserToTenantAsync(nonExistentTenantId, userId, "Admin"));

        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public async Task AssignUserToTenantAsync_ProvisioningEnabled_QueuesCommand()
    {
        // Arrange
        _mockFeatureFlags.Setup(x => x.Value).Returns(new FeatureFlags
        {
            EnableUserProvisioning = true
        });

        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            TenantId = tenantId,
            Name = "Test Tenant",
            Code = "test-tenant",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var userId = "user-123";

        // Act
        await _service.AssignUserToTenantAsync(tenantId, userId, "Admin");

        // Assert
        _mockProvisioningQueue.Verify(x => x.QueueAsync(
            It.Is<ProvisionCommand>(cmd =>
                cmd.UserId == userId &&
                cmd.Reason == "MembershipChanged"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveUserFromTenantAsync_ExistingMembership_Succeeds()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            TenantId = tenantId,
            Name = "Test Tenant",
            Code = "test-tenant",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Tenants.Add(tenant);

        var userId = "user-123";
        var membership = new TenantUser
        {
            TenantId = tenantId,
            UserId = userId,
            Role = "User",
            AssignedAt = DateTime.UtcNow,
            AssignedBy = "admin"
        };
        _dbContext.TenantUsers.Add(membership);
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.RemoveUserFromTenantAsync(tenantId, userId);

        // Assert
        var removedMembership = await _dbContext.TenantUsers
            .FirstOrDefaultAsync(tu => tu.TenantId == tenantId && tu.UserId == userId);

        Assert.Null(removedMembership);

        // Verify audit event was logged
        _mockAuditService.Verify(x => x.LogAsync(
            It.Is<AuditEvent>(e => e.Action == "UserRemoved" && e.Entity == "TenantUser"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveUserFromTenantAsync_NonExistentMembership_IdempotentNoError()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = "user-123";

        // Act - should not throw
        await _service.RemoveUserFromTenantAsync(tenantId, userId);

        // Assert - no exception thrown, operation is idempotent
        // Verify no audit event was logged for non-existent membership
        _mockAuditService.Verify(x => x.LogAsync(
            It.IsAny<AuditEvent>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemoveUserFromTenantAsync_ProvisioningEnabled_QueuesCommand()
    {
        // Arrange
        _mockFeatureFlags.Setup(x => x.Value).Returns(new FeatureFlags
        {
            EnableUserProvisioning = true
        });

        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            TenantId = tenantId,
            Name = "Test Tenant",
            Code = "test-tenant",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Tenants.Add(tenant);

        var userId = "user-123";
        var membership = new TenantUser
        {
            TenantId = tenantId,
            UserId = userId,
            Role = "User",
            AssignedAt = DateTime.UtcNow,
            AssignedBy = "admin"
        };
        _dbContext.TenantUsers.Add(membership);
        await _dbContext.SaveChangesAsync();

        // Act
        await _service.RemoveUserFromTenantAsync(tenantId, userId);

        // Assert
        _mockProvisioningQueue.Verify(x => x.QueueAsync(
            It.Is<ProvisionCommand>(cmd =>
                cmd.UserId == userId &&
                cmd.Reason == "MembershipChanged"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListTenantsAsync_NoPagination_ReturnsAllTenants()
    {
        // Arrange
        var tenants = new[]
        {
            new Tenant { TenantId = Guid.NewGuid(), Name = "Tenant A", Code = "tenant-a", IsActive = true, CreatedAt = DateTime.UtcNow },
            new Tenant { TenantId = Guid.NewGuid(), Name = "Tenant B", Code = "tenant-b", IsActive = true, CreatedAt = DateTime.UtcNow },
            new Tenant { TenantId = Guid.NewGuid(), Name = "Tenant C", Code = "tenant-c", IsActive = false, CreatedAt = DateTime.UtcNow }
        };
        _dbContext.Tenants.AddRange(tenants);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.ListTenantsAsync(page: 1, pageSize: 10, isActive: null);

        // Assert
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.Items.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(10, result.PageSize);
    }

    [Fact]
    public async Task ListTenantsAsync_WithPagination_ReturnsPagedResults()
    {
        // Arrange
        for (int i = 0; i < 25; i++)
        {
            _dbContext.Tenants.Add(new Tenant
            {
                TenantId = Guid.NewGuid(),
                Name = $"Tenant {i}",
                Code = $"tenant-{i}",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }
        await _dbContext.SaveChangesAsync();

        // Act - Get page 2 with page size 10
        var result = await _service.ListTenantsAsync(page: 2, pageSize: 10, isActive: null);

        // Assert
        Assert.Equal(25, result.TotalCount);
        Assert.Equal(10, result.Items.Count);
        Assert.Equal(2, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(3, result.TotalPages);
        Assert.True(result.HasNextPage);
        Assert.True(result.HasPreviousPage);
    }

    [Fact]
    public async Task ListTenantsAsync_FilterByIsActive_ReturnsFilteredResults()
    {
        // Arrange
        var tenants = new[]
        {
            new Tenant { TenantId = Guid.NewGuid(), Name = "Active 1", Code = "active-1", IsActive = true, CreatedAt = DateTime.UtcNow },
            new Tenant { TenantId = Guid.NewGuid(), Name = "Active 2", Code = "active-2", IsActive = true, CreatedAt = DateTime.UtcNow },
            new Tenant { TenantId = Guid.NewGuid(), Name = "Inactive 1", Code = "inactive-1", IsActive = false, CreatedAt = DateTime.UtcNow },
            new Tenant { TenantId = Guid.NewGuid(), Name = "Inactive 2", Code = "inactive-2", IsActive = false, CreatedAt = DateTime.UtcNow }
        };
        _dbContext.Tenants.AddRange(tenants);
        await _dbContext.SaveChangesAsync();

        // Act - Filter by active only
        var activeResult = await _service.ListTenantsAsync(page: 1, pageSize: 10, isActive: true);

        // Assert
        Assert.Equal(2, activeResult.TotalCount);
        Assert.Equal(2, activeResult.Items.Count);
        Assert.All(activeResult.Items, t => Assert.True(t.IsActive));

        // Act - Filter by inactive only
        var inactiveResult = await _service.ListTenantsAsync(page: 1, pageSize: 10, isActive: false);

        // Assert
        Assert.Equal(2, inactiveResult.TotalCount);
        Assert.Equal(2, inactiveResult.Items.Count);
        Assert.All(inactiveResult.Items, t => Assert.False(t.IsActive));
    }

    [Fact]
    public async Task ListTenantsAsync_InvalidPageNumber_NormalizesToPageOne()
    {
        // Arrange
        _dbContext.Tenants.Add(new Tenant
        {
            TenantId = Guid.NewGuid(),
            Name = "Test Tenant",
            Code = "test-tenant",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Act - Invalid page numbers
        var resultZero = await _service.ListTenantsAsync(page: 0, pageSize: 10, isActive: null);
        var resultNegative = await _service.ListTenantsAsync(page: -5, pageSize: 10, isActive: null);

        // Assert - Should normalize to page 1
        Assert.Equal(1, resultZero.Page);
        Assert.Equal(1, resultNegative.Page);
    }

    [Fact]
    public async Task ListTenantsAsync_PageSizeExceedsMax_CapsAtMaxPageSize()
    {
        // Arrange
        for (int i = 0; i < 150; i++)
        {
            _dbContext.Tenants.Add(new Tenant
            {
                TenantId = Guid.NewGuid(),
                Name = $"Tenant {i}",
                Code = $"tenant-{i}",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }
        await _dbContext.SaveChangesAsync();

        // Act - Request page size larger than max (100)
        var result = await _service.ListTenantsAsync(page: 1, pageSize: 500, isActive: null);

        // Assert - Should cap at 100
        Assert.Equal(100, result.Items.Count);
        Assert.Equal(100, result.PageSize);
    }

    [Fact]
    public async Task ListTenantsAsync_OrdersByName_ReturnsAlphabeticallySorted()
    {
        // Arrange
        var tenants = new[]
        {
            new Tenant { TenantId = Guid.NewGuid(), Name = "Zebra Tenant", Code = "zebra", IsActive = true, CreatedAt = DateTime.UtcNow },
            new Tenant { TenantId = Guid.NewGuid(), Name = "Alpha Tenant", Code = "alpha", IsActive = true, CreatedAt = DateTime.UtcNow },
            new Tenant { TenantId = Guid.NewGuid(), Name = "Beta Tenant", Code = "beta", IsActive = true, CreatedAt = DateTime.UtcNow }
        };
        _dbContext.Tenants.AddRange(tenants);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.ListTenantsAsync(page: 1, pageSize: 10, isActive: null);

        // Assert
        Assert.Equal("Alpha Tenant", result.Items[0].Name);
        Assert.Equal("Beta Tenant", result.Items[1].Name);
        Assert.Equal("Zebra Tenant", result.Items[2].Name);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }
}
