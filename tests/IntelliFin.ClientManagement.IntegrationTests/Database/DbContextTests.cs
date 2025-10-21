using IntelliFin.ClientManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace IntelliFin.ClientManagement.IntegrationTests.Database;

/// <summary>
/// Integration tests for ClientManagementDbContext with SQL Server TestContainers
/// </summary>
public class DbContextTests : IAsyncLifetime
{
    private readonly MsSqlContainer _msSqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("YourStrong!Passw0rd")
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
    public async Task DbContext_Should_Connect_Successfully()
    {
        // Arrange
        var connectionString = _msSqlContainer.GetConnectionString();
        var options = new DbContextOptionsBuilder<ClientManagementDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        // Act
        using var context = new ClientManagementDbContext(options);
        await context.Database.MigrateAsync();

        // Assert
        var canConnect = await context.Database.CanConnectAsync();
        canConnect.Should().BeTrue();
    }

    [Fact]
    public async Task Database_Should_Apply_Migrations_Successfully()
    {
        // Arrange
        var connectionString = _msSqlContainer.GetConnectionString();
        var options = new DbContextOptionsBuilder<ClientManagementDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        // Act
        using var context = new ClientManagementDbContext(options);
        await context.Database.MigrateAsync();

        // Assert
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
        pendingMigrations.Should().BeEmpty("All migrations should be applied");

        var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain("20251020000000_InitialCreate");
    }

    [Fact]
    public async Task Database_Should_Execute_Simple_Query()
    {
        // Arrange
        var connectionString = _msSqlContainer.GetConnectionString();
        var options = new DbContextOptionsBuilder<ClientManagementDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        using var context = new ClientManagementDbContext(options);
        await context.Database.MigrateAsync();

        // Act
        var result = await context.Database.SqlQueryRaw<int>("SELECT 1 AS Value").FirstOrDefaultAsync();

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public async Task Database_Should_Have_Correct_Schema()
    {
        // Arrange
        var connectionString = _msSqlContainer.GetConnectionString();
        var options = new DbContextOptionsBuilder<ClientManagementDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        using var context = new ClientManagementDbContext(options);
        await context.Database.MigrateAsync();

        // Act - Verify migration history table exists
        var tables = await context.Database.SqlQueryRaw<string>(
            "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' AND TABLE_NAME = '__EFMigrationsHistory'")
            .ToListAsync();

        // Assert
        tables.Should().Contain("__EFMigrationsHistory", "EF Core migration history table should exist");
    }
}
