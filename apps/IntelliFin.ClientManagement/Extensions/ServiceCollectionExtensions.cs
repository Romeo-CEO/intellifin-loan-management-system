using IntelliFin.ClientManagement.Infrastructure.Persistence;
using IntelliFin.ClientManagement.Infrastructure.Vault;
using Microsoft.EntityFrameworkCore;

namespace IntelliFin.ClientManagement.Extensions;

/// <summary>
/// Extension methods for service collection configuration
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configures database services including DbContext and health checks
    /// </summary>
    public static IServiceCollection AddDatabaseServices(
        this IServiceCollection services, 
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Register Vault service
        services.AddSingleton<IVaultService, VaultService>();
        
        // Get connection string from Vault or fallback to appsettings
        var connectionString = GetConnectionString(services, configuration, environment);
        
        // Register DbContext with SQL Server
        services.AddDbContext<ClientManagementDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.CommandTimeout(30);
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null
                );
            });
            
            // Enable sensitive data logging in Development only
            if (environment.IsDevelopment())
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
        });
        
        // Add database health check
        services.AddHealthChecks()
            .AddSqlServer(
                connectionString: connectionString,
                healthQuery: "SELECT 1",
                name: "db",
                failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                tags: new[] { "database", "sql" }
            );
        
        return services;
    }
    
    private static string GetConnectionString(
        IServiceCollection services, 
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // In development, try appsettings first, then Vault
        // In production, use Vault only
        if (environment.IsDevelopment())
        {
            var devConnectionString = configuration.GetConnectionString("ClientManagement");
            if (!string.IsNullOrEmpty(devConnectionString))
            {
                return devConnectionString;
            }
        }
        
        // Try to get from Vault
        try
        {
            var serviceProvider = services.BuildServiceProvider();
            var vaultService = serviceProvider.GetRequiredService<IVaultService>();
            var vaultPath = configuration["Vault:ConnectionStringPath"] ?? "intellifin/db-passwords/client-svc";
            
            return vaultService.GetConnectionStringAsync(vaultPath).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            if (environment.IsDevelopment())
            {
                // Fallback for local development
                var fallbackConnectionString = "Server=localhost,1433;Database=IntelliFin.ClientManagement;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=true;";
                Console.WriteLine($"Warning: Using fallback connection string for development. Vault error: {ex.Message}");
                return fallbackConnectionString;
            }
            
            throw new InvalidOperationException("Failed to retrieve connection string from Vault and no fallback available in production", ex);
        }
    }
}
