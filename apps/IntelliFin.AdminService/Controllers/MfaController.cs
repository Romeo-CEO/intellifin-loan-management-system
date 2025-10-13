using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using IntelliFin.AdminService.Attributes;
using IntelliFin.AdminService.Contracts.Requests;
using IntelliFin.AdminService.Contracts.Responses;
using IntelliFin.AdminService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelliFin.AdminService.Controllers;

[ApiController]
[Route("api/admin/mfa")]
[Authorize]
public class MfaController : ControllerBase
{
    private readonly IMfaService _mfaService;
    private readonly ILogger<MfaController> _logger;

    public MfaController(IMfaService mfaService, ILogger<MfaController> logger)
    {
        _mfaService = mfaService;
        _logger = logger;
    }

    [HttpPost("challenge")]
    [ProducesResponseType(typeof(MfaChallengeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> InitiateChallenge([FromBody] MfaChallengeRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var userName = User.FindFirstValue(ClaimTypes.Name) ?? userId;
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        _logger.LogInformation("MFA challenge requested by {UserId} for {Operation}", userId, request.Operation);

        try
        {
            var response = await _mfaService.InitiateChallengeAsync(
                userId,
                userName,
                request.Operation,
                ipAddress,
                userAgent,
                cancellationToken);

            return Ok(response);
        }
        catch (UserNotEnrolledException)
        {
            return Ok(new MfaChallengeResponse
            {
                RequiresEnrollment = true,
                EnrollmentUrl = "/api/admin/mfa/enroll"
            });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("validate")]
    [ProducesResponseType(typeof(MfaValidationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ValidateChallenge([FromBody] MfaValidationRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        try
        {
            var response = await _mfaService.ValidateChallengeAsync(userId, request.ChallengeId, request.OtpCode, cancellationToken);
            return Ok(response);
        }
        catch (ChallengeNotFoundException)
        {
            return BadRequest(new { error = "Challenge not found or expired" });
        }
        catch (UserLockedException ex)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                error = "user_locked",
                message = ex.Message,
                lockoutUntil = ex.LockoutUntil
            });
        }
        catch (UnauthorizedMfaException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("enroll")]
    [ProducesResponseType(typeof(MfaEnrollmentResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> EnrollUser(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var userName = User.FindFirstValue(ClaimTypes.Name) ?? userId;
        var userEmail = User.FindFirstValue(ClaimTypes.Email);

        var response = await _mfaService.GenerateEnrollmentAsync(userId, userName, userEmail, cancellationToken);
        return Ok(response);
    }

    [HttpPost("enroll/verify")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEnrollment([FromBody] MfaEnrollmentVerificationRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        try
        {
            await _mfaService.VerifyEnrollmentAsync(userId, request.SecretKey, request.OtpCode, cancellationToken);
            return Ok(new { message = "MFA enrollment completed successfully" });
        }
        catch (InvalidOtpException)
        {
            return BadRequest(new { error = "Invalid OTP code. Please try again." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("enrollment/status")]
    [ProducesResponseType(typeof(MfaEnrollmentStatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEnrollmentStatus(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var status = await _mfaService.GetEnrollmentStatusAsync(userId, cancellationToken);
        return Ok(status);
    }

    [HttpGet("config")]
    [Authorize(Roles = "System Administrator")]
    [ProducesResponseType(typeof(IReadOnlyCollection<MfaConfigDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConfiguration(CancellationToken cancellationToken)
    {
        var config = await _mfaService.GetMfaConfigurationAsync(cancellationToken);
        return Ok(config);
    }

    [HttpPut("config/{operationName}")]
    [Authorize(Roles = "System Administrator")]
    [RequiresMfa(TimeoutMinutes = 15)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateConfiguration(string operationName, [FromBody] MfaConfigUpdateDto update, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        try
        {
            await _mfaService.UpdateMfaConfigurationAsync(operationName, update, adminId, cancellationToken);
            return Ok(new { message = "MFA configuration updated successfully" });
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }
}
