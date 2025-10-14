using IntelliFin.Shared.Observability;
using IntelliFin.FinancialService.Clients;
using IntelliFin.FinancialService.Services;
using IntelliFin.Shared.Audit;
using IntelliFin.Shared.Infrastructure.Messaging;
using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.Shared.DomainModels.Repositories;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using Hangfire;
using Hangfire.SqlServer;
using Polly;
using Polly.Extensions.Http;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetryInstrumentation(builder.Configuration);

// Add services to the container
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

// Add Entity Framework with SQL Server
builder.Services.AddDbContext<LmsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection") ?? 
        "Server=(localdb)\\mssqllocaldb;Database=IntelliFin_LoanManagement;Trusted_Connection=true;MultipleActiveResultSets=true"));

// Add Redis distributed cache for performance optimization
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    options.InstanceName = "IntelliFin.FinancialService";
});

// Add repositories
builder.Services.AddScoped<IGLAccountRepository, GLAccountRepository>();
builder.Services.AddScoped<IGLEntryRepository, GLEntryRepository>();

// Add financial services
builder.Services.AddScoped<IGeneralLedgerService, GeneralLedgerService>();
builder.Services.AddScoped<ICollectionsService, CollectionsService>();
builder.Services.AddScoped<IPmecService, PmecService>();
builder.Services.AddScoped<IPaymentProcessingService, PaymentProcessingService>();

// Add payment optimization services
builder.Services.AddScoped<IPaymentRetryService, PaymentRetryService>();
builder.Services.AddScoped<IPaymentReconciliationService, PaymentReconciliationService>();
builder.Services.AddScoped<IPaymentOptimizationService, PaymentOptimizationService>();
builder.Services.AddSingleton<IPaymentMonitoringService, PaymentMonitoringService>();

// Add reporting services with proper dependency injection
builder.Services.AddScoped<IReportingService, ReportingService>();
builder.Services.AddScoped<IBozReportingService, BozReportingService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

// Add compliance monitoring services
builder.Services.AddScoped<IComplianceMonitoringService, ComplianceMonitoringService>();
builder.Services.AddScoped<IBozComplianceService, BozComplianceService>();

// Forward audit writes and queries to Admin Service
builder.Services.AddAuditClient(builder.Configuration);

builder.Services.AddHttpClient<IAdminAuditClient, AdminAuditClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptionsMonitor<AuditClientOptions>>().CurrentValue;
    if (options.BaseAddress is null)
    {
        throw new InvalidOperationException("AuditService:BaseAddress configuration is required for audit forwarding.");
    }

    client.BaseAddress = options.BaseAddress;
    client.Timeout = options.HttpTimeout;
})
.AddPolicyHandler(GetRetryPolicy())
.AddPolicyHandler(GetCircuitBreakerPolicy());

// Add JasperReports client with HttpClient factory and Polly resilience patterns
builder.Services.AddHttpClient<IJasperReportsClient, JasperReportsClient>(client =>
{
    // Configure client timeouts and headers
    client.Timeout = TimeSpan.FromMinutes(10); // Allow for large report generation
    client.DefaultRequestHeaders.Add("User-Agent", "IntelliFin.FinancialService/1.0");
})
.AddPolicyHandler(GetRetryPolicy())
.AddPolicyHandler(GetCircuitBreakerPolicy());

// Add Hangfire for report scheduling
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHangfireServer();

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

// Configure Hangfire Dashboard
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new Hangfire.Dashboard.LocalRequestsOnlyAuthorizationFilter() }
});

app.MapGet("/", () => Results.Ok(new { 
    name = "IntelliFin.FinancialService", 
    status = "OK",
    description = "Consolidated Financial Service - GL, Collections, PMEC, and Payment Processing"
}));

app.Run();

// Helper methods for Polly policies
static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(msg => !msg.IsSuccessStatusCode)
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryCount, context) =>
            {
                Console.WriteLine($"JasperReports retry {retryCount} after {timespan}s");
            });
}

static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 5,
            durationOfBreak: TimeSpan.FromMinutes(1),
            onBreak: (result, timespan) =>
            {
                Console.WriteLine($"JasperReports circuit breaker opened for {timespan}");
            },
            onReset: () =>
            {
                Console.WriteLine("JasperReports circuit breaker reset");
            });
}

public partial class Program { }