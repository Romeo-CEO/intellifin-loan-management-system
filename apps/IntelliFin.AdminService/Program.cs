using System.Reflection;
using System.Linq;
using System.Net;
using IntelliFin.AdminService.Data;
using IntelliFin.AdminService.Contracts.Requests;
using IntelliFin.AdminService.ExceptionHandling;
using IntelliFin.AdminService.HealthChecks;
using IntelliFin.AdminService.Options;
using IntelliFin.AdminService.Services;
using IntelliFin.AdminService.Utilities;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using Prometheus;
using IntelliFin.Shared.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetryInstrumentation(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<KeycloakExceptionHandler>();
builder.Services.AddOpenApi();
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

builder.Services.AddDbContext<AdminDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Default")
                           ?? "Server=(localdb)\\mssqllocaldb;Database=IntelliFin_AdminService;Trusted_Connection=True;MultipleActiveResultSets=True";
    options.UseSqlServer(connectionString);
});

builder.Services.AddMemoryCache();

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

builder.Services.AddScoped<IAuditService, AuditService>();

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

var adminGroup = app.MapGroup("/api/admin");

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
        GetActor(httpContext),
        "UserCreated",
        "User",
        created.Id,
        new { created.Username, created.Email },
        GetCorrelationId(httpContext),
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
        GetActor(httpContext),
        "UserUpdated",
        "User",
        updated.Id,
        new { updated.Username, updated.Email },
        GetCorrelationId(httpContext),
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
        GetActor(httpContext),
        "UserDeleted",
        "User",
        id,
        null,
        GetCorrelationId(httpContext),
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
        GetActor(httpContext),
        "UserPasswordReset",
        "User",
        id,
        new { Temporary = request.Temporary },
        GetCorrelationId(httpContext),
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
        GetActor(httpContext),
        "UserRolesAssigned",
        "User",
        id,
        new { request.Roles },
        GetCorrelationId(httpContext),
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
        GetActor(httpContext),
        "UserRoleRemoved",
        "User",
        id,
        new { roleName },
        GetCorrelationId(httpContext),
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
        GetActor(httpContext),
        "RoleCreated",
        "Role",
        role.Id,
        new { role.Name },
        GetCorrelationId(httpContext),
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
        GetActor(httpContext),
        "RoleUpdated",
        "Role",
        role.Id,
        new { role.Name },
        GetCorrelationId(httpContext),
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
        GetActor(httpContext),
        "RoleDeleted",
        "Role",
        name,
        null,
        GetCorrelationId(httpContext),
        cancellationToken);

    return Results.NoContent();
})
.WithName("DeleteRole")
.WithOpenApi()
.Produces(StatusCodes.Status204NoContent);

app.MapGet("/", () => Results.Redirect("/api/admin/version"));

app.Run();

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
    var headerValue = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
    return !string.IsNullOrWhiteSpace(headerValue) ? headerValue : context.TraceIdentifier;
}