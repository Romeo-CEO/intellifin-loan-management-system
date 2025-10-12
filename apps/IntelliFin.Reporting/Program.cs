using IntelliFin.Shared.Observability;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetryInstrumentation(builder.Configuration);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new { name = "IntelliFin.Reporting", status = "OK" }));

app.Run();