using IntelliFin.Shared.Observability;
using IntelliFin.Shared.DomainModels.Data;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using Serilog;
using Serilog.Formatting.Compact;

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

    // Add Entity Framework with PostgreSQL (shared LmsDbContext)
    var connectionString = builder.Configuration.GetConnectionString("LmsDatabase") 
        ?? throw new InvalidOperationException("LmsDatabase connection string is required");

    builder.Services.AddDbContext<LmsDbContext>(options =>
        options.UseNpgsql(connectionString));

    // Add health checks
    builder.Services.AddHealthChecks()
        .AddNpgSql(connectionString, name: "database", tags: new[] { "ready", "database" })
        .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: new[] { "live" });

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

    // Add HTTP clients for external service integrations (to be implemented in later stories)
    builder.Services.AddHttpClient();

    var app = builder.Build();

    // Configure the HTTP request pipeline
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    // Use Serilog request logging
    app.UseSerilogRequestLogging();

    // Use Prometheus metrics
    app.UseMetricServer(); // Exposes /metrics endpoint
    app.UseHttpMetrics();  // Collects HTTP request metrics

    app.UseHttpsRedirection();
    app.UseRouting();

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
