using IntelliFin.LoanOriginationService.Models;

namespace IntelliFin.LoanOriginationService.Services;

/// <summary>
/// Service for generating loan agreements using JasperReports and storing them in MinIO.
/// Handles PDF generation, document hashing for integrity, and audit event publishing.
/// </summary>
public interface IAgreementGenerationService
{
    /// <summary>
    /// Generates a loan agreement PDF for an approved loan application.
    /// </summary>
    /// <param name="applicationId">The ID of the loan application to generate an agreement for.</param>
    /// <param name="cancellationToken">Cancellation token for async operation.</param>
    /// <returns>Agreement document metadata including file hash and MinIO storage path.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the loan application is not found.</exception>
    /// <exception cref="AgreementGenerationException">Thrown when PDF generation or storage fails.</exception>
    /// <remarks>
    /// This method performs the following operations:
    /// 1. Fetches loan application data including client and assessment information
    /// 2. Retrieves product configuration from Vault for template version and EAR
    /// 3. Prepares JSON payload for JasperReports API
    /// 4. Calls JasperReports Server to generate PDF
    /// 5. Computes SHA256 hash for document integrity verification
    /// 6. Stores PDF in MinIO at path: loan-agreements/{clientId}/{loanNumber}_v{version}.pdf
    /// 7. Updates loan application record with agreement metadata
    /// 8. Publishes LoanAgreementGenerated audit event
    /// </remarks>
    Task<AgreementDocument> GenerateAgreementAsync(
        Guid applicationId,
        CancellationToken cancellationToken);
}
