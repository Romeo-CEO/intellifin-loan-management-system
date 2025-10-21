using IntelliFin.ClientManagement.Controllers.DTOs;
using IntelliFin.ClientManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace IntelliFin.ClientManagement.Controllers;

/// <summary>
/// API controller for client operations
/// </summary>
[ApiController]
[Route("api/clients")]
[Authorize]
public class ClientController : ControllerBase
{
    private readonly IClientService _clientService;
    private readonly IClientVersioningService _versioningService;
    private readonly ILogger<ClientController> _logger;

    public ClientController(
        IClientService clientService, 
        IClientVersioningService versioningService,
        ILogger<ClientController> logger)
    {
        _clientService = clientService;
        _versioningService = versioningService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new client
    /// </summary>
    /// <param name="request">Client creation details</param>
    /// <returns>Created client information</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ClientResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateClient([FromBody] CreateClientRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                     ?? User.FindFirst("sub")?.Value 
                     ?? "system";

        _logger.LogInformation("Creating client with NRC {Nrc} by user {UserId}", request.Nrc, userId);

        var result = await _clientService.CreateClientAsync(request, userId);

        return result.Match<IActionResult>(
            onSuccess: client => CreatedAtAction(
                nameof(GetClientById),
                new { id = client.Id },
                client),
            onFailure: error =>
            {
                if (error.Contains("already exists"))
                    return Conflict(new { error });
                return BadRequest(new { error });
            }
        );
    }

    /// <summary>
    /// Retrieves a client by ID
    /// </summary>
    /// <param name="id">Client ID</param>
    /// <returns>Client information</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ClientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetClientById(Guid id)
    {
        var result = await _clientService.GetClientByIdAsync(id);

        return result.Match<IActionResult>(
            onSuccess: client => Ok(client),
            onFailure: error => NotFound(new { error })
        );
    }

    /// <summary>
    /// Retrieves a client by NRC
    /// </summary>
    /// <param name="nrc">National Registration Card number</param>
    /// <returns>Client information</returns>
    [HttpGet("by-nrc/{nrc}")]
    [ProducesResponseType(typeof(ClientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetClientByNrc(string nrc)
    {
        var result = await _clientService.GetClientByNrcAsync(nrc);

        return result.Match<IActionResult>(
            onSuccess: client => Ok(client),
            onFailure: error => NotFound(new { error })
        );
    }

    /// <summary>
    /// Updates an existing client
    /// </summary>
    /// <param name="id">Client ID</param>
    /// <param name="request">Client update details</param>
    /// <returns>Updated client information</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ClientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateClient(Guid id, [FromBody] UpdateClientRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                     ?? User.FindFirst("sub")?.Value 
                     ?? "system";

        _logger.LogInformation("Updating client {ClientId} by user {UserId}", id, userId);

        var result = await _clientService.UpdateClientAsync(id, request, userId);

        return result.Match<IActionResult>(
            onSuccess: client => Ok(client),
            onFailure: error =>
            {
                if (error.Contains("not found"))
                    return NotFound(new { error });
                return BadRequest(new { error });
            }
        );
    }

    /// <summary>
    /// Retrieves version history for a client
    /// </summary>
    /// <param name="id">Client ID</param>
    /// <returns>List of all client versions</returns>
    [HttpGet("{id:guid}/versions")]
    [ProducesResponseType(typeof(List<ClientVersionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetVersionHistory(Guid id)
    {
        var result = await _versioningService.GetVersionHistoryAsync(id);

        return result.Match<IActionResult>(
            onSuccess: versions => Ok(versions),
            onFailure: error => NotFound(new { error })
        );
    }

    /// <summary>
    /// Retrieves a specific version of a client
    /// </summary>
    /// <param name="id">Client ID</param>
    /// <param name="versionNumber">Version number</param>
    /// <returns>Client version information</returns>
    [HttpGet("{id:guid}/versions/{versionNumber:int}")]
    [ProducesResponseType(typeof(ClientVersionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetVersionByNumber(Guid id, int versionNumber)
    {
        var result = await _versioningService.GetVersionByNumberAsync(id, versionNumber);

        return result.Match<IActionResult>(
            onSuccess: version => Ok(version),
            onFailure: error => NotFound(new { error })
        );
    }

    /// <summary>
    /// Retrieves the client version that was valid at a specific point in time
    /// </summary>
    /// <param name="id">Client ID</param>
    /// <param name="timestamp">ISO 8601 timestamp (e.g., 2025-10-20T14:30:00Z)</param>
    /// <returns>Client version valid at the specified timestamp</returns>
    [HttpGet("{id:guid}/versions/at/{timestamp}")]
    [ProducesResponseType(typeof(ClientVersionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetVersionAtTimestamp(Guid id, string timestamp)
    {
        if (!DateTime.TryParse(timestamp, out var asOfDate))
        {
            return BadRequest(new { error = "Invalid timestamp format. Use ISO 8601 format (e.g., 2025-10-20T14:30:00Z)" });
        }

        var result = await _versioningService.GetVersionAtTimestampAsync(id, asOfDate);

        return result.Match<IActionResult>(
            onSuccess: version => Ok(version),
            onFailure: error => NotFound(new { error })
        );
    }
}
