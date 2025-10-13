using System.ComponentModel.DataAnnotations;
using IntelliFin.AdminService.Contracts.Requests;
using IntelliFin.AdminService.Contracts.Responses;
using IntelliFin.AdminService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelliFin.AdminService.Controllers;

[ApiController]
[Route("api/admin/sboms")]
[Authorize(Roles = "System Administrator,Security Engineer")]
public class SBOMController : ControllerBase
{
    private readonly ISbomService _sbomService;
    private readonly ILogger<SBOMController> _logger;

    public SBOMController(ISbomService sbomService, ILogger<SBOMController> logger)
    {
        _sbomService = sbomService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<SBOMSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync(
        [FromQuery] string? serviceName,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _sbomService.ListSBOMsAsync(serviceName, page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{serviceName}/{version}")]
    [ProducesResponseType(typeof(SBOMDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(string serviceName, string version, CancellationToken cancellationToken)
    {
        var sbom = await _sbomService.GetSBOMAsync(serviceName, version, cancellationToken);
        return sbom is null ? NotFound() : Ok(sbom);
    }

    [HttpGet("{serviceName}/{version}/vulnerabilities")]
    [ProducesResponseType(typeof(VulnerabilityReportDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVulnerabilitiesAsync(string serviceName, string version, CancellationToken cancellationToken)
    {
        var report = await _sbomService.GetVulnerabilitiesAsync(serviceName, version, cancellationToken);
        return Ok(report);
    }

    [HttpGet("{serviceName}/{version}/download")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadAsync(
        string serviceName,
        string version,
        [FromQuery] string format = "spdx",
        CancellationToken cancellationToken = default)
    {
        var content = await _sbomService.DownloadSBOMAsync(serviceName, version, format, cancellationToken);
        if (content is null)
        {
            return NotFound();
        }

        var mimeType = "application/json";
        var fileName = $"{serviceName}-{version}-sbom.{format}.json";
        return File(content, mimeType, fileName);
    }

    [HttpGet("statistics")]
    [ProducesResponseType(typeof(VulnerabilityStatisticsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatisticsAsync(CancellationToken cancellationToken)
    {
        var statistics = await _sbomService.GetVulnerabilityStatisticsAsync(cancellationToken);
        return Ok(statistics);
    }

    [HttpPost("compliance-report")]
    [ProducesResponseType(typeof(ComplianceReportDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GenerateComplianceReportAsync(
        [FromBody] ComplianceReportRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var report = await _sbomService.GenerateComplianceReportAsync(request, cancellationToken);
            return Ok(report);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Invalid compliance report request");
            return BadRequest(new { error = ex.Message });
        }
    }
}
