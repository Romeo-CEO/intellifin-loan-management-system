using IntelliFin.KycDocumentService.Services;
using IntelliFin.Shared.DomainModels.Data;
using Microsoft.EntityFrameworkCore;
using Minio;
using Serilog;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Azure.Identity;
using IntelliFin.Shared.Observability;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/kycdocumentservice-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Starting IntelliFin KYC Document Service");

    var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetryInstrumentation(builder.Configuration);

    // Add Serilog
    builder.Host.UseSerilog();

    // Add services
    builder.Services.AddControllers();
    builder.Services.AddOpenApi();
    builder.Services.AddHealthChecks();

    // Add MinIO
    var minioConfig = builder.Configuration.GetSection("MinIO");
    builder.Services.AddMinio(configureClient => configureClient
        .WithEndpoint(minioConfig.GetValue<string>("Endpoint") ?? "localhost:9000")
        .WithCredentials(
            minioConfig.GetValue<string>("AccessKey") ?? "minioadmin",
            minioConfig.GetValue<string>("SecretKey") ?? "minioadmin")
        .WithSSL(minioConfig.GetValue<bool>("UseSSL")));

    // Add Entity Framework
    builder.Services.AddDbContext<LmsDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection") ?? 
            "Server=(localdb)\\mssqllocaldb;Database=IntelliFin_LoanManagement;Trusted_Connection=true;MultipleActiveResultSets=true"));

    // Add Azure Document Intelligence
    var azureOcrConfig = builder.Configuration.GetSection("AzureOcr");
    var endpoint = azureOcrConfig.GetValue<string>("Endpoint");
    var apiKey = azureOcrConfig.GetValue<string>("ApiKey");
    var useSystemIdentity = azureOcrConfig.GetValue<bool>("UseSystemIdentity");

    if (!string.IsNullOrEmpty(endpoint))
    {
        if (useSystemIdentity)
        {
            builder.Services.AddSingleton(sp => new DocumentAnalysisClient(
                new Uri(endpoint), 
                new DefaultAzureCredential()));
        }
        else if (!string.IsNullOrEmpty(apiKey))
        {
            builder.Services.AddSingleton(sp => new DocumentAnalysisClient(
                new Uri(endpoint), 
                new Azure.AzureKeyCredential(apiKey)));
        }
        else
        {
            Log.Warning("Azure OCR endpoint configured but no authentication method provided");
        }
    }

    // Add application services
    builder.Services.AddScoped<IDocumentStorageService, MinioDocumentStorageService>();
    builder.Services.AddScoped<IAzureOcrService, AzureOcrService>();
    builder.Services.AddScoped<IDocumentValidationService, DocumentValidationService>();
    builder.Services.AddScoped<IKycDocumentService, KycDocumentService>();
    builder.Services.AddScoped<IKycWorkflowService, KycWorkflowService>();
    
    // JWT Authentication
    var jwtConfig = builder.Configuration.GetSection("Jwt");
    builder.Services.AddAuthentication("Bearer")
        .AddJwtBearer("Bearer", options =>
        {
            options.Authority = jwtConfig.GetValue<string>("Authority");
            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateAudience = false
            };
        });

    builder.Services.AddAuthorization();

    // CORS
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("KycDocumentService", builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });
    });

    var app = builder.Build();

    // Configure the HTTP request pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
        app.MapOpenApi();
    }

    app.UseHttpsRedirection();
    app.UseCors("KycDocumentService");

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health");

    app.MapGet("/", () => Results.Ok(new { 
        name = "IntelliFin.KycDocumentService", 
        status = "OK",
        version = "1.0.0",
        timestamp = DateTime.UtcNow 
    }));

    Log.Information("IntelliFin KYC Document Service configured successfully");

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "IntelliFin KYC Document Service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}