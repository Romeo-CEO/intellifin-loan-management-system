namespace IntelliFin.Shared.DomainModels.Entities;

/// <summary>
/// System-Assisted Manual Verification Model
/// Represents the document verification process where OCR assists human verification
/// </summary>
public class DocumentVerification
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public string DocumentType { get; set; } = string.Empty; // NRC, Passport, etc.
    public string DocumentNumber { get; set; } = string.Empty;
    public string DocumentImagePath { get; set; } = string.Empty;
    
    // Manual entry by loan officer
    public string ManuallyEnteredData { get; set; } = string.Empty; // JSON
    
    // OCR extracted data
    public string OcrExtractedData { get; set; } = string.Empty; // JSON
    public decimal OcrConfidenceScore { get; set; }
    public string OcrProvider { get; set; } = string.Empty; // Azure, AWS, etc.
    
    // Verification decision
    public bool IsVerified { get; set; }
    public string? VerifiedBy { get; set; } // Loan officer ID/email
    public DateTime? VerificationDate { get; set; }
    public string VerificationNotes { get; set; } = string.Empty;
    public string VerificationDecisionReason { get; set; } = string.Empty;
    
    // Data mismatch tracking
    public string DataMismatches { get; set; } = string.Empty; // JSON array of mismatched fields
    public bool HasDataMismatches { get; set; }
    
    // Audit trail
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public Client? Client { get; set; }
}

/// <summary>
/// Represents OCR-extracted document data structure
/// </summary>
public class OcrDocumentData
{
    public string FullName { get; set; } = string.Empty;
    public string DocumentNumber { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string PlaceOfBirth { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public decimal ConfidenceScore { get; set; }
    public Dictionary<string, decimal> FieldConfidenceScores { get; set; } = new();
}

/// <summary>
/// Represents manually entered data by loan officer
/// </summary>
public class ManualEntryData
{
    public string FullName { get; set; } = string.Empty;
    public string DocumentNumber { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string PlaceOfBirth { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string EnteredBy { get; set; } = string.Empty;
    public DateTime EnteredAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents data mismatches between manual entry and OCR
/// </summary>
public class DataMismatch
{
    public string FieldName { get; set; } = string.Empty;
    public string ManualValue { get; set; } = string.Empty;
    public string OcrValue { get; set; } = string.Empty;
    public decimal OcrConfidence { get; set; }
    public string MismatchType { get; set; } = string.Empty; // Minor, Major, Critical
}