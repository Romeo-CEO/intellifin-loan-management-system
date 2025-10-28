using IntelliFin.Shared.Observability;
using IntelliFin.Shared.Audit;
using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.TreasuryService.Data;
using IntelliFin.TreasuryService.Services;
using IntelliFin.TreasuryService.Options;
using IntelliFin.TreasuryService.Contracts;
using IntelliFin.TreasuryService.Infrastructure;
using IntelliFin.TreasuryService.Clients;
using IntelliFin.TreasuryService.Extensions;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using Polly;
using Polly.Extensions.Http;
using Microsoft.Extensions.Options;
using Minio;
using VaultSharp;
using VaultSharp.V1.AuthMethods;
using VaultSharp.V1.AuthMethods.Token;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetryInstrumentation(builder.Configuration);

// Add services to the container
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddControllers();

// Configure TreasuryDbContext with connection string from appsettings
builder.Services.AddDbContext<TreasuryDbContext>((sp, options) =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
        "Server=MOFIN-MFL0320\\SQLEXPRESS;Database=IntelliFinLms_Dev;Trusted_Connection=true;MultipleActiveResultSets=true;TrustServerCertificate=true;";
    options.UseSqlServer(connectionString);
});

// Add Redis distributed cache for real-time balance caching
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    options.InstanceName = "IntelliFin.TreasuryService";
});

// Add repositories
builder.Services.AddScoped<ITreasuryTransactionRepository, TreasuryTransactionRepository>();
builder.Services.AddScoped<IBranchFloatRepository, BranchFloatRepository>();
builder.Services.AddScoped<IReconciliationRepository, ReconciliationRepository>();

// Add Treasury services
builder.Services.AddScoped<ITreasuryService, TreasuryService>();
builder.Services.AddScoped<IBranchFloatService, BranchFloatService>();
builder.Services.AddScoped<IReconciliationService, ReconciliationService>();
builder.Services.AddScoped<ILoanDisbursementService, LoanDisbursementService>();

// Add disbursement-specific services
builder.Services.AddScoped<IFundingValidationService, FundingValidationService>();
builder.Services.AddScoped<IBankingApiService, BankingApiService>();
builder.Services.AddScoped<IIdempotencyService, IdempotencyService>();
builder.Services.AddScoped<IDisbursementAuditService, DisbursementAuditService>();

// Add event handlers
builder.Services.AddScoped<IntelliFin.TreasuryService.Contracts.IDomainEventHandler<IntelliFin.TreasuryService.Events.LoanDisbursementRequestedEvent>, IntelliFin.TreasuryService.Services.LoanDisbursementEventHandler>();

// Add Vault integration
builder.Services.AddSingleton<IVaultSecretResolver, VaultSecretResolver>();

// Add MinIO integration for document storage
builder.Services.AddSingleton<IMinioClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<MinioOptions>>().Value;
    var secretResolver = sp.GetRequiredService<IVaultSecretResolver>();
    var credentials = secretResolver.GetMinioCredentialsAsync(CancellationToken.None).GetAwaiter().GetResult();

    // Use IMinioClient throughout to avoid casting issues
    IMinioClient client = new MinioClient();

    if (!string.IsNullOrWhiteSpace(options.Endpoint))
    {
        if (Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var uri))
        {
            var port = uri.IsDefaultPort ? (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? 443 : 80) : uri.Port;
            client = client.WithEndpoint(uri.Host, port);
        }
        else if (options.Endpoint.Contains(':', StringComparison.Ordinal))
        {
            var parts = options.Endpoint.Split(':', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && int.TryParse(parts[1], out var port))
            {
                client = client.WithEndpoint(parts[0], port);
            }
            else
            {
                client = client.WithEndpoint(options.Endpoint);
            }
        }
        else
        {
            client = client.WithEndpoint(options.Endpoint);
        }
    }

    var accessKey = string.IsNullOrWhiteSpace(options.AccessKey) ? credentials.AccessKey : options.AccessKey;
    var secretKey = string.IsNullOrWhiteSpace(options.SecretKey) ? credentials.SecretKey : options.SecretKey;
    client = client.WithCredentials(accessKey, secretKey);

    if (options.UseSsl)
    {
        client = client.WithSSL();
    }

    if (!string.IsNullOrWhiteSpace(options.Region))
    {
        client = client.WithRegion(options.Region);
    }

    return client;
});

// Add Vault client
builder.Services.AddSingleton<IVaultClient>(sp =>
{
    var vaultOptions = sp.GetRequiredService<IOptions<VaultOptions>>().Value;
    var authMethod = (IAuthMethodInfo)new TokenAuthMethodInfo(vaultOptions.Token ?? string.Empty);
    var settings = new VaultClientSettings(vaultOptions.Address, authMethod)
    {
        Namespace = string.IsNullOrWhiteSpace(vaultOptions.Namespace) ? null : vaultOptions.Namespace,
        VaultServiceTimeout = TimeSpan.FromSeconds(Math.Clamp(vaultOptions.TimeoutSeconds, 5, 120))
    };

    return new VaultClient(settings);
});

// Forward audit writes to Admin Service
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

// Configure MassTransit for messaging
builder.Services.AddTreasuryMassTransit(builder.Configuration);

// Configure options
builder.Services.Configure<VaultOptions>(builder.Configuration.GetSection(VaultOptions.SectionName));
builder.Services.Configure<MinioOptions>(builder.Configuration.GetSection(MinioOptions.SectionName));

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
    name = "IntelliFin.TreasuryService",
    status = "OK",
    description = "Treasury & Branch Operations Service - Financial Control Center"
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
                Console.WriteLine($"TreasuryService retry {retryCount} after {timespan}s");
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
                Console.WriteLine($"TreasuryService circuit breaker opened for {timespan}");
            },
            onReset: () =>
            {
                Console.WriteLine("TreasuryService circuit breaker reset");
            });
}

public partial class Program { }
