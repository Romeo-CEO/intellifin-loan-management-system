using IntelliFin.IdentityService.Models;
using IntelliFin.Shared.DomainModels.Entities;

namespace IntelliFin.IdentityService.Services;

public interface IMigrationOrchestrationService
{
    Task<SchemaVerificationResult> VerifyDatabaseSchemaAsync(CancellationToken cancellationToken = default);
    Task<BaselineMetrics> CapturePerformanceBaselineAsync(CancellationToken cancellationToken = default);
    Task<int> GetActiveUserCountAsync(CancellationToken cancellationToken = default);
    Task<BulkProvisionSummary> BulkProvisionUsersAsync(int batchSize, bool dryRun, CancellationToken cancellationToken = default);
    Task<ProvisionVerificationResult> VerifyProvisioningSampleAsync(int sampleSize, CancellationToken cancellationToken = default);
    Task<MigrationMetrics> GetCurrentMetricsAsync(CancellationToken cancellationToken = default);
}

public record SchemaVerificationResult(bool Valid, List<string> Errors);

public record BaselineMetrics(double AuthP95, double SuccessRate);

public record BulkProvisionSummary(int Provisioned, int Failed);

public record ProvisionVerificationResult(int Sampled, int Matched);

public record MigrationMetrics(double SuccessRate, int CustomJwtCount, int KeycloakCount);