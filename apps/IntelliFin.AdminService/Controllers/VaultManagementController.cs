using System.Security.Claims;
using IntelliFin.AdminService.Attributes;
using IntelliFin.AdminService.Contracts.Requests;
using IntelliFin.AdminService.Contracts.Responses;
using IntelliFin.AdminService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelliFin.AdminService.Controllers;

[ApiController]
[Route("api/admin/vault")]
[Authorize(Roles = "System Administrator,Security Engineer")]
public class VaultManagementController : ControllerBase
{
    private readonly IVaultManagementService _vaultService;
    private readonly ILogger<VaultManagementController> _logger;

    public VaultManagementController(
        IVaultManagementService vaultService,
        ILogger<VaultManagementController> logger)
    {
        _vaultService = vaultService;
        _logger = logger;
    }

    /// <summary>
    /// Revoke a Vault lease immediately (emergency credential revocation).
    /// </summary>
    [HttpPost("revoke-lease")]
    [RequiresMfa(TimeoutMinutes = 15)]
    [ProducesResponseType(typeof(VaultLeaseRevocationResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> RevokeLeaseAsync(
        [FromBody] VaultLeaseRevocationRequest request,
        CancellationToken cancellationToken)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
        var adminName = User.FindFirstValue(ClaimTypes.Name);

        _logger.LogInformation("Emergency Vault lease revocation requested by {AdminId} for {LeaseId}", adminId, request.LeaseId);

        var result = await _vaultService.RevokeLeaseAsync(
            request.LeaseId,
            request.Reason,
            request.IncidentId,
            adminId,
            adminName,
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// List active Vault leases known to the control plane.
    /// </summary>
    [HttpGet("leases")]
    [ProducesResponseType(typeof(IReadOnlyList<VaultLeaseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListLeasesAsync(
        [FromQuery] string? serviceName,
        CancellationToken cancellationToken)
    {
        var leases = await _vaultService.GetActiveLeasesAsync(serviceName, cancellationToken);
        return Ok(leases);
    }
}
