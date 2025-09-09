using IntelliFin.IdentityService.Models;
using IntelliFin.IdentityService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace IntelliFin.IdentityService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,SuperAdmin")]
public class RoleController : ControllerBase
{
    private readonly IRoleService _roleService;
    private readonly ILogger<RoleController> _logger;

    public RoleController(IRoleService roleService, ILogger<RoleController> logger)
    {
        _roleService = roleService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<RoleResponse>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetAllRolesAsync(
        [FromQuery] bool includeInactive = false, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var roles = await _roleService.GetAllRolesAsync(includeInactive, cancellationToken);
            
            var response = roles.Select(r => new RoleResponse
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description,
                Type = r.Type,
                IsActive = r.IsActive,
                IsSystemRole = r.IsSystemRole,
                ParentRoleId = r.ParentRoleId,
                Level = r.Level,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt,
                CreatedBy = r.CreatedBy,
                UpdatedBy = r.UpdatedBy
            });

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving roles");
            return Problem(
                title: "Error Retrieving Roles",
                detail: "An error occurred while retrieving roles",
                statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(RoleResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> GetRoleByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var role = await _roleService.GetRoleByIdAsync(id, cancellationToken);
            
            if (role == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Role Not Found",
                    Detail = $"Role with ID {id} was not found",
                    Status = (int)HttpStatusCode.NotFound
                });
            }

            var response = new RoleResponse
            {
                Id = role.Id,
                Name = role.Name,
                Description = role.Description,
                Type = role.Type,
                IsActive = role.IsActive,
                IsSystemRole = role.IsSystemRole,
                ParentRoleId = role.ParentRoleId,
                Level = role.Level,
                CreatedAt = role.CreatedAt,
                UpdatedAt = role.UpdatedAt,
                CreatedBy = role.CreatedBy,
                UpdatedBy = role.UpdatedBy
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving role {RoleId}", id);
            return Problem(
                title: "Error Retrieving Role",
                detail: "An error occurred while retrieving the role",
                statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(RoleResponse), (int)HttpStatusCode.Created)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
    public async Task<IActionResult> CreateRoleAsync(
        [FromBody] RoleRequest request, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var createdBy = User.FindFirst("sub")?.Value ?? "system";
            
            var role = await _roleService.CreateRoleAsync(request, createdBy, cancellationToken);

            var response = new RoleResponse
            {
                Id = role.Id,
                Name = role.Name,
                Description = role.Description,
                Type = role.Type,
                IsActive = role.IsActive,
                IsSystemRole = role.IsSystemRole,
                ParentRoleId = role.ParentRoleId,
                Level = role.Level,
                CreatedAt = role.CreatedAt,
                CreatedBy = role.CreatedBy
            };

            return CreatedAtAction(nameof(GetRoleByIdAsync), new { id = role.Id }, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating role {RoleName}", request.Name);
            return Problem(
                title: "Error Creating Role",
                detail: "An error occurred while creating the role",
                statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(RoleResponse), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
    public async Task<IActionResult> UpdateRoleAsync(
        string id,
        [FromBody] RoleRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var updatedBy = User.FindFirst("sub")?.Value ?? "system";
            
            var role = await _roleService.UpdateRoleAsync(id, request, updatedBy, cancellationToken);

            var response = new RoleResponse
            {
                Id = role.Id,
                Name = role.Name,
                Description = role.Description,
                Type = role.Type,
                IsActive = role.IsActive,
                IsSystemRole = role.IsSystemRole,
                ParentRoleId = role.ParentRoleId,
                Level = role.Level,
                CreatedAt = role.CreatedAt,
                UpdatedAt = role.UpdatedAt,
                CreatedBy = role.CreatedBy,
                UpdatedBy = role.UpdatedBy
            };

            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Role Not Found",
                Detail = $"Role with ID {id} was not found",
                Status = (int)HttpStatusCode.NotFound
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating role {RoleId}", id);
            return Problem(
                title: "Error Updating Role",
                detail: "An error occurred while updating the role",
                statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }

    [HttpDelete("{id}")]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> DeleteRoleAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var deletedBy = User.FindFirst("sub")?.Value ?? "system";
            
            var success = await _roleService.DeleteRoleAsync(id, deletedBy, cancellationToken);
            
            if (!success)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Role Not Found",
                    Detail = $"Role with ID {id} was not found",
                    Status = (int)HttpStatusCode.NotFound
                });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting role {RoleId}", id);
            return Problem(
                title: "Error Deleting Role",
                detail: "An error occurred while deleting the role",
                statusCode: (int)HttpStatusCode.InternalServerError);
        }
    }
}