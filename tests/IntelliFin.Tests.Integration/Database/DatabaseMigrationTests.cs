using IntelliFin.Shared.DomainModels.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace IntelliFin.Tests.Integration.Database;

public class DatabaseMigrationTests : IAsyncLifetime
{
    private readonly MsSqlContainer _msSqlContainer = new MsSqlBuilder()
        .WithPassword("Your_password123!")
        .Build();

    public async Task InitializeAsync()
    {
        await _msSqlContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _msSqlContainer.DisposeAsync();
    }

    [Fact]
    public async Task Database_Should_Apply_Migrations_Successfully()
    {
        // Arrange
        var connectionString = _msSqlContainer.GetConnectionString();
        var options = new DbContextOptionsBuilder<LmsDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        // Act
        using var context = new LmsDbContext(options);
        await context.Database.MigrateAsync();

        // Assert
        var canConnect = await context.Database.CanConnectAsync();
        canConnect.Should().BeTrue();

        // Verify tables exist
        var tables = await context.Database.SqlQueryRaw<string>(
            "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'")
            .ToListAsync();

        tables.Should().Contain("Clients");
        tables.Should().Contain("LoanApplications");
        tables.Should().Contain("LoanProducts");
        tables.Should().Contain("GLAccounts");
        tables.Should().Contain("AuditEvents");
    }

    [Fact]
    public async Task Database_Should_Seed_Reference_Data_After_Migration()
    {
        // Arrange
        var connectionString = _msSqlContainer.GetConnectionString();
        var options = new DbContextOptionsBuilder<LmsDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        // Act
        using var context = new LmsDbContext(options);
        await context.Database.MigrateAsync();

        // Assert
        var loanProducts = await context.LoanProducts.CountAsync();
        loanProducts.Should().Be(3);

        var glAccounts = await context.GLAccounts.CountAsync();
        glAccounts.Should().Be(6);
    }

    [Fact]
    public async Task Database_Should_Enforce_Constraints()
    {
        // Arrange
        var connectionString = _msSqlContainer.GetConnectionString();
        var options = new DbContextOptionsBuilder<LmsDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        using var context = new LmsDbContext(options);
        await context.Database.MigrateAsync();

        // Act & Assert - Test unique constraint on NationalId
        var client1 = new IntelliFin.Shared.DomainModels.Entities.Client
        {
            Id = Guid.NewGuid(),
            FirstName = "John",
            LastName = "Doe",
            NationalId = "123456789"
        };

        var client2 = new IntelliFin.Shared.DomainModels.Entities.Client
        {
            Id = Guid.NewGuid(),
            FirstName = "Jane",
            LastName = "Smith",
            NationalId = "123456789" // Same NationalId
        };

        context.Clients.Add(client1);
        await context.SaveChangesAsync();

        context.Clients.Add(client2);
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        exception.Should().NotBeNull();
    }
}
