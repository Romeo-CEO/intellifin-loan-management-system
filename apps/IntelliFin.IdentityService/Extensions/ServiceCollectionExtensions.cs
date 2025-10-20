using IntelliFin.IdentityService.Configuration;
using IntelliFin.IdentityService.Constants;
using IntelliFin.IdentityService.Models;
using IntelliFin.IdentityService.Services;
using IntelliFin.Shared.Audit;
using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.Shared.DomainModels.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Text;
using Polly;
using Polly.Extensions.Http;

namespace IntelliFin.IdentityService.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<VaultConfiguration>(configuration.GetSection("Vault"));

        services.AddSingleton<VaultDatabaseCredentialService>();
        services.AddSingleton<IVaultDatabaseCredentialService>(sp => sp.GetRequiredService<VaultDatabaseCredentialService>());
        services.AddSingleton<IDatabaseConnectionPoolManager, DatabaseConnectionPoolManager>();
        services.AddHostedService(sp => sp.GetRequiredService<VaultDatabaseCredentialService>());

    // Database Context
    var useInMemory = configuration.GetValue<bool>("UseInMemoryDatabase") ||
                      string.Equals(configuration["Database:Provider"], "InMemory", StringComparison.OrdinalIgnoreCase);

    if (useInMemory)
    {
        services.AddDbContext<LmsDbContext>(options =>
        {
            var dbName = configuration.GetValue<string>("InMemoryDatabaseName") ?? "IntelliFin_Identity_Tests";
            options.UseInMemoryDatabase(dbName);
        });
    }
    else
    {
        services.AddDbContext<LmsDbContext>((serviceProvider, options) =>
        {
            var baseConnectionString = configuration.GetConnectionString("IdentityDb")
                ?? configuration.GetConnectionString("DefaultConnection")
                ?? "Server=(localdb)\\mssqllocaldb;Database=IntelliFin_LoanManagement;Trusted_Connection=true;MultipleActiveResultSets=true";

            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("VaultSqlConnection");

            try
            {
                var credentials = serviceProvider
                    .GetRequiredService<IVaultDatabaseCredentialService>()
                    .GetCurrentCredentials();

                var builder = new SqlConnectionStringBuilder(baseConnectionString)
                {
                    UserID = credentials.Username,
                    Password = credentials.Password,
                    ConnectTimeout = 30
                };

                options.UseSqlServer(builder.ConnectionString);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Falling back to base connection string while Vault credentials are unavailable");
                options.UseSqlServer(baseConnectionString);
            }
        });
    }

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();

        // Configuration
        services.Configure<JwtConfiguration>(configuration.GetSection("Jwt"));
        services.Configure<PasswordConfiguration>(configuration.GetSection("Password"));
        services.Configure<ServiceAccountConfiguration>(configuration.GetSection("ServiceAccounts"));
        services.Configure<AuthorizationConfiguration>(configuration.GetSection("Authorization"));
        services.Configure<RedisConfiguration>(configuration.GetSection("Redis"));
        services.Configure<SessionConfiguration>(configuration.GetSection("Session"));
        services.Configure<AccountLockoutConfiguration>(configuration.GetSection("AccountLockout"));
        services.Configure<SecurityConfiguration>(configuration.GetSection("Security"));
        services.Configure<FeatureFlags>(configuration.GetSection(FeatureFlags.SectionName));
        services.Configure<ProvisioningOptions>(configuration.GetSection(ProvisioningOptions.SectionName));
        services.Configure<KeycloakOptions>(configuration.GetSection(KeycloakOptions.SectionName));

        // Redis connection
        var redisConfig = configuration.GetSection("Redis").Get<RedisConfiguration>() ?? new RedisConfiguration();
        if (!string.IsNullOrEmpty(redisConfig.ConnectionString))
        {
            services.AddSingleton<IConnectionMultiplexer>(provider =>
                ConnectionMultiplexer.Connect(redisConfig.ConnectionString));
        }
        else
        {
            // Fallback for development
            services.AddSingleton<IConnectionMultiplexer>(provider =>
                ConnectionMultiplexer.Connect("localhost:6379"));
        }

        // Audit client
        services.AddAuditClient(configuration);

        // Services
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ITokenFamilyService, TokenFamilyService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IAccountLockoutService, AccountLockoutService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<ISoDValidationService, SoDValidationService>();
        services.AddScoped<IServiceAccountService, ServiceAccountService>();
        services.AddScoped<IServiceTokenService, ServiceTokenService>();
        services.AddScoped<ITokenIntrospectionService, TokenIntrospectionService>();
        services.AddScoped<IPermissionCheckService, PermissionCheckService>();

        services.AddSingleton<IKeycloakAdminClient, NullKeycloakAdminClient>();

        services.AddHttpClient<IKeycloakTokenClient, KeycloakTokenClient>()
            .AddPolicyHandler((provider, _) =>
            {
                var logger = provider.GetRequiredService<ILogger<KeycloakTokenClient>>();
                return HttpPolicyExtensions
                    .HandleTransientHttpError()
                    .OrResult(response => (int)response.StatusCode >= 500)
                    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromMilliseconds(Math.Pow(2, retryAttempt) * 100),
                        (outcome, _, retryAttempt, _) =>
                        {
                            if (outcome.Exception is not null)
                            {
                                logger.LogWarning(outcome.Exception, "Retrying Keycloak token request (attempt {Attempt})", retryAttempt);
                            }
                            else if (outcome.Result is not null)
                            {
                                logger.LogWarning(
                                    "Retrying Keycloak token request due to status {StatusCode} (attempt {Attempt})",
                                    outcome.Result.StatusCode,
                                    retryAttempt);
                            }
                        });
            });

        // Permission Catalog Services
        services.AddScoped<IPermissionCatalogService, PermissionCatalogService>();
        services.AddScoped<ITenantResolver, TenantResolver>();
        services.AddHttpContextAccessor();
        services.AddMemoryCache();

        // Role Composition Services
        services.AddScoped<IRoleCompositionService, RoleCompositionService>();
        services.AddScoped<IRoleTemplateService, RoleTemplateService>();

        // Baseline seed service
        services.AddScoped<IBaselineSeedService, BaselineSeedService>();
        
        // Keycloak Provisioning Services (conditionally registered based on feature flag)
        var featureFlags = configuration.GetSection(FeatureFlags.SectionName).Get<FeatureFlags>() ?? new FeatureFlags();
        var provisioningOptions = configuration.GetSection(ProvisioningOptions.SectionName).Get<ProvisioningOptions>() ?? new ProvisioningOptions();
        
        if (featureFlags.EnableUserProvisioning)
        {
            // Register Keycloak Admin Client
            services.AddHttpClient<IKeycloakAdminClient, KeycloakAdminClient>();
            services.AddScoped<IKeycloakAdminClient, KeycloakAdminClient>();
            
// Register Provisioning Service
            services.AddScoped<IUserProvisioningService, KeycloakProvisioningService>();
            
            // Register Background Queue
            services.AddSingleton<IBackgroundQueue<ProvisionCommand>>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<InMemoryBackgroundQueue<ProvisionCommand>>>();
                return new InMemoryBackgroundQueue<ProvisionCommand>(logger, provisioningOptions.QueueCapacity);
            });
            
            // Register Background Worker
            services.AddHostedService<ProvisioningWorker>();
        }
        
        // OIDC Services (conditionally registered based on feature flag)
        if (featureFlags.EnableOidc)
        {
            // Register Keycloak OIDC Service
            services.AddHttpClient<IKeycloakService, KeycloakService>();
            services.AddScoped<IKeycloakService, KeycloakService>();
            
            // Register OIDC State Store
            services.AddScoped<IOidcStateStore, OidcStateStore>();
        }

        // Permission-Role Bridge Services
        services.AddScoped<IPermissionRoleBridgeService, PermissionRoleBridgeService>();

        // Rule-Based Authorization Services
        services.AddScoped<IRuleEngineService, RuleEngineService>();
        services.AddScoped<IRuleTemplateService, RuleTemplateService>();
        services.AddScoped<IUserRuleService, UserRuleService>();

        // ASP.NET Identity for role management
        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = true;
            options.Password.RequiredLength = 8;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<LmsDbContext>()
        .AddDefaultTokenProviders();

        // JWT Authentication
        var jwtConfig = configuration.GetSection("Jwt").Get<JwtConfiguration>() ?? new JwtConfiguration();
        var keycloakConfig = configuration.GetSection("Keycloak").Get<KeycloakOptions>() ?? new KeycloakOptions();
        
        // Determine default authentication scheme based on dual-mode setting
        var defaultScheme = (keycloakConfig.Enabled && keycloakConfig.DualModeEnabled)
            ? "DualMode"
            : JwtBearerDefaults.AuthenticationScheme;
        
        var authBuilder = services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = defaultScheme;
            options.DefaultChallengeScheme = defaultScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = jwtConfig.ValidateIssuerSigningKey,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtConfig.SigningKey)),
                ValidateIssuer = jwtConfig.ValidateIssuer,
                ValidIssuer = jwtConfig.Issuer,
                ValidateAudience = jwtConfig.ValidateAudience,
                ValidAudience = jwtConfig.Audience,
                ValidateLifetime = jwtConfig.ValidateLifetime,
                RequireExpirationTime = jwtConfig.RequireExpirationTime,
                ClockSkew = TimeSpan.FromMinutes(jwtConfig.ClockSkew)
            };

            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var tokenService = context.HttpContext.RequestServices.GetRequiredService<IJwtTokenService>();
                    var tokenId = context.Principal?.FindFirst("jti")?.Value;

                    if (!string.IsNullOrEmpty(tokenId))
                    {
                        var isRevoked = await tokenService.IsTokenRevokedAsync(tokenId);
                        if (isRevoked)
                        {
                            context.Fail("Token has been revoked");
                        }
                    }
                },
                OnAuthenticationFailed = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogWarning("JWT authentication failed: {Error}", context.Exception.Message);
                    return Task.CompletedTask;
                }
            };
        });

        services.AddHttpClient("oidc-metadata");

        services.AddAuthorization(options =>
        {
            // Platform-level policy for managing tenants
            options.AddPolicy(IntelliFin.IdentityService.Constants.SystemPermissions.PlatformTenantsManage, policy =>
            {
                policy.RequireClaim("permissions", IntelliFin.IdentityService.Constants.SystemPermissions.PlatformTenantsManage);
            });

            // System-level policies for token introspection and permission checks
            options.AddPolicy(AuthorizationPolicies.SystemTokenIntrospect, policy =>
                policy.RequireClaim("scope", "system:token_introspect"));

            options.AddPolicy(AuthorizationPolicies.SystemPermissionCheck, policy =>
                policy.RequireClaim("scope", "system:permission_check"));

            options.AddPolicy(AuthorizationPolicies.PlatformServiceAccounts, policy =>
                policy.RequireClaim("scope", "platform:service_accounts"));
        });

        // Add Keycloak authentication if enabled
        authBuilder.AddKeycloakAuthentication(configuration);
        
        // Add dual-mode authentication support
        authBuilder.AddDualModeJwtAuthentication(configuration);

        return services;
    }

    public static IServiceCollection AddCorsPolicy(this IServiceCollection services, IConfiguration configuration)
    {
        var securityConfig = configuration.GetSection("Security").Get<SecurityConfiguration>() ?? new SecurityConfiguration();

        services.AddCors(options =>
        {
            options.AddPolicy("IntelliFin", builder =>
            {
                if (securityConfig.AllowedOrigins.Length > 0)
                {
                    builder.WithOrigins(securityConfig.AllowedOrigins);
                }
                else
                {
                    builder.AllowAnyOrigin();
                }

                builder.AllowAnyMethod()
                       .AllowAnyHeader();

                if (securityConfig.AllowedOrigins.Length > 0)
                {
                    builder.AllowCredentials();
                }
            });
        });

        return services;
    }

    public static IServiceCollection AddRateLimiting(this IServiceCollection services)
    {
        // Rate limiting would be implemented here
        // For now, we'll add it as a placeholder
        return services;
    }
}