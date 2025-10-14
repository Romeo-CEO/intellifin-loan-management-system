using IntelliFin.IdentityService.Extensions;
using IntelliFin.IdentityService.Middleware;
using IntelliFin.Shared.Observability;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Prometheus;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console(new JsonFormatter())
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Configuration
        .SetBasePath(builder.Environment.ContentRootPath)
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables()
        .AddEnvironmentVariables(prefix: "IDENTITY_");

    builder.Host.UseSerilog((context, services, configuration) =>
    {
        var minimumLevel = context.HostingEnvironment.IsDevelopment()
            ? LogEventLevel.Debug
            : LogEventLevel.Information;

        configuration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .Enrich.With<TraceContextEnricher>()
            .Enrich.WithProperty("Application", "IntelliFin.IdentityService")
            .MinimumLevel.Is(minimumLevel)
            .WriteTo.Console(new JsonFormatter())
            .WriteTo.File(new JsonFormatter(), "logs/identityservice-.txt", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7);
    });

    builder.Services.AddOpenTelemetryInstrumentation(builder.Configuration);

    var connectionString = builder.Configuration.GetConnectionString("IdentityDb")
        ?? builder.Configuration.GetConnectionString("DefaultConnection");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("The SQL Server connection string (ConnectionStrings:IdentityDb) is not configured.");
    }

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new()
        {
            Title = "IntelliFin Identity Service API",
            Version = "v1",
            Description = "Foundation endpoints for identity and access management."
        });

        options.AddSecurityDefinition("Bearer", new()
        {
            Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
            Name = "Authorization",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        });

        options.AddSecurityRequirement(new()
        {
            {
                new()
                {
                    Reference = new()
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    var healthChecks = builder.Services.AddHealthChecks();
    var disableSqlHealthCheck = builder.Configuration.GetValue<bool>("Features:DisableSqlHealthCheck");
    if (!disableSqlHealthCheck)
    {
        healthChecks.AddSqlServer(connectionString, name: "sql-server", tags: new[] { "db", "ready" });
    }

    builder.Services.AddIdentityServices(builder.Configuration, connectionString);
    builder.Services.AddCorsPolicy(builder.Configuration);
    builder.Services.AddRateLimiting();

    var app = builder.Build();

    var startupCounter = Metrics.CreateCounter(
        "identity_service_startup_total",
        "Counts the number of times the Identity Service has started.");
    startupCounter.Inc();

    var requestCounter = Metrics.CreateCounter(
        "identity_service_requests_total",
        "Counts the number of HTTP requests processed by the Identity Service.",
        new CounterConfiguration
        {
            LabelNames = new[] { "method", "endpoint", "status_code" }
        });

    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "IntelliFin Identity Service v1");
        });
    }

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseSerilogRequestLogging();

    app.UseSecurityHeaders();

    app.UseRouting();
    app.UseCors("IntelliFin");

    app.UseAuthentication();
    app.UseAuthorization();

    app.UseHttpMetrics();

    app.Use(async (context, next) =>
    {
        await next();

        var endpoint = context.GetEndpoint()?.DisplayName ?? context.Request.Path.Value ?? "unknown";
        requestCounter.WithLabels(context.Request.Method, endpoint, context.Response.StatusCode.ToString()).Inc();
    });

    app.MapControllers();

    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false
    });

    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });

    app.MapMetrics();

    app.MapGet("/", (IConfiguration configuration) => Results.Ok(new
    {
        name = "IntelliFin.IdentityService",
        status = "OK",
        version = configuration["ServiceVersion"] ?? "1.0.0",
        timestamp = DateTime.UtcNow
    }));

    Log.Information("IntelliFin Identity Service configured successfully");

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "IntelliFin Identity Service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;


