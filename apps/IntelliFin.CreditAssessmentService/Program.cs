using IntelliFin.Shared.Observability;
using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.CreditAssessmentService.Services.Core;
using IntelliFin.CreditAssessmentService.EventHandlers;
using IntelliFin.CreditAssessmentService.Services.Integration;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Prometheus;
using Serilog;
using Serilog.Formatting.Compact;
using FluentValidation;
using FluentValidation.AspNetCore;
using System.Reflection;
using System.Text;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateLogger();

try
{
    Log.Information("Starting IntelliFin.CreditAssessmentService");

    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog from configuration
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("ServiceName", "CreditAssessmentService")
        .WriteTo.Console(new CompactJsonFormatter()));

    // Add OpenTelemetry instrumentation
    builder.Services.AddOpenTelemetryInstrumentation(builder.Configuration);

    // Add services to the container
    builder.Services.AddOpenApi();
    builder.Services.AddControllers();
    
    // Add FluentValidation
    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();

    // Add Entity Framework with PostgreSQL (shared LmsDbContext)
    var connectionString = builder.Configuration.GetConnectionString("LmsDatabase") 
        ?? throw new InvalidOperationException("LmsDatabase connection string is required");

    builder.Services.AddDbContext<LmsDbContext>(options =>
        options.UseNpgsql(connectionString));

    // Register application services
    builder.Services.AddScoped<ICreditAssessmentService, CreditAssessmentService>();
    builder.Services.AddScoped<IRiskCalculationEngine, RiskCalculationEngine>();
    builder.Services.AddSingleton<IntelliFin.CreditAssessmentService.Services.Configuration.IVaultConfigService, IntelliFin.CreditAssessmentService.Services.Configuration.VaultConfigService>();

    // Add JWT Authentication
    var jwtSettings = builder.Configuration.GetSection("Jwt");
    var jwtSecret = jwtSettings["Secret"] ?? "development-secret-key-change-in-production";
    var jwtIssuer = jwtSettings["Issuer"] ?? "IntelliFin.IdentityService";
    var jwtAudience = jwtSettings["Audience"] ?? "IntelliFin.Services";

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    });

    builder.Services.AddAuthorization();

    // Add Swagger/OpenAPI with JWT support
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "IntelliFin Credit Assessment Service API",
            Version = "v1",
            Description = "Intelligent credit scoring and risk assessment engine for IntelliFin Loan Management System",
            Contact = new OpenApiContact
            {
                Name = "IntelliFin Development Team",
                Email = "devops@intellifin.com"
            }
        });

        // Add JWT Bearer authentication to Swagger
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer"
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });

        // Include XML comments for API documentation
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            c.IncludeXmlComments(xmlPath);
        }
    });

    // Add health checks
    builder.Services.AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" });

    // Add Redis distributed cache for caching
    var redisConnection = builder.Configuration.GetConnectionString("Redis");
    if (!string.IsNullOrEmpty(redisConnection))
    {
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnection;
            options.InstanceName = "IntelliFin.CreditAssessment:";
        });
    }

    // Add MassTransit with RabbitMQ
    builder.Services.AddMassTransit(x =>
    {
        // Register consumers
        x.AddConsumer<KycStatusEventHandler>();

        x.UsingRabbitMq((context, cfg) =>
        {
            var rabbitMqHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
            var rabbitMqPort = builder.Configuration.GetValue<int>("RabbitMQ:Port", 5672);
            var rabbitMqUsername = builder.Configuration["RabbitMQ:Username"] ?? "guest";
            var rabbitMqPassword = builder.Configuration["RabbitMQ:Password"] ?? "guest";
            var rabbitMqVirtualHost = builder.Configuration["RabbitMQ:VirtualHost"] ?? "/";

            cfg.Host(rabbitMqHost, h =>
            {
                h.Username(rabbitMqUsername);
                h.Password(rabbitMqPassword);
            });

            // Configure message retry
            cfg.UseMessageRetry(r => r.Intervals(
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10)
            ));

            // Configure receive endpoint for KYC events
            cfg.ReceiveEndpoint("credit-assessment-kyc-events", e =>
            {
                e.ConfigureConsumer<KycStatusEventHandler>(context);
                
                // Error handling
                e.UseMessageRetry(r => r.Intervals(
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(10)
                ));
                
                // Note: InMemoryInboxOutbox configuration removed for compatibility
            });

            cfg.ConfigureEndpoints(context);
        });
    });

    // Add HTTP clients for external service integrations
    builder.Services.AddHttpClient();
    
    // Client Management HTTP Client
    builder.Services.AddHttpClient<ClientManagementClient>((sp, client) =>
    {
        var baseUrl = builder.Configuration.GetSection("ExternalServices:ClientManagement:BaseUrl").Value ?? "http://localhost:5001";
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
    });

    // TransUnion HTTP Client
    builder.Services.AddHttpClient<TransUnionClient>((sp, client) =>
    {
        var baseUrl = builder.Configuration.GetSection("ExternalServices:TransUnion:BaseUrl").Value ?? "https://api.transunion.co.zm";
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(60);
    });

    // PMEC HTTP Client
    builder.Services.AddHttpClient<PmecClient>((sp, client) =>
    {
        var baseUrl = builder.Configuration.GetSection("ExternalServices:PMEC:BaseUrl").Value ?? "https://pmec-api.gov.zm";
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(45);
    });

    // AdminService HTTP Client
    builder.Services.AddHttpClient<AdminServiceClient>((sp, client) =>
    {
        var baseUrl = builder.Configuration.GetSection("ExternalServices:AdminService:BaseUrl").Value ?? "http://localhost:5002";
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(10);
    });

    // Register integration services
    builder.Services.AddScoped<IClientManagementClient, ClientManagementClient>();
    builder.Services.AddScoped<ITransUnionClient, TransUnionClient>();
    builder.Services.AddScoped<IPmecClient, PmecClient>();
    builder.Services.AddScoped<IAdminServiceClient, AdminServiceClient>();

    var app = builder.Build();

    // Configure the HTTP request pipeline
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Credit Assessment API v1");
            c.RoutePrefix = "swagger";
        });
    }

    // Use Serilog request logging
    app.UseSerilogRequestLogging();

    // Use Prometheus metrics
    app.UseMetricServer(); // Exposes /metrics endpoint
    app.UseHttpMetrics();  // Collects HTTP request metrics

    app.UseHttpsRedirection();
    app.UseRouting();
    
    // Authentication and Authorization
    app.UseAuthentication();
    app.UseAuthorization();

    // Health check endpoints
    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("live")
    });

    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var result = System.Text.Json.JsonSerializer.Serialize(new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    duration = e.Value.Duration.ToString()
                })
            });
            await context.Response.WriteAsync(result);
        }
    });

    app.MapControllers();

    // Root endpoint
    app.MapGet("/", () => Results.Ok(new
    {
        name = "IntelliFin.CreditAssessmentService",
        status = "OK",
        description = "Intelligent Credit Assessment and Risk Scoring Engine",
        version = "1.0.0",
        endpoints = new
        {
            health_live = "/health/live",
            health_ready = "/health/ready",
            metrics = "/metrics",
            api_docs = app.Environment.IsDevelopment() ? "/openapi/v1.json" : null
        }
    }));

    Log.Information("IntelliFin.CreditAssessmentService started successfully");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Partial class for testing
public partial class Program { }
