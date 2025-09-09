using IntelliFin.KycDocumentService.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using System.Security.Cryptography;

namespace IntelliFin.KycDocumentService.Services;

public class DocumentValidationService : IDocumentValidationService
{
    private readonly IDocumentStorageService _storageService;
    private readonly IAzureOcrService _azureOcrService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DocumentValidationService> _logger;
    
    private static readonly Dictionary<KycDocumentType, string[]> AllowedContentTypes = new()
    {
        [KycDocumentType.NationalId] = new[] { "image/jpeg", "image/png", "image/tiff", "application/pdf" },
        [KycDocumentType.Passport] = new[] { "image/jpeg", "image/png", "image/tiff", "application/pdf" },
        [KycDocumentType.DriversLicense] = new[] { "image/jpeg", "image/png", "image/tiff", "application/pdf" },
        [KycDocumentType.ProofOfAddress] = new[] { "image/jpeg", "image/png", "image/tiff", "application/pdf" },
        [KycDocumentType.PaySlip] = new[] { "image/jpeg", "image/png", "image/tiff", "application/pdf" },
        [KycDocumentType.BankStatement] = new[] { "application/pdf", "image/jpeg", "image/png" },
        [KycDocumentType.EmploymentLetter] = new[] { "application/pdf", "image/jpeg", "image/png" },
        [KycDocumentType.TaxCertificate] = new[] { "application/pdf", "image/jpeg", "image/png" },
        [KycDocumentType.Other] = new[] { "image/jpeg", "image/png", "image/tiff", "application/pdf" }
    };

    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10MB
    private const int MinImageWidth = 300;
    private const int MinImageHeight = 300;

