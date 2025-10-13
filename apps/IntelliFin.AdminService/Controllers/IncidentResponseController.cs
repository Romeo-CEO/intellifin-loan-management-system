using System.Security.Claims;
using IntelliFin.AdminService.Contracts.Requests;
using IntelliFin.AdminService.Contracts.Responses;
using IntelliFin.AdminService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace IntelliFin.AdminService.Controllers;

[ApiController]
[Route("api/admin/incident-response")]
[Authorize(Roles = "System Administrator,DevOps Engineer,Security Engineer")]
public sealed class IncidentResponseController : ControllerBase
{
    private readonly IIncidentResponseService _incidentResponseService;
    private readonly ILogger<IncidentResponseController> _logger;

    public IncidentResponseController(IIncidentResponseService incidentResponseService, ILogger<IncidentResponseController> logger)
    {
        _incidentResponseService = incidentResponseService;
        _logger = logger;
    }

    [HttpGet("playbooks")]
    [ProducesResponseType(typeof(IReadOnlyCollection<IncidentPlaybookDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlaybooks(CancellationToken cancellationToken)
    {
        var playbooks = await _incidentResponseService.GetPlaybooksAsync(cancellationToken);
        return Ok(playbooks);
    }

    [HttpGet("playbooks/{playbookId:guid}")]
    [ProducesResponseType(typeof(IncidentPlaybookDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPlaybook(Guid playbookId, CancellationToken cancellationToken)
    {
        var playbook = await _incidentResponseService.GetPlaybookAsync(playbookId, cancellationToken);
        return playbook is null ? NotFound() : Ok(playbook);
    }

    [HttpPost("playbooks")]
    [ProducesResponseType(typeof(IncidentPlaybookDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreatePlaybook([FromBody] CreateIncidentPlaybookRequest request, CancellationToken cancellationToken)
    {
        var actor = GetUserId();
        _logger.LogInformation("Creating incident playbook for {Alert} by {Actor}", request.AlertName, actor);
        var playbook = await _incidentResponseService.CreatePlaybookAsync(request, actor, cancellationToken);
        return CreatedAtAction(nameof(GetPlaybook), new { playbookId = playbook.PlaybookId }, playbook);
    }

    [HttpPut("playbooks/{playbookId:guid}")]
    [ProducesResponseType(typeof(IncidentPlaybookDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePlaybook(Guid playbookId, [FromBody] UpdateIncidentPlaybookRequest request, CancellationToken cancellationToken)
    {
        var actor = GetUserId();
        var updated = await _incidentResponseService.UpdatePlaybookAsync(playbookId, request, actor, cancellationToken);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpPost("playbooks/{playbookId:guid}/runs")]
    [ProducesResponseType(typeof(IncidentPlaybookRunDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> RecordPlaybookRun(Guid playbookId, [FromBody] RecordPlaybookUsageRequest request, CancellationToken cancellationToken)
    {
        var actor = GetUserId();
        var run = await _incidentResponseService.RecordPlaybookRunAsync(playbookId, request, actor, cancellationToken);
        return CreatedAtAction(nameof(GetPlaybook), new { playbookId }, run);
    }

    [HttpGet("silences")]
    [ProducesResponseType(typeof(IReadOnlyCollection<AlertSilenceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSilences(CancellationToken cancellationToken)
    {
        var silences = await _incidentResponseService.GetSilencesAsync(cancellationToken);
        return Ok(silences);
    }

    [HttpPost("silences")]
    [ProducesResponseType(typeof(AlertSilenceDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateSilence([FromBody] CreateAlertSilenceRequest request, CancellationToken cancellationToken)
    {
        var actor = GetUserId();
        _logger.LogInformation("Creating alert silence for user {Actor} with {MatcherCount} matchers", actor, request.Matchers.Count);
        var silence = await _incidentResponseService.CreateSilenceAsync(request, actor, cancellationToken);
        return CreatedAtAction(nameof(GetSilences), new { }, silence);
    }

    [HttpGet("incidents")]
    [ProducesResponseType(typeof(IReadOnlyCollection<OperationalIncidentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIncidents(CancellationToken cancellationToken)
    {
        var incidents = await _incidentResponseService.GetIncidentsAsync(cancellationToken);
        return Ok(incidents);
    }

    [HttpPost("incidents")]
    [ProducesResponseType(typeof(OperationalIncidentDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateIncident([FromBody] CreateOperationalIncidentRequest request, CancellationToken cancellationToken)
    {
        var actor = GetUserId();
        _logger.LogWarning("Operational incident raised for {AlertName} by {Actor}", request.AlertName, actor);
        var incident = await _incidentResponseService.CreateIncidentAsync(request, actor, cancellationToken);
        return CreatedAtAction(nameof(GetIncidents), new { incidentId = incident.IncidentId }, incident);
    }

    [HttpPost("incidents/{incidentId:guid}/resolve")]
    [ProducesResponseType(typeof(OperationalIncidentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResolveIncident(Guid incidentId, [FromBody] ResolveOperationalIncidentRequest request, CancellationToken cancellationToken)
    {
        var actor = GetUserId();
        _logger.LogInformation("Resolving incident {IncidentId} by {Actor}", incidentId, actor);
        var resolved = await _incidentResponseService.ResolveIncidentAsync(incidentId, request, actor, cancellationToken);
        return resolved is null ? NotFound() : Ok(resolved);
    }

    private string GetUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "system";
}
