using IntelliFin.ClientManagement.Extensions;
using IntelliFin.ClientManagement.Infrastructure.Logging;
using IntelliFin.ClientManagement.Middleware;
using IntelliFin.Shared.Observability;
using Serilog;
using Serilog.Events;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog for logging
    builder.Host.UseSerilog((context, services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName()
            .Enrich.With(services.GetRequiredService<CorrelationIdEnricher>())
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj} {Properties:j}{NewLine}{Exception}");
    });

    // Register services
    builder.Services.AddSingleton<CorrelationIdEnricher>();
    
    // Add OpenTelemetry instrumentation
    builder.Services.AddOpenTelemetryInstrumentation(builder.Configuration);

    // Add database services (DbContext, Vault, health checks)
    builder.Services.AddDatabaseServices(builder.Configuration, builder.Environment);

    // Add JWT authentication
    builder.Services.AddJwtAuthentication(builder.Configuration);

    // Add FluentValidation
    builder.Services.AddFluentValidationConfiguration();

    // Add application services
    builder.Services.AddClientManagementServices(builder.Configuration);

    // Add infrastructure services
    builder.Services.AddClientManagementInfrastructure(builder.Configuration);

    // Add Camunda workers
    builder.Services.AddCamundaWorkers(builder.Configuration);

    // Add MassTransit messaging (Story 1.14b)
    builder.Services.AddMassTransitMessaging(builder.Configuration);

    // Add API services
    builder.Services.AddOpenApi();
    builder.Services.AddControllers();

    var app = builder.Build();

    // Configure middleware pipeline (ORDER IS CRITICAL)
    
    // 1. Correlation ID (first - tracks all requests)
    app.UseCorrelationId();

    // 2. Serilog request logging
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("CorrelationId", 
                CorrelationIdMiddleware.GetCorrelationId(httpContext) ?? "none");
            diagnosticContext.Set("User", httpContext.User?.Identity?.Name ?? "anonymous");
        };
    });

    // 3. Global exception handler (early - catches all downstream errors)
    app.UseGlobalExceptionHandler();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseHttpsRedirection();

    // 4. Authentication (before authorization)
    app.UseAuthentication();

    // 5. Authorization
    app.UseAuthorization();

    // Map health check endpoints
    app.MapHealthChecks("/health");
    app.MapHealthChecks("/health/db", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("database")
    });
    app.MapHealthChecks("/health/camunda", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("camunda")
    });

    // Map default route
    app.MapGet("/", () => Results.Ok(new 
    { 
        name = "IntelliFin.ClientManagement", 
        status = "OK",
        version = "1.0.0"
    })).WithName("GetServiceInfo").WithOpenApi();

    // Map controllers (with authentication required)
    app.MapControllers();

    Log.Information("IntelliFin.ClientManagement starting up");
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

// Partial class for WebApplicationFactory testing
public partial class Program { }