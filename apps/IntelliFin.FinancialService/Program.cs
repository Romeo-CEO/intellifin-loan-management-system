using IntelliFin.FinancialService.Services;
using IntelliFin.Shared.Infrastructure.Messaging;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

// Add financial services
builder.Services.AddScoped<IGeneralLedgerService, GeneralLedgerService>();
builder.Services.AddScoped<ICollectionsService, CollectionsService>();
builder.Services.AddScoped<IPmecService, PmecService>();
builder.Services.AddScoped<IPaymentProcessingService, PaymentProcessingService>();

// Configure MassTransit for messaging
builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", 35672, "/", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });

        cfg.ConfigureEndpoints(context);
    });
});

// Add controllers
builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseRouting();

app.MapHealthChecks("/health");
app.MapControllers();

app.MapGet("/", () => Results.Ok(new { 
    name = "IntelliFin.FinancialService", 
    status = "OK",
    description = "Consolidated Financial Service - GL, Collections, PMEC, and Payment Processing"
}));

app.Run();
