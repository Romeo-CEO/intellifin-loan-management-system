namespace IntelliFin.LoanOriginationService.Exceptions;

/// <summary>
/// Exception thrown when agreement generation fails (e.g., JasperReports API failure, MinIO storage failure).
/// </summary>
public class AgreementGenerationException : Exception
{
    /// <summary>
    /// The ID of the loan application for which agreement generation failed.
    /// </summary>
    public Guid ApplicationId { get; }
    
    /// <summary>
    /// The template path that was being used for agreement generation.
    /// </summary>
    public string? TemplatePath { get; }
    
    /// <summary>
    /// Initializes a new instance of the AgreementGenerationException with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public AgreementGenerationException(string message) 
        : base(message)
    {
    }
    
    /// <summary>
    /// Initializes a new instance of the AgreementGenerationException with application context.
    /// </summary>
    /// <param name="applicationId">The loan application ID.</param>
    /// <param name="templatePath">The template path being used.</param>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception that caused this failure.</param>
    public AgreementGenerationException(
        Guid applicationId, 
        string templatePath, 
        string message, 
        Exception innerException)
        : base(message, innerException)
    {
        ApplicationId = applicationId;
        TemplatePath = templatePath;
    }
}
