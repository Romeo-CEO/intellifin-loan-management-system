using System.Security.Cryptography;
using IntelliFin.ClientManagement.Common;
using Microsoft.AspNetCore.Http;

namespace IntelliFin.ClientManagement.Services;

/// <summary>
/// Helper class for document validation and hash calculation
/// </summary>
public static class DocumentValidator
{
    // Maximum file size: 10MB
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;

    // Allowed content types
    private static readonly HashSet<string> AllowedContentTypes = new()
    {
        "application/pdf",
        "image/jpeg",
        "image/png"
    };

    // Allowed file extensions
    private static readonly Dictionary<string, string> ExtensionToContentType = new()
    {
        { ".pdf", "application/pdf" },
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".png", "image/png" }
    };

    /// <summary>
    /// Validates file size and content type
    /// </summary>
    public static Result<bool> ValidateFile(IFormFile file)
    {
        // Check if file is null or empty
        if (file == null || file.Length == 0)
        {
            return Result<bool>.Failure("File is required and cannot be empty");
        }

        // Validate file size
        if (file.Length > MaxFileSizeBytes)
        {
            var maxSizeMB = MaxFileSizeBytes / (1024.0 * 1024.0);
            return Result<bool>.Failure($"File size exceeds maximum allowed size of {maxSizeMB:F2}MB");
        }

        // Validate content type
        if (string.IsNullOrWhiteSpace(file.ContentType) || !AllowedContentTypes.Contains(file.ContentType.ToLower()))
        {
            return Result<bool>.Failure(
                $"Invalid content type '{file.ContentType}'. Allowed types: PDF, JPEG, PNG");
        }

        // Validate file extension
        var fileExtension = Path.GetExtension(file.FileName).ToLower();
        if (!ExtensionToContentType.ContainsKey(fileExtension))
        {
            return Result<bool>.Failure(
                $"Invalid file extension '{fileExtension}'. Allowed extensions: .pdf, .jpg, .jpeg, .png");
        }

        // Verify extension matches content type
        var expectedContentType = ExtensionToContentType[fileExtension];
        if (!file.ContentType.ToLower().Equals(expectedContentType, StringComparison.OrdinalIgnoreCase))
        {
            return Result<bool>.Failure(
                $"File extension '{fileExtension}' does not match content type '{file.ContentType}'");
        }

        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Calculates SHA256 hash of file content
    /// </summary>
    public static async Task<string> CalculateSha256HashAsync(Stream stream)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(stream);
        
        // Reset stream position for subsequent reads
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        // Convert to hex string
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// Validates document type is recognized
    /// </summary>
    public static Result<bool> ValidateDocumentType(string documentType)
    {
        var validTypes = new[] { "NRC", "Payslip", "ProofOfResidence", "EmploymentLetter", "BankStatement", "Other" };
        
        if (string.IsNullOrWhiteSpace(documentType))
        {
            return Result<bool>.Failure("Document type is required");
        }

        if (!validTypes.Contains(documentType, StringComparer.OrdinalIgnoreCase))
        {
            return Result<bool>.Failure(
                $"Invalid document type '{documentType}'. Allowed types: {string.Join(", ", validTypes)}");
        }

        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Validates document category
    /// </summary>
    public static Result<bool> ValidateCategory(string category)
    {
        var validCategories = new[] { "KYC", "Loan", "Compliance", "General" };

        if (string.IsNullOrWhiteSpace(category))
        {
            return Result<bool>.Failure("Category is required");
        }

        if (!validCategories.Contains(category, StringComparer.OrdinalIgnoreCase))
        {
            return Result<bool>.Failure(
                $"Invalid category '{category}'. Allowed categories: {string.Join(", ", validCategories)}");
        }

        return Result<bool>.Success(true);
    }
}
