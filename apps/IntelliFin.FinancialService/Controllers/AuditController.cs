using IntelliFin.FinancialService.Clients;
using IntelliFin.FinancialService.Exceptions;
using IntelliFin.FinancialService.Models.Audit;
using Microsoft.AspNetCore.Mvc;

namespace IntelliFin.FinancialService.Controllers;

[ApiController]
[Route("api/audit")]
public class AuditController : ControllerBase
{
    private readonly IAdminAuditClient _auditClient;
    private readonly ILogger<AuditController> _logger;

    public AuditController(IAdminAuditClient auditClient, ILogger<AuditController> logger)
    {
        _auditClient = auditClient;
        _logger = logger;
    }

    [HttpGet("events")]
    [ProducesResponseType(typeof(AuditEventPageResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvents([FromQuery] AuditEventQuery query, CancellationToken cancellationToken = default)
    {
        try
        {
            var safeQuery = query ?? new AuditEventQuery();
            var response = await _auditClient.GetEventsAsync(safeQuery, cancellationToken);
            return Ok(response);
        }
        catch (AuditForwardingException ex)
        {
            _logger.LogError(ex, "Failed to proxy audit events from Admin Service");
            return StatusCode(503, new { message = "Admin Service audit endpoint unavailable", detail = ex.Message });
        }
    }

    [HttpGet("events/export")]
    public async Task<IActionResult> ExportEvents(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string format = "csv",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var export = await _auditClient.ExportEventsAsync(startDate, endDate, format, cancellationToken);
            return File(export.Content, export.ContentType, export.FileName);
        }
        catch (AuditForwardingException ex)
        {
            _logger.LogError(ex, "Failed to export audit events from Admin Service");
            return StatusCode(503, new { message = "Admin Service audit endpoint unavailable", detail = ex.Message });
        }
    }

    [HttpGet("integrity/status")]
    [ProducesResponseType(typeof(AuditIntegrityStatusResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIntegrityStatus(CancellationToken cancellationToken)
    {
        try
        {
            var status = await _auditClient.GetIntegrityStatusAsync(cancellationToken);
            return Ok(status);
        }
        catch (AuditForwardingException ex)
        {
            _logger.LogError(ex, "Failed to proxy audit integrity status from Admin Service");
            return StatusCode(503, new { message = "Admin Service audit endpoint unavailable", detail = ex.Message });
        }
    }

    [HttpGet("integrity/history")]
    [ProducesResponseType(typeof(AuditIntegrityHistoryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIntegrityHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (page <= 0)
            {
                page = 1;
            }

            if (pageSize <= 0 || pageSize > 500)
            {
                pageSize = 50;
            }

            var history = await _auditClient.GetIntegrityHistoryAsync(page, pageSize, cancellationToken);
            return Ok(history);
        }
        catch (AuditForwardingException ex)
        {
            _logger.LogError(ex, "Failed to proxy audit integrity history from Admin Service");
            return StatusCode(503, new { message = "Admin Service audit endpoint unavailable", detail = ex.Message });
        }
    }
}
