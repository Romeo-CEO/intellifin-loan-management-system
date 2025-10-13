using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Security.Claims;
using IntelliFin.AdminService.Data;
using IntelliFin.AdminService.Contracts.Requests;
using IntelliFin.AdminService.Contracts.Responses;
using IntelliFin.AdminService.ExceptionHandling;
using IntelliFin.AdminService.HealthChecks;
using IntelliFin.AdminService.Models;
using IntelliFin.AdminService.Options;
using IntelliFin.AdminService.Services;
using IntelliFin.AdminService.Utilities;
using IntelliFin.AdminService.Jobs;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using Prometheus;
using IntelliFin.Shared.Observability;
using Microsoft.AspNetCore.Mvc;
using Minio;
using VaultSharp;
using VaultSharp.V1.AuthMethods;
using VaultSharp.V1.AuthMethods.Token;
using Quartz;
using IntelliFin.Shared.DomainModels.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetryInstrumentation(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<KeycloakExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "IntelliFin Admin Service",
        Version = "v1",
        Description = "Control plane orchestration hub for IntelliFin platform"
    });
});

builder.Services.Configure<KeycloakOptions>(builder.Configuration.GetSection("Keycloak"));
builder.Services.Configure<AuditIngestionOptions>(builder.Configuration.GetSection(AuditIngestionOptions.SectionName));
builder.Services.Configure<AuditRabbitMqOptions>(builder.Configuration.GetSection(AuditRabbitMqOptions.SectionName));
builder.Services.Configure<AuditChainOptions>(builder.Configuration.GetSection(AuditChainOptions.SectionName));
builder.Services.Configure<AuditArchiveOptions>(builder.Configuration.GetSection(AuditArchiveOptions.SectionName));
builder.Services.Configure<MinioOptions>(builder.Configuration.GetSection(MinioOptions.SectionName));
builder.Services.Configure<ElevationOptions>(builder.Configuration.GetSection(ElevationOptions.SectionName));
builder.Services.Configure<CamundaOptions>(builder.Configuration.GetSection(CamundaOptions.SectionName));
builder.Services.Configure<ConfigurationManagementOptions>(builder.Configuration.GetSection("ConfigurationManagement"));
builder.Services.Configure<VaultOptions>(builder.Configuration.GetSection(VaultOptions.SectionName));
builder.Services.Configure<ArgoCdOptions>(builder.Configuration.GetSection(ArgoCdOptions.SectionName));
builder.Services.Configure<SbomOptions>(builder.Configuration.GetSection(SbomOptions.SectionName));
builder.Services.Configure<BastionOptions>(builder.Configuration.GetSection(BastionOptions.SectionName));
builder.Services.Configure<IncidentResponseOptions>(builder.Configuration.GetSection(IncidentResponseOptions.SectionName));

builder.Services.AddDbContext<AdminDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Default")
                           ?? "Server=(localdb)\\mssqllocaldb;Database=IntelliFin_AdminService;Trusted_Connection=True;MultipleActiveResultSets=True";
    options.UseSqlServer(connectionString);
});

builder.Services.AddDbContext<FinancialDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("FinancialService")
                           ?? builder.Configuration.GetConnectionString("Default")
                           ?? "Server=(localdb)\\mssqllocaldb;Database=IntelliFin_FinancialService;Trusted_Connection=True;MultipleActiveResultSets=True";
    options.UseSqlServer(connectionString);
});

builder.Services.AddDbContextFactory<LmsDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("IdentityDb")
                           ?? builder.Configuration.GetConnectionString("Default")
                           ?? "Server=(localdb)\\mssqllocaldb;Database=IntelliFin_Identity;Trusted_Connection=True;MultipleActiveResultSets=True";
    options.UseSqlServer(connectionString);
});

builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();

builder.Services.AddHttpClient<IKeycloakTokenService, KeycloakTokenService>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptionsMonitor<KeycloakOptions>>().CurrentValue;
    client.BaseAddress = EnsureBaseAddress(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(15);
})
    .AddPolicyHandler(CreateRetryPolicy());