    public DocumentValidationService(
        IDocumentStorageService storageService, 
        IAzureOcrService azureOcrService,
        IConfiguration configuration,
        ILogger<DocumentValidationService> logger)
    {
        _storageService = storageService;
        _azureOcrService = azureOcrService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<DocumentValidationResult> ValidateDocumentAsync(string documentId, Stream documentStream,
        KycDocumentType documentType, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var result = new DocumentValidationResult
        {
            DocumentId = documentId,
            ProcessorUsed = "DocumentValidationService"
        };

        try
        {
            _logger.LogInformation("Starting document validation for document {DocumentId} of type {DocumentType}", 
                documentId, documentType);

            // File format validation
            var formatErrors = await ValidateFileFormatAsync(documentStream, $"document_{documentId}", 
                GetContentTypeFromStream(documentStream));
            result.Errors.AddRange(formatErrors);

            // Content validation based on document type
            if (result.Errors.Count == 0)
            {
                var contentValidation = await ValidateDocumentContentAsync(documentStream, documentType);
                result.Errors.AddRange(contentValidation.Errors);
                result.Warnings.AddRange(contentValidation.Warnings);
                result.ExtractedData = contentValidation.ExtractedData;
                result.ConfidenceScore = contentValidation.ConfidenceScore;
            }

            // Check if manual review is required
            result.RequiresManualReview = DetermineManualReviewRequirement(result);
            result.IsValid = result.Errors.Count == 0;
            result.ProcessedAt = DateTime.UtcNow;
            result.ProcessingTime = result.ProcessedAt - startTime;

            _logger.LogInformation("Document validation completed for {DocumentId}. Valid: {IsValid}, Manual Review: {RequiresManualReview}",
                documentId, result.IsValid, result.RequiresManualReview);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating document {DocumentId}", documentId);
            result.Errors.Add(new ValidationError
            {
                Code = "VALIDATION_ERROR",
                Message = "An error occurred during document validation",
                Severity = "Error"
            });
            result.IsValid = false;
            result.RequiresManualReview = true;
            result.ProcessedAt = DateTime.UtcNow;
            result.ProcessingTime = result.ProcessedAt - startTime;
            return result;
        }
    }

    public async Task<DocumentValidationResult> ValidateDocumentContentAsync(string documentId, string filePath,
        KycDocumentType documentType, CancellationToken cancellationToken = default)
    {
        try
        {
            using var documentStream = await _storageService.GetDocumentAsync(filePath, cancellationToken);
            return await ValidateDocumentAsync(documentId, documentStream, documentType, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating document content from path {FilePath}", filePath);
            return new DocumentValidationResult
            {
                DocumentId = documentId,
                IsValid = false,
                RequiresManualReview = true,
                Errors = new List<ValidationError>
                {
                    new()
                    {
                        Code = "CONTENT_ACCESS_ERROR",
                        Message = "Unable to access document content for validation",
                        Severity = "Error"
                    }
                }
            };
        }
    }

    public Task<bool> IsDocumentTypeValidAsync(KycDocumentType documentType, string contentType)
    {
        if (!AllowedContentTypes.TryGetValue(documentType, out var allowedTypes))
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(allowedTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase));
    }

    public async Task<ValidationError[]> ValidateFileFormatAsync(Stream documentStream, string fileName, string contentType)
    {
        var errors = new List<ValidationError>();

        try
        {
            // Check file size
            if (documentStream.Length > MaxFileSizeBytes)
            {
                errors.Add(new ValidationError
                {
                    Code = "FILE_TOO_LARGE",
                    Message = $"File size {documentStream.Length} bytes exceeds maximum allowed size of {MaxFileSizeBytes} bytes",
                    Field = "fileSize",
                    Severity = "Error"
                });
            }

            // Check if file has content
            if (documentStream.Length == 0)
            {
                errors.Add(new ValidationError
                {
                    Code = "EMPTY_FILE",
                    Message = "File appears to be empty",
                    Field = "fileSize",
                    Severity = "Error"
                });
                return errors.ToArray();
            }

            // Validate image files
            if (contentType.StartsWith("image/"))
            {
                documentStream.Position = 0;
                await ValidateImageFormatAsync(documentStream, errors);
            }

            // Validate PDF files
            else if (contentType == "application/pdf")
            {
                documentStream.Position = 0;
                await ValidatePdfFormatAsync(documentStream, errors);
            }

            documentStream.Position = 0;
            return errors.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating file format for {FileName}", fileName);
            errors.Add(new ValidationError
            {
                Code = "FORMAT_VALIDATION_ERROR",
                Message = "Unable to validate file format",
                Severity = "Error"
            });
            return errors.ToArray();
        }
    }

    public async Task<Dictionary<string, object>> ExtractDocumentDataAsync(Stream documentStream, 
        KycDocumentType documentType, CancellationToken cancellationToken = default)
    {
        var extractedData = new Dictionary<string, object>();

        try
        {
            documentStream.Position = 0;

            // Check if Azure OCR is enabled
            var useAzureOcr = _configuration.GetValue<bool>("DocumentValidation:UseAzureOcr", true);
            
            if (useAzureOcr)
            {
                _logger.LogInformation("Using Azure OCR for document extraction: {DocumentType}", documentType);
                
                var ocrResult = await _azureOcrService.ExtractDataAsync(documentStream, documentType, cancellationToken);
                
                // Convert OCR result to our format
                extractedData = ocrResult.ExtractedFields;
                extractedData["azure_confidence"] = ocrResult.OverallConfidence;
                extractedData["processing_time_ms"] = ocrResult.ProcessingTime.TotalMilliseconds;
                extractedData["model_version"] = ocrResult.ModelVersion;
                
                if (ocrResult.QualityMetrics != null)
                {
                    extractedData["quality_metrics"] = new Dictionary<string, object>
                    {
                        ["image_quality"] = ocrResult.QualityMetrics.ImageQuality,
                        ["text_clarity"] = ocrResult.QualityMetrics.TextClarity,
                        ["has_blur"] = ocrResult.QualityMetrics.HasBlur,
                        ["has_skew"] = ocrResult.QualityMetrics.HasSkew,
                        ["dpi_estimate"] = ocrResult.QualityMetrics.DpiEstimate
                    };
                }

                if (ocrResult.Errors.Any())
                {
                    extractedData["ocr_errors"] = ocrResult.Errors;
                }

                if (ocrResult.Warnings.Any())
                {
                    extractedData["ocr_warnings"] = ocrResult.Warnings;
                }
                
                _logger.LogInformation("Azure OCR extraction completed with confidence: {Confidence}", ocrResult.OverallConfidence);
            }
            else
            {
                _logger.LogInformation("Using fallback extraction for document type: {DocumentType}", documentType);
                extractedData = await ExtractDocumentDataFallbackAsync(documentStream, documentType, cancellationToken);
            }

            return extractedData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting data from document of type {DocumentType}", documentType);
            
            // Fallback to basic extraction on Azure OCR failure
            try
            {
                _logger.LogWarning("Falling back to basic extraction due to OCR service failure");
                documentStream.Position = 0;
                return await ExtractDocumentDataFallbackAsync(documentStream, documentType, cancellationToken);
            }
            catch (Exception fallbackEx)
            {
                _logger.LogError(fallbackEx, "Fallback extraction also failed");
                return extractedData;
            }
        }
    }

    private async Task<Dictionary<string, object>> ExtractDocumentDataFallbackAsync(Stream documentStream, 
        KycDocumentType documentType, CancellationToken cancellationToken)
    {
        // Fallback to simplified extraction when Azure OCR is not available
        switch (documentType)
        {
            case KycDocumentType.NationalId:
                return await ExtractNationalIdDataAsync(documentStream, cancellationToken);
            case KycDocumentType.Passport:
                return await ExtractPassportDataAsync(documentStream, cancellationToken);
            case KycDocumentType.DriversLicense:
                return await ExtractDriversLicenseDataAsync(documentStream, cancellationToken);
            case KycDocumentType.PaySlip:
                return await ExtractPaySlipDataAsync(documentStream, cancellationToken);
            default:
                return await ExtractGenericDocumentDataAsync(documentStream, cancellationToken);
        }
    }

    public async Task<float> CalculateConfidenceScoreAsync(Dictionary<string, object> extractedData, 
        KycDocumentType documentType)
    {
        try
        {
            // If Azure OCR confidence is available, use it as the primary score
            if (extractedData.ContainsKey("azure_confidence") && 
                extractedData["azure_confidence"] is float azureConfidence)
            {
                _logger.LogDebug("Using Azure OCR confidence score: {Confidence}", azureConfidence);
                
                // Apply quality adjustments based on metrics
                var adjustedScore = azureConfidence;
                
                if (extractedData.ContainsKey("quality_metrics") && 
                    extractedData["quality_metrics"] is Dictionary<string, object> qualityMetrics)
                {
                    // Reduce confidence for blurry images
                    if (qualityMetrics.ContainsKey("has_blur") && 
                        qualityMetrics["has_blur"] is bool hasBlur && hasBlur)
                    {
                        adjustedScore *= 0.9f;
                        _logger.LogDebug("Reduced confidence due to image blur");
                    }

                    // Reduce confidence for low DPI
                    if (qualityMetrics.ContainsKey("dpi_estimate") && 
                        qualityMetrics["dpi_estimate"] is int dpi && dpi < 150)
                    {
                        adjustedScore *= 0.95f;
                        _logger.LogDebug("Reduced confidence due to low DPI: {Dpi}", dpi);
                    }
                }

                // Check for OCR errors or warnings
                if (extractedData.ContainsKey("ocr_errors") && 
                    extractedData["ocr_errors"] is List<string> errors && errors.Any())
                {
                    adjustedScore *= 0.8f;
                    _logger.LogDebug("Reduced confidence due to OCR errors");
                }

                return Math.Min(1.0f, Math.Max(0.0f, adjustedScore));
            }

            // Fallback to traditional confidence calculation
            var score = 0.5f; // Base score

            // Check for presence of expected fields based on document type
            var expectedFields = GetExpectedFieldsForDocumentType(documentType);
            var presentFields = extractedData.Keys.Where(expectedFields.Contains).Count();
            
            if (expectedFields.Length > 0)
            {
                score += (presentFields / (float)expectedFields.Length) * 0.4f;
            }

            // Bonus for high-quality text extraction (fallback method)
            if (extractedData.ContainsKey("text_quality") && 
                extractedData["text_quality"] is float textQuality && textQuality > 0.8f)
            {
                score += 0.1f;
            }

            _logger.LogDebug("Using fallback confidence calculation: {Score}", score);
            return Math.Min(1.0f, Math.Max(0.0f, score));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating confidence score for document type {DocumentType}", documentType);
            return 0.5f; // Default confidence score
        }
    }

    public async Task<bool> CheckDocumentIntegrityAsync(Stream documentStream, string expectedHash)
    {
        try
        {
            documentStream.Position = 0;
            using var sha256 = SHA256.Create();
            var hashBytes = await Task.Run(() => sha256.ComputeHash(documentStream));
            var actualHash = Convert.ToBase64String(hashBytes);
            
            return string.Equals(expectedHash, actualHash, StringComparison.Ordinal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking document integrity");
            return false;
        }
    }

    public Task<ValidationError[]> ValidateDocumentExpiryAsync(Dictionary<string, object> extractedData)
    {
        var errors = new List<ValidationError>();

        try
        {
            if (extractedData.TryGetValue("expiry_date", out var expiryObj) &&
                expiryObj is DateTime expiryDate)
            {
                if (expiryDate <= DateTime.UtcNow.AddDays(30)) // Expiring within 30 days
                {
                    if (expiryDate <= DateTime.UtcNow)
                    {
                        errors.Add(new ValidationError
                        {
                            Code = "DOCUMENT_EXPIRED",
                            Message = $"Document expired on {expiryDate:yyyy-MM-dd}",
                            Field = "expiry_date",
                            Severity = "Error"
                        });
                    }
                    else
                    {
                        errors.Add(new ValidationError
                        {
                            Code = "DOCUMENT_EXPIRING_SOON",
                            Message = $"Document expires on {expiryDate:yyyy-MM-dd}",
                            Field = "expiry_date",
                            Severity = "Warning"
                        });
                    }
                }
            }

            return Task.FromResult(errors.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating document expiry");
            return Task.FromResult(new[] { new ValidationError
            {
                Code = "EXPIRY_VALIDATION_ERROR",
                Message = "Unable to validate document expiry date",
                Severity = "Warning"
            }});
        }
    }

    private async Task ValidateImageFormatAsync(Stream imageStream, List<ValidationError> errors)
    {
        try
        {
            using var image = await Image.LoadAsync(imageStream);
            
            // Check minimum dimensions
            if (image.Width < MinImageWidth || image.Height < MinImageHeight)
            {
                errors.Add(new ValidationError
                {
                    Code = "IMAGE_TOO_SMALL",
                    Message = $"Image dimensions {image.Width}x{image.Height} are below minimum required {MinImageWidth}x{MinImageHeight}",
                    Field = "imageDimensions",
                    Severity = "Error"
                });
            }

            // Check image quality indicators
            var aspectRatio = (float)image.Width / image.Height;
            if (aspectRatio < 0.5f || aspectRatio > 3.0f)
            {
                errors.Add(new ValidationError
                {
                    Code = "UNUSUAL_ASPECT_RATIO",
                    Message = $"Image has unusual aspect ratio: {aspectRatio:F2}",
                    Field = "aspectRatio",
                    Severity = "Warning"
                });
            }
        }
        catch (Exception ex)
        {
            errors.Add(new ValidationError
            {
                Code = "INVALID_IMAGE_FORMAT",
                Message = "Unable to read image file or invalid image format",
                Field = "imageFormat",
                Severity = "Error"
            });
        }
    }

    private async Task ValidatePdfFormatAsync(Stream pdfStream, List<ValidationError> errors)
    {
        try
        {
            // Basic PDF header validation
            pdfStream.Position = 0;
            var buffer = new byte[4];
            await pdfStream.ReadAsync(buffer, 0, 4);
            
            var pdfHeader = System.Text.Encoding.ASCII.GetString(buffer);
            if (!pdfHeader.StartsWith("%PDF"))
            {
                errors.Add(new ValidationError
                {
                    Code = "INVALID_PDF_FORMAT",
                    Message = "File does not appear to be a valid PDF document",
                    Field = "pdfFormat",
                    Severity = "Error"
                });
            }
        }
        catch (Exception ex)
        {
            errors.Add(new ValidationError
            {
                Code = "PDF_VALIDATION_ERROR",
                Message = "Unable to validate PDF format",
                Field = "pdfFormat",
                Severity = "Error"
            });
        }
    }

    private async Task<(List<ValidationError> Errors, List<ValidationWarning> Warnings, Dictionary<string, object> ExtractedData, float ConfidenceScore)> ValidateDocumentContentAsync(Stream documentStream, KycDocumentType documentType)
    {
        var errors = new List<ValidationError>();
        var warnings = new List<ValidationWarning>();
        var extractedData = await ExtractDocumentDataAsync(documentStream, documentType);
        var confidenceScore = await CalculateConfidenceScoreAsync(extractedData, documentType);

        // Validate extracted data
        var expiryErrors = await ValidateDocumentExpiryAsync(extractedData);
        errors.AddRange(expiryErrors.Where(e => e.Severity == "Error"));
        
        var expiryWarnings = expiryErrors.Where(e => e.Severity == "Warning")
            .Select(e => new ValidationWarning { Code = e.Code, Message = e.Message, Field = e.Field });
        warnings.AddRange(expiryWarnings);

        return (errors, warnings, extractedData, confidenceScore);
    }

    private bool DetermineManualReviewRequirement(DocumentValidationResult result)
    {
        // Require manual review if confidence is low
        if (result.ConfidenceScore < 0.7f)
            return true;

        // Require manual review if there are warnings
        if (result.Warnings.Count > 0)
            return true;

        // Require manual review for certain error types
        var criticalErrors = new[] { "DOCUMENT_EXPIRED", "UNUSUAL_ASPECT_RATIO", "IMAGE_TOO_SMALL" };
        if (result.Errors.Any(e => criticalErrors.Contains(e.Code)))
            return true;

        return false;
    }

    private string GetContentTypeFromStream(Stream stream)
    {
        // Simple content type detection based on file signature
        stream.Position = 0;
        var buffer = new byte[8];
        stream.Read(buffer, 0, 8);
        stream.Position = 0;

        // PDF signature
        if (buffer[0] == 0x25 && buffer[1] == 0x50 && buffer[2] == 0x44 && buffer[3] == 0x46)
            return "application/pdf";

        // JPEG signature
        if (buffer[0] == 0xFF && buffer[1] == 0xD8)
            return "image/jpeg";

        // PNG signature
        if (buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47)
            return "image/png";

        return "application/octet-stream";
    }

    private string[] GetExpectedFieldsForDocumentType(KycDocumentType documentType)
    {
        return documentType switch
        {
            KycDocumentType.NationalId => new[] { "id_number", "full_name", "date_of_birth", "expiry_date" },
            KycDocumentType.Passport => new[] { "passport_number", "full_name", "date_of_birth", "expiry_date", "nationality" },
            KycDocumentType.DriversLicense => new[] { "license_number", "full_name", "date_of_birth", "expiry_date" },
            KycDocumentType.PaySlip => new[] { "employee_name", "salary", "pay_period", "employer" },
            _ => Array.Empty<string>()
        };
    }

    private async Task<Dictionary<string, object>> ExtractNationalIdDataAsync(Stream stream, CancellationToken cancellationToken)
    {
        // Simplified data extraction - in production this would use OCR services
        return new Dictionary<string, object>
        {
            ["document_type"] = "national_id",
            ["extraction_method"] = "simplified",
            ["text_quality"] = 0.8f
        };
    }

    private async Task<Dictionary<string, object>> ExtractPassportDataAsync(Stream stream, CancellationToken cancellationToken)
    {
        return new Dictionary<string, object>
        {
            ["document_type"] = "passport",
            ["extraction_method"] = "simplified",
            ["text_quality"] = 0.8f
        };
    }

    private async Task<Dictionary<string, object>> ExtractDriversLicenseDataAsync(Stream stream, CancellationToken cancellationToken)
    {
        return new Dictionary<string, object>
        {
            ["document_type"] = "drivers_license",
            ["extraction_method"] = "simplified",
            ["text_quality"] = 0.8f
        };
    }

    private async Task<Dictionary<string, object>> ExtractPaySlipDataAsync(Stream stream, CancellationToken cancellationToken)
    {
        return new Dictionary<string, object>
        {
            ["document_type"] = "pay_slip",
            ["extraction_method"] = "simplified",
            ["text_quality"] = 0.8f
        };
    }

    private async Task<Dictionary<string, object>> ExtractGenericDocumentDataAsync(Stream stream, CancellationToken cancellationToken)
    {
        return new Dictionary<string, object>
        {
            ["document_type"] = "generic",
            ["extraction_method"] = "simplified",
            ["text_quality"] = 0.7f
        };
    }
}