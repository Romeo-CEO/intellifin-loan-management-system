using IntelliFin.UserMigration.Data;
using IntelliFin.UserMigration.Options;
using IntelliFin.UserMigration.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.CommandLine;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables("INTELLIFIN_MIGRATION_");

builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
});

builder.Services.AddOptions<KeycloakOptions>()
    .Bind(builder.Configuration.GetSection("Keycloak"))
    .ValidateDataAnnotations()
    .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _), "Keycloak:BaseUrl must be a valid absolute URI")
    .ValidateOnStart();

builder.Services.AddOptions<DatabaseOptions>()
    .Bind(builder.Configuration.GetSection("Databases"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<MigrationOptions>()
    .Bind(builder.Configuration.GetSection("Migration"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient("keycloak-admin", (sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<KeycloakOptions>>().Value;
    client.BaseAddress = BuildBaseAddress(options.BaseUrl);
});

builder.Services.AddHttpClient("keycloak-token", (sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<KeycloakOptions>>().Value;
    client.BaseAddress = BuildBaseAddress(options.BaseUrl);
});

builder.Services.AddDbContext<IdentityDbContext>((sp, options) =>
{
    var dbOptions = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
    options.UseSqlServer(dbOptions.IdentityConnectionString);
});

builder.Services.AddDbContext<AdminDbContext>((sp, options) =>
{
    var dbOptions = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value;
    options.UseSqlServer(dbOptions.AdminConnectionString);
});

builder.Services.AddSingleton<KeycloakTokenService>();
builder.Services.AddSingleton<KeycloakAdminClient>();
builder.Services.AddScoped<RoleMigrationService>();
builder.Services.AddScoped<UserMigrationService>();
builder.Services.AddScoped<MigrationValidationService>();
builder.Services.AddScoped<MigrationRollbackService>();
builder.Services.AddScoped<MigrationReportService>();
builder.Services.AddScoped<MigrationOrchestrator>();

using var host = builder.Build();
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cts.Cancel();
};

var root = new RootCommand("IntelliFin ASP.NET Identity to Keycloak migration tool");

var skipValidationOption = new Option<bool>("--skip-validation", description: "Skip validation after migrating users and roles.");
var migrateCommand = new Command("migrate", "Migrate roles and users into Keycloak.") { skipValidationOption };
migrateCommand.SetHandler(async (bool skipValidation) =>
{
    using var scope = host.Services.CreateScope();
    var orchestrator = scope.ServiceProvider.GetRequiredService<MigrationOrchestrator>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("MigrationCommand");

    if (skipValidation)
    {
        var migrationResult = await orchestrator.ExecuteUserMigrationOnlyAsync(cts.Token).ConfigureAwait(false);
        logger.LogInformation("Migration completed with {Success} successes, {Skipped} skipped, {Failures} failures.", migrationResult.SuccessCount, migrationResult.SkippedCount, migrationResult.FailedUsers.Count);
    }
    else
    {
        var (migrationResult, validationResult) = await orchestrator.ExecuteFullMigrationAsync(cts.Token).ConfigureAwait(false);
        logger.LogInformation("Migration completed with {Success} successes, {Skipped} skipped, {Failures} failures. Validation success={ValidationSuccess}.", migrationResult.SuccessCount, migrationResult.SkippedCount, migrationResult.FailedUsers.Count, validationResult.IsSuccessful);
    }
}, skipValidationOption);

var validateCommand = new Command("validate", "Run validation checks against Keycloak realm.");
validateCommand.SetHandler(async () =>
{
    using var scope = host.Services.CreateScope();
    var orchestrator = scope.ServiceProvider.GetRequiredService<MigrationOrchestrator>();
    var validationResult = await orchestrator.ValidateAsync(cts.Token).ConfigureAwait(false);
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("ValidationCommand");
    logger.LogInformation("Validation result: Users={UserMatch}, Roles={RoleMatch}, Assignments={AssignmentMatch}, Samples={SampleErrors}", validationResult.UserCountMatches, validationResult.RoleCountMatches, validationResult.AssignmentCountMatches, validationResult.SampleErrors.Count);
    if (validationResult.SampleErrors.Count > 0)
    {
        foreach (var error in validationResult.SampleErrors)
        {
            logger.LogWarning("Sample validation error: {Error}", error);
        }
    }
});

var rollbackCommand = new Command("rollback", "Rollback Keycloak migration by removing users, roles and clearing mapping tables.");
rollbackCommand.SetHandler(async () =>
{
    using var scope = host.Services.CreateScope();
    var orchestrator = scope.ServiceProvider.GetRequiredService<MigrationOrchestrator>();
    await orchestrator.ExecuteRollbackAsync(cts.Token).ConfigureAwait(false);
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("RollbackCommand");
    logger.LogWarning("Rollback completed. ASP.NET Core Identity database was not modified.");
});

root.AddCommand(migrateCommand);
root.AddCommand(validateCommand);
root.AddCommand(rollbackCommand);

if (args.Length == 0)
{
    await root.InvokeAsync("--help");
    return;
}

var exitCode = await root.InvokeAsync(args);
Environment.ExitCode = exitCode;

static Uri BuildBaseAddress(string baseUrl)
{
    if (!baseUrl.EndsWith('/'))
    {
        baseUrl += "/";
    }

    return new Uri(baseUrl, UriKind.Absolute);
}