builder.Services.AddHttpClient<IKeycloakAdminService, KeycloakAdminService>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptionsMonitor<KeycloakOptions>>().CurrentValue;
    client.BaseAddress = EnsureBaseAddress(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
    .AddPolicyHandler(CreateRetryPolicy());

builder.Services.AddScoped<IAuditHashService, AuditHashService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IOfflineAuditMergeService, OfflineAuditMergeService>();
builder.Services.AddHostedService<AuditBufferFlushService>();
builder.Services.AddHostedService<AuditRabbitMqConsumer>();
builder.Services.AddHostedService<AuditChainVerificationService>();
builder.Services.AddScoped<AuditMigrationService>();
builder.Services.AddScoped<IAuditArchiveService, AuditArchiveService>();
builder.Services.AddHostedService<AuditArchiveExportWorker>();
builder.Services.AddHostedService<AuditArchiveReplicationMonitor>();
builder.Services.AddScoped<IAccessElevationService, AccessElevationService>();
builder.Services.AddScoped<IElevationNotificationService, ElevationNotificationService>();
builder.Services.AddHostedService<ElevationExpirationWorker>();
builder.Services.AddScoped<IMfaService, MfaService>();
builder.Services.AddScoped<IRoleManagementService, RoleManagementService>();
builder.Services.AddScoped<ISodExceptionService, SodExceptionService>();
builder.Services.AddScoped<IConfigurationDeployer, ConfigurationDeployer>();
builder.Services.AddScoped<IConfigurationManagementService, ConfigurationManagementService>();
builder.Services.AddScoped<IVaultManagementService, VaultManagementService>();
builder.Services.AddScoped<IManagerDirectoryService, KeycloakManagerDirectoryService>();
builder.Services.AddScoped<IRecertificationNotificationService, RecertificationNotificationService>();
builder.Services.AddScoped<IRecertificationService, RecertificationService>();
builder.Services.AddScoped<ISbomService, SbomService>();
builder.Services.AddScoped<IBastionAccessService, BastionAccessService>();
builder.Services.AddScoped<IIncidentResponseService, IncidentResponseService>();

builder.Services.AddHttpClient<ICamundaWorkflowService, CamundaWorkflowService>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptionsMonitor<CamundaOptions>>().CurrentValue;
    if (!string.IsNullOrWhiteSpace(options.BaseUrl))
    {
        client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
    }
    client.Timeout = TimeSpan.FromSeconds(15);
})
    .AddPolicyHandler(CreateRetryPolicy());

builder.Services.AddHttpClient<IAlertmanagerClient, AlertmanagerClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptionsMonitor<IncidentResponseOptions>>().CurrentValue;
    if (!string.IsNullOrWhiteSpace(options.AlertmanagerBaseUrl))
    {
        client.BaseAddress = new Uri(options.AlertmanagerBaseUrl, UriKind.Absolute);
    }

    client.Timeout = TimeSpan.FromSeconds(10);
})
    .AddPolicyHandler(CreateRetryPolicy());

builder.Services.AddHttpClient<IArgoCdIntegrationService, ArgoCdIntegrationService>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptionsMonitor<ArgoCdOptions>>().CurrentValue;
    if (!string.IsNullOrWhiteSpace(options.Url))
    {
        client.BaseAddress = new Uri(options.Url, UriKind.Absolute);
    }

    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 5, 600));
})
    .AddPolicyHandler(CreateRetryPolicy());

builder.Services.AddSingleton<IVaultClient>(sp =>
{
    var vaultOptions = sp.GetRequiredService<IOptions<VaultOptions>>().Value;
    var authMethod = (IAuthMethodInfo)new TokenAuthMethodInfo(vaultOptions.Token ?? string.Empty);
    var settings = new VaultClientSettings(vaultOptions.Address, authMethod)
    {
        Namespace = string.IsNullOrWhiteSpace(vaultOptions.Namespace) ? null : vaultOptions.Namespace
    };

    return new VaultClient(settings);
});

builder.Services.AddSingleton<IMinioClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<MinioOptions>>().Value;
    var client = new MinioClient();
    var endpoint = options.Endpoint?.Trim();
    var useSsl = options.UseSsl;

    if (!string.IsNullOrWhiteSpace(endpoint))
    {
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            var port = uri.IsDefaultPort ? (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? 443 : 80) : uri.Port;
            client = client.WithEndpoint(uri.Host, port);
            useSsl = useSsl || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        }
        else if (endpoint.Contains(':', StringComparison.Ordinal))
        {
            var parts = endpoint.Split(':', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && int.TryParse(parts[1], out var port))
            {
                client = client.WithEndpoint(parts[0], port);
            }
            else
            {
                client = client.WithEndpoint(endpoint);
            }
        }
        else
        {
            client = client.WithEndpoint(endpoint);
        }
    }

    client = client.WithCredentials(options.AccessKey, options.SecretKey);

    if (useSsl)
    {
        client = client.WithSSL();
    }

    if (!string.IsNullOrWhiteSpace(options.Region))
    {
        client = client.WithRegion(options.Region);
    }

    return client.Build();
});

builder.Services.AddQuartz(q =>
{
    q.UseMicrosoftDependencyInjectionJobFactory();
    q.ConfigureRecertification();
});

builder.Services.AddQuartzHostedService(options =>
{
    options.WaitForJobsToComplete = true;
});

builder.Services.AddHttpClient(nameof(KeycloakHealthCheck), static (sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<KeycloakOptions>>().Value;
    if (!string.IsNullOrWhiteSpace(options.BaseUrl))
    {
        client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
    }
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddHealthChecks()
    .AddCheck<KeycloakHealthCheck>("keycloak", tags: new[] { "ready" })
    .AddDbContextCheck<AdminDbContext>("sql", tags: new[] { "ready" });

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.UseRouting();
app.UseHttpMetrics();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

var applyMigrations = app.Configuration.GetValue("Database:ApplyMigrations", true);
if (applyMigrations)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("AdminService.Startup");
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
        dbContext.Database.Migrate();
        logger.LogInformation("Applied database migrations for Admin Service");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to apply Admin Service database migrations");
        if (!app.Environment.IsDevelopment())
        {
            throw;
        }
    }
}

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthCheckResponseWriter.WriteResponse
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteResponse
});

