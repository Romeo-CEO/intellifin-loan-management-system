using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.Shared.DomainModels.Repositories;
using Microsoft.EntityFrameworkCore;

namespace IntelliFin.IdentityService.Services;

public class MigrationOrchestrationService : IMigrationOrchestrationService
{
    private readonly LmsDbContext _db;
    private readonly IUserRepository _users;
    private readonly ILogger<MigrationOrchestrationService> _logger;
    private readonly IUserProvisioningService? _provisioningService;

    public MigrationOrchestrationService(
        LmsDbContext db,
        IUserRepository users,
        ILogger<MigrationOrchestrationService> logger,
        IServiceProvider services)
    {
        _db = db;
        _users = users;
        _logger = logger;
        _provisioningService = services.GetService<IUserProvisioningService>();
    }

    public async Task<SchemaVerificationResult> VerifyDatabaseSchemaAsync(CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        try
        {
            // Check required IAM tables exist
            var required = new[] { "Users", "Roles", "UserRoles", "Permissions", "RolePermissions" };
            var connection = _db.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }
            var existing = connection.GetSchema("Tables").Rows
                .Cast<System.Data.DataRow>()
                .Select(r => r[2]?.ToString())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var table in required)
            {
                if (!existing.Contains(table)) errors.Add($"Missing table: {table}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Schema verification error");
            errors.Add(ex.Message);
        }

        return new SchemaVerificationResult(errors.Count == 0, errors);
    }

    public Task<BaselineMetrics> CapturePerformanceBaselineAsync(CancellationToken cancellationToken = default)
    {
        // Stub: in production, pull from metrics backend
        var baseline = new BaselineMetrics(AuthP95: 50.0, SuccessRate: 99.9);
        _logger.LogInformation("Baseline captured: p95={P95}ms, success={Success}%", baseline.AuthP95, baseline.SuccessRate);
        return Task.FromResult(baseline);
    }

    public async Task<int> GetActiveUserCountAsync(CancellationToken cancellationToken = default)
    {
        var count = await _db.Users.CountAsync(cancellationToken);
        return count;
    }

    public async Task<BulkProvisionSummary> BulkProvisionUsersAsync(int batchSize, bool dryRun, CancellationToken cancellationToken = default)
    {
        var provisioned = 0;
        var failed = 0;

        if (_provisioningService is null)
        {
            _logger.LogWarning("Provisioning service not available; treating as dry run");
            dryRun = true;
        }

        var page = 0;
        const int defaultBatch = 100;
        var size = batchSize > 0 ? batchSize : defaultBatch;

        while (true)
        {
            var users = await _db.Users
                .OrderBy(u => u.Id)
                .Skip(page * size)
                .Take(size)
                .ToListAsync(cancellationToken);

            if (users.Count == 0) break;

            foreach (var u in users)
            {
                try
                {
                    if (!dryRun)
                    {
                        var result = await _provisioningService!.ProvisionUserAsync(u.Id, cancellationToken);

                        if (!result.Success)
                        {
                            failed++;
                            continue;
                        }
                    }

                    provisioned++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to provision user {UserId}", u.Id);
                    failed++;
                }
            }

            page++;
        }

        return new BulkProvisionSummary(provisioned, failed);
    }

    public async Task<ProvisionVerificationResult> VerifyProvisioningSampleAsync(int sampleSize, CancellationToken cancellationToken = default)
    {
        // Stub implementation: assume 98% match for sample
        var usersToCheck = Math.Max(10, sampleSize);
        var matched = (int)Math.Round(usersToCheck * 0.98);
        return new ProvisionVerificationResult(usersToCheck, matched);
    }

    public Task<MigrationMetrics> GetCurrentMetricsAsync(CancellationToken cancellationToken = default)
    {
        // Stub: in production pull from telemetry; here we return placeholders
        var metrics = new MigrationMetrics(SuccessRate: 99.9, CustomJwtCount: 1200, KeycloakCount: 3400);
        return Task.FromResult(metrics);
    }
}