namespace IntelliFin.LoanOriginationService.Exceptions;

/// <summary>
/// Exception thrown when a loan product configuration violates EAR (Effective Annual Rate) compliance.
/// This enforces the Bank of Zambia Money Lenders Act 48% EAR cap.
/// </summary>
public class ComplianceException : Exception
{
    /// <summary>
    /// Product code that failed compliance check
    /// </summary>
    public string ProductCode { get; }
    
    /// <summary>
    /// Calculated Effective Annual Rate that exceeded the limit
    /// </summary>
    public decimal CalculatedEAR { get; }
    
    /// <summary>
    /// Maximum allowed EAR limit
    /// </summary>
    public decimal EarLimit { get; }
    
    /// <summary>
    /// Creates a new ComplianceException
    /// </summary>
    /// <param name="productCode">Product code that failed compliance</param>
    /// <param name="calculatedEAR">The calculated EAR</param>
    /// <param name="earLimit">The maximum allowed EAR</param>
    public ComplianceException(string productCode, decimal calculatedEAR, decimal earLimit)
        : base($"Product {productCode} EAR {calculatedEAR:P2} exceeds Bank of Zambia limit {earLimit:P2}")
    {
        ProductCode = productCode;
        CalculatedEAR = calculatedEAR;
        EarLimit = earLimit;
    }
    
    /// <summary>
    /// Creates a new ComplianceException with custom message
    /// </summary>
    /// <param name="message">Custom error message</param>
    /// <param name="productCode">Product code that failed compliance</param>
    /// <param name="calculatedEAR">The calculated EAR</param>
    /// <param name="earLimit">The maximum allowed EAR</param>
    public ComplianceException(string message, string productCode, decimal calculatedEAR, decimal earLimit)
        : base(message)
    {
        ProductCode = productCode;
        CalculatedEAR = calculatedEAR;
        EarLimit = earLimit;
    }
}