app.MapMetrics();

app.MapControllers();

var adminGroup = app.MapGroup("/api/admin");

var auditGroup = adminGroup.MapGroup("/audit");

var accessGroup = adminGroup.MapGroup("/access");

accessGroup.MapPost("/elevate", async (
    ElevationRequestDto request,
    HttpContext httpContext,
    IAccessElevationService elevationService,
    CancellationToken cancellationToken) =>
{
    var userId = httpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrWhiteSpace(userId))
    {
        return Results.Forbid();
    }

    try
    {
        var userName = httpContext.User?.Identity?.Name ?? userId;
        var response = await elevationService.RequestElevationAsync(userId, userName, request, cancellationToken);
        return Results.Accepted($"/api/admin/access/elevations/{response.ElevationId}", response);
    }
    catch (ValidationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

accessGroup.MapGet("/elevations/{elevationId:guid}", async (
    Guid elevationId,
    IAccessElevationService elevationService,
    CancellationToken cancellationToken) =>
{
    var status = await elevationService.GetElevationStatusAsync(elevationId, cancellationToken);
    return status is null ? Results.NotFound() : Results.Ok(status);
});

accessGroup.MapGet("/elevations", async (
    string? filter,
    int page,
    int pageSize,
    HttpContext httpContext,
    IAccessElevationService elevationService,
    CancellationToken cancellationToken) =>
{
    var userId = httpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrWhiteSpace(userId))
    {
        return Results.Forbid();
    }

    var result = await elevationService.ListElevationsAsync(userId, filter, page <= 0 ? 1 : page, pageSize <= 0 ? 50 : pageSize, cancellationToken);
    return Results.Ok(result);
});

accessGroup.MapPost("/elevations/{elevationId:guid}/approve", async (
    Guid elevationId,
    ElevationApprovalDto request,
    HttpContext httpContext,
    IAccessElevationService elevationService,
    CancellationToken cancellationToken) =>
{
    var managerId = httpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrWhiteSpace(managerId))
    {
        return Results.Forbid();
    }

    var managerName = httpContext.User?.Identity?.Name ?? managerId;

    try
    {
        await elevationService.ApproveElevationAsync(elevationId, managerId, managerName, request.ApprovedDuration, cancellationToken);
        return Results.Ok(new { message = "Elevation approved successfully" });
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
    catch (ValidationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Forbid();
    }
});

accessGroup.MapPost("/elevations/{elevationId:guid}/reject", async (
    Guid elevationId,
    ElevationRejectionDto request,
    HttpContext httpContext,
    IAccessElevationService elevationService,
    CancellationToken cancellationToken) =>
{
    var managerId = httpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrWhiteSpace(managerId))
    {
        return Results.Forbid();
    }

    var managerName = httpContext.User?.Identity?.Name ?? managerId;

    try
    {
        await elevationService.RejectElevationAsync(elevationId, managerId, managerName, request.Reason, cancellationToken);
        return Results.Ok(new { message = "Elevation rejected successfully" });
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Forbid();
    }
});

