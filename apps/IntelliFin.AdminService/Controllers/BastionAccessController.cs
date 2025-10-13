using System.Security.Claims;
using IntelliFin.AdminService.Attributes;
using IntelliFin.AdminService.Contracts.Requests;
using IntelliFin.AdminService.Contracts.Responses;
using IntelliFin.AdminService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelliFin.AdminService.Controllers;

[ApiController]
[Route("api/admin/bastion")]
[Authorize]
public sealed class BastionAccessController : ControllerBase
{
    private readonly IBastionAccessService _bastionAccessService;
    private readonly ILogger<BastionAccessController> _logger;

    public BastionAccessController(
        IBastionAccessService bastionAccessService,
        ILogger<BastionAccessController> logger)
    {
        _bastionAccessService = bastionAccessService;
        _logger = logger;
    }

    [HttpPost("access-requests")]
    [ProducesResponseType(typeof(BastionAccessRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BastionAccessRequestDto), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> RequestAccess(
        [FromBody] BastionAccessRequestInput request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Missing user id");
        var userName = User.FindFirstValue(ClaimTypes.Name) ?? userId;
        var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? $"{userId}@unknown";

        _logger.LogInformation(
            "Bastion access requested by {UserId} for environment {Environment}",
            userId,
            request.Environment);

        var response = await _bastionAccessService.RequestAccessAsync(
            request,
            userId,
            userName,
            userEmail,
            cancellationToken);

        if (response.RequiresApproval)
        {
            return AcceptedAtAction(
                nameof(GetAccessRequestStatus),
                new { requestId = response.RequestId },
                response);
        }

        return Ok(response);
    }

    [HttpGet("access-requests/{requestId}")]
    [ProducesResponseType(typeof(BastionAccessRequestStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAccessRequestStatus(Guid requestId, CancellationToken cancellationToken)
    {
        var status = await _bastionAccessService.GetAccessRequestStatusAsync(requestId, cancellationToken);
        return status is null ? NotFound() : Ok(status);
    }

    [HttpGet("access-requests/{requestId}/certificate")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadCertificate(Guid requestId, CancellationToken cancellationToken)
    {
        var certificate = await _bastionAccessService.GetSshCertificateAsync(requestId, cancellationToken);
        if (certificate is null)
        {
            return NotFound();
        }

        var fileName = $"bastion-cert-{requestId}.pub";
        return File(System.Text.Encoding.UTF8.GetBytes(certificate.CertificateContent), "application/x-pem-file", fileName);
    }

    [HttpGet("sessions")]
    [Authorize(Roles = "System Administrator,Security Engineer")]
    [ProducesResponseType(typeof(IReadOnlyCollection<BastionSessionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveSessions(CancellationToken cancellationToken)
    {
        var sessions = await _bastionAccessService.GetActiveSessionsAsync(cancellationToken);
        return Ok(sessions);
    }

    [HttpPost("sessions")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> RecordSession(
        [FromBody] BastionSessionIngestRequest request,
        CancellationToken cancellationToken)
    {
        await _bastionAccessService.RecordSessionAsync(request, cancellationToken);
        return Accepted();
    }

    [HttpGet("sessions/{sessionId}/recording")]
    [Authorize(Roles = "System Administrator,Security Engineer,Auditor")]
    [ProducesResponseType(typeof(SessionRecordingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSessionRecording(string sessionId, CancellationToken cancellationToken)
    {
        var recording = await _bastionAccessService.GetSessionRecordingAsync(sessionId, cancellationToken);
        return recording is null ? NotFound() : Ok(recording);
    }

    [HttpPost("emergency-access")]
    [Authorize(Roles = "System Administrator")]
    [RequiresMfa(TimeoutMinutes = 15)]
    [ProducesResponseType(typeof(EmergencyAccessDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> RequestEmergencyAccess(
        [FromBody] EmergencyAccessRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Missing user id");
        _logger.LogWarning(
            "Emergency bastion access requested by {UserId} for incident {Incident}",
            userId,
            request.IncidentTicketId);

        var response = await _bastionAccessService.RequestEmergencyAccessAsync(request, userId, cancellationToken);
        return Ok(response);
    }
}
