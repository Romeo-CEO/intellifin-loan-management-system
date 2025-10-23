using System.Net.Http;
using System.Reflection;
using FluentValidation;
using FluentValidation.AspNetCore;
using IntelliFin.CreditAssessmentService.Extensions;
using IntelliFin.CreditAssessmentService.Infrastructure.External;
using IntelliFin.CreditAssessmentService.Infrastructure.Messaging;
using IntelliFin.CreditAssessmentService.Infrastructure.Persistence;
using IntelliFin.CreditAssessmentService.Options;
using IntelliFin.CreditAssessmentService.Services;
using IntelliFin.CreditAssessmentService.Services.Interfaces;
using IntelliFin.CreditAssessmentService.Validators;
using IntelliFin.CreditAssessmentService.Workflows.CamundaWorkers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Polly;
using Polly.Extensions.Http;
using Prometheus;
using Serilog;
using Microsoft.Extensions.Options;
using Zeebe.Client;
using Zeebe.Client.Api.Builder;

const string ServiceName = "IntelliFin.CreditAssessmentService";
const string CorsPolicyName = "DefaultCorsPolicy";

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithEnvironmentName()
        .Enrich.WithMachineName()
        .Enrich.WithProcessId()
        .Enrich.WithThreadId()
        .WriteTo.Console();
});

builder.Services.AddOpenTelemetryInstrumentation(builder.Configuration);

builder.Services.Configure<VaultRuleOptions>(builder.Configuration.GetSection(VaultRuleOptions.SectionName));
builder.Services.Configure<TransUnionOptions>(builder.Configuration.GetSection(TransUnionOptions.SectionName));
builder.Services.Configure<PmecOptions>(builder.Configuration.GetSection(PmecOptions.SectionName));
builder.Services.Configure<ClientManagementOptions>(builder.Configuration.GetSection(ClientManagementOptions.SectionName));
builder.Services.Configure<FeatureFlagOptions>(builder.Configuration.GetSection(FeatureFlagOptions.SectionName));

builder.Services.AddMemoryCache();

builder.Services.AddHttpClient("vault", client =>
    {
        var vaultAddress = builder.Configuration[$"{VaultRuleOptions.SectionName}:Address"] ?? "http://vault:8200";
        client.BaseAddress = new Uri(vaultAddress);
    })
    .AddPolicyHandler(GetResiliencePolicy());

builder.Services.AddHttpClient<ITransUnionClient, TransUnionClient>((sp, client) =>
    {
        var options = sp.GetRequiredService<IOptions<TransUnionOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl);
        client.Timeout = options.Timeout;
    })
    .AddPolicyHandler(GetResiliencePolicy());

builder.Services.AddHttpClient<IPmecClient, PmecClient>((sp, client) =>
    {
        var options = sp.GetRequiredService<IOptions<PmecOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl);
        client.Timeout = options.Timeout;
        client.DefaultRequestHeaders.Add("x-api-key", options.ApiKey);
    })
    .AddPolicyHandler(GetResiliencePolicy());

builder.Services.AddHttpClient<IClientManagementClient, ClientManagementClient>((sp, client) =>
    {
        var options = sp.GetRequiredService<IOptions<ClientManagementOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl);
        client.Timeout = options.Timeout;
    })
    .AddPolicyHandler(GetResiliencePolicy());

builder.Services.AddProblemDetails();

builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

    options.AddPolicy(CorsPolicyName, policy =>
    {
        if (allowedOrigins.Length == 0)
        {
            policy.AllowAnyOrigin();
        }
        else
        {
            policy.WithOrigins(allowedOrigins).AllowCredentials();
        }

        policy.AllowAnyHeader();
        policy.AllowAnyMethod();
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Authentication:Authority"];
        options.Audience = builder.Configuration["Authentication:Audience"];
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    });

builder.Services.AddAuthorization();

builder.Services.AddDbContext<CreditAssessmentDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("CreditAssessmentDatabase"), npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), null);
    });
});

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "live" })
    .AddNpgSql(
        connectionString: builder.Configuration.GetConnectionString("CreditAssessmentDatabase"),
        name: "postgres",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "ready" });

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<AssessmentRequestHandler>();
    x.AddConsumer<KycStatusEventHandler>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitMqSection = builder.Configuration.GetSection("RabbitMq");
        var host = rabbitMqSection["Host"] ?? "localhost";
        var port = rabbitMqSection.GetValue("Port", 35672);
        var virtualHost = rabbitMqSection["VirtualHost"] ?? "/";
        cfg.Host(host, port, virtualHost, h =>
        {
            h.Username(rabbitMqSection["Username"] ?? "guest");
            h.Password(rabbitMqSection["Password"] ?? "guest");
        });

        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddSingleton<IVaultConfigService, VaultConfigService>();
builder.Services.AddSingleton<IRuleEngine, VaultRuleEngine>();
builder.Services.AddScoped<ICreditAssessmentService, CreditAssessmentService>();
builder.Services.AddSingleton<IExplainabilityService, ExplainabilityService>();
builder.Services.AddScoped<IAuditTrailPublisher, AuditTrailPublisher>();

builder.Services.AddScoped<CreditAssessmentRequestValidator>();
builder.Services.AddScoped<ManualOverrideRequestValidator>();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

builder.Services.AddHostedService<AssessmentWorker>();
builder.Services.AddHostedService<OverrideWorker>();

builder.Services.AddSingleton<IZeebeClient>(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    var gatewayAddress = configuration["Zeebe:GatewayAddress"];
    var builder = ZeebeClient.Builder();

    if (!string.IsNullOrWhiteSpace(gatewayAddress))
    {
        builder = builder.UseGatewayAddress(gatewayAddress);
        if (!string.IsNullOrWhiteSpace(configuration["Zeebe:ClientId"]))
        {
            builder = builder.UseOAuthCredentials(new OAuthCredentials
            {
                Audience = configuration["Zeebe:Audience"],
                ClientId = configuration["Zeebe:ClientId"],
                ClientSecret = configuration["Zeebe:ClientSecret"],
                AuthorizationServerUrl = configuration["Zeebe:AuthorizationServerUrl"]
            });
        }
        else
        {
            builder = builder.UsePlainText();
        }
    }
    else
    {
        builder = builder.UseGatewayAddress("localhost:26500").UsePlainText();
    }

    return builder.Build();
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddControllers();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseExceptionHandler();
}

app.UseSerilogRequestLogging();

app.UseHttpsRedirection();

app.UseRouting();

app.UseHttpMetrics();
app.UseCors(CorsPolicyName);
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/", () => Results.Ok(new
{
    name = ServiceName,
    version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0",
    status = "Healthy"
}));

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live"),
    ResponseWriter = HealthCheckResponseWriter.WriteMinimalResponse
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteDetailedResponse
});

app.MapMetrics("/metrics");

app.Run();

static IAsyncPolicy<HttpResponseMessage> GetResiliencePolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(response => (int)response.StatusCode == 429)
        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
}

public partial class Program;
