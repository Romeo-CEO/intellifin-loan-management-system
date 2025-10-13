using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using IntelliFin.AdminService.Attributes;
using IntelliFin.AdminService.Contracts.Requests;
using IntelliFin.AdminService.Contracts.Responses;
using IntelliFin.AdminService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace IntelliFin.AdminService.Controllers;

[ApiController]
[Route("api/admin/config")]
[Authorize(Roles = "System Administrator")]
public class ConfigurationController(IConfigurationManagementService configService, ILogger<ConfigurationController> logger) : ControllerBase
{
    private readonly IConfigurationManagementService _configService = configService;
    private readonly ILogger<ConfigurationController> _logger = logger;

    [HttpGet("policies")]
    [ProducesResponseType(typeof(IReadOnlyCollection<ConfigurationPolicyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPolicies([FromQuery] string? category, CancellationToken cancellationToken)
    {
        var policies = await _configService.GetPoliciesAsync(category, cancellationToken);
        return Ok(policies);
    }

    [HttpGet("values")]
    [ProducesResponseType(typeof(IReadOnlyCollection<ConfigurationValueDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetValues([FromQuery] string? category, CancellationToken cancellationToken)
    {
        var values = await _configService.GetCurrentValuesAsync(category, cancellationToken);
        return Ok(values);
    }

    [HttpPost("change-request")]
    [RequiresMfa(TimeoutMinutes = 15)]
    [ProducesResponseType(typeof(ConfigChangeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ConfigChangeResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequestChange([FromBody] ConfigChangeRequest request, CancellationToken cancellationToken)
    {
        var requestorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var requestorName = User.FindFirstValue(ClaimTypes.Name) ?? requestorId;

        _logger.LogInformation("Configuration change request submitted for {ConfigKey} by {Requestor}", request.ConfigKey, requestorId);

        try
        {
            var response = await _configService.RequestChangeAsync(request, requestorId, requestorName, cancellationToken);
            if (response.RequiresApproval)
            {
                return AcceptedAtAction(nameof(GetChangeRequestStatus), new { changeRequestId = response.ChangeRequestId }, response);
            }

            return Ok(response);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("change-requests/{changeRequestId}")]
    [ProducesResponseType(typeof(ConfigChangeStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChangeRequestStatus(Guid changeRequestId, CancellationToken cancellationToken)
    {
        var status = await _configService.GetChangeRequestStatusAsync(changeRequestId, cancellationToken);
        return status is null ? NotFound() : Ok(status);
    }

    [HttpGet("change-requests")]
    [ProducesResponseType(typeof(PagedResult<ConfigChangeSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListChangeRequests([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var result = await _configService.ListChangeRequestsAsync(status, page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpPost("change-requests/{changeRequestId}/approve")]
    [Authorize(Roles = "System Administrator,Manager")]
    [RequiresMfa(TimeoutMinutes = 15)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveChange(Guid changeRequestId, [FromBody] ConfigChangeApprovalDto approval, CancellationToken cancellationToken)
    {
        var approverId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var approverName = User.FindFirstValue(ClaimTypes.Name) ?? approverId;

        try
        {
            await _configService.ApproveChangeAsync(changeRequestId, approverId, approverName, approval.Comments, cancellationToken);
            return Ok(new { message = "Configuration change approved and applied successfully" });
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("change-requests/{changeRequestId}/reject")]
    [Authorize(Roles = "System Administrator,Manager")]
    [RequiresMfa(TimeoutMinutes = 15)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RejectChange(Guid changeRequestId, [FromBody] ConfigChangeRejectionDto rejection, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rejection.Reason) || rejection.Reason.Length < 20)
        {
            return BadRequest(new { error = "Rejection reason must be at least 20 characters" });
        }

        var reviewerId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var reviewerName = User.FindFirstValue(ClaimTypes.Name) ?? reviewerId;

        try
        {
            await _configService.RejectChangeAsync(changeRequestId, reviewerId, reviewerName, rejection.Reason, cancellationToken);
            return Ok(new { message = "Configuration change rejected successfully" });
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("rollback")]
    [RequiresMfa(TimeoutMinutes = 15)]
    [ProducesResponseType(typeof(ConfigRollbackResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Rollback([FromBody] ConfigRollbackRequest request, CancellationToken cancellationToken)
    {
        if (request.ChangeRequestId is null && string.IsNullOrWhiteSpace(request.GitCommitSha))
        {
            return BadRequest(new { error = "Either changeRequestId or gitCommitSha must be provided" });
        }

        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length < 20)
        {
            return BadRequest(new { error = "Rollback reason must be at least 20 characters" });
        }

        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var adminName = User.FindFirstValue(ClaimTypes.Name) ?? adminId;

        try
        {
            var response = await _configService.RollbackChangeAsync(request, adminId, adminName, cancellationToken);
            return AcceptedAtAction(nameof(GetChangeRequestStatus), new { changeRequestId = response.NewChangeRequestId }, response);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("history/{configKey}")]
    [ProducesResponseType(typeof(IReadOnlyCollection<ConfigChangeHistoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(string configKey, [FromQuery] int limit = 50, CancellationToken cancellationToken = default)
    {
        var history = await _configService.GetChangeHistoryAsync(configKey, limit, cancellationToken);
        return Ok(history);
    }

    [HttpPut("policies/{policyId}")]
    [RequiresMfa(TimeoutMinutes = 15)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePolicy(int policyId, [FromBody] ConfigPolicyUpdateDto update, CancellationToken cancellationToken)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        try
        {
            await _configService.UpdatePolicyAsync(policyId, update, adminId, cancellationToken);
            return Ok(new { message = "Configuration policy updated successfully" });
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }
}
