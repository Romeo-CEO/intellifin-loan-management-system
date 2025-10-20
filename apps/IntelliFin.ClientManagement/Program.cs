using IntelliFin.ClientManagement.Extensions;
using IntelliFin.Shared.Observability;

var builder = WebApplication.CreateBuilder(args);

// Add OpenTelemetry instrumentation
builder.Services.AddOpenTelemetryInstrumentation(builder.Configuration);

// Add database services (DbContext, Vault, health checks)
builder.Services.AddDatabaseServices(builder.Configuration, builder.Environment);

// Add API services
builder.Services.AddOpenApi();
builder.Services.AddControllers();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Map health check endpoints
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/db", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("database")
});

// Map default route
app.MapGet("/", () => Results.Ok(new 
{ 
    name = "IntelliFin.ClientManagement", 
    status = "OK",
    version = "1.0.0"
}));

app.MapControllers();

app.Run();