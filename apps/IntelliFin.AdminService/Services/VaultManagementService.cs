using IntelliFin.AdminService.Contracts.Responses;
using IntelliFin.AdminService.Data;
using IntelliFin.AdminService.Models;
using IntelliFin.AdminService.Options;
using IntelliFin.Shared.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VaultSharp;
using VaultSharp.V1.SystemBackend;

namespace IntelliFin.AdminService.Services;

public class VaultManagementService : IVaultManagementService
{
    private readonly AdminDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly IVaultClient _vaultClient;
    private readonly ILogger<VaultManagementService> _logger;
    private readonly VaultOptions _options;

    public VaultManagementService(
        AdminDbContext dbContext,
        IAuditService auditService,
        IVaultClient vaultClient,
        IOptions<VaultOptions> options,
        ILogger<VaultManagementService> logger)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _vaultClient = vaultClient;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<VaultLeaseRevocationResult> RevokeLeaseAsync(
        string leaseId,
        string? reason,
        string? incidentId,
        string requestedBy,
        string? requestedByName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseId);

        var correlationId = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;

        _logger.LogWarning(
            "Revoking Vault lease {LeaseId} by {RequestedBy} ({RequestedByName}) with correlation {CorrelationId}",
            leaseId,
            requestedBy,
            requestedByName,
            correlationId);

        // Attempt revocation in Vault when integration is enabled and a token is available.
        if (_options.Enabled && !string.IsNullOrWhiteSpace(_options.Token))
        {
            try
            {
                await _vaultClient.V1.System.RevokeLeaseAsync(leaseId, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to revoke lease {LeaseId} from Vault", leaseId);
                throw;
            }
        }
        else
        {
            _logger.LogInformation("Vault integration disabled - storing revocation intent locally for lease {LeaseId}", leaseId);
        }

        var record = await _dbContext.VaultLeaseRecords
            .FirstOrDefaultAsync(l => l.LeaseId == leaseId, cancellationToken);

        if (record == null)
        {
            record = await CreateRecordFromLookupAsync(leaseId, cancellationToken)
                     ?? new VaultLeaseRecord
                     {
                         LeaseId = leaseId,
                         ServiceName = "unknown",
                         DatabaseName = "unknown",
                         Username = "unknown",
                         IssuedAtUtc = now,
                         ExpiresAtUtc = now,
                         Renewable = false,
                         CreatedAtUtc = now,
                         UpdatedAtUtc = now,
                         Status = VaultLeaseStatus.Active
                     };

            _dbContext.VaultLeaseRecords.Add(record);
        }

        record.Status = VaultLeaseStatus.Revoked;
        record.RevokedAtUtc = now;
        record.RevokedBy = requestedBy;
        record.RevocationReason = string.IsNullOrWhiteSpace(reason) ? null : reason;
        record.UpdatedAtUtc = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEvent
        {
            Actor = requestedBy,
            Action = "VaultLeaseRevoked",
            EntityType = "VaultLease",
            EntityId = leaseId,
            CorrelationId = correlationId,
            Severity = "Critical",
            EventData = System.Text.Json.JsonSerializer.Serialize(new
            {
                leaseId,
                reason,
                incidentId,
                requestedBy = requestedByName ?? requestedBy,
                revokedAtUtc = now
            })
        }, cancellationToken);

        return new VaultLeaseRevocationResult
        {
            LeaseId = leaseId,
            Status = VaultLeaseStatus.Revoked,
            RevokedAtUtc = now,
            CorrelationId = correlationId
        };
    }

    public async Task<IReadOnlyList<VaultLeaseDto>> GetActiveLeasesAsync(
        string? serviceName,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var query = _dbContext.VaultLeaseRecords.AsQueryable();
        if (!string.IsNullOrWhiteSpace(serviceName))
        {
            query = query.Where(x => x.ServiceName == serviceName);
        }

        var records = await query.ToListAsync(cancellationToken);
        var updated = false;

        foreach (var record in records)
        {
            if (record.Status is not VaultLeaseStatus.Revoked and not VaultLeaseStatus.Expired
                && record.ExpiresAtUtc <= now)
            {
                record.Status = VaultLeaseStatus.Expired;
                record.UpdatedAtUtc = now;
                updated = true;
            }
        }

        if (updated)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return records
            .Where(r => r.Status is VaultLeaseStatus.Active or VaultLeaseStatus.PendingRevocation)
            .OrderBy(r => r.ExpiresAtUtc)
            .Select(r => ToDto(r, now))
            .ToList();
    }

    private async Task<VaultLeaseRecord?> CreateRecordFromLookupAsync(
        string leaseId,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.Token))
        {
            return null;
        }

        try
        {
            var lookup = await _vaultClient.V1.System.LookupLeaseAsync(new LeaseLookupRequest
            {
                LeaseId = leaseId
            }, cancellationToken);

            var metadata = lookup.Data?.Metadata ?? new Dictionary<string, string>();
            var now = DateTime.UtcNow;

            return new VaultLeaseRecord
            {
                LeaseId = leaseId,
                ServiceName = metadata.TryGetValue("service", out var service) && !string.IsNullOrWhiteSpace(service)
                    ? service
                    : "unknown",
                DatabaseName = metadata.TryGetValue("database", out var database) && !string.IsNullOrWhiteSpace(database)
                    ? database
                    : metadata.TryGetValue("db_name", out var dbName) ? dbName ?? "unknown" : "unknown",
                Username = metadata.TryGetValue("username", out var username) && !string.IsNullOrWhiteSpace(username)
                    ? username
                    : "unknown",
                IssuedAtUtc = lookup.Data?.IssueTime ?? now,
                ExpiresAtUtc = lookup.Data?.ExpireTime ?? now,
                LastRenewedAtUtc = lookup.Data?.LastRenewalTime,
                Renewable = lookup.Data?.Renewable ?? false,
                MetadataJson = metadata.Count > 0
                    ? System.Text.Json.JsonSerializer.Serialize(metadata)
                    : null,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                Status = VaultLeaseStatus.Active
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to lookup lease {LeaseId} in Vault", leaseId);
            return null;
        }
    }

    private static VaultLeaseDto ToDto(VaultLeaseRecord record, DateTime referenceTimeUtc)
    {
        var remaining = (int)Math.Max(0, (record.ExpiresAtUtc - referenceTimeUtc).TotalSeconds);

        return new VaultLeaseDto
        {
            LeaseId = record.LeaseId,
            ServiceName = record.ServiceName,
            DatabaseName = record.DatabaseName,
            Username = record.Username,
            Renewable = record.Renewable,
            Status = record.Status,
            IssuedAtUtc = record.IssuedAtUtc,
            ExpiresAtUtc = record.ExpiresAtUtc,
            LastRenewedAtUtc = record.LastRenewedAtUtc,
            RemainingSeconds = remaining,
            CorrelationId = record.CorrelationId
        };
    }
}
