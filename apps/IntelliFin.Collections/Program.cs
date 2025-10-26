using IntelliFin.Collections.Application.BackgroundServices;
using IntelliFin.Collections.Application.Services;
using IntelliFin.Collections.Infrastructure.Messaging.Consumers;
using IntelliFin.Collections.Infrastructure.Persistence;
using IntelliFin.Collections.Workflows.Workers;
using IntelliFin.Shared.Audit;
using IntelliFin.Shared.Observability;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using HealthChecks.RabbitMQ;
// Health checks are included in the main package
using Zeebe.Client;
using Zeebe.Client.Api.Worker;

var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry
builder.Services.AddOpenTelemetryInstrumentation(builder.Configuration);

// Database
builder.Services.AddDbContext<CollectionsDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null)));

// Audit Client
builder.Services.AddAuditClient(builder.Configuration);

// Application Services
builder.Services.AddScoped<IRepaymentScheduleService, RepaymentScheduleService>();
builder.Services.AddScoped<IPaymentProcessingService, PaymentProcessingService>();
builder.Services.AddScoped<IArrearsClassificationService, ArrearsClassificationService>();
builder.Services.AddScoped<ICollectionsReportingService, CollectionsReportingService>();

// Notification Service with HttpClient
builder.Services.AddHttpClient<INotificationService, NotificationService>(client =>
{
    var communicationServiceUrl = builder.Configuration["CommunicationService:BaseUrl"] ?? "http://localhost:5002";
    client.BaseAddress = new Uri(communicationServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Background Services
builder.Services.AddHostedService<NightlyClassificationService>();

// Camunda Workers
builder.Services.AddSingleton<CheckDpdWorker>();
builder.Services.AddSingleton<SendReminderSmsWorker>();
builder.Services.AddSingleton<CreateCallTaskWorker>();
builder.Services.AddSingleton<EscalateToManagerWorker>();
builder.Services.AddSingleton<LegalActionReviewWorker>();

// Zeebe Client (Camunda Platform 8)
builder.Services.AddSingleton<IZeebeClient>(sp =>
{
    var camundaConfig = builder.Configuration.GetSection("Camunda");
    var gatewayAddress = camundaConfig["GatewayAddress"] ?? "localhost:26500";
    
    return ZeebeClient.Builder()
        .UseGatewayAddress(gatewayAddress)
        .UsePlainText()
        .Build();
});

// MassTransit / RabbitMQ
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<LoanDisbursedConsumer>();
    x.AddConsumer<PmecPaymentReceivedConsumer>();
    x.AddConsumer<TreasuryPaymentReceivedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitMqConfig = builder.Configuration.GetSection("RabbitMQ");
        cfg.Host(rabbitMqConfig["Host"] ?? "localhost", h =>
        {
            h.Username(rabbitMqConfig["Username"] ?? "guest");
            h.Password(rabbitMqConfig["Password"] ?? "guest");
        });

        cfg.ReceiveEndpoint("collections-loan-disbursed", e =>
        {
            e.ConfigureConsumer<LoanDisbursedConsumer>(context);
            e.UseMessageRetry(r => r.Intervals(
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(15),
                TimeSpan.FromSeconds(30)));
        });

        cfg.ReceiveEndpoint("collections-pmec-payment", e =>
        {
            e.ConfigureConsumer<PmecPaymentReceivedConsumer>(context);
            e.UseMessageRetry(r => r.Intervals(
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(15),
                TimeSpan.FromSeconds(30)));
        });

        cfg.ReceiveEndpoint("collections-treasury-payment", e =>
        {
            e.ConfigureConsumer<TreasuryPaymentReceivedConsumer>(context);
            e.UseMessageRetry(r => r.Intervals(
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(15),
                TimeSpan.FromSeconds(30)));
        });
    });
});

// Controllers
builder.Services.AddControllers();

// OpenAPI
builder.Services.AddOpenApi();

// Health Checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Apply migrations in development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<CollectionsDbContext>();
    await dbContext.Database.MigrateAsync();
    
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new { name = "IntelliFin.Collections", status = "OK", version = "1.0.0" }));

app.Run();