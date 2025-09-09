using IntelliFin.Communications.Models;
using IntelliFin.Communications.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace IntelliFin.Communications.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SmsNotificationController : ControllerBase
{
    private readonly ILogger<SmsNotificationController> _logger;
    private readonly ISmsService _smsService;
    private readonly ISmsTemplateService _templateService;
    private readonly INotificationWorkflowService _workflowService;

    public SmsNotificationController(
        ILogger<SmsNotificationController> logger,
        ISmsService smsService,
        ISmsTemplateService templateService,
        INotificationWorkflowService workflowService)
    {
        _logger = logger;
        _smsService = smsService;
        _templateService = templateService;
        _workflowService = workflowService;
    }

    [HttpPost("send")]
    public async Task<ActionResult<SmsNotificationResponse>> SendSmsAsync([FromBody] SmsNotificationRequest request)
    {
        try
        {
            var response = await _smsService.SendSmsAsync(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending SMS notification");
            return StatusCode(500, new { error = "Failed to send SMS notification" });
        }
    }

    [HttpPost("send-bulk")]
    public async Task<ActionResult<BulkSmsResponse>> SendBulkSmsAsync([FromBody] BulkSmsRequest request)
    {
        try
        {
            var response = await _smsService.SendBulkSmsAsync(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending bulk SMS notifications");
            return StatusCode(500, new { error = "Failed to send bulk SMS notifications" });
        }
    }

    [HttpPost("send-templated")]
    public async Task<ActionResult<SmsNotificationResponse>> SendTemplatedSmsAsync([FromBody] TemplatedSmsRequest request)
    {
        try
        {
            var response = await _smsService.SendTemplatedSmsAsync(
                request.TemplateId, 
                request.PhoneNumber, 
                request.TemplateData, 
                request.NotificationType);
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending templated SMS notification");
            return StatusCode(500, new { error = "Failed to send templated SMS notification" });
        }
    }

    [HttpGet("delivery-status/{notificationId}")]
    public async Task<ActionResult<SmsDeliveryReport>> GetDeliveryStatusAsync(string notificationId)
    {
        try
        {
            var status = await _smsService.GetDeliveryStatusAsync(notificationId);
            
            if (status == null)
            {
                return NotFound(new { error = "Notification not found" });
            }
            
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving delivery status for {NotificationId}", notificationId);
            return StatusCode(500, new { error = "Failed to retrieve delivery status" });
        }
    }

    [HttpGet("bulk-delivery-status/{batchId}")]
    public async Task<ActionResult<List<SmsDeliveryReport>>> GetBulkDeliveryStatusAsync(string batchId)
    {
        try
        {
            var status = await _smsService.GetBulkDeliveryStatusAsync(batchId);
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bulk delivery status for {BatchId}", batchId);
            return StatusCode(500, new { error = "Failed to retrieve bulk delivery status" });
        }
    }

    [HttpGet("analytics")]
    public async Task<ActionResult<SmsAnalytics>> GetAnalyticsAsync([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
    {
        try
        {
            var analytics = await _smsService.GetSmsAnalyticsAsync(startDate, endDate);
            return Ok(analytics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving SMS analytics");
            return StatusCode(500, new { error = "Failed to retrieve SMS analytics" });
        }
    }

    [HttpPost("validate-phone")]
    public async Task<ActionResult<PhoneValidationResponse>> ValidatePhoneNumberAsync([FromBody] PhoneValidationRequest request)
    {
        try
        {
            var isValid = await _smsService.ValidatePhoneNumberAsync(request.PhoneNumber);
            var provider = isValid ? await _smsService.GetOptimalProviderAsync(request.PhoneNumber) : SmsProvider.Airtel;
            
            return Ok(new PhoneValidationResponse
            {
                IsValid = isValid,
                OptimalProvider = provider,
                PhoneNumber = request.PhoneNumber
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating phone number");
            return StatusCode(500, new { error = "Failed to validate phone number" });
        }
    }

    [HttpPost("estimate-cost")]
    public async Task<ActionResult<CostEstimateResponse>> EstimateCostAsync([FromBody] SmsNotificationRequest request)
    {
        try
        {
            var cost = await _smsService.EstimateCostAsync(request);
            
            return Ok(new CostEstimateResponse
            {
                EstimatedCost = cost,
                Currency = "ZMW"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error estimating SMS cost");
            return StatusCode(500, new { error = "Failed to estimate SMS cost" });
        }
    }

    [HttpPost("estimate-bulk-cost")]
    public async Task<ActionResult<CostEstimateResponse>> EstimateBulkCostAsync([FromBody] BulkSmsRequest request)
    {
        try
        {
            var cost = await _smsService.EstimateBulkCostAsync(request);
            
            return Ok(new CostEstimateResponse
            {
                EstimatedCost = cost,
                Currency = "ZMW"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error estimating bulk SMS cost");
            return StatusCode(500, new { error = "Failed to estimate bulk SMS cost" });
        }
    }

    [HttpGet("templates")]
    public async Task<ActionResult<List<SmsTemplate>>> GetTemplatesAsync()
    {
        try
        {
            var templates = await _templateService.GetAllTemplatesAsync();
            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving SMS templates");
            return StatusCode(500, new { error = "Failed to retrieve SMS templates" });
        }
    }

    [HttpGet("templates/{templateId}")]
    public async Task<ActionResult<SmsTemplate>> GetTemplateAsync(string templateId)
    {
        try
        {
            var template = await _templateService.GetTemplateAsync(templateId);
            
            if (template == null)
            {
                return NotFound(new { error = "Template not found" });
            }
            
            return Ok(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving SMS template {TemplateId}", templateId);
            return StatusCode(500, new { error = "Failed to retrieve SMS template" });
        }
    }

    [HttpPost("templates")]
    [Authorize(Roles = "Admin,Communications")]
    public async Task<ActionResult<SmsTemplate>> CreateTemplateAsync([FromBody] SmsTemplate template)
    {
        try
        {
            var createdTemplate = await _templateService.CreateTemplateAsync(template);
            return CreatedAtAction(nameof(GetTemplateAsync), new { templateId = createdTemplate.Id }, createdTemplate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating SMS template");
            return StatusCode(500, new { error = "Failed to create SMS template" });
        }
    }

    [HttpPut("templates/{templateId}")]
    [Authorize(Roles = "Admin,Communications")]
    public async Task<ActionResult<SmsTemplate>> UpdateTemplateAsync(string templateId, [FromBody] SmsTemplate template)
    {
        try
        {
            template.Id = templateId;
            var updatedTemplate = await _templateService.UpdateTemplateAsync(template);
            return Ok(updatedTemplate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating SMS template {TemplateId}", templateId);
            return StatusCode(500, new { error = "Failed to update SMS template" });
        }
    }

    [HttpDelete("templates/{templateId}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> DeleteTemplateAsync(string templateId)
    {
        try
        {
            var deleted = await _templateService.DeleteTemplateAsync(templateId);
            
            if (!deleted)
            {
                return NotFound(new { error = "Template not found" });
            }
            
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting SMS template {TemplateId}", templateId);
            return StatusCode(500, new { error = "Failed to delete SMS template" });
        }
    }

    [HttpPost("templates/{templateId}/test")]
    [Authorize(Roles = "Admin,Communications")]
    public async Task<ActionResult<TemplateTestResponse>> TestTemplateAsync(string templateId, [FromBody] TemplateTestRequest request)
    {
        try
        {
            var isValid = await _templateService.TestTemplateAsync(templateId, request.TestData);
            var renderedMessage = await _templateService.RenderTemplateAsync(templateId, request.TestData);
            
            return Ok(new TemplateTestResponse
            {
                IsValid = isValid,
                RenderedMessage = renderedMessage
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing SMS template {TemplateId}", templateId);
            return StatusCode(500, new { error = "Failed to test SMS template" });
        }
    }

    [HttpGet("notification-history/{clientId}")]
    public async Task<ActionResult<List<SmsNotificationResponse>>> GetNotificationHistoryAsync(
        string clientId, 
        [FromQuery] DateTime? startDate = null, 
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var history = await _workflowService.GetNotificationHistoryAsync(clientId, startDate, endDate);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notification history for client {ClientId}", clientId);
            return StatusCode(500, new { error = "Failed to retrieve notification history" });
        }
    }

    [HttpPost("opt-out")]
    public async Task<ActionResult> OptOutAsync([FromBody] SmsOptOutRequest request)
    {
        try
        {
            // This would typically update the database
            // For now, just log the opt-out request
            _logger.LogInformation("Opt-out request received for {PhoneNumber}", request.PhoneNumber);
            return Ok(new { message = "Opt-out request processed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing opt-out request");
            return StatusCode(500, new { error = "Failed to process opt-out request" });
        }
    }

    [HttpPost("retry-failed")]
    [Authorize(Roles = "Admin,Communications")]
    public async Task<ActionResult> RetryFailedMessagesAsync([FromQuery] DateTime? cutoffDate = null)
    {
        try
        {
            var cutoff = cutoffDate ?? DateTime.UtcNow.AddHours(-24);
            await _smsService.RetryFailedMessagesAsync(cutoff);
            
            return Ok(new { message = "Failed messages retry initiated" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrying failed messages");
            return StatusCode(500, new { error = "Failed to retry failed messages" });
        }
    }

    [HttpGet("health")]
    public async Task<ActionResult<SmsHealthStatus>> GetHealthStatusAsync()
    {
        try
        {
            var rateLimitExceeded = await _smsService.IsRateLimitExceededAsync();
            
            return Ok(new SmsHealthStatus
            {
                IsHealthy = !rateLimitExceeded,
                RateLimitExceeded = rateLimitExceeded,
                CheckedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking SMS service health");
            return StatusCode(500, new { error = "Failed to check SMS service health" });
        }
    }
}

public class TemplatedSmsRequest
{
    public string TemplateId { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public Dictionary<string, object> TemplateData { get; set; } = new();
    public SmsNotificationType NotificationType { get; set; }
}

public class PhoneValidationRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
}

public class PhoneValidationResponse
{
    public bool IsValid { get; set; }
    public SmsProvider OptimalProvider { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
}

public class CostEstimateResponse
{
    public decimal EstimatedCost { get; set; }
    public string Currency { get; set; } = "ZMW";
}

public class TemplateTestRequest
{
    public Dictionary<string, object> TestData { get; set; } = new();
}

public class TemplateTestResponse
{
    public bool IsValid { get; set; }
    public string RenderedMessage { get; set; } = string.Empty;
}

public class SmsHealthStatus
{
    public bool IsHealthy { get; set; }
    public bool RateLimitExceeded { get; set; }
    public DateTime CheckedAt { get; set; }
}