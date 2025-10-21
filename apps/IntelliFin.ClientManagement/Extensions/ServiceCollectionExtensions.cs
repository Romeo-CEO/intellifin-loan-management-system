using FluentValidation;
using IntelliFin.ClientManagement.Infrastructure.Configuration;
using IntelliFin.ClientManagement.Infrastructure.HealthChecks;
using IntelliFin.ClientManagement.Infrastructure.Persistence;
using IntelliFin.ClientManagement.Infrastructure.Vault;
using IntelliFin.ClientManagement.Workflows.CamundaWorkers;
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

        // Register document service (Story 1.6)
        services.AddScoped<Services.IDocumentLifecycleService, Services.DocumentLifecycleService>();

        // Register KycDocumentService HTTP client (Story 1.6)
        services.AddRefitClient<Integration.IKycDocumentServiceClient>()
            .ConfigureHttpClient(c =>
            {
                var baseUrl = configuration["KycDocumentService:BaseUrl"] ?? "http://kyc-document-service:5000";
                c.BaseAddress = new Uri(baseUrl);
                c.Timeout = TimeSpan.Parse(configuration["KycDocumentService:Timeout"] ?? "00:01:00");
            })
            .AddStandardResilienceHandler(); // Adds retry, timeout, circuit breaker

        // Register consent management service (Story 1.7)
        services.AddScoped<Services.IConsentManagementService, Services.ConsentManagementService>();

        // Register notification service (Story 1.7)
        services.AddScoped<Services.INotificationService, Services.NotificationService>();

        // Register KYC workflow service (Story 1.10)
        services.AddScoped<Services.IKycWorkflowService, Services.KycWorkflowService>();

        // Register AML screening service (Story 1.11)
        services.AddScoped<Services.IAmlScreeningService, Services.ManualAmlScreeningService>();

        // Register CommunicationsService HTTP client (Story 1.7)
        services.AddRefitClient<Integration.ICommunicationsClient>()
            .ConfigureHttpClient(c =>
            {
                var baseUrl = configuration["CommunicationsService:BaseUrl"] ?? "http://communications-service:5000";
                c.BaseAddress = new Uri(baseUrl);
                c.Timeout = TimeSpan.Parse(configuration["CommunicationsService:Timeout"] ?? "00:00:30");
            })
            .AddStandardResilienceHandler(); // Adds retry, timeout, circuit breaker

        // Future services will be added here:
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

    /// <summary>
    /// Registers Camunda/Zeebe worker infrastructure and health checks
    /// </summary>
    public static IServiceCollection AddCamundaWorkers(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration
        services.Configure<CamundaOptions>(
            configuration.GetSection(CamundaOptions.SectionName));

        var camundaOptions = configuration
            .GetSection(CamundaOptions.SectionName)
            .Get<CamundaOptions>();

        if (camundaOptions?.Enabled != true)
        {
            // Camunda disabled - skip worker registration
            return services;
        }

        // Register worker handlers
        services.AddScoped<ICamundaJobHandler, HealthCheckWorker>();
        services.AddScoped<ICamundaJobHandler, KycDocumentCheckWorker>();
        services.AddScoped<ICamundaJobHandler, AmlScreeningWorker>();
        services.AddScoped<ICamundaJobHandler, RiskAssessmentWorker>();

        // Register worker configurations
        var workerRegistrations = new List<CamundaWorkerRegistration>
        {
            new CamundaWorkerRegistration
            {
                TopicName = "client.health.check",
                JobType = "io.intellifin.health.check",
                HandlerType = typeof(HealthCheckWorker),
                MaxJobsToActivate = 10,
                TimeoutSeconds = 30
            },
            new CamundaWorkerRegistration
            {
                TopicName = "client.kyc.check-documents",
                JobType = "io.intellifin.kyc.check-documents",
                HandlerType = typeof(KycDocumentCheckWorker),
                MaxJobsToActivate = 32,
                TimeoutSeconds = 30
            },
            new CamundaWorkerRegistration
            {
                TopicName = "client.kyc.aml-screening",
                JobType = "io.intellifin.kyc.aml-screening",
                HandlerType = typeof(AmlScreeningWorker),
                MaxJobsToActivate = 16,
                TimeoutSeconds = 60
            },
            new CamundaWorkerRegistration
            {
                TopicName = "client.kyc.risk-assessment",
                JobType = "io.intellifin.kyc.risk-assessment",
                HandlerType = typeof(RiskAssessmentWorker),
                MaxJobsToActivate = 32,
                TimeoutSeconds = 30
            }
        };

        // Register as singleton for hosted service access
        services.AddSingleton<IEnumerable<CamundaWorkerRegistration>>(workerRegistrations);

        // Register hosted service
        services.AddHostedService<CamundaWorkerHostedService>();

        // Register health check
        services.AddHealthChecks()
            .AddCheck<CamundaHealthCheck>(
                "camunda",
                tags: new[] { "ready", "camunda" });

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
