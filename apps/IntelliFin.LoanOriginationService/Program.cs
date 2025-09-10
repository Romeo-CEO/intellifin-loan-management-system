using IntelliFin.LoanOriginationService.Services;
using IntelliFin.LoanOriginationService.Workers;
using IntelliFin.Shared.Infrastructure.Messaging;
using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.Shared.DomainModels.Repositories;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using Zeebe.Client;
using Zeebe.Client.Api.Builder;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
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

// Add loan origination services
builder.Services.AddScoped<ILoanApplicationService, LoanApplicationService>();
builder.Services.AddScoped<ICreditAssessmentService, CreditAssessmentService>();
builder.Services.AddScoped<ILoanProductService, LoanProductService>();
builder.Services.AddScoped<IWorkflowService, WorkflowService>();
builder.Services.AddScoped<IRiskCalculationEngine, RiskCalculationEngine>();
builder.Services.AddScoped<IComplianceService, ComplianceService>();
builder.Services.AddScoped<IDocumentIntelligenceService, DocumentIntelligenceService>();

// Add Zeebe client for Camunda 8 integration
builder.Services.AddSingleton<IZeebeClient>(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    return ZeebeClient.Builder()
        .UseGatewayAddress(configuration["Zeebe:GatewayAddress"])
        .UseOAuthCredentials(new OAuthCredentials
        {
            AuthorizationServerUrl = configuration["Zeebe:AuthorizationServerUrl"],
            Audience = configuration["Zeebe:Audience"],
            ClientId = configuration["Zeebe:ClientId"],
            ClientSecret = configuration["Zeebe:ClientSecret"]
        })
        .Build();
});

// Add InitialValidationWorker as hosted service
builder.Services.AddHostedService<InitialValidationWorker>();

// Add HTTP client for external services (Camunda, etc.)
builder.Services.AddHttpClient();

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
    name = "IntelliFin.LoanOriginationService", 
    status = "OK",
    description = "Comprehensive Loan Origination with Credit Assessment and Workflow Management",
    version = "1.0.0"
}));

app.Run();
