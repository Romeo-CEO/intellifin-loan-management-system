using IntelliFin.IdentityService.Configuration;
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

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();

        // Configuration
        services.Configure<JwtConfiguration>(configuration.GetSection("Jwt"));
        services.Configure<PasswordConfiguration>(configuration.GetSection("Password"));
        services.Configure<RedisConfiguration>(configuration.GetSection("Redis"));
        services.Configure<SessionConfiguration>(configuration.GetSection("Session"));
        services.Configure<AccountLockoutConfiguration>(configuration.GetSection("AccountLockout"));
        services.Configure<SecurityConfiguration>(configuration.GetSection("Security"));

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

        // Permission Catalog Services
        services.AddScoped<IPermissionCatalogService, PermissionCatalogService>();
        services.AddScoped<ITenantResolver, TenantResolver>();
        services.AddHttpContextAccessor();
        services.AddMemoryCache();

        // Role Composition Services
        services.AddScoped<IRoleCompositionService, RoleCompositionService>();
        services.AddScoped<IRoleTemplateService, RoleTemplateService>();

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
        
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
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

        services.AddAuthorization();

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