using IntelliFin.IdentityService.Extensions;
using Serilog;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/identityservice-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Starting IntelliFin Identity Service");

    var builder = WebApplication.CreateBuilder(args);

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
