using IntelliFin.CreditAssessmentService.Models.Requests;
using IntelliFin.CreditAssessmentService.Models.Responses;

namespace IntelliFin.CreditAssessmentService.Services.Core;

/// <summary>
/// Core credit assessment service interface.
/// </summary>
public interface ICreditAssessmentService
{
    /// <summary>
    /// Performs a comprehensive credit assessment for a loan application.
    /// </summary>
    /// <param name="request">Assessment request details</param>
    /// <param name="userId">User ID initiating the assessment</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Complete assessment result</returns>
    Task<AssessmentResponse> PerformAssessmentAsync(
        AssessmentRequest request, 
        Guid? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an existing assessment by ID.
    /// </summary>
    /// <param name="assessmentId">Assessment identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Assessment details or null if not found</returns>
    Task<AssessmentResponse?> GetAssessmentByIdAsync(
        Guid assessmentId, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the latest assessment for a client.
    /// </summary>
    /// <param name="clientId">Client identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Latest assessment or null if none exists</returns>
    Task<AssessmentResponse?> GetLatestAssessmentForClientAsync(
        Guid clientId, 
        CancellationToken cancellationToken = default);
}
