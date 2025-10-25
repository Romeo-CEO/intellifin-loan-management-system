using System.ComponentModel.DataAnnotations;

namespace IntelliFin.CreditAssessmentService.Models.Requests;

/// <summary>
/// Request model for initiating a credit assessment.
/// </summary>
public class AssessmentRequest
{
    /// <summary>
    /// Unique identifier of the loan application being assessed.
    /// </summary>
    [Required]
    public Guid LoanApplicationId { get; set; }

    /// <summary>
    /// Unique identifier of the client/borrower.
    /// </summary>
    [Required]
    public Guid ClientId { get; set; }

    /// <summary>
    /// Employer identifier (required for PAYROLL loans, optional for BUSINESS).
    /// </summary>
    public Guid? EmployerId { get; set; }

    /// <summary>
    /// Requested loan amount in ZMW.
    /// </summary>
    [Required]
    [Range(100, double.MaxValue, ErrorMessage = "Requested amount must be greater than 100 ZMW")]
    public decimal RequestedAmount { get; set; }

    /// <summary>
    /// Loan term in months.
    /// </summary>
    [Required]
    [Range(1, 360, ErrorMessage = "Term must be between 1 and 360 months")]
    public int TermMonths { get; set; }

    /// <summary>
    /// Product type: PAYROLL or BUSINESS.
    /// </summary>
    [Required]
    [RegularExpression("^(PAYROLL|BUSINESS)$", ErrorMessage = "Product type must be PAYROLL or BUSINESS")]
    public string ProductType { get; set; } = string.Empty;

    /// <summary>
    /// Optional additional data for assessment (e.g., collateral details for BUSINESS loans).
    /// </summary>
    public Dictionary<string, object>? AdditionalData { get; set; }

    /// <summary>
    /// Assessment context: Initial, Renewal, Modification.
    /// </summary>
    public string? AssessmentContext { get; set; } = "Initial";
}
