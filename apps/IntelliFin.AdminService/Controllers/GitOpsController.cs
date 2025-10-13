using IntelliFin.AdminService.Attributes;
using IntelliFin.AdminService.Contracts.Requests;
using IntelliFin.AdminService.Contracts.Responses;
using IntelliFin.AdminService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelliFin.AdminService.Controllers;

[ApiController]
[Route("api/admin/gitops")]
[Authorize(Roles = "System Administrator,Compliance Officer")]
public class GitOpsController : ControllerBase
{
    private readonly IArgoCdIntegrationService _argoService;
    private readonly ILogger<GitOpsController> _logger;

    public GitOpsController(
        IArgoCdIntegrationService argoService,
        ILogger<GitOpsController> logger)
    {
        _argoService = argoService;
        _logger = logger;
    }

    [HttpGet("applications")]
    [ProducesResponseType(typeof(IReadOnlyList<ArgoCdApplicationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetApplications(CancellationToken cancellationToken)
    {
        var applications = await _argoService.GetApplicationsAsync(cancellationToken);
        return Ok(applications);
    }

    [HttpGet("applications/{appName}")]
    [ProducesResponseType(typeof(ArgoCdApplicationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetApplication(string appName, CancellationToken cancellationToken)
    {
        var application = await _argoService.GetApplicationAsync(appName, cancellationToken);
        if (application is null)
        {
            return NotFound();
        }

        return Ok(application);
    }

    [HttpGet("applications/{appName}/history")]
    [ProducesResponseType(typeof(IReadOnlyList<ArgoCdRevisionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetApplicationHistory(string appName, CancellationToken cancellationToken)
    {
        var history = await _argoService.GetApplicationHistoryAsync(appName, cancellationToken);
        return Ok(history);
    }

    [HttpPost("applications/{appName}/sync")]
    [RequiresMfa]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> TriggerSync(
        string appName,
        [FromBody] ArgoCdSyncRequest request,
        CancellationToken cancellationToken)
    {
        request ??= new ArgoCdSyncRequest();
        await _argoService.TriggerSyncAsync(
            appName,
            new ArgoCdSyncRequestParameters(request.Prune, request.DryRun, request.RetryLimit),
            cancellationToken);

        _logger.LogInformation("ArgoCD sync requested via API for application {Application}", appName);
        return Accepted(new { message = "Sync triggered", application = appName });
    }

    [HttpPost("applications/{appName}/rollback")]
    [RequiresMfa]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> TriggerRollback(
        string appName,
        [FromBody] ArgoCdRollbackRequest request,
        CancellationToken cancellationToken)
    {
        request ??= new ArgoCdRollbackRequest();
        await _argoService.TriggerRollbackAsync(appName, request.RevisionId, cancellationToken);
        _logger.LogWarning(
            "ArgoCD rollback requested via API for application {Application} to revision {Revision}",
            appName,
            request.RevisionId);

        return Accepted(new { message = "Rollback triggered", application = appName, revision = request.RevisionId });
    }
}