accessGroup.MapPost("/revoke/{elevationId:guid}", async (
    Guid elevationId,
    ElevationRevocationDto request,
    HttpContext httpContext,
    IAccessElevationService elevationService,
    CancellationToken cancellationToken) =>
{
    var adminId = httpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrWhiteSpace(adminId))
    {
        return Results.Forbid();
    }

    var adminName = httpContext.User?.Identity?.Name ?? adminId;

    try
    {
        await elevationService.RevokeElevationAsync(elevationId, adminId, adminName, request.Reason, cancellationToken);
        return Results.Ok(new { message = "Elevation revoked successfully" });
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

accessGroup.MapGet("/elevated-sessions", async (
    IAccessElevationService elevationService,
    CancellationToken cancellationToken) =>
{
    var sessions = await elevationService.GetActiveSessionsAsync(cancellationToken);
    return Results.Ok(sessions);
});

auditGroup.MapPost("/events", async (
    [FromBody] AuditEventRequest request,
    HttpContext httpContext,
    IAuditService auditService,
    CancellationToken cancellationToken) =>
{
    if (!TryValidate(request, out var errors))
    {
        return Results.ValidationProblem(errors);
    }

    var auditEvent = FromRequest(request, httpContext);
    await auditService.LogEventAsync(auditEvent, cancellationToken);
    await auditService.FlushBufferAsync(cancellationToken);

    return Results.Accepted($"/api/admin/audit/events/{auditEvent.EventId}", new { auditEvent.EventId });
})
.WithName("CreateAuditEvent")
.WithOpenApi()
.Produces(StatusCodes.Status202Accepted)
.ProducesValidationProblem();

auditGroup.MapPost("/events/batch", async (
    [FromBody] AuditEventBatchRequest request,
    HttpContext httpContext,
    IAuditService auditService,
    CancellationToken cancellationToken) =>
{
    if (!TryValidate(request, out var errors))
    {
        return Results.ValidationProblem(errors);
    }

    var events = request.Events.Select(evt => FromRequest(evt, httpContext)).ToList();
    var inserted = await auditService.LogEventsBatchAsync(events, cancellationToken);

    return Results.Accepted($"/api/admin/audit/events/batch", new { insertedCount = inserted });
})
.WithName("CreateAuditEventBatch")
.WithOpenApi()
.Produces(StatusCodes.Status202Accepted)
.ProducesValidationProblem();

auditGroup.MapGet("/events", async (
    [AsParameters] AuditQueryRequest query,
    IAuditService auditService,
    CancellationToken cancellationToken) =>
{
    var filter = new AuditEventFilter
    {
        StartDate = query.StartDate ?? DateTime.UtcNow.AddDays(-30),
        EndDate = query.EndDate ?? DateTime.UtcNow,
        Actor = query.Actor,
        Action = query.Action,
        EntityType = query.EntityType,
        EntityId = query.EntityId,
        CorrelationId = query.CorrelationId,
        Page = query.Page,
        PageSize = query.PageSize
    };

    var result = await auditService.GetAuditEventsAsync(filter, cancellationToken);

    return Results.Ok(new AuditEventPageResponse
    {
        Data = result.Events.Select(ToResponse).ToList(),
        Pagination = new PaginationMetadata
        {
            CurrentPage = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
            TotalPages = (int)Math.Ceiling(result.TotalCount / (double)result.PageSize)
        }
    });
})
.WithName("GetAuditEvents")
.WithOpenApi()
.Produces<AuditEventPageResponse>(StatusCodes.Status200OK);

auditGroup.MapGet("/events/export", async (
    DateTime? startDate,
    DateTime? endDate,
    string format,
    IAuditService auditService,
    CancellationToken cancellationToken) =>
{
    var filter = new AuditEventFilter
    {
        StartDate = startDate ?? DateTime.UtcNow.AddDays(-30),
        EndDate = endDate ?? DateTime.UtcNow,
        Page = 1,
        PageSize = 1_000
    };

    var events = await auditService.GetAllAuditEventsAsync(filter, cancellationToken);

    if (!string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
    {
        return Results.BadRequest(new { message = "Unsupported format. Use 'csv'." });
    }

    var bytes = CsvExporter.Export(events);
    return Results.File(bytes, "text/csv", $"audit-events-{DateTime.UtcNow:yyyyMMdd}.csv");
})
.WithName("ExportAuditEvents")
.WithOpenApi()
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest);

auditGroup.MapGet("/archive/search", async (
    DateTime? startDate,
    DateTime? endDate,
    IAuditArchiveService archiveService,
    CancellationToken cancellationToken) =>
{
    var archives = await archiveService.SearchArchivesAsync(startDate, endDate, cancellationToken);
    var response = new AuditArchiveSearchResponse
    {
        Archives = archives.Select(ToArchiveItemResponse).ToList(),
        TotalCount = archives.Count,
        TotalEvents = archives.Sum(item => (long)item.EventCount),
        TotalSize = archives.Sum(item => item.FileSize)
    };

    return Results.Ok(response);
})
.WithName("SearchAuditArchives")
.WithOpenApi()
.Produces<AuditArchiveSearchResponse>(StatusCodes.Status200OK);

auditGroup.MapGet("/archive/download/{archiveId:guid}", async (
    Guid archiveId,
    HttpContext httpContext,
    IAuditArchiveService archiveService,
    IAuditService auditService,
    IOptionsMonitor<MinioOptions> minioOptions,
    CancellationToken cancellationToken) =>
{
    var archive = await archiveService.GetArchiveAsync(archiveId, cancellationToken);
    if (archive is null)
    {
        return Results.NotFound();
    }

    var downloadUrl = await archiveService.GenerateDownloadUrlAsync(archive, cancellationToken);

    await auditService.LogEventAsync(
        CreateAuditEvent(httpContext, "AuditArchiveDownload", "AuditArchive", archive.ArchiveId.ToString(), new
        {
            archive.FileName,
            archive.EventCount,
            archive.ObjectKey
        }),
        cancellationToken);
    await auditService.FlushBufferAsync(cancellationToken);

    await archiveService.UpdateAccessMetadataAsync(archive, GetActor(httpContext), cancellationToken);

    var options = minioOptions.CurrentValue;
    var response = new AuditArchiveDownloadResponse
    {
        DownloadUrl = downloadUrl,
        FileName = archive.FileName,
        ExpiresInSeconds = options.PresignedUrlExpirySeconds,
        RetentionExpiryDate = archive.RetentionExpiryDate
    };

    return Results.Ok(response);
})
.WithName("DownloadAuditArchive")
.WithOpenApi()
.Produces<AuditArchiveDownloadResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);

auditGroup.MapPost("/merge-offline", async (
    [FromBody] OfflineMergeRequest request,
    HttpContext httpContext,
    IOfflineAuditMergeService mergeService,
    CancellationToken cancellationToken) =>
{
    if (!TryValidate(request, out var errors))
    {
        return Results.ValidationProblem(errors);
    }

    var actor = GetActor(httpContext);
    var result = await mergeService.MergeAsync(request, actor, cancellationToken);

    var message = result.Status switch
    {
        "FAILED" => "Offline merge failed. Check history for details.",
        "PARTIAL_SUCCESS" => $"Merged {result.EventsMerged} events with {result.DuplicatesSkipped} duplicates skipped and {result.ConflictsDetected} conflicts flagged.",
        _ when result.EventsMerged == 0 && result.DuplicatesSkipped > 0 => $"No new events merged; {result.DuplicatesSkipped} duplicates skipped.",
        _ => $"Merged {result.EventsMerged} offline events."
    };

    var response = new OfflineMergeResponse
    {
        MergeId = result.MergeId,
        Status = result.Status,
        EventsReceived = result.EventsReceived,
        EventsMerged = result.EventsMerged,
        DuplicatesSkipped = result.DuplicatesSkipped,
        ConflictsDetected = result.ConflictsDetected,
        EventsReHashed = result.EventsReHashed,
        MergeDurationMs = result.MergeDurationMs,
        Message = message
    };

    return Results.Ok(response);
})
.WithName("MergeOfflineAuditEvents")
.WithOpenApi()
.Produces<OfflineMergeResponse>(StatusCodes.Status200OK)
.ProducesValidationProblem();

auditGroup.MapPost("/verify-integrity", async (
    DateTime? startDate,
    DateTime? endDate,
    HttpContext httpContext,
    IAuditService auditService,
    CancellationToken cancellationToken) =>
{
    var initiatedBy = httpContext.User?.Identity?.Name ?? httpContext.Request.Headers["X-Actor"].FirstOrDefault() ?? "System";
    var result = await auditService.VerifyChainIntegrityAsync(startDate, endDate, initiatedBy, cancellationToken);

    var statusText = result.Status switch
    {
        ChainStatus.Valid => "VALID",
        ChainStatus.Broken => "BROKEN",
        ChainStatus.Tampered => "TAMPERED",
        _ => "ERROR"
    };

    var message = statusText switch
    {
        "VALID" => "Audit chain integrity verified successfully.",
        "BROKEN" => "Audit chain integrity compromised. Security incident logged.",
        "TAMPERED" => "Audit chain tampering detected. Security incident logged.",
        _ => "Audit chain verification encountered an unexpected error."
    };

    return Results.Ok(new
    {
        status = statusText,
        eventsVerified = result.EventsVerified,
        brokenEventId = result.BrokenEventId,
        brokenEventTimestamp = result.BrokenEventTimestamp,
        durationMs = result.DurationMs,
        message
    });
})
.WithName("VerifyAuditChainIntegrity")
.WithOpenApi()
.Produces(StatusCodes.Status200OK);

auditGroup.MapPost("/minio/access", (
    JsonElement payload,
    ILoggerFactory loggerFactory) =>
{
    var logger = loggerFactory.CreateLogger("MinioAccessLog");
    logger.LogInformation("MinIO access log received: {Payload}", payload.ToString());
    return Results.Accepted();
})
.WithName("IngestMinioAccessLog")
.WithOpenApi()
.Produces(StatusCodes.Status202Accepted);

auditGroup.MapGet("/integrity/status", async (
    IAuditService auditService,
    CancellationToken cancellationToken) =>
{
    var status = await auditService.GetIntegrityStatusAsync(cancellationToken);

    return Results.Ok(new
    {
        lastVerification = status.LastVerification is null
            ? null
            : new
            {
                status = status.LastVerification.ChainStatus,
                timestamp = status.LastVerification.EndTime,
                eventsVerified = status.LastVerification.EventsVerified,
                durationMs = status.LastVerification.VerificationDurationMs,
                initiatedBy = status.LastVerification.InitiatedBy
            },
        chainStatus = new
        {
            totalEvents = status.TotalEvents,
            verifiedEvents = status.VerifiedEvents,
            brokenEvents = status.BrokenEvents,
            coveragePercentage = status.CoveragePercentage
        }
    });
})
.WithName("GetAuditChainStatus")
.WithOpenApi()
.Produces(StatusCodes.Status200OK);

auditGroup.MapGet("/integrity/history", async (
    int page = 1,
    int pageSize = 50,
    IAuditService auditService,
    CancellationToken cancellationToken) =>
{
    var history = await auditService.GetVerificationHistoryAsync(page, pageSize, cancellationToken);

    return Results.Ok(new
    {
        data = history.Items,
        pagination = new
        {
            currentPage = history.Page,
            pageSize = history.PageSize,
            totalCount = history.TotalCount,
            totalPages = (int)Math.Ceiling(history.TotalCount / (double)history.PageSize)
        }
    });
})
.WithName("GetAuditChainHistory")
.WithOpenApi()
.Produces(StatusCodes.Status200OK);

adminGroup.MapGet("/version", () =>
{
    var assembly = Assembly.GetExecutingAssembly();
    var fileVersion = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "0.0.0";
    var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? fileVersion;

    return Results.Ok(new
    {
        name = "IntelliFin.AdminService",
        version = informationalVersion,
        fileVersion,
        build = assembly.GetName().Version?.ToString(),
        environment = app.Environment.EnvironmentName,
        serverTimeUtc = DateTime.UtcNow
    });
})
.WithName("GetAdminServiceVersion")
.WithOpenApi();


adminGroup.MapGet("/users", async (
    int pageNumber,
    int pageSize,
    IKeycloakAdminService keycloakAdminService,
    CancellationToken cancellationToken) =>
{
    var safePage = pageNumber <= 0 ? 1 : pageNumber;
    var safeSize = pageSize <= 0 ? 50 : pageSize;
    var result = await keycloakAdminService.GetUsersAsync(safePage, safeSize, cancellationToken);
    return Results.Ok(result);
})
.WithName("ListUsers")
.WithOpenApi()
.Produces(StatusCodes.Status200OK);

adminGroup.MapGet("/users/{id}", async (
    string id,
    IKeycloakAdminService keycloakAdminService,
    CancellationToken cancellationToken) =>
{
    var user = await keycloakAdminService.GetUserAsync(id, cancellationToken);
    return user is null ? Results.NotFound() : Results.Ok(user);
})
.WithName("GetUserById")
.WithOpenApi()
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);

