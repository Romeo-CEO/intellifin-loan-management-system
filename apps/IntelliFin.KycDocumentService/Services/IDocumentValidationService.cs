using IntelliFin.KycDocumentService.Models;

namespace IntelliFin.KycDocumentService.Services;

public interface IDocumentValidationService
{
    Task<DocumentValidationResult> ValidateDocumentAsync(string documentId, Stream documentStream, 
        KycDocumentType documentType, CancellationToken cancellationToken = default);
    
    Task<DocumentValidationResult> ValidateDocumentContentAsync(string documentId, string filePath,
        KycDocumentType documentType, CancellationToken cancellationToken = default);
    
    Task<bool> IsDocumentTypeValidAsync(KycDocumentType documentType, string contentType);
    
    Task<ValidationError[]> ValidateFileFormatAsync(Stream documentStream, string fileName, string contentType);
    
    Task<Dictionary<string, object>> ExtractDocumentDataAsync(Stream documentStream, KycDocumentType documentType,
        CancellationToken cancellationToken = default);
    
    Task<float> CalculateConfidenceScoreAsync(Dictionary<string, object> extractedData, KycDocumentType documentType);
    
    Task<bool> CheckDocumentIntegrityAsync(Stream documentStream, string expectedHash);
    
    Task<ValidationError[]> ValidateDocumentExpiryAsync(Dictionary<string, object> extractedData);
}