using IntelliFin.Communications.Consumers;
using IntelliFin.Communications.Models;
using IntelliFin.Communications.Services;
using IntelliFin.Communications.Providers;
using IntelliFin.Shared.Infrastructure.Messaging;
using MassTransit;
using Polly;
using Polly.Extensions.Http;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();
builder.Services.AddHealthChecks();

// Configure Redis caching
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
});

// Configure SMS provider settings
builder.Services.Configure<Dictionary<SmsProvider, SmsProviderSettings>>(options =>
{
    options[SmsProvider.Airtel] = new SmsProviderSettings
    {
        ApiUrl = builder.Configuration["SmsProviders:Airtel:ApiUrl"] ?? "https://api.airtel.co.zm/sms",
        ApiKey = builder.Configuration["SmsProviders:Airtel:ApiKey"] ?? "",
        Username = builder.Configuration["SmsProviders:Airtel:Username"] ?? "",
        Password = builder.Configuration["SmsProviders:Airtel:Password"] ?? "",
        SenderId = "IntelliFin",
        CostPerSms = 0.05m,
        TimeoutSeconds = 30,
        IsActive = true,
        MaxRetries = 3
    };
    
    options[SmsProvider.MTN] = new SmsProviderSettings
    {
        ApiUrl = builder.Configuration["SmsProviders:MTN:ApiUrl"] ?? "https://api.mtn.co.zm/sms",
        ApiKey = builder.Configuration["SmsProviders:MTN:ApiKey"] ?? "",
        Username = builder.Configuration["SmsProviders:MTN:Username"] ?? "",
        Password = builder.Configuration["SmsProviders:MTN:Password"] ?? "",
        SenderId = "IntelliFin",
        CostPerSms = 0.04m,
        TimeoutSeconds = 30,
        IsActive = true,
        MaxRetries = 3
    };
    
    options[SmsProvider.Zamtel] = new SmsProviderSettings
    {
        ApiUrl = builder.Configuration["SmsProviders:Zamtel:ApiUrl"] ?? "https://api.zamtel.co.zm/sms",
        ApiKey = builder.Configuration["SmsProviders:Zamtel:ApiKey"] ?? "",
        Username = builder.Configuration["SmsProviders:Zamtel:Username"] ?? "",
        Password = builder.Configuration["SmsProviders:Zamtel:Password"] ?? "",
        SenderId = "IntelliFin",
        CostPerSms = 0.06m,
        TimeoutSeconds = 30,
        IsActive = false, // Currently not supported
        MaxRetries = 3
    };
});

// Configure SMS rate limiting
builder.Services.Configure<SmsRateLimitConfig>(builder.Configuration.GetSection("SmsRateLimit"));

// Configure HTTP clients with Polly policies
var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .WaitAndRetryAsync(3, retryAttempt => 
        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

var circuitBreakerPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));

var combinedPolicy = Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);

// Register HTTP clients for SMS providers
builder.Services.AddHttpClient<AirtelSmsProvider>("AirtelSms")
    .AddPolicyHandler(combinedPolicy);

builder.Services.AddHttpClient<MtnSmsProvider>("MtnSms")
    .AddPolicyHandler(combinedPolicy);

// Register SMS services
builder.Services.AddScoped<IAirtelSmsProvider, AirtelSmsProvider>();
builder.Services.AddScoped<IMtnSmsProvider, MtnSmsProvider>();
builder.Services.AddScoped<ISmsService, SmsService>();
builder.Services.AddScoped<ISmsTemplateService, SmsTemplateService>();
builder.Services.AddScoped<INotificationWorkflowService, NotificationWorkflowService>();

// Configure MassTransit with the consumer
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<LoanApplicationCreatedConsumer>();
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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new { name = "IntelliFin.Communications", status = "OK" }));

app.Run();
