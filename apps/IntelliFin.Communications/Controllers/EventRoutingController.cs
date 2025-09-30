using IntelliFin.Communications.Services;
using IntelliFin.Shared.DomainModels.Entities;
using IntelliFin.Shared.Infrastructure.Messaging.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IntelliFin.Communications.Controllers;

/// <summary>
/// Controller for managing event routing rules and monitoring routing operations
/// </summary>
[Authorize]
[ApiController]
[Route("api/event-routing")]
[Produces("application/json")]
public class EventRoutingController : ControllerBase
{
    private readonly IEventRoutingService _routingService;

    public EventRoutingController(IEventRoutingService routingService)
    {
        _routingService = routingService ?? throw new ArgumentNullException(nameof(routingService));
    }

    /// <summary>
    /// Gets all routing rules with optional filtering
    /// </summary>
    [HttpGet("rules")]
    [ProducesResponseType(typeof(IEnumerable<EventRoutingRule>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EventRoutingRule>>> GetRoutingRules(
        [FromQuery] string? eventType = null,
        [FromQuery] string? consumerType = null,
        [FromQuery] bool? isActive = null)
    {
        try
        {
            var rules = await _routingService.GetRoutingRulesAsync(eventType, consumerType, isActive);
            return Ok(rules);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to retrieve routing rules", details = ex.Message });
        }
    }

    /// <summary>
    /// Creates a new routing rule
    /// </summary>
    [HttpPost("rules")]
    [ProducesResponseType(typeof(EventRoutingRule), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EventRoutingRule>> CreateRoutingRule([FromBody] CreateRoutingRuleRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var rule = new EventRoutingRule
            {
                EventType = request.EventType,
                ConsumerType = request.ConsumerType,
                Priority = request.Priority,
                Conditions = request.Conditions,
                IsActive = request.IsActive ?? true,
                Description = request.Description
            };

            await _routingService.CreateRoutingRuleAsync(rule);

            return CreatedAtAction(
                nameof(GetRoutingRules),
                new { eventType = rule.EventType },
                rule
            );
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = "Failed to create routing rule", details = ex.Message });
        }
    }

    /// <summary>
    /// Updates an existing routing rule
    /// </summary>
    [HttpPut("rules/{ruleId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateRoutingRule(int ruleId, [FromBody] UpdateRoutingRuleRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var updatedRule = new EventRoutingRule
            {
                EventType = request.EventType,
                ConsumerType = request.ConsumerType,
                Priority = request.Priority,
                Conditions = request.Conditions,
                IsActive = request.IsActive ?? true,
                Description = request.Description,
                UpdatedAt = DateTime.UtcNow
            };

            await _routingService.UpdateRoutingRuleAsync(ruleId, updatedRule);

            return NoContent();
        }
        catch (InvalidOperationException)
        {
            return NotFound(new { error = $"Routing rule {ruleId} not found" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = "Failed to update routing rule", details = ex.Message });
        }
    }

    /// <summary>
    /// Deletes a routing rule
    /// </summary>
    [HttpDelete("rules/{ruleId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRoutingRule(int ruleId)
    {
        try
        {
            await _routingService.DeleteRoutingRuleAsync(ruleId);
            return NoContent();
        }
        catch (InvalidOperationException)
        {
            return NotFound(new { error = $"Routing rule {ruleId} not found" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to delete routing rule", details = ex.Message });
        }
    }

    /// <summary>
    /// Routes an event to demonstrate the routing functionality
    /// </summary>
    [HttpPost("route")]
    [ProducesResponseType(typeof(RouteResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<RouteResult>> RouteEvent([FromBody] TestRouteRequest request)
    {
        try
        {
            // Create a test business event
            var testEvent = new TestBusinessEvent
            {
                EventId = Guid.NewGuid(),
                EventTimestamp = DateTime.UtcNow,
                EventType = request.EventType,
                SourceService = request.SourceService ?? "TestService"
            };

            var result = await _routingService.RouteEventAsync(testEvent);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to route event", details = ex.Message });
        }
    }

    /// <summary>
    /// Validates if a consumer can process a specific event type
    /// </summary>
    [HttpGet("can-process/{consumerType}/{eventType}")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<ActionResult<bool>> CanProcessEvent(string consumerType, string eventType)
    {
        try
        {
            var testEvent = new TestBusinessEvent
            {
                EventId = Guid.NewGuid(),
                EventTimestamp = DateTime.UtcNow,
                EventType = eventType,
                SourceService = "TestService"
            };

            var canProcess = await _routingService.CanConsumerProcessEventAsync(testEvent, consumerType);
            return Ok(canProcess);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to validate consumer", details = ex.Message });
        }
    }

    /// <summary>
    /// Gets routing performance metrics
    /// </summary>
    [HttpGet("metrics")]
    [ProducesResponseType(typeof(EventRoutingMetrics), StatusCodes.Status200OK)]
    public async Task<ActionResult<EventRoutingMetrics>> GetMetrics()
    {
        try
        {
            var metrics = await _routingService.GetMetricsAsync();
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to retrieve routing metrics", details = ex.Message });
        }
    }

    /// <summary>
    /// Gets system status and available consumers
    /// </summary>
    [HttpGet("system-status")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RoutingSystemStatus), StatusCodes.Status200OK)]
    public ActionResult<RoutingSystemStatus> GetSystemStatus()
    {
        var status = new RoutingSystemStatus
        {
            ServiceName = "Event Routing Framework",
            Version = "1.0.0",
            Status = "Active",
            AvailableEventTypes = new[]
            {
                "LoanApplicationCreated",
                "LoanStatusChanged",
                "PaymentDueReminder"
            },
            AvailableConsumerTypes = new[]
            {
                "LoanApplicationConsumer",
                "LoanStatusConsumer",
                "PaymentReminderConsumer"
            },
            LastHealthCheck = DateTime.UtcNow,
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
        };

        return Ok(status);
    }

    /// <summary>
    /// Seeds the system with default routing rules
    /// </summary>
    [HttpPost("seed-default-rules")]
    [ProducesResponseType(typeof(Dictionary<string, int>), StatusCodes.Status201Created)]
    public async Task<ActionResult<Dictionary<string, int>>> SeedDefaultRules()
    {
        try
        {
            var createdRules = new Dictionary<string, int>();

            // Default rules for loan applications
            await _routingService.CreateRoutingRuleAsync(new EventRoutingRule
            {
                EventType = "LoanApplicationCreated",
                ConsumerType = "LoanApplicationConsumer",
                Priority = 1,
                IsActive = true,
                Description = "Default rule for loan application notifications"
            });
            createdRules["LoanApplicationCreated"] = 1;

            // Default rules for status changes
            await _routingService.CreateRoutingRuleAsync(new EventRoutingRule
            {
                EventType = "LoanStatusChanged",
                ConsumerType = "LoanStatusConsumer",
                Priority = 1,
                IsActive = true,
                Description = "Default rule for loan status change notifications"
            });
            createdRules["LoanStatusChanged"] = 1;

            // Default rules for payment reminders
            await _routingService.CreateRoutingRuleAsync(new EventRoutingRule
            {
                EventType = "PaymentDueReminder",
                ConsumerType = "PaymentReminderConsumer",
                Priority = 1,
                IsActive = true,
                Description = "Default rule for payment due reminders"
            });
            createdRules["PaymentDueReminder"] = 1;

            return Created("", createdRules);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Failed to seed default rules", details = ex.Message });
        }
    }
}

/// <summary>
/// Request model for creating routing rules
/// </summary>
public class CreateRoutingRuleRequest
{
    public string EventType { get; set; } = string.Empty;
    public string ConsumerType { get; set; } = string.Empty;
    public int Priority { get; set; } = 1;
    public string? Conditions { get; set; }
    public bool? IsActive { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Request model for updating routing rules
/// </summary>
public class UpdateRoutingRuleRequest
{
    public string EventType { get; set; } = string.Empty;
    public string ConsumerType { get; set; } = string.Empty;
    public int Priority { get; set; } = 1;
    public string? Conditions { get; set; }
    public bool? IsActive { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Request model for testing event routing
/// </summary>
public class TestRouteRequest
{
    public string EventType { get; set; } = string.Empty;
    public string? SourceService { get; set; }
}

/// <summary>
/// Simple test business event for routing
/// </summary>
public class TestBusinessEvent : IBusinessEvent
{
    public Guid EventId { get; set; }
    public DateTime EventTimestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string SourceService { get; set; } = string.Empty;
}

/// <summary>
/// System status response
/// </summary>
public class RoutingSystemStatus
{
    public string ServiceName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public IEnumerable<string> AvailableEventTypes { get; set; } = new List<string>();
    public IEnumerable<string> AvailableConsumerTypes { get; set; } = new List<string>();
    public DateTime LastHealthCheck { get; set; }
    public string Environment { get; set; } = string.Empty;
}
