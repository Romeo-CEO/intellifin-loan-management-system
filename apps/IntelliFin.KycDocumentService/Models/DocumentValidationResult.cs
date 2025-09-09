namespace IntelliFin.KycDocumentService.Models;

public class DocumentValidationResult
{
    public string DocumentId { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public float ConfidenceScore { get; set; }
    public List<ValidationError> Errors { get; set; } = new();
    public List<ValidationWarning> Warnings { get; set; } = new();
    public Dictionary<string, object> ExtractedData { get; set; } = new();
    public bool RequiresManualReview { get; set; }
    public string? ProcessorUsed { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public TimeSpan ProcessingTime { get; set; }
}

public class ValidationError
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string Severity { get; set; } = "Error";
}

public class ValidationWarning
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}