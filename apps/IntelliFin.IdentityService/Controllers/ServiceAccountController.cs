using IntelliFin.IdentityService.Models;
using IntelliFin.IdentityService.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Net;

namespace IntelliFin.IdentityService.Controllers;

[ApiController]
public class ServiceAccountController : ControllerBase
{
    private readonly IServiceTokenService _serviceTokenService;
    private readonly ILogger<ServiceAccountController> _logger;

    public ServiceAccountController(IServiceTokenService serviceTokenService, ILogger<ServiceAccountController> logger)
    {
        _serviceTokenService = serviceTokenService;
        _logger = logger;
    }

    [HttpPost("/api/auth/service-token")]
    [ProducesResponseType(typeof(ServiceTokenResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.ServiceUnavailable)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.InternalServerError)]
    public async Task<IActionResult> GenerateTokenAsync([FromBody] ClientCredentialsRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var response = await _serviceTokenService.GenerateTokenAsync(request, cancellationToken).ConfigureAwait(false);
            return Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("Service token request rejected for client {ClientId}", request.ClientId);
            return Unauthorized(new ProblemDetails
            {
                Title = "Invalid client credentials",
                Status = (int)HttpStatusCode.Unauthorized
            });
        }
        catch (KeycloakTokenException ex)
        {
            _logger.LogError(ex, "Keycloak token exchange failed for client {ClientId}", request.ClientId);
            return StatusCode(StatusCodes.Status502BadGateway, new ProblemDetails
            {
                Title = "Token issuer unavailable",
                Detail = "Unable to obtain token from upstream identity provider.",
                Status = StatusCodes.Status502BadGateway
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Keycloak configuration error while issuing token for client {ClientId}", request.ClientId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Identity provider configuration error",
                Detail = ex.Message,
                Status = StatusCodes.Status503ServiceUnavailable
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error issuing service token for client {ClientId}", request.ClientId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Token issuance failed",
                Detail = "An unexpected error occurred while issuing the service token.",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }
}
