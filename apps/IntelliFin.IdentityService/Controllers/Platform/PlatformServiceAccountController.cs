using IntelliFin.IdentityService.Constants;
using IntelliFin.IdentityService.Models;
using IntelliFin.IdentityService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Net;

namespace IntelliFin.IdentityService.Controllers.Platform;

/// <summary>
/// Platform Service Account Management Controller
/// Provides CRUD operations for service account lifecycle management (Story 3.1)
/// </summary>
[ApiController]
[Route("api/platform/service-accounts")]
[Authorize(Policy = AuthorizationPolicies.PlatformServiceAccounts)]
[Produces("application/json")]
public class PlatformServiceAccountController : ControllerBase
{
    private readonly IServiceAccountService _serviceAccountService;
    private readonly ILogger<PlatformServiceAccountController> _logger;

    public PlatformServiceAccountController(
        IServiceAccountService serviceAccountService,
        ILogger<PlatformServiceAccountController> logger)
    {
        _serviceAccountService = serviceAccountService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new service account with an initial credential.
    /// Returns clientId and plaintext secret once; secret is BCrypt-hashed in storage.
    /// </summary>
    /// <param name="request">Service account creation parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Service account DTO with plaintext credential (returned once only)</returns>
    /// <response code="201">Service account created successfully</response>
    /// <response code="400">Invalid request parameters</response>
    /// <response code="401">Unauthorized - invalid or missing authentication</response>
    /// <response code="403">Forbidden - insufficient permissions</response>
    /// <response code="500">Internal server error</response>
    [HttpPost]
    [ProducesResponseType(typeof(ServiceAccountDto), (int)HttpStatusCode.Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.InternalServerError)]
    public async Task<IActionResult> CreateServiceAccountAsync(
        [FromBody] ServiceAccountCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            _logger.LogInformation("Creating service account: {Name}", request.Name);
            
            var result = await _serviceAccountService.CreateServiceAccountAsync(request, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Service account created successfully: ClientId={ClientId}, Id={Id}",
                result.ClientId,
                result.Id);

            return CreatedAtAction(
                nameof(GetServiceAccountAsync),
                new { id = result.Id },
                result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to create service account due to invalid operation: {Name}", request.Name);
            return BadRequest(new ProblemDetails
            {
                Title = "Service account creation failed",
                Detail = ex.Message,
                Status = (int)HttpStatusCode.BadRequest
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating service account: {Name}", request.Name);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Service account creation failed",
                Detail = "An unexpected error occurred while creating the service account.",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Rotates the secret for an existing service account.
    /// Creates a new credential; the previous credential remains valid until explicitly revoked or expired.
    /// Returns the new plaintext secret once.
    /// </summary>
    /// <param name="id">Service account unique identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>New credential with plaintext secret (returned once only)</returns>
    /// <response code="200">Secret rotated successfully</response>
    /// <response code="404">Service account not found</response>
    /// <response code="400">Invalid operation (e.g., account inactive)</response>
    /// <response code="401">Unauthorized - invalid or missing authentication</response>
    /// <response code="403">Forbidden - insufficient permissions</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("{id:guid}/rotate")]
    [ProducesResponseType(typeof(ServiceCredentialDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.InternalServerError)]
    public async Task<IActionResult> RotateSecretAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Rotating secret for service account: {Id}", id);
            
            var result = await _serviceAccountService.RotateSecretAsync(id, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Secret rotated successfully for service account: Id={Id}, CredentialId={CredentialId}",
                id,
                result.Id);

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Service account not found: {Id}", id);
            return NotFound(new ProblemDetails
            {
                Title = "Service account not found",
                Detail = ex.Message,
                Status = (int)HttpStatusCode.NotFound
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot rotate secret for service account: {Id}", id);
            return BadRequest(new ProblemDetails
            {
                Title = "Secret rotation failed",
                Detail = ex.Message,
                Status = (int)HttpStatusCode.BadRequest
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error rotating secret for service account: {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Secret rotation failed",
                Detail = "An unexpected error occurred while rotating the service account secret.",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Revokes a service account, deactivating it and revoking all active credentials.
    /// Once revoked, the account cannot issue new tokens.
    /// </summary>
    /// <param name="id">Service account unique identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content on success</returns>
    /// <response code="204">Service account revoked successfully</response>
    /// <response code="404">Service account not found</response>
    /// <response code="401">Unauthorized - invalid or missing authentication</response>
    /// <response code="403">Forbidden - insufficient permissions</response>
    /// <response code="500">Internal server error</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.InternalServerError)]
    public async Task<IActionResult> RevokeServiceAccountAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Revoking service account: {Id}", id);
            
            await _serviceAccountService.RevokeServiceAccountAsync(id, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation("Service account revoked successfully: {Id}", id);

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Service account not found: {Id}", id);
            return NotFound(new ProblemDetails
            {
                Title = "Service account not found",
                Detail = ex.Message,
                Status = (int)HttpStatusCode.NotFound
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error revoking service account: {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Service account revocation failed",
                Detail = "An unexpected error occurred while revoking the service account.",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Retrieves a service account by ID (without credentials).
    /// </summary>
    /// <param name="id">Service account unique identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Service account details</returns>
    /// <response code="200">Service account retrieved successfully</response>
    /// <response code="404">Service account not found</response>
    /// <response code="401">Unauthorized - invalid or missing authentication</response>
    /// <response code="403">Forbidden - insufficient permissions</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ServiceAccountDto), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.InternalServerError)]
    public async Task<IActionResult> GetServiceAccountAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _serviceAccountService.GetServiceAccountAsync(id, cancellationToken)
                .ConfigureAwait(false);

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Service account not found: {Id}", id);
            return NotFound(new ProblemDetails
            {
                Title = "Service account not found",
                Detail = ex.Message,
                Status = (int)HttpStatusCode.NotFound
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving service account: {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Service account retrieval failed",
                Detail = "An unexpected error occurred while retrieving the service account.",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Lists all service accounts with optional filtering.
    /// </summary>
    /// <param name="isActive">Filter by active status (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of service accounts</returns>
    /// <response code="200">Service accounts retrieved successfully</response>
    /// <response code="401">Unauthorized - invalid or missing authentication</response>
    /// <response code="403">Forbidden - insufficient permissions</response>
    /// <response code="500">Internal server error</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ServiceAccountDto>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.InternalServerError)]
    public async Task<IActionResult> ListServiceAccountsAsync(
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _serviceAccountService.ListServiceAccountsAsync(isActive, cancellationToken)
                .ConfigureAwait(false);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error listing service accounts");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Service account listing failed",
                Detail = "An unexpected error occurred while listing service accounts.",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }
}
