namespace IntelliFin.LoanOriginationService.Models;

/// <summary>
/// Represents a single entry in a loan repayment schedule.
/// Used for agreement generation and amortization calculations.
/// </summary>
public class RepaymentScheduleEntry
{
    /// <summary>
    /// The month number in the repayment schedule (1-based).
    /// </summary>
    public int Month { get; set; }
    
    /// <summary>
    /// The due date for this payment.
    /// </summary>
    public DateTime DueDate { get; set; }
    
    /// <summary>
    /// The principal amount paid in this installment.
    /// </summary>
    public decimal PrincipalPayment { get; set; }
    
    /// <summary>
    /// The interest amount paid in this installment.
    /// </summary>
    public decimal InterestPayment { get; set; }
    
    /// <summary>
    /// The total payment amount (principal + interest).
    /// </summary>
    public decimal TotalPayment { get; set; }
    
    /// <summary>
    /// The remaining principal balance after this payment.
    /// </summary>
    public decimal Balance { get; set; }
}
