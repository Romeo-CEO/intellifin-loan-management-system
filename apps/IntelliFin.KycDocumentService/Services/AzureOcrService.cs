using Azure;
using Azure.AI.FormRecognizer.DocumentAnalysis;
using Azure.Identity;
using IntelliFin.KycDocumentService.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace IntelliFin.KycDocumentService.Services;

public class AzureOcrService : IAzureOcrService
{
    private readonly DocumentAnalysisClient _client;
    private readonly ILogger<AzureOcrService> _logger;
    private readonly AzureOcrConfiguration _configuration;

    public AzureOcrService(DocumentAnalysisClient client, IConfiguration configuration, ILogger<AzureOcrService> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger;
        _configuration = configuration.GetSection("AzureOcr").Get<AzureOcrConfiguration>() 
            ?? throw new ArgumentException("Azure OCR configuration is missing");
    }

    public async Task<OcrExtractionResult> ExtractDataAsync(Stream documentStream, KycDocumentType documentType, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger.LogInformation("Starting Azure OCR extraction for document type: {DocumentType}", documentType);

            // Reset stream position
            documentStream.Position = 0;

            // Choose the appropriate model based on document type
            var modelId = GetModelIdForDocumentType(documentType);
            
            // Analyze document
            var operation = await _client.AnalyzeDocumentAsync(WaitUntil.Completed, modelId, documentStream, cancellationToken: cancellationToken);
            var result = operation.Value;

            // Create extraction result
            var extractionResult = new OcrExtractionResult
            {
                DocumentType = documentType.ToString().ToLowerInvariant(),
                ProcessingTime = DateTime.UtcNow - startTime,
                ModelVersion = "Azure AI Document Intelligence"
            };

            // Process documents
            if (result.Documents.Any())
            {
                var document = result.Documents.First();
                extractionResult.OverallConfidence = document.Confidence;
                
                // Extract fields based on document type
                await ExtractFieldsByDocumentType(document, documentType, extractionResult);
            }
            else
            {
                extractionResult.OverallConfidence = 0.0f;
                extractionResult.Warnings.Add("No structured data found in document");
            }

            // Add quality metrics if enabled
            if (_configuration.EnableQualityAssessment)
            {
                extractionResult.QualityMetrics = await AssessDocumentQualityAsync(result);
            }

            _logger.LogInformation("Azure OCR extraction completed. Confidence: {Confidence:P2}, Processing time: {ProcessingTime:N2}ms", 
                extractionResult.OverallConfidence, extractionResult.ProcessingTime.TotalMilliseconds);

            return extractionResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure OCR extraction failed for document type: {DocumentType}", documentType);
            throw;
        }
    }

    public async Task<OcrExtractionResult> ExtractDataFromUrlAsync(string documentUrl, KycDocumentType documentType, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger.LogInformation("Starting Azure OCR extraction from URL for document type: {DocumentType}", documentType);

            var modelId = GetModelIdForDocumentType(documentType);
            var uri = new Uri(documentUrl);
            
            var operation = await _client.AnalyzeDocumentFromUriAsync(WaitUntil.Completed, modelId, uri, cancellationToken: cancellationToken);
            var result = operation.Value;

            var extractionResult = new OcrExtractionResult
            {
                DocumentType = documentType.ToString().ToLowerInvariant(),
                ProcessingTime = DateTime.UtcNow - startTime,
                ModelVersion = "Azure AI Document Intelligence"
            };

            if (result.Documents.Any())
            {
                var document = result.Documents.First();
                extractionResult.OverallConfidence = document.Confidence;
                await ExtractFieldsByDocumentType(document, documentType, extractionResult);
            }

            return extractionResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure OCR extraction from URL failed: {Url}", documentUrl);
            throw;
        }
    }

    public async Task<bool> IsServiceAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Create a minimal test document
            var testData = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // PDF header
            using var testStream = new MemoryStream(testData);
            
            var operation = await _client.AnalyzeDocumentAsync(
                WaitUntil.Started, 
                "prebuilt-document", 
                testStream, 
                cancellationToken: cancellationToken);
            
            // If we can start the operation, the service is available
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure OCR service availability check failed");
            return false;
        }
    }

    public async Task<DocumentLayoutAnalysis> AnalyzeLayoutAsync(Stream documentStream, CancellationToken cancellationToken = default)
    {
        try
        {
            documentStream.Position = 0;
            
            var operation = await _client.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-layout", documentStream, cancellationToken: cancellationToken);
            var result = operation.Value;

            var analysis = new DocumentLayoutAnalysis
            {
                OverallConfidence = result.Documents.Any() ? result.Documents.Average(d => d.Confidence) : 0.0f
            };

            // Convert Azure result to our model
            foreach (var page in result.Pages)
            {
                var docPage = new DocumentPage
                {
                    PageNumber = page.PageNumber,
                    Width = page.Width ?? 0,
                    Height = page.Height ?? 0
                };

                if (page.Lines != null)
                {
                    docPage.Lines = page.Lines.Select(line => new DocumentLine
                    {
                        Text = line.Content,
                        BoundingBox = line.BoundingPolygon.Select(p => (float)p.X).Concat(line.BoundingPolygon.Select(p => (float)p.Y)).ToList(),
                        Confidence = 1.0f // Line confidence not available in current SDK version
                    }).ToList();
                }

                if (page.Words != null)
                {
                    docPage.Words = page.Words.Select(word => new DocumentWord
                    {
                        Text = word.Content,
                        BoundingBox = word.BoundingPolygon.Select(p => (float)p.X).Concat(word.BoundingPolygon.Select(p => (float)p.Y)).ToList(),
                        Confidence = word.Confidence
                    }).ToList();
                }

                analysis.Pages.Add(docPage);
            }

            // Process tables
            if (result.Tables != null)
            {
                analysis.Tables = result.Tables.Select(table => new DocumentTable
                {
                    RowCount = table.RowCount,
                    ColumnCount = table.ColumnCount,
                    Cells = table.Cells.Select(cell => new DocumentTableCell
                    {
                        Text = cell.Content,
                        RowIndex = cell.RowIndex,
                        ColumnIndex = cell.ColumnIndex,
                        Confidence = 1.0f // Cell confidence not available in current SDK version
                    }).ToList()
                }).ToList();
            }

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Document layout analysis failed");
            throw;
        }
    }

    private string GetModelIdForDocumentType(KycDocumentType documentType)
    {
        return documentType switch
        {
            KycDocumentType.NationalId or KycDocumentType.Passport or KycDocumentType.DriversLicense => "prebuilt-idDocument",
            KycDocumentType.BankStatement or KycDocumentType.PaySlip or KycDocumentType.Other => "prebuilt-document",
            _ => "prebuilt-document"
        };
    }

    private async Task ExtractFieldsByDocumentType(AnalyzedDocument document, KycDocumentType documentType, OcrExtractionResult extractionResult)
    {
        // Add common fields
        extractionResult.ExtractedFields["extraction_method"] = "azure_ocr";
        extractionResult.ExtractedFields["azure_ocr_confidence"] = extractionResult.OverallConfidence;

        // Process all available fields from the document
        foreach (var field in document.Fields)
        {
            var fieldValue = GetAzureFieldValue(field.Value);
            if (fieldValue != null)
            {
                extractionResult.ExtractedFields[field.Key.ToLowerInvariant()] = fieldValue;
                extractionResult.FieldConfidences[field.Key.ToLowerInvariant()] = field.Value.Confidence ?? 0.5f;
            }
        }

        // Document type specific processing
        switch (documentType)
        {
            case KycDocumentType.NationalId:
            case KycDocumentType.Passport:
            case KycDocumentType.DriversLicense:
                await ExtractIdDocumentFields(document, extractionResult);
                break;
            case KycDocumentType.BankStatement:
                await ExtractBankStatementFields(document, extractionResult);
                break;
            case KycDocumentType.PaySlip:
                await ExtractPaySlipFields(document, extractionResult);
                break;
        }

        await Task.CompletedTask;
    }

    private async Task ExtractIdDocumentFields(AnalyzedDocument document, OcrExtractionResult extractionResult)
    {
        // Map common ID document fields
        var fieldMappings = new Dictionary<string, string>
        {
            ["FirstName"] = "first_name",
            ["LastName"] = "last_name", 
            ["DateOfBirth"] = "date_of_birth",
            ["DocumentNumber"] = "id_number",
            ["ExpirationDate"] = "expiry_date",
            ["Address"] = "address",
            ["Sex"] = "gender"
        };

        foreach (var mapping in fieldMappings)
        {
            if (document.Fields.TryGetValue(mapping.Key, out var field))
            {
                var value = GetAzureFieldValue(field);
                if (value != null)
                {
                    extractionResult.ExtractedFields[mapping.Value] = value;
                    extractionResult.FieldConfidences[mapping.Value] = field.Confidence ?? 0.5f;
                }
            }
        }

        // Create full name if we have first and last name
        if (extractionResult.ExtractedFields.ContainsKey("first_name") && 
            extractionResult.ExtractedFields.ContainsKey("last_name"))
        {
            extractionResult.ExtractedFields["full_name"] = 
                $"{extractionResult.ExtractedFields["first_name"]} {extractionResult.ExtractedFields["last_name"]}";
        }

        await Task.CompletedTask;
    }

    private async Task ExtractBankStatementFields(AnalyzedDocument document, OcrExtractionResult extractionResult)
    {
        // Extract common bank statement information
        var fieldMappings = new Dictionary<string, string>
        {
            ["AccountNumber"] = "account_number",
            ["AccountHolder"] = "account_holder_name",
            ["StatementDate"] = "statement_date",
            ["Balance"] = "closing_balance"
        };

        foreach (var mapping in fieldMappings)
        {
            if (document.Fields.TryGetValue(mapping.Key, out var field))
            {
                var value = GetAzureFieldValue(field);
                if (value != null)
                {
                    extractionResult.ExtractedFields[mapping.Value] = value;
                    extractionResult.FieldConfidences[mapping.Value] = field.Confidence ?? 0.5f;
                }
            }
        }

        await Task.CompletedTask;
    }

    private async Task ExtractPaySlipFields(AnalyzedDocument document, OcrExtractionResult extractionResult)
    {
        // Extract payslip information
        var fieldMappings = new Dictionary<string, string>
        {
            ["EmployeeName"] = "employee_name",
            ["EmployeeId"] = "employee_id",
            ["PayPeriod"] = "pay_period",
            ["GrossPay"] = "gross_pay",
            ["NetPay"] = "net_pay",
            ["Employer"] = "employer_name"
        };

        foreach (var mapping in fieldMappings)
        {
            if (document.Fields.TryGetValue(mapping.Key, out var field))
            {
                var value = GetAzureFieldValue(field);
                if (value != null)
                {
                    extractionResult.ExtractedFields[mapping.Value] = value;
                    extractionResult.FieldConfidences[mapping.Value] = field.Confidence ?? 0.5f;
                }
            }
        }

        await Task.CompletedTask;
    }

    private object? GetAzureFieldValue(Azure.AI.FormRecognizer.DocumentAnalysis.DocumentField field)
    {
        if (field?.Content == null) return null;

        return field.FieldType switch
        {
            DocumentFieldType.String => field.Content,
            DocumentFieldType.Date when DateTime.TryParse(field.Content, out var date) => date,
            DocumentFieldType.Double when double.TryParse(field.Content, out var doubleVal) => doubleVal,
            DocumentFieldType.Int64 when long.TryParse(field.Content, out var longVal) => longVal,
            _ => field.Content
        };
    }

    private async Task<DocumentQualityMetrics> AssessDocumentQualityAsync(AnalyzeResult result)
    {
        var metrics = new DocumentQualityMetrics();
        
        if (result.Pages.Any())
        {
            var page = result.Pages.First();
            
            // Estimate image quality based on confidence scores
            var avgConfidence = result.Documents.Any() ? result.Documents.Average(d => d.Confidence) : 0.0f;
            metrics.ImageQuality = avgConfidence;
            metrics.TextClarity = avgConfidence;

            // Simple heuristics for quality assessment
            metrics.HasBlur = avgConfidence < 0.7f;
            metrics.HasSkew = false; // Would need more complex analysis
            metrics.SkewAngle = 0.0f;
            metrics.DpiEstimate = 200; // Default assumption
        }

        return await Task.FromResult(metrics);
    }
}

public class AzureOcrConfiguration
{
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public bool UseSystemIdentity { get; set; } = false;
    public int TimeoutSeconds { get; set; } = 120;
    public bool EnableQualityAssessment { get; set; } = true;
}