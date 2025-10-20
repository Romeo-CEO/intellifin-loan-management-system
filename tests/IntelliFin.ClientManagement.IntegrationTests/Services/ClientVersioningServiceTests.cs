using IntelliFin.ClientManagement.Domain.Entities;
using IntelliFin.ClientManagement.Infrastructure.Persistence;
using IntelliFin.ClientManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Testcontainers.MsSql;

namespace IntelliFin.ClientManagement.IntegrationTests.Services;

/// <summary>
/// Unit tests for ClientVersioningService
/// </summary>
public class ClientVersioningServiceTests : IAsyncLifetime
{
    private readonly MsSqlContainer _msSqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("YourStrong!Passw0rd")
        .Build();

    private ClientManagementDbContext? _context;
    private ClientVersioningService? _service;

    public async Task InitializeAsync()
    {
        await _msSqlContainer.StartAsync();

        var options = new DbContextOptionsBuilder<ClientManagementDbContext>()
            .UseSqlServer(_msSqlContainer.GetConnectionString())
            .Options;

        _context = new ClientManagementDbContext(options);
        await _context.Database.MigrateAsync();

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<ClientVersioningService>();
        _service = new ClientVersioningService(_context, logger);
    }

    public async Task DisposeAsync()
    {
        if (_context != null)
            await _context.DisposeAsync();
        await _msSqlContainer.DisposeAsync();
    }

    [Fact]
    public async Task CreateVersionAsync_FirstVersion_ShouldCreateVersionNumber1()
    {
        // Arrange
        var client = CreateTestClient();
        _context!.Clients.Add(client);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service!.CreateVersionAsync(client, "Initial version", "test-user");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.VersionNumber.Should().Be(1);
        result.Value.IsCurrent.Should().BeTrue();
        result.Value.ValidTo.Should().BeNull();
    }

    [Fact]
    public async Task CreateVersionAsync_SecondVersion_ShouldIncrementVersionNumber()
    {
        // Arrange
        var client = CreateTestClient();
        _context!.Clients.Add(client);
        await _context.SaveChangesAsync();

        await _service!.CreateVersionAsync(client, "First version", "user1");
        
        // Act - Create second version
        var result = await _service.CreateVersionAsync(client, "Second version", "user2");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.VersionNumber.Should().Be(2);
    }

    [Fact]
    public async Task CreateVersionAsync_ShouldSetValidFromToCurrentTime()
    {
        // Arrange
        var client = CreateTestClient();
        _context!.Clients.Add(client);
        await _context.SaveChangesAsync();
        var beforeCreate = DateTime.UtcNow;

        // Act
        var result = await _service!.CreateVersionAsync(client, "Test version", "test-user");
        var afterCreate = DateTime.UtcNow;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.ValidFrom.Should().BeOnOrAfter(beforeCreate);
        result.Value.ValidFrom.Should().BeOnOrBefore(afterCreate);
    }

    [Fact]
    public async Task CreateVersionAsync_ShouldCalculateChangeSummary()
    {
        // Arrange
        var client = CreateTestClient();
        _context!.Clients.Add(client);
        await _context.SaveChangesAsync();

        await _service!.CreateVersionAsync(client, "First version", "user1");

        // Modify client
        client.PrimaryPhone = "+260971111111";
        client.City = "Ndola";

        // Act
        var result = await _service.CreateVersionAsync(client, "Updated phone and city", "user2");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.ChangeSummaryJson.Should().Contain("PrimaryPhone");
        result.Value.ChangeSummaryJson.Should().Contain("City");
        result.Value.ChangeReason.Should().Be("Updated phone and city");
    }

