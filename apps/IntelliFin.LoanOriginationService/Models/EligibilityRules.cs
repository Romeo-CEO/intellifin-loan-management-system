namespace IntelliFin.LoanOriginationService.Models;

/// <summary>
/// Eligibility rules for loan product qualification.
/// These rules are stored in Vault and evaluated during loan application.
/// </summary>
public class EligibilityRules
{
    /// <summary>
    /// Required KYC status for applicant (e.g., "Approved")
    /// </summary>
    public string RequiredKycStatus { get; set; } = "Approved";
    
    /// <summary>
    /// Minimum monthly income required in ZMW
    /// </summary>
    public decimal MinMonthlyIncome { get; set; }
    
    /// <summary>
    /// Maximum debt-to-income ratio allowed (0.0-1.0)
    /// </summary>
    public decimal MaxDtiRatio { get; set; }
    
    /// <summary>
    /// Whether PMEC (Pension and Insurance Authority) registration is required
    /// </summary>
    public bool PmecRegistrationRequired { get; set; }
}