adminGroup.MapPost("/users", async (
    CreateUserRequest request,
    HttpContext httpContext,
    IKeycloakAdminService keycloakAdminService,
    IAuditService auditService,
    CancellationToken cancellationToken) =>
{
    var created = await keycloakAdminService.CreateUserAsync(request, cancellationToken);
    await auditService.LogEventAsync(
        CreateAuditEvent(httpContext, "UserCreated", "User", created.Id, new { created.Username, created.Email }),
        cancellationToken);

    return Results.Created($"/api/admin/users/{created.Id}", created);
})
.WithName("CreateUser")
.WithOpenApi()
.Produces(StatusCodes.Status201Created);

adminGroup.MapPut("/users/{id}", async (
    string id,
    UpdateUserRequest request,
    HttpContext httpContext,
    IKeycloakAdminService keycloakAdminService,
    IAuditService auditService,
    CancellationToken cancellationToken) =>
{
    var updated = await keycloakAdminService.UpdateUserAsync(id, request, cancellationToken);
    await auditService.LogEventAsync(
        CreateAuditEvent(httpContext, "UserUpdated", "User", updated.Id, new { updated.Username, updated.Email }),
        cancellationToken);

    return Results.Ok(updated);
})
.WithName("UpdateUser")
.WithOpenApi()
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);