    [Fact]
    public async Task GetVersionHistoryAsync_ShouldReturnVersionsInDescendingOrder()
    {
        // Arrange
        var client = CreateTestClient();
        _context!.Clients.Add(client);
        await _context.SaveChangesAsync();

        await _service!.CreateVersionAsync(client, "Version 1", "user1");
        await _service.CreateVersionAsync(client, "Version 2", "user2");
        await _service.CreateVersionAsync(client, "Version 3", "user3");

        // Act
        var result = await _service.GetVersionHistoryAsync(client.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Count.Should().Be(3);
        result.Value[0].VersionNumber.Should().Be(3);
        result.Value[1].VersionNumber.Should().Be(2);
        result.Value[2].VersionNumber.Should().Be(1);
    }

    [Fact]
    public async Task GetVersionByNumberAsync_ShouldReturnSpecificVersion()
    {
        // Arrange
        var client = CreateTestClient();
        _context!.Clients.Add(client);
        await _context.SaveChangesAsync();

        await _service!.CreateVersionAsync(client, "Version 1", "user1");
        await _service.CreateVersionAsync(client, "Version 2", "user2");

        // Act
        var result = await _service.GetVersionByNumberAsync(client.Id, 1);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.VersionNumber.Should().Be(1);
        result.Value.ChangeReason.Should().Be("Version 1");
    }

    [Fact]
    public async Task GetVersionAtTimestampAsync_ShouldReturnVersionValidAtTimestamp()
    {
        // Arrange
        var client = CreateTestClient();
        _context!.Clients.Add(client);
        await _context.SaveChangesAsync();

        // Create first version
        var version1Result = await _service!.CreateVersionAsync(client, "Version 1", "user1");
        var version1Time = version1Result.Value!.ValidFrom;

        // Wait a moment and close first version
        await Task.Delay(100);
        await _service.CloseCurrentVersionAsync(client.Id);

        // Create second version
        await _service.CreateVersionAsync(client, "Version 2", "user2");

        // Act - Query for version valid at version1Time
        var result = await _service.GetVersionAtTimestampAsync(client.Id, version1Time.AddSeconds(1));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.VersionNumber.Should().Be(1);
    }

    [Fact]
    public async Task GetVersionAtTimestampAsync_FutureDate_ShouldReturnCurrentVersion()
    {
        // Arrange
        var client = CreateTestClient();
        _context!.Clients.Add(client);
        await _context.SaveChangesAsync();

        await _service!.CreateVersionAsync(client, "Version 1", "user1");

        // Act - Query future date
        var futureDate = DateTime.UtcNow.AddYears(1);
        var result = await _service.GetVersionAtTimestampAsync(client.Id, futureDate);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.IsCurrent.Should().BeTrue();
    }

    [Fact]
    public async Task CloseCurrentVersionAsync_ShouldSetIsCurrentFalseAndValidTo()
    {
        // Arrange
        var client = CreateTestClient();
        _context!.Clients.Add(client);
        await _context.SaveChangesAsync();

        var versionResult = await _service!.CreateVersionAsync(client, "Version 1", "user1");
        var versionId = versionResult.Value!.Id;
        var beforeClose = DateTime.UtcNow;

        // Act
        var result = await _service.CloseCurrentVersionAsync(client.Id);
        var afterClose = DateTime.UtcNow;

        // Assert
        result.IsSuccess.Should().BeTrue();

        var closedVersion = await _context!.ClientVersions.FindAsync(versionId);
        closedVersion!.IsCurrent.Should().BeFalse();
        closedVersion.ValidTo.Should().NotBeNull();
        closedVersion.ValidTo.Should().BeOnOrAfter(beforeClose);
        closedVersion.ValidTo.Should().BeOnOrBefore(afterClose);
    }

    [Fact]
    public async Task CreateVersionAsync_MultipleVersions_ShouldOnlyHaveOneCurrentVersion()
    {
        // Arrange
        var client = CreateTestClient();
        _context!.Clients.Add(client);
        await _context.SaveChangesAsync();

        // Act - Create multiple versions
        await _service!.CreateVersionAsync(client, "Version 1", "user1");
        await _service.CloseCurrentVersionAsync(client.Id);
        
        await _service.CreateVersionAsync(client, "Version 2", "user2");
        await _service.CloseCurrentVersionAsync(client.Id);
        
        await _service.CreateVersionAsync(client, "Version 3", "user3");

        // Assert
        var currentVersions = await _context!.ClientVersions
            .Where(cv => cv.ClientId == client.Id && cv.IsCurrent)
            .ToListAsync();

        currentVersions.Count.Should().Be(1);
        currentVersions[0].VersionNumber.Should().Be(3);
    }

    private static Client CreateTestClient()
    {
        return new Client
        {
            Id = Guid.NewGuid(),
            Nrc = "123456/78/9",
            FirstName = "Test",
            LastName = "Client",
            DateOfBirth = new DateTime(1990, 1, 1),
            Gender = "M",
            MaritalStatus = "Single",
            PrimaryPhone = "+260977123456",
            PhysicalAddress = "123 Test St",
            City = "Lusaka",
            Province = "Lusaka",
            BranchId = Guid.NewGuid(),
            Status = "Active",
            KycStatus = "Pending",
            AmlRiskLevel = "Low",
            RiskRating = "Low",
            VersionNumber = 1,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test-user",
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = "test-user"
        };
    }
}
