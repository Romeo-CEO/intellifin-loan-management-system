using IntelliFin.LoanOriginationService.Services;
using IntelliFin.LoanOriginationService.Workers;
using IntelliFin.Shared.Infrastructure.Messaging;
using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.Shared.DomainModels.Repositories;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using Zeebe.Client;
using Zeebe.Client.Api.Builder;
using IntelliFin.Shared.Observability;
using Serilog;
using Serilog.Events;
using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.AuthMethods;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog for logging
    builder.Host.UseSerilog((context, services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");
    });

    // Add OpenTelemetry instrumentation
    builder.Services.AddOpenTelemetryInstrumentation(builder.Configuration);

    // Add services to the container
    builder.Services.AddOpenApi();
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<LmsDbContext>("database");
    
    builder.Services.AddControllers();

    // Add Entity Framework with SQL Server
    builder.Services.AddDbContext<LmsDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection") ?? 
            "Server=(localdb)\\mssqllocaldb;Database=IntelliFin_LoanManagement;Trusted_Connection=true;MultipleActiveResultSets=true"));

    // Add repositories
    builder.Services.AddScoped<ILoanApplicationRepository, LoanApplicationRepository>();
    builder.Services.AddScoped<ILoanProductRepository, LoanProductRepository>();
    builder.Services.AddScoped<ICreditAssessmentRepository, CreditAssessmentRepository>();
    builder.Services.AddScoped<IGLAccountRepository, GLAccountRepository>();
    builder.Services.AddScoped<IGLEntryRepository, GLEntryRepository>();
    builder.Services.AddScoped<IDocumentVerificationRepository, DocumentVerificationRepository>();

    // Configure Vault client (optional - if not configured, service falls back to in-memory)
    var vaultAddress = builder.Configuration["Vault:Address"];
    var vaultToken = builder.Configuration["Vault:Token"];
    
    if (!string.IsNullOrEmpty(vaultAddress) && !string.IsNullOrEmpty(vaultToken))
    {
        builder.Services.AddSingleton<IVaultClient>(provider =>
        {
            var authMethod = new TokenAuthMethodInfo(vaultToken);
            var vaultClientSettings = new VaultClientSettings(vaultAddress, authMethod);
            return new VaultClient(vaultClientSettings);
        });
        builder.Services.AddScoped<IVaultProductConfigService, VaultProductConfigService>();
        builder.Services.AddMemoryCache(); // For Vault config caching
    }
    else
    {
        Log.Warning("Vault not configured - LoanProductService will use in-memory configuration");
    }

    // Add HTTP context accessor for IP address extraction in audit trails
    builder.Services.AddHttpContextAccessor();
    
    // Add loan origination services
    builder.Services.AddScoped<ILoanApplicationService, LoanApplicationService>();
    builder.Services.AddScoped<ILoanVersioningService, LoanVersioningService>();
    builder.Services.AddScoped<ICreditAssessmentService, CreditAssessmentService>();
    builder.Services.AddScoped<ILoanProductService, LoanProductService>();
    builder.Services.AddScoped<IWorkflowService, WorkflowService>();
    builder.Services.AddScoped<IRiskCalculationEngine, RiskCalculationEngine>();
    builder.Services.AddScoped<IComplianceService, ComplianceService>();
    builder.Services.AddScoped<IDocumentIntelligenceService, DocumentIntelligenceService>();
    
    // Add dual control validator for segregation of duties enforcement (Story 1.7)
    builder.Services.AddScoped<IDualControlValidator, DualControlValidator>();
    
    // Configure MinIO client for document storage (Story 1.8)
    var minioEndpoint = builder.Configuration["MinIO:Endpoint"];
    var minioAccessKey = builder.Configuration["MinIO:AccessKey"];
    var minioSecretKey = builder.Configuration["MinIO:SecretKey"];
    var minioUseSSL = bool.Parse(builder.Configuration["MinIO:UseSSL"] ?? "false");
    
    if (!string.IsNullOrEmpty(minioEndpoint) && !string.IsNullOrEmpty(minioAccessKey) && !string.IsNullOrEmpty(minioSecretKey))
    {
        builder.Services.AddSingleton<Minio.IMinioClient>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Configuring MinIO client for endpoint: {MinioEndpoint}", minioEndpoint);
            
            var client = new Minio.MinioClient()
                .WithEndpoint(minioEndpoint)
                .WithCredentials(minioAccessKey, minioSecretKey);
            
            if (minioUseSSL)
            {
                client = client.WithSSL();
            }
            
            logger.LogInformation("MinIO client configured successfully");
            return client.Build();
        });
        
        Log.Information("MinIO client registered for endpoint: {MinioEndpoint}", minioEndpoint);
    }
    else
    {
        Log.Warning("MinIO not configured - Agreement generation will not be available");
    }
    
    // Configure JasperReports HttpClient for agreement generation (Story 1.8)
    var jasperBaseUrl = builder.Configuration["JasperReports:BaseUrl"];
    var jasperUsername = builder.Configuration["JasperReports:Username"];
    var jasperPassword = builder.Configuration["JasperReports:Password"];
    
    if (!string.IsNullOrEmpty(jasperBaseUrl))
    {
        builder.Services.AddHttpClient<IAgreementGenerationService, AgreementGenerationService>(client =>
        {
            client.BaseAddress = new Uri(jasperBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(10); // 10-second timeout for JasperReports
            
            // Add Basic Authentication if credentials provided
            if (!string.IsNullOrEmpty(jasperUsername) && !string.IsNullOrEmpty(jasperPassword))
            {
                var authValue = Convert.ToBase64String(
                    System.Text.Encoding.UTF8.GetBytes($"{jasperUsername}:{jasperPassword}"));
                client.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);
            }
        });
        
        Log.Information("JasperReports HttpClient registered for: {JasperBaseUrl}", jasperBaseUrl);
    }
    else
    {
        Log.Warning("JasperReports not configured - Agreement generation will not be available");
    }

    // Configure Zeebe client for Camunda 8 integration
    var zeebeConfig = builder.Configuration.GetSection("Zeebe");
    var gatewayAddress = zeebeConfig["GatewayAddress"];
    var clientId = zeebeConfig["ClientId"];
    var clientSecret = zeebeConfig["ClientSecret"];
    var authorizationServerUrl = zeebeConfig["AuthorizationServerUrl"];
    var audience = zeebeConfig["Audience"];

    if (!string.IsNullOrEmpty(gatewayAddress) && !string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(clientSecret))
    {
        builder.Services.AddSingleton<IZeebeClient>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Configuring Zeebe client for gateway: {GatewayAddress}", gatewayAddress);

            var client = Zeebe.Client.ZeebeClient.Builder()
                .UseGatewayAddress(gatewayAddress)
                .UseTransportEncryption()
                .UseAccessTokenSupplier(
                    Zeebe.Client.CamundaCloudTokenProvider.Builder()
                        .UseAuthServer(authorizationServerUrl!)
                        .UseClientId(clientId)
                        .UseClientSecret(clientSecret)
                        .UseAudience(audience!)
                        .Build()
                )
                .Build();

            logger.LogInformation("Zeebe client configured successfully");
            return client;
        });

        // Register BPMN deployment service to deploy workflows on startup
        builder.Services.AddHostedService<BpmnDeploymentService>();
        
        // Register KYC Verification Worker to process verify-kyc jobs
        builder.Services.AddHostedService<KycVerificationWorker>();
        
        // Register Agreement Generation Worker to process generate-agreement jobs (Story 1.8)
        if (!string.IsNullOrEmpty(jasperBaseUrl) && !string.IsNullOrEmpty(minioEndpoint))
        {
            builder.Services.AddHostedService<GenerateAgreementWorker>();
            Log.Information("Agreement Generation Worker registered");
        }
        
        Log.Information("Zeebe client, BPMN deployment service, and job workers registered");
    }
    else
    {
        Log.Warning("Zeebe not configured - Workflow orchestration will not be available");
    }

    // Add HTTP client for external services (Camunda, etc.)
    builder.Services.AddHttpClient();
    
    // Configure Client Management Service HTTP client with resilience policies
    var clientManagementBaseUrl = builder.Configuration["ClientManagementService:BaseUrl"];
    if (!string.IsNullOrEmpty(clientManagementBaseUrl))
    {
        builder.Services.AddHttpClient<IClientManagementClient, ClientManagementClient>(client =>
        {
            client.BaseAddress = new Uri(clientManagementBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(35); // Slightly higher than resilience timeout
        })
        .AddStandardResilienceHandler(options =>
        {
            // Retry policy: 3 attempts with exponential backoff
            options.Retry.MaxRetryAttempts = 3;
            options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
            options.Retry.UseJitter = true;
            options.Retry.Delay = TimeSpan.FromSeconds(2);
            options.Retry.OnRetry = args =>
            {
                Log.Warning(
                    "Client Management Service retry attempt {RetryAttempt} after {Delay}s",
                    args.AttemptNumber,
                    args.RetryDelay.TotalSeconds);
                return ValueTask.CompletedTask;
            };

            // Circuit breaker: open after 5 consecutive failures, half-open after 60s
            options.CircuitBreaker.FailureRatio = 0.5;
            options.CircuitBreaker.MinimumThroughput = 5;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(60);

            // Timeout: 30 seconds per attempt
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
        });
                
        Log.Information("Client Management Service integration enabled: {BaseUrl}", clientManagementBaseUrl);
    }
    else
    {
        Log.Warning("Client Management Service not configured - KYC verification will fail for new loan applications");
    }

    // Configure MassTransit for messaging
    builder.Services.AddMassTransit(x =>
    {
        // Register event consumers for ClientManagement integration
        x.AddConsumer<IntelliFin.LoanOriginationService.Consumers.ClientKycApprovedEventConsumer>();
        x.AddConsumer<IntelliFin.LoanOriginationService.Consumers.ClientKycRevokedEventConsumer>();
        x.AddConsumer<IntelliFin.LoanOriginationService.Consumers.ClientProfileUpdatedEventConsumer>();
        
        // Register ClientKycRevoked consumer from Story 1.6
        x.AddConsumer<IntelliFin.LoanOriginationService.Consumers.ClientKycRevokedConsumer>();
        
        x.SetKebabCaseEndpointNameFormatter();

        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(builder.Configuration.GetConnectionString("RabbitMQ") ?? "localhost", 5672, "/", h =>
            {
                h.Username("guest");
                h.Password("guest");
            });
            
            // Configure dedicated receive endpoint for KYC revoked events with retry policy
            cfg.ReceiveEndpoint("loan-origination-kyc-revoked", e =>
            {
                e.ConfigureConsumer<IntelliFin.LoanOriginationService.Consumers.ClientKycRevokedConsumer>(context);
                e.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(5)));
            });

            cfg.ConfigureEndpoints(context);
        });
    });

    var app = builder.Build();

    // Configure the HTTP request pipeline
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseHttpsRedirection();
    app.UseRouting();

    // Map health check endpoints
    app.MapHealthChecks("/health");
    app.MapHealthChecks("/health/db", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("database")
    });

    app.MapControllers();

    app.MapGet("/", () => Results.Ok(new { 
        name = "IntelliFin.LoanOriginationService", 
        status = "OK",
        description = "Comprehensive Loan Origination with Credit Assessment and Workflow Management",
        version = "1.0.0"
    })).WithName("GetServiceInfo").WithOpenApi();

    Log.Information("IntelliFin.LoanOriginationService starting up");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Partial class for WebApplicationFactory testing
public partial class Program { }