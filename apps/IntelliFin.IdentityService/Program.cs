using IntelliFin.IdentityService.Extensions;
using Serilog;
using IntelliFin.Shared.Observability;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.With<TraceContextEnricher>()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} | TraceId={TraceId} SpanId={SpanId}{NewLine}{Exception}")
    .WriteTo.File("logs/identityservice-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Starting IntelliFin Identity Service");

    var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetryInstrumentation(builder.Configuration);

    // Add Serilog
    builder.Host.UseSerilog();

    // Add services
    builder.Services.AddControllers();
    builder.Services.AddOpenApi();
    builder.Services.AddHealthChecks();

    // Add Identity Services
    builder.Services.AddIdentityServices(builder.Configuration);

    // Add CORS
    builder.Services.AddCorsPolicy(builder.Configuration);

    // Add Rate Limiting
    builder.Services.AddRateLimiting();

    var app = builder.Build();

    // Configure pipeline
    app.ConfigureIdentityApplication();

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