using FluentValidation;
using IntelliFin.ClientManagement.Infrastructure.Persistence;
using IntelliFin.ClientManagement.Infrastructure.Vault;
using IntelliFin.Shared.Audit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

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

    /// <summary>
    /// Configures JWT authentication with IdentityService as authority
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var authSection = configuration.GetSection("Authentication");
        var authority = authSection["Authority"];
        var audience = authSection["Audience"] ?? "intellifin.client-management";
        var secretKey = authSection["SecretKey"] ?? configuration["JWT:SecretKey"];

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            // If using IdentityService as authority
            if (!string.IsNullOrEmpty(authority))
            {
                options.Authority = authority;
                options.Audience = audience;
                options.RequireHttpsMetadata = authSection.GetValue<bool>("RequireHttpsMetadata", true);
            }
            // Otherwise use secret key validation
            else if (!string.IsNullOrEmpty(secretKey))
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = authSection.GetValue<bool>("ValidateIssuer", true),
                    ValidateAudience = authSection.GetValue<bool>("ValidateAudience", true),
                    ValidateLifetime = authSection.GetValue<bool>("ValidateLifetime", true),
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = authSection["Issuer"] ?? "intellifin-identity",
                    ValidAudience = audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
                };
            }

            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILogger<Program>>();
                    logger.LogWarning("Authentication failed: {Error}", context.Exception.Message);
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILogger<Program>>();
                    logger.LogDebug("Token validated for user: {User}", 
                        context.Principal?.Identity?.Name ?? "unknown");
                    return Task.CompletedTask;
                }
            };
        });

        services.AddAuthorization();

        return services;
    }

    /// <summary>
    /// Configures FluentValidation for automatic model validation
    /// </summary>
    public static IServiceCollection AddFluentValidationConfiguration(
        this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<Program>();
        return services;
    }

    /// <summary>
    /// Registers application services
    /// </summary>
    public static IServiceCollection AddClientManagementServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Add audit client
        services.AddAuditClient(configuration);

        // Add HttpContextAccessor for correlation ID enricher
        services.AddHttpContextAccessor();

        // Register audit service (Story 1.5)
        services.AddScoped<Services.IAuditService, Services.AuditService>();

        // Register domain services (Story 1.3, 1.4)
        services.AddScoped<Services.IClientVersioningService, Services.ClientVersioningService>();
        services.AddScoped<Services.IClientService, Services.ClientService>();

        // Future services will be added here:
        // Story 1.6: DocumentService
        // Story 1.13: RiskScoringService

        return services;
    }

    /// <summary>
    /// Configures infrastructure services
    /// </summary>
    public static IServiceCollection AddClientManagementInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Infrastructure services will be added here in future stories
        // Example: Document storage, message bus, cache, etc.

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
