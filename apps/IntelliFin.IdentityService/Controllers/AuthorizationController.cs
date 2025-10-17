using IntelliFin.IdentityService.Constants;
using IntelliFin.IdentityService.Models;
using IntelliFin.IdentityService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace IntelliFin.IdentityService.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthorizationController : ControllerBase
{
    private readonly ITokenIntrospectionService _tokenIntrospectionService;
    private readonly IPermissionCheckService _permissionCheckService;
    private readonly ILogger<AuthorizationController> _logger;

    public AuthorizationController(
        ITokenIntrospectionService tokenIntrospectionService,
        IPermissionCheckService permissionCheckService,
        ILogger<AuthorizationController> logger)
    {
        _tokenIntrospectionService = tokenIntrospectionService;
        _permissionCheckService = permissionCheckService;
        _logger = logger;
    }

    [HttpPost("introspect")]
    [Authorize(Policy = AuthorizationPolicies.SystemTokenIntrospect)]
    [ProducesResponseType(typeof(IntrospectionResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.InternalServerError)]
    public async Task<IActionResult> IntrospectAsync([FromBody] IntrospectionRequest request, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var response = await _tokenIntrospectionService.IntrospectAsync(request, cancellationToken).ConfigureAwait(false);
            return Ok(response);
        }
        catch (UnknownIssuerException ex)
        {
            _logger.LogWarning(ex, "Introspection denied for untrusted issuer");
            return BadRequest(new ProblemDetails
            {
                Title = "Unsupported issuer",
                Detail = ex.Message,
                Status = (int)HttpStatusCode.BadRequest
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during token introspection");
            return StatusCode((int)HttpStatusCode.InternalServerError, new ProblemDetails
            {
                Title = "Introspection failed",
                Detail = "Unable to introspect token at this time.",
                Status = (int)HttpStatusCode.InternalServerError
            });
        }
    }

    [HttpPost("check-permission")]
    [Authorize(Policy = AuthorizationPolicies.SystemPermissionCheck)]
    [ProducesResponseType(typeof(PermissionCheckResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.InternalServerError)]
    public async Task<IActionResult> CheckPermissionAsync([FromBody] PermissionCheckRequest request, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var result = await _permissionCheckService.CheckPermissionAsync(request, cancellationToken).ConfigureAwait(false);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while checking permission {Permission} for user {UserId}", request.Permission, request.UserId);
            return StatusCode((int)HttpStatusCode.InternalServerError, new ProblemDetails
            {
                Title = "Permission check failed",
                Detail = "Unable to complete the permission check at this time.",
                Status = (int)HttpStatusCode.InternalServerError
            });
        }
    }
}
