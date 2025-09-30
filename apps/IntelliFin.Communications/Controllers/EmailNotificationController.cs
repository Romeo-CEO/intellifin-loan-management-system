using IntelliFin.Communications.Models;
using IntelliFin.Communications.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace IntelliFin.Communications.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EmailNotificationController : ControllerBase
{
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _templateService;
    private readonly ILogger<EmailNotificationController> _logger;

    public EmailNotificationController(
        IEmailService emailService,
        IEmailTemplateService templateService,
        ILogger<EmailNotificationController> logger)
    {
        _emailService = emailService;
        _templateService = templateService;
        _logger = logger;
    }

    /// <summary>
    /// Send a single email notification
    /// </summary>
    [HttpPost("send")]
    public async Task<IActionResult> SendEmail([FromBody] SendEmailRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var response = await _emailService.SendEmailAsync(request);
            
            if (response.Success)
            {
                return Ok(response);
            }
            
            return BadRequest(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email to {To}", request.To);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Send bulk email notifications
    /// </summary>
    [HttpPost("send-bulk")]
    public async Task<IActionResult> SendBulkEmail([FromBody] BulkEmailRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var response = await _emailService.SendBulkEmailAsync(request);
            
            if (response.Success)
            {
                return Ok(response);
            }
            
            return BadRequest(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending bulk email with {Count} recipients", request.Recipients.Count);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get email delivery report for a specific message
    /// </summary>
    [HttpGet("delivery-report/{messageId}")]
    public async Task<IActionResult> GetDeliveryReport(string messageId)
    {
        try
        {
            var report = await _emailService.GetDeliveryReportAsync(messageId);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting delivery report for message {MessageId}", messageId);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get delivery reports for multiple messages
    /// </summary>
    [HttpPost("delivery-reports")]
    public async Task<IActionResult> GetDeliveryReports([FromBody] List<string> messageIds)
    {
        try
        {
            var reports = await _emailService.GetDeliveryReportsAsync(messageIds);
            return Ok(reports);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting delivery reports for {Count} messages", messageIds.Count);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get email analytics for a date range
    /// </summary>
    [HttpGet("analytics")]
    public async Task<IActionResult> GetAnalytics([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
        try
        {
            if (startDate == default || endDate == default)
            {
                return BadRequest("Start date and end date are required");
            }

            if (startDate > endDate)
            {
                return BadRequest("Start date cannot be greater than end date");
            }

            var analytics = await _emailService.GetAnalyticsAsync(startDate, endDate);
            return Ok(analytics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting email analytics from {StartDate} to {EndDate}", startDate, endDate);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get email history for a specific email address
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetEmailHistory([FromQuery] string emailAddress, [FromQuery] int pageSize = 50, [FromQuery] int pageNumber = 1)
    {
        try
        {
            if (string.IsNullOrEmpty(emailAddress))
            {
                return BadRequest("Email address is required");
            }

            var history = await _emailService.GetEmailHistoryAsync(emailAddress, pageSize, pageNumber);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting email history for {EmailAddress}", emailAddress);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Process webhook from email gateway
    /// </summary>
    [HttpPost("webhook/{gateway}")]
    [AllowAnonymous] // Webhooks typically come from external services without auth
    public async Task<IActionResult> ProcessWebhook(string gateway, [FromBody] object payload)
    {
        try
        {
            var payloadString = System.Text.Json.JsonSerializer.Serialize(payload);
            var processed = await _emailService.ProcessWebhookAsync(gateway, payloadString);
            
            if (processed)
            {
                return Ok(new { message = "Webhook processed successfully" });
            }
            
            return BadRequest(new { message = "Failed to process webhook" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook from gateway {Gateway}", gateway);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Process unsubscribe request
    /// </summary>
    [HttpPost("unsubscribe")]
    [AllowAnonymous]
    public async Task<IActionResult> ProcessUnsubscribe([FromBody] UnsubscribeRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            await _emailService.ProcessUnsubscribeAsync(request);
            return Ok(new { message = "Unsubscribe processed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing unsubscribe for email {EmailAddress}", request.EmailAddress);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    #region Email Templates

    /// <summary>
    /// Create a new email template
    /// </summary>
    [HttpPost("templates")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateEmailTemplateRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var template = await _templateService.CreateTemplateAsync(request);
            return CreatedAtAction(nameof(GetTemplate), new { templateId = template.Id }, template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating email template {TemplateName}", request.Name);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Update an existing email template
    /// </summary>
    [HttpPut("templates/{templateId}")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> UpdateTemplate(string templateId, [FromBody] CreateEmailTemplateRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var template = await _templateService.UpdateTemplateAsync(templateId, request);
            return Ok(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating email template {TemplateId}", templateId);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get a specific email template
    /// </summary>
    [HttpGet("templates/{templateId}")]
    public async Task<IActionResult> GetTemplate(string templateId)
    {
        try
        {
            var template = await _templateService.GetTemplateAsync(templateId);
            
            if (template == null)
            {
                return NotFound();
            }
            
            return Ok(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting email template {TemplateId}", templateId);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Get all email templates, optionally filtered by category
    /// </summary>
    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates([FromQuery] EmailCategory? category = null)
    {
        try
        {
            var templates = await _templateService.GetTemplatesAsync(category);
            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting email templates");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Delete an email template
    /// </summary>
    [HttpDelete("templates/{templateId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteTemplate(string templateId)
    {
        try
        {
            await _templateService.DeleteTemplateAsync(templateId);
            return Ok(new { message = "Template deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting email template {TemplateId}", templateId);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Render an email template with parameters
    /// </summary>
    [HttpPost("templates/{templateId}/render")]
    public async Task<IActionResult> RenderTemplate(string templateId, [FromBody] Dictionary<string, string> parameters)
    {
        try
        {
            var (subject, textContent, htmlContent) = await _templateService.RenderFullTemplateAsync(templateId, parameters);
            
            return Ok(new
            {
                subject,
                textContent,
                htmlContent
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rendering email template {TemplateId}", templateId);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Validate an email template
    /// </summary>
    [HttpPost("templates/{templateId}/validate")]
    public async Task<IActionResult> ValidateTemplate(string templateId)
    {
        try
        {
            var errors = await _templateService.ValidateTemplateAsync(templateId);
            
            return Ok(new
            {
                isValid = !errors.Any(),
                errors
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating email template {TemplateId}", templateId);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    #endregion

    #region Test Endpoints

    /// <summary>
    /// Send a test email (development/testing only)
    /// </summary>
    [HttpPost("send-test")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> SendTestEmail([FromBody] SendTestEmailRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var emailRequest = new SendEmailRequest
            {
                To = request.To,
                Subject = request.Subject ?? "Test Email from IntelliFin",
                TextContent = request.Message ?? "This is a test email from IntelliFin Communications Service.",
                HtmlContent = request.HtmlMessage ?? $"<p>{request.Message ?? "This is a test email from IntelliFin Communications Service."}</p>",
                Priority = EmailPriority.Low,
                Metadata = new Dictionary<string, string>
                {
                    ["test"] = "true",
                    ["sent_by"] = GetCurrentUserId(),
                    ["sent_at"] = DateTime.UtcNow.ToString("O")
                }
            };

            var response = await _emailService.SendEmailAsync(emailRequest);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending test email to {To}", request.To);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    #endregion

    private string GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? 
               User.FindFirst("sub")?.Value ?? 
               User.FindFirst("user_id")?.Value ?? 
               string.Empty;
    }
}

public class SendTestEmailRequest
{
    public string To { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string? Message { get; set; }
    public string? HtmlMessage { get; set; }
}