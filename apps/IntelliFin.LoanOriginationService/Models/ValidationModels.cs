namespace IntelliFin.LoanOriginationService.Models;

public record LoanApplicationVariables
{
    public decimal LoanAmount { get; init; }
    public string ProductType { get; init; } = string.Empty;
    public string ApplicantNrc { get; init; } = string.Empty;
    public string BranchId { get; init; } = string.Empty;
}

public record ValidationResult
{
    public bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime ProcessedAt { get; init; } = DateTime.UtcNow;
}