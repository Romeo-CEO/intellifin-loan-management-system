namespace IntelliFin.CreditAssessmentService.Models;

/// <summary>
/// Represents the payload for initiating a credit assessment.
/// </summary>
public sealed class CreditAssessmentRequestDto
{
    public Guid LoanApplicationId { get; set; }
    public Guid ClientId { get; set; }
    public decimal RequestedAmount { get; set; }
    public int TermMonths { get; set; }
    public decimal InterestRate { get; set; }
}