adminGroup.MapDelete("/users/{id}", async (
    string id,
    HttpContext httpContext,
    IKeycloakAdminService keycloakAdminService,
    IAuditService auditService,
    CancellationToken cancellationToken) =>
{
    await keycloakAdminService.DeleteUserAsync(id, cancellationToken);
    await auditService.LogEventAsync(
        CreateAuditEvent(httpContext, "UserDeleted", "User", id, null),
        cancellationToken);

    return Results.NoContent();
})
.WithName("DeleteUser")
.WithOpenApi()
.Produces(StatusCodes.Status204NoContent);

adminGroup.MapPost("/users/{id}/reset-password", async (
    string id,
    ResetPasswordRequest request,
    HttpContext httpContext,
    IKeycloakAdminService keycloakAdminService,
    IAuditService auditService,
    CancellationToken cancellationToken) =>
{
    await keycloakAdminService.ResetUserPasswordAsync(id, request, cancellationToken);
    await auditService.LogEventAsync(
        CreateAuditEvent(httpContext, "UserPasswordReset", "User", id, new { Temporary = request.Temporary }),
        cancellationToken);

    return Results.NoContent();
})
.WithName("ResetUserPassword")
.WithOpenApi()
.Produces(StatusCodes.Status204NoContent);

adminGroup.MapGet("/users/{id}/roles", async (
    string id,
    IKeycloakAdminService keycloakAdminService,
    CancellationToken cancellationToken) =>
{
    var roles = await keycloakAdminService.GetUserRolesAsync(id, cancellationToken);
    return Results.Ok(roles);
})
.WithName("GetUserRoles")
.WithOpenApi()
.Produces(StatusCodes.Status200OK);

adminGroup.MapPost("/users/{id}/roles", async (
    string id,
    AssignRolesRequest request,
    HttpContext httpContext,
    IKeycloakAdminService keycloakAdminService,
    IAuditService auditService,
    CancellationToken cancellationToken) =>
{
    await keycloakAdminService.AssignRolesAsync(id, request, cancellationToken);
    await auditService.LogEventAsync(
        CreateAuditEvent(httpContext, "UserRolesAssigned", "User", id, new { request.Roles }),
        cancellationToken);

    return Results.NoContent();
})
.WithName("AssignUserRoles")
.WithOpenApi()
.Produces(StatusCodes.Status204NoContent);

adminGroup.MapDelete("/users/{id}/roles/{roleName}", async (
    string id,
    string roleName,
    HttpContext httpContext,
    IKeycloakAdminService keycloakAdminService,
    IAuditService auditService,
    CancellationToken cancellationToken) =>
{
    await keycloakAdminService.RemoveRoleAsync(id, roleName, cancellationToken);
    await auditService.LogEventAsync(
        CreateAuditEvent(httpContext, "UserRoleRemoved", "User", id, new { roleName }),
        cancellationToken);

    return Results.NoContent();
})
.WithName("RemoveUserRole")
.WithOpenApi()
.Produces(StatusCodes.Status204NoContent);

adminGroup.MapGet("/roles", async (
    IKeycloakAdminService keycloakAdminService,
    CancellationToken cancellationToken) =>
{
    var roles = await keycloakAdminService.GetRolesAsync(cancellationToken);
    return Results.Ok(roles);
})
.WithName("ListRoles")
.WithOpenApi()
.Produces(StatusCodes.Status200OK);

adminGroup.MapGet("/roles/{name}", async (
    string name,
    IKeycloakAdminService keycloakAdminService,
    CancellationToken cancellationToken) =>
{
    var role = await keycloakAdminService.GetRoleAsync(name, cancellationToken);
    return role is null ? Results.NotFound() : Results.Ok(role);
})
.WithName("GetRole")
.WithOpenApi()
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);

adminGroup.MapPost("/roles", async (
    CreateRoleRequest request,
    HttpContext httpContext,
    IKeycloakAdminService keycloakAdminService,
    IAuditService auditService,
    CancellationToken cancellationToken) =>
{
    var role = await keycloakAdminService.CreateRoleAsync(request, cancellationToken);
    await auditService.LogEventAsync(
        CreateAuditEvent(httpContext, "RoleCreated", "Role", role.Id, new { role.Name }),
        cancellationToken);

    return Results.Created($"/api/admin/roles/{role.Name}", role);
})
.WithName("CreateRole")
.WithOpenApi()
.Produces(StatusCodes.Status201Created);

adminGroup.MapPut("/roles/{name}", async (
    string name,
    UpdateRoleRequest request,
    HttpContext httpContext,
    IKeycloakAdminService keycloakAdminService,
    IAuditService auditService,
    CancellationToken cancellationToken) =>
{
    var role = await keycloakAdminService.UpdateRoleAsync(name, request, cancellationToken);
    await auditService.LogEventAsync(
        CreateAuditEvent(httpContext, "RoleUpdated", "Role", role.Id, new { role.Name }),
        cancellationToken);

    return Results.Ok(role);
})
.WithName("UpdateRole")
.WithOpenApi()
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);

adminGroup.MapDelete("/roles/{name}", async (
    string name,
    HttpContext httpContext,
    IKeycloakAdminService keycloakAdminService,
    IAuditService auditService,
    CancellationToken cancellationToken) =>
{
    await keycloakAdminService.DeleteRoleAsync(name, cancellationToken);
    await auditService.LogEventAsync(
        CreateAuditEvent(httpContext, "RoleDeleted", "Role", name, null),
        cancellationToken);

    return Results.NoContent();
})
.WithName("DeleteRole")
.WithOpenApi()
.Produces(StatusCodes.Status204NoContent);

app.MapGet("/", () => Results.Redirect("/api/admin/version"));

app.Run();

static bool TryValidate<T>(T instance, out Dictionary<string, string[]> errors)
{
    var validationContext = new ValidationContext(instance!);
    var validationResults = new List<ValidationResult>();
    var isValid = Validator.TryValidateObject(instance!, validationContext, validationResults, validateAllProperties: true);

    if (isValid)
    {
        errors = new Dictionary<string, string[]>();
        return true;
    }

    errors = validationResults
        .GroupBy(result => result.MemberNames.FirstOrDefault() ?? string.Empty)
        .ToDictionary(group => group.Key, group => group.Select(result => result.ErrorMessage ?? string.Empty).ToArray());
    return false;
}

static AuditEvent FromRequest(AuditEventRequest request, HttpContext context) => new()
{
    Timestamp = request.Timestamp == default ? DateTime.UtcNow : request.Timestamp.ToUniversalTime(),
    Actor = request.Actor,
    Action = request.Action,
    EntityType = request.EntityType,
    EntityId = request.EntityId,
    CorrelationId = request.CorrelationId ?? GetCorrelationId(context),
    IpAddress = request.IpAddress ?? context.Connection.RemoteIpAddress?.ToString(),
    UserAgent = request.UserAgent ?? context.Request.Headers.UserAgent.ToString(),
    EventData = request.EventData.HasValue ? request.EventData.Value.GetRawText() : null
};

static AuditEventResponse ToResponse(AuditEvent evt) => new()
{
    EventId = evt.EventId,
    Timestamp = evt.Timestamp,
    Actor = evt.Actor,
    Action = evt.Action,
    EntityType = evt.EntityType,
    EntityId = evt.EntityId,
    CorrelationId = evt.CorrelationId,
    IpAddress = evt.IpAddress,
    UserAgent = evt.UserAgent,
    EventData = evt.EventData,
    MigrationSource = evt.MigrationSource,
    PreviousEventHash = evt.PreviousEventHash,
    CurrentEventHash = evt.CurrentEventHash,
    IntegrityStatus = evt.IntegrityStatus,
    IsGenesisEvent = evt.IsGenesisEvent,
    LastVerifiedAt = evt.LastVerifiedAt,
    CreatedAt = evt.CreatedAt,
    IsOfflineEvent = evt.IsOfflineEvent,
    OfflineDeviceId = evt.OfflineDeviceId,
    OfflineSessionId = evt.OfflineSessionId,
    OfflineMergeId = evt.OfflineMergeId,
    OriginalHash = evt.OriginalHash
};

static AuditArchiveItemResponse ToArchiveItemResponse(AuditArchiveMetadata metadata) => new()
{
    ArchiveId = metadata.ArchiveId,
    FileName = metadata.FileName,
    ObjectKey = metadata.ObjectKey,
    EventDateStart = metadata.EventDateStart,
    EventDateEnd = metadata.EventDateEnd,
    EventCount = metadata.EventCount,
    FileSize = metadata.FileSize,
    CompressionRatio = metadata.CompressionRatio,
    ChainStartHash = metadata.ChainStartHash,
    ChainEndHash = metadata.ChainEndHash,
    PreviousDayEndHash = metadata.PreviousDayEndHash,
    RetentionExpiryDate = metadata.RetentionExpiryDate,
    StorageLocation = metadata.StorageLocation,
    ReplicationStatus = metadata.ReplicationStatus,
    LastReplicationCheckUtc = metadata.LastReplicationCheckUtc,
    LastAccessedAtUtc = metadata.LastAccessedAtUtc,
    LastAccessedBy = metadata.LastAccessedBy
};

static AuditEvent CreateAuditEvent(HttpContext context, string action, string entityType, string? entityId, object? eventData)
    => new()
    {
        Timestamp = DateTime.UtcNow,
        Actor = GetActor(context),
        Action = action,
        EntityType = entityType,
        EntityId = entityId,
        CorrelationId = GetCorrelationId(context),
        IpAddress = context.Connection.RemoteIpAddress?.ToString(),
        UserAgent = context.Request.Headers.UserAgent.ToString(),
        EventData = eventData is null ? null : JsonSerializer.Serialize(eventData)
    };

static Uri EnsureBaseAddress(string baseUrl)
{
    if (string.IsNullOrWhiteSpace(baseUrl))
    {
        throw new InvalidOperationException("Keycloak BaseUrl configuration is required");
    }

    return baseUrl.EndsWith('/') ? new Uri(baseUrl, UriKind.Absolute) : new Uri(baseUrl + "/", UriKind.Absolute);
}

static IAsyncPolicy<HttpResponseMessage> CreateRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(response => response.StatusCode == HttpStatusCode.TooManyRequests)
        .WaitAndRetryAsync(
            3,
            retryAttempt => TimeSpan.FromMilliseconds(Math.Pow(2, retryAttempt - 1) * 250),
            onRetry: (outcome, timespan, retryAttempt, _) =>
            {
                // optional logging hook
            });
}

static string GetActor(HttpContext context)
    => string.IsNullOrWhiteSpace(context.User?.Identity?.Name)
        ? "system"
        : context.User!.Identity!.Name!;

static string? GetCorrelationId(HttpContext context)
{
    var activity = Activity.Current ?? context.Features.Get<IHttpActivityFeature>()?.Activity;
    if (activity is not null && activity.TraceId != default)
    {
        return activity.TraceId.ToString();
    }

    if (context.Request.Headers.TryGetValue("traceparent", out var traceParentValues))
    {
        var traceParent = traceParentValues.FirstOrDefault();
        var parsedTraceId = TryParseTraceId(traceParent);
        if (!string.IsNullOrWhiteSpace(parsedTraceId))
        {
            return parsedTraceId;
        }
    }

    var correlationHeader = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
    if (!string.IsNullOrWhiteSpace(correlationHeader))
    {
        return correlationHeader;
    }

    return context.TraceIdentifier;
}

static string? TryParseTraceId(string? traceParent)
{
    if (string.IsNullOrWhiteSpace(traceParent))
    {
        return null;
    }

    var parts = traceParent.Split('-', StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length >= 4 && parts[1].Length == 32)
    {
        return parts[1];
    }

    return null;
}