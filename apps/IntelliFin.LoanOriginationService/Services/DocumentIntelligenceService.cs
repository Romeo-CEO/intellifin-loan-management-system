using IntelliFin.Shared.DomainModels.Entities;
using IntelliFin.Shared.DomainModels.Repositories;
using System.Text.Json;

namespace IntelliFin.LoanOriginationService.Services;

/// <summary>
/// Document Intelligence Service implementing System-Assisted Manual Verification
/// Uses OCR to assist loan officers in making accurate verification decisions
/// </summary>
public class DocumentIntelligenceService : IDocumentIntelligenceService
{
    private readonly ILogger<DocumentIntelligenceService> _logger;
    private readonly IDocumentVerificationRepository _verificationRepository;

    public DocumentIntelligenceService(
        ILogger<DocumentIntelligenceService> logger,
        IDocumentVerificationRepository verificationRepository)
    {
        _logger = logger;
        _verificationRepository = verificationRepository;
    }

    public async Task<OcrDocumentData> ExtractDocumentDataAsync(string imagePath, string documentType, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Extracting data from document: {DocumentType} at {ImagePath}", documentType, imagePath);

            // **SPRINT 4: External Integrations** - Replace with Azure Cognitive Services OCR API or equivalent
            // For now, simulate OCR processing
            await Task.Delay(2000, cancellationToken); // Simulate OCR processing time

            // Mock OCR results based on document type
            var ocrData = documentType.ToUpperInvariant() switch
            {
                "NRC" => SimulateNrcOcrExtraction(),
                "PASSPORT" => SimulatePassportOcrExtraction(), 
                _ => SimulateGenericDocumentOcrExtraction()
            };

            _logger.LogInformation("OCR extraction completed with confidence score: {ConfidenceScore}", ocrData.ConfidenceScore);
            
            return ocrData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting data from document: {DocumentType}", documentType);
            throw;
        }
    }

    public async Task<List<DataMismatch>> CompareDataAsync(ManualEntryData manualData, OcrDocumentData ocrData, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask; // For async consistency
        
        var mismatches = new List<DataMismatch>();

        // Compare each field
        CompareField("FullName", manualData.FullName, ocrData.FullName, ocrData.FieldConfidenceScores.GetValueOrDefault("FullName", 0.5m), mismatches);
        CompareField("DocumentNumber", manualData.DocumentNumber, ocrData.DocumentNumber, ocrData.FieldConfidenceScores.GetValueOrDefault("DocumentNumber", 0.5m), mismatches);
        
        if (manualData.DateOfBirth.HasValue && ocrData.DateOfBirth.HasValue)
        {
            CompareField("DateOfBirth", manualData.DateOfBirth.Value.ToString("yyyy-MM-dd"), ocrData.DateOfBirth.Value.ToString("yyyy-MM-dd"), 
                ocrData.FieldConfidenceScores.GetValueOrDefault("DateOfBirth", 0.5m), mismatches);
        }

        CompareField("Address", manualData.Address, ocrData.Address, ocrData.FieldConfidenceScores.GetValueOrDefault("Address", 0.5m), mismatches);
        CompareField("Gender", manualData.Gender, ocrData.Gender, ocrData.FieldConfidenceScores.GetValueOrDefault("Gender", 0.5m), mismatches);

        _logger.LogInformation("Data comparison completed. Found {MismatchCount} mismatches", mismatches.Count);
        
        return mismatches;
    }

    public async Task<DocumentVerification> CreateVerificationRecordAsync(
        Guid clientId, 
        string documentType, 
        string imagePath, 
        ManualEntryData manualData, 
        OcrDocumentData ocrData, 
        List<DataMismatch> mismatches, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var verification = new DocumentVerification
            {
                Id = Guid.NewGuid(),
                ClientId = clientId,
                DocumentType = documentType,
                DocumentNumber = manualData.DocumentNumber,
                DocumentImagePath = imagePath,
                ManuallyEnteredData = JsonSerializer.Serialize(manualData),
                OcrExtractedData = JsonSerializer.Serialize(ocrData),
                OcrConfidenceScore = ocrData.ConfidenceScore,
                OcrProvider = "Azure Cognitive Services", // **SPRINT 4: External Integrations** - Will be configurable
                DataMismatches = JsonSerializer.Serialize(mismatches),
                HasDataMismatches = mismatches.Any(),
                IsVerified = false, // Pending loan officer verification
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };

            verification = await _verificationRepository.CreateAsync(verification, cancellationToken);
            
            _logger.LogInformation("Document verification record created: {VerificationId} for client {ClientId}", 
                verification.Id, clientId);
            
            return verification;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating verification record for client {ClientId}", clientId);
            throw;
        }
    }

    public async Task<DocumentVerification> CompleteVerificationAsync(
        Guid verificationId, 
        bool isVerified, 
        string verifiedBy, 
        string notes, 
        string decisionReason, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var verification = await _verificationRepository.GetByIdAsync(verificationId, cancellationToken);
            if (verification == null)
            {
                throw new KeyNotFoundException($"Verification record {verificationId} not found");
            }

            verification.IsVerified = isVerified;
            verification.VerifiedBy = verifiedBy;
            verification.VerificationDate = DateTime.UtcNow;
            verification.VerificationNotes = notes;
            verification.VerificationDecisionReason = decisionReason;
            verification.LastModified = DateTime.UtcNow;

            verification = await _verificationRepository.UpdateAsync(verification, cancellationToken);
            
            _logger.LogInformation("Document verification completed: {VerificationId} - {Decision} by {VerifiedBy}", 
                verificationId, isVerified ? "VERIFIED" : "REJECTED", verifiedBy);
            
            return verification;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing verification {VerificationId}", verificationId);
            throw;
        }
    }

    public async Task<IEnumerable<DocumentVerification>> GetPendingVerificationsAsync(CancellationToken cancellationToken = default)
    {
        return await _verificationRepository.GetPendingVerificationsAsync(cancellationToken);
    }

    #region Private Helper Methods

    private void CompareField(string fieldName, string manualValue, string ocrValue, decimal ocrConfidence, List<DataMismatch> mismatches)
    {
        if (string.IsNullOrWhiteSpace(manualValue) && string.IsNullOrWhiteSpace(ocrValue))
            return; // Both empty, no mismatch

        var normalizedManual = manualValue?.Trim().ToUpperInvariant() ?? "";
        var normalizedOcr = ocrValue?.Trim().ToUpperInvariant() ?? "";

        if (normalizedManual != normalizedOcr)
        {
            var mismatchType = DetermineMismatchType(fieldName, normalizedManual, normalizedOcr, ocrConfidence);
            
            mismatches.Add(new DataMismatch
            {
                FieldName = fieldName,
                ManualValue = manualValue ?? "",
                OcrValue = ocrValue ?? "",
                OcrConfidence = ocrConfidence,
                MismatchType = mismatchType
            });
        }
    }

    private string DetermineMismatchType(string fieldName, string manualValue, string ocrValue, decimal ocrConfidence)
    {
        // Critical fields require higher scrutiny
        var criticalFields = new[] { "DOCUMENTNUMBER", "DATEOFBIRTH" };
        
        if (criticalFields.Contains(fieldName.ToUpperInvariant()))
            return "Critical";
            
        // Low OCR confidence suggests OCR error
        if (ocrConfidence < 0.7m)
            return "Minor";
            
        // High OCR confidence but mismatch suggests manual entry error
        if (ocrConfidence > 0.9m)
            return "Major";
            
        return "Minor";
    }

    private OcrDocumentData SimulateNrcOcrExtraction()
    {
        return new OcrDocumentData
        {
            FullName = "JOHN MUKAMBA BANDA",
            DocumentNumber = "123456/78/9",
            DateOfBirth = new DateTime(1985, 5, 15),
            ExpiryDate = new DateTime(2030, 5, 15),
            PlaceOfBirth = "LUSAKA",
            Address = "PLOT 123, KABULONGA, LUSAKA",
            Gender = "MALE",
            ConfidenceScore = 0.92m,
            FieldConfidenceScores = new Dictionary<string, decimal>
            {
                ["FullName"] = 0.95m,
                ["DocumentNumber"] = 0.98m,
                ["DateOfBirth"] = 0.85m,
                ["PlaceOfBirth"] = 0.90m,
                ["Address"] = 0.88m,
                ["Gender"] = 0.96m
            }
        };
    }

    private OcrDocumentData SimulatePassportOcrExtraction()
    {
        return new OcrDocumentData
        {
            FullName = "BANDA, JOHN MUKAMBA",
            DocumentNumber = "ZM1234567",
            DateOfBirth = new DateTime(1985, 5, 15),
            ExpiryDate = new DateTime(2028, 3, 20),
            PlaceOfBirth = "ZAMBIA",
            Gender = "M",
            ConfidenceScore = 0.89m,
            FieldConfidenceScores = new Dictionary<string, decimal>
            {
                ["FullName"] = 0.93m,
                ["DocumentNumber"] = 0.96m,
                ["DateOfBirth"] = 0.88m,
                ["PlaceOfBirth"] = 0.85m,
                ["Gender"] = 0.94m
            }
        };
    }

    private OcrDocumentData SimulateGenericDocumentOcrExtraction()
    {
        return new OcrDocumentData
        {
            FullName = "DOCUMENT HOLDER NAME",
            DocumentNumber = "DOC123456",
            ConfidenceScore = 0.75m,
            FieldConfidenceScores = new Dictionary<string, decimal>
            {
                ["FullName"] = 0.80m,
                ["DocumentNumber"] = 0.75m
            }
        };
    }

    #endregion
}