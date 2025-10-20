using IntelliFin.IdentityService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelliFin.IdentityService.Controllers.Platform;

[ApiController]
[Route("api/platform/seed")]
[Authorize(Roles = "PlatformAdmin")]
public class SeedController : ControllerBase
{
    private readonly IBaselineSeedService _seedService;

    public SeedController(IBaselineSeedService seedService)
    {
        _seedService = seedService;
    }

    [HttpPost("baseline")]
    public async Task<IActionResult> SeedBaselineData(CancellationToken cancellationToken)
    {
        var result = await _seedService.SeedBaselineDataAsync(cancellationToken);
        if (result.Success) return Ok(result);
        return BadRequest(result);
    }

    [HttpPost("baseline/validate")]
    public async Task<IActionResult> ValidateBaselineData(CancellationToken cancellationToken)
    {
        var result = await _seedService.ValidateSeedDataAsync(cancellationToken);
        return Ok(result);
    }
}
