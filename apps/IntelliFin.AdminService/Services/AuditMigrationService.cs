using IntelliFin.AdminService.Data;
using IntelliFin.AdminService.Models;
using Microsoft.EntityFrameworkCore;

namespace IntelliFin.AdminService.Services;

public sealed class AuditMigrationService
{
    private readonly FinancialDbContext _financialDbContext;
    private readonly AdminDbContext _adminDbContext;
    private readonly ILogger<AuditMigrationService> _logger;

    public AuditMigrationService(
        FinancialDbContext financialDbContext,
        AdminDbContext adminDbContext,
        ILogger<AuditMigrationService> logger)
    {
        _financialDbContext = financialDbContext;
        _adminDbContext = adminDbContext;
        _logger = logger;
    }

    public async Task<MigrationResult> MigrateAsync(CancellationToken cancellationToken = default)
    {
        var sourceEvents = await _financialDbContext.AuditLogs
            .OrderBy(e => e.Timestamp)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Migrating {Count} audit events from FinancialService", sourceEvents.Count);

        const int batchSize = 1000;
        var migrated = 0;

        for (var index = 0; index < sourceEvents.Count; index += batchSize)
        {
            var batch = sourceEvents.Skip(index).Take(batchSize).ToList();
            var adminEvents = batch.Select(e => new AuditEvent
            {
                EventId = Guid.NewGuid(),
                Timestamp = e.Timestamp,
                Actor = e.UserId,
                Action = e.Action,
                EntityType = e.EntityType,
                EntityId = e.EntityId,
                CorrelationId = e.CorrelationId,
                EventData = e.Details,
                MigrationSource = "FinancialService"
            }).ToList();

            await _adminDbContext.AuditEvents.AddRangeAsync(adminEvents, cancellationToken);
            await _adminDbContext.SaveChangesAsync(cancellationToken);
            migrated += adminEvents.Count;
            _logger.LogInformation("Migrated batch {Batch} with {Count} events", (index / batchSize) + 1, adminEvents.Count);
        }

        return new MigrationResult
        {
            SourceCount = sourceEvents.Count,
            MigratedCount = migrated,
            IsSuccess = migrated == sourceEvents.Count
        };
    }
}

public sealed class MigrationResult
{
    public int SourceCount { get; set; }
    public int MigratedCount { get; set; }
    public bool IsSuccess { get; set; }
}
