using IntelliFin.ClientManagement.Controllers.DTOs;
using IntelliFin.ClientManagement.Infrastructure.Persistence;
using IntelliFin.ClientManagement.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Testcontainers.MsSql;

namespace IntelliFin.ClientManagement.IntegrationTests.Services;

/// <summary>
/// Unit tests for ClientService
/// </summary>
public class ClientServiceTests : IAsyncLifetime
{
    private readonly MsSqlContainer _msSqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("YourStrong!Passw0rd")
        .Build();

    private ClientManagementDbContext? _context;
    private ClientService? _service;

    public async Task InitializeAsync()
    {
        await _msSqlContainer.StartAsync();

        var options = new DbContextOptionsBuilder<ClientManagementDbContext>()
            .UseSqlServer(_msSqlContainer.GetConnectionString())
            .Options;

        _context = new ClientManagementDbContext(options);
        await _context.Database.MigrateAsync();

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<ClientService>();
        _service = new ClientService(_context, logger);
    }

    public async Task DisposeAsync()
    {
        if (_context != null)
            await _context.DisposeAsync();
        await _msSqlContainer.DisposeAsync();
    }

    [Fact]
    public async Task CreateClientAsync_WithValidData_ShouldCreateClient()
    {
        // Arrange
        var request = CreateValidClientRequest();

        // Act
        var result = await _service!.CreateClientAsync(request, "test-user");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.FirstName.Should().Be("John");
        result.Value.LastName.Should().Be("Doe");
        result.Value.Nrc.Should().Be("123456/78/9");
        result.Value.Status.Should().Be("Active");
        result.Value.KycStatus.Should().Be("Pending");
        result.Value.VersionNumber.Should().Be(1);
        result.Value.CreatedBy.Should().Be("test-user");
        result.Value.UpdatedBy.Should().Be("test-user");
    }

    [Fact]
    public async Task CreateClientAsync_WithDuplicateNrc_ShouldFail()
    {
        // Arrange
        var request = CreateValidClientRequest();
        await _service!.CreateClientAsync(request, "user1");

        // Act - Try to create with same NRC
        var result = await _service.CreateClientAsync(request, "user2");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already exists");
    }

    [Fact]
    public async Task GetClientByIdAsync_WhenExists_ShouldReturnClient()
    {
        // Arrange
        var createResult = await _service!.CreateClientAsync(CreateValidClientRequest(), "test-user");
        var clientId = createResult.Value!.Id;

        // Act
        var result = await _service.GetClientByIdAsync(clientId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(clientId);
        result.Value.Nrc.Should().Be("123456/78/9");
    }

    [Fact]
    public async Task GetClientByIdAsync_WhenNotExists_ShouldFail()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _service!.GetClientByIdAsync(nonExistentId);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task GetClientByNrcAsync_WhenExists_ShouldReturnClient()
    {
        // Arrange
        await _service!.CreateClientAsync(CreateValidClientRequest(), "test-user");

        // Act
        var result = await _service.GetClientByNrcAsync("123456/78/9");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Nrc.Should().Be("123456/78/9");
    }

    [Fact]
    public async Task GetClientByNrcAsync_CaseInsensitive_ShouldReturnClient()
    {
        // Arrange
        await _service!.CreateClientAsync(CreateValidClientRequest(), "test-user");

        // Act
        var result = await _service.GetClientByNrcAsync("123456/78/9".ToUpper());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetClientByNrcAsync_WhenNotExists_ShouldFail()
    {
        // Act
        var result = await _service!.GetClientByNrcAsync("999999/99/9");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task UpdateClientAsync_WhenExists_ShouldUpdateClient()
    {
        // Arrange
        var createResult = await _service!.CreateClientAsync(CreateValidClientRequest(), "user1");
        var clientId = createResult.Value!.Id;

        var updateRequest = new UpdateClientRequest
        {
            FirstName = "Jane",
            LastName = "Smith",
            MaritalStatus = "Married",
            PrimaryPhone = "+260971234567",
            PhysicalAddress = "456 New Street",
            City = "Ndola",
            Province = "Copperbelt"
        };

        // Act
        var result = await _service.UpdateClientAsync(clientId, updateRequest, "user2");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.FirstName.Should().Be("Jane");
        result.Value.LastName.Should().Be("Smith");
        result.Value.City.Should().Be("Ndola");
        result.Value.UpdatedBy.Should().Be("user2");
        result.Value.CreatedBy.Should().Be("user1"); // Should not change
        result.Value.UpdatedAt.Should().BeAfter(result.Value.CreatedAt);
    }

    [Fact]
    public async Task UpdateClientAsync_WhenNotExists_ShouldFail()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var updateRequest = new UpdateClientRequest
        {
            FirstName = "Jane",
            LastName = "Smith",
            MaritalStatus = "Single",
            PrimaryPhone = "+260971234567",
            PhysicalAddress = "123 Test St",
            City = "Lusaka",
            Province = "Lusaka"
        };

        // Act
        var result = await _service!.UpdateClientAsync(nonExistentId, updateRequest, "test-user");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task UpdateClientAsync_ShouldNotChangeImmutableFields()
    {
        // Arrange
        var createRequest = CreateValidClientRequest();
        var createResult = await _service!.CreateClientAsync(createRequest, "user1");
        var clientId = createResult.Value!.Id;
        var originalNrc = createResult.Value.Nrc;
        var originalDateOfBirth = createResult.Value.DateOfBirth;
        var originalCreatedAt = createResult.Value.CreatedAt;
        var originalCreatedBy = createResult.Value.CreatedBy;

        var updateRequest = new UpdateClientRequest
        {
            FirstName = "UpdatedFirst",
            LastName = "UpdatedLast",
            MaritalStatus = "Single",
            PrimaryPhone = "+260971234567",
            PhysicalAddress = "Updated Address",
            City = "Lusaka",
            Province = "Lusaka"
        };

        // Act
        var result = await _service.UpdateClientAsync(clientId, updateRequest, "user2");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Nrc.Should().Be(originalNrc); // Immutable
        result.Value.DateOfBirth.Should().Be(originalDateOfBirth); // Immutable
        result.Value.CreatedAt.Should().Be(originalCreatedAt); // Immutable
        result.Value.CreatedBy.Should().Be(originalCreatedBy); // Immutable
        result.Value.UpdatedBy.Should().Be("user2"); // Should change
    }

    private static CreateClientRequest CreateValidClientRequest(string nrc = "123456/78/9")
    {
        return new CreateClientRequest
        {
            Nrc = nrc,
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
            Gender = "M",
            MaritalStatus = "Single",
            PrimaryPhone = "+260977123456",
            PhysicalAddress = "123 Test Street",
            City = "Lusaka",
            Province = "Lusaka",
            BranchId = Guid.NewGuid()
        };
    }
}
