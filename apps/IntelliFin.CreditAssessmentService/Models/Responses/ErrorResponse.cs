namespace IntelliFin.CreditAssessmentService.Models.Responses;

/// <summary>
/// Standardized error response model matching IntelliFin error format.
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// Error type/category.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Error title/summary.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// HTTP status code.
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// Detailed error message.
    /// </summary>
    public string? Detail { get; set; }

    /// <summary>
    /// Unique correlation ID for request tracing.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Timestamp when error occurred.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Validation errors (if applicable).
    /// </summary>
    public Dictionary<string, string[]>? Errors { get; set; }

    /// <summary>
    /// Additional error metadata.
    /// </summary>
    public Dictionary<string, object>? Extensions { get; set; }
}
