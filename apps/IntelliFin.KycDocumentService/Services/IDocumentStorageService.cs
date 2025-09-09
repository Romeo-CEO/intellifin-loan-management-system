namespace IntelliFin.KycDocumentService.Services;

public interface IDocumentStorageService
{
    Task<string> StoreDocumentAsync(Stream documentStream, string fileName, string contentType, 
        Dictionary<string, string>? metadata = null, CancellationToken cancellationToken = default);
    
    Task<string> SaveDocumentAsync(string filePath, Stream documentStream, string contentType, 
        CancellationToken cancellationToken = default);
    
    Task<Stream> GetDocumentAsync(string filePath, CancellationToken cancellationToken = default);
    
    Task<bool> DeleteDocumentAsync(string filePath, CancellationToken cancellationToken = default);
    
    Task<string> GetDocumentUrlAsync(string filePath, TimeSpan expiry, CancellationToken cancellationToken = default);
    
    Task<string> GenerateDownloadUrlAsync(string filePath, TimeSpan expiry, CancellationToken cancellationToken = default);
    
    Task<DocumentInfo> GetDocumentInfoAsync(string filePath, CancellationToken cancellationToken = default);
    
    Task<bool> DocumentExistsAsync(string filePath, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<string>> ListDocumentsAsync(string prefix, CancellationToken cancellationToken = default);
    
    Task<string> CopyDocumentAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default);
}

public class DocumentInfo
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime LastModified { get; set; }
    public string ETag { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
}