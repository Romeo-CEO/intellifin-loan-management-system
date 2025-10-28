namespace IntelliFin.LoanOriginationService.Models;

/// <summary>
/// Loan product configuration loaded from HashiCorp Vault.
/// Contains dynamic product rules including EAR compliance validation.
/// </summary>
public class LoanProductConfig
{
    /// <summary>
    /// Human-readable product name
    /// </summary>
    public string ProductName { get; set; } = string.Empty;
    
    /// <summary>
    /// Minimum loan amount in ZMW
    /// </summary>
    public decimal MinAmount { get; set; }
    
    /// <summary>
    /// Maximum loan amount in ZMW
    /// </summary>
    public decimal MaxAmount { get; set; }
    
    /// <summary>
    /// Minimum loan term in months
    /// </summary>
    public int MinTermMonths { get; set; }
    
    /// <summary>
    /// Maximum loan term in months
    /// </summary>
    public int MaxTermMonths { get; set; }
    
    /// <summary>
    /// Base annual interest rate as decimal (e.g., 0.12 for 12%)
    /// </summary>
    public decimal BaseInterestRate { get; set; }
    
    /// <summary>
    /// One-time administrative fee as decimal (e.g., 0.02 for 2%)
    /// </summary>
    public decimal AdminFee { get; set; }
    
    /// <summary>
    /// Recurring management fee as decimal (e.g., 0.01 for 1%)
    /// </summary>
    public decimal ManagementFee { get; set; }
    
    /// <summary>
    /// Calculated Effective Annual Rate including all fees (e.g., 0.152 for 15.2%)
    /// Must be ≤ EarLimit for Bank of Zambia compliance
    /// </summary>
    public decimal CalculatedEAR { get; set; }
    
    /// <summary>
    /// Whether this product configuration meets EAR cap compliance
    /// </summary>
    public bool EarCapCompliance { get; set; }
    
    /// <summary>
    /// Maximum allowed EAR per Bank of Zambia Money Lenders Act (typically 0.48 for 48%)
    /// </summary>
    public decimal EarLimit { get; set; }
    
    /// <summary>
    /// Eligibility rules for applicant qualification
    /// </summary>
    public EligibilityRules EligibilityRules { get; set; } = new();
}
