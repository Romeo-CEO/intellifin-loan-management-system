using IntelliFin.IdentityService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelliFin.IdentityService.Controllers.Platform;

[ApiController]
[Route("api/platform/migration")]
[Authorize(Policy = "RequireSystemAdmin")]
public class MigrationController : ControllerBase
{
    private readonly IMigrationOrchestrationService _migrationService;

    public MigrationController(IMigrationOrchestrationService migrationService)
    {
        _migrationService = migrationService;
    }

    [HttpGet("verify-schema")]
    public async Task<IActionResult> VerifySchema(CancellationToken cancellationToken)
    {
        var result = await _migrationService.VerifyDatabaseSchemaAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("baseline")]
    public async Task<IActionResult> CaptureBaseline(CancellationToken cancellationToken)
    {
        var baseline = await _migrationService.CapturePerformanceBaselineAsync(cancellationToken);
        return Ok(new { authP95 = baseline.AuthP95, successRate = baseline.SuccessRate });
    }

    [HttpGet("user-count")]
    public async Task<IActionResult> GetUserCount(CancellationToken cancellationToken)
    {
        var count = await _migrationService.GetActiveUserCountAsync(cancellationToken);
        return Ok(new { count });
    }

    public record BulkProvisionRequest(int BatchSize, bool DryRun);

    [HttpPost("provision-all")]
    public async Task<IActionResult> ProvisionAllUsers([FromBody] BulkProvisionRequest request, CancellationToken cancellationToken)
    {
        var result = await _migrationService.BulkProvisionUsersAsync(request.BatchSize, request.DryRun, cancellationToken);
        return Ok(new { provisioned = result.Provisioned, failed = result.Failed });
    }

    public record VerificationRequest(int SampleSize);

    [HttpPost("verify-provision")]
    public async Task<IActionResult> VerifyProvisioning([FromBody] VerificationRequest request, CancellationToken cancellationToken)
    {
        var result = await _migrationService.VerifyProvisioningSampleAsync(request.SampleSize, cancellationToken);
        return Ok(new { sampled = result.Sampled, matched = result.Matched });
    }

    [HttpGet("metrics")]
    public async Task<IActionResult> GetMigrationMetrics(CancellationToken cancellationToken)
    {
        var metrics = await _migrationService.GetCurrentMetricsAsync(cancellationToken);
        return Ok(new { successRate = metrics.SuccessRate, customJwtCount = metrics.CustomJwtCount, keycloakCount = metrics.KeycloakCount });
    }
}