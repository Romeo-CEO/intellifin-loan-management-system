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
    private readonly ILogger<ClientController> _logger;

    public ClientController(IClientService clientService, ILogger<ClientController> logger)
    {
        _clientService = clientService;
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

        return result.Match(
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
}
