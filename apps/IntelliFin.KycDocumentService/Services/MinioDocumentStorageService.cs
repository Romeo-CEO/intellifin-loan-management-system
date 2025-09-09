using Minio;
using Minio.DataModel.Args;
using System.Security.Cryptography;

namespace IntelliFin.KycDocumentService.Services;

public class MinioDocumentStorageService : IDocumentStorageService
{
    private readonly IMinioClient _minioClient;
    private readonly ILogger<MinioDocumentStorageService> _logger;
    private readonly string _bucketName;

    public MinioDocumentStorageService(IMinioClient minioClient, IConfiguration configuration, ILogger<MinioDocumentStorageService> logger)
    {
        _minioClient = minioClient;
        _logger = logger;
        _bucketName = configuration.GetValue<string>("MinIO:BucketName") ?? "kyc-documents";
    }

    public async Task<string> StoreDocumentAsync(Stream documentStream, string fileName, string contentType,
        Dictionary<string, string>? metadata = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Ensure bucket exists
            await EnsureBucketExistsAsync();

            // Generate unique file path
            var fileExtension = Path.GetExtension(fileName);
            var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
            var filePath = $"documents/{DateTime.UtcNow:yyyy/MM/dd}/{uniqueFileName}";

            // Calculate file hash
            documentStream.Position = 0;
            var hash = await CalculateFileHashAsync(documentStream);
            documentStream.Position = 0;

            // Prepare metadata
            var objectMetadata = metadata ?? new Dictionary<string, string>();
            objectMetadata["original-filename"] = fileName;
            objectMetadata["content-type"] = contentType;
            objectMetadata["file-hash"] = hash;
            objectMetadata["upload-timestamp"] = DateTime.UtcNow.ToString("O");

            // Upload to MinIO
            var putObjectArgs = new PutObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(filePath)
                .WithStreamData(documentStream)
                .WithObjectSize(documentStream.Length)
                .WithContentType(contentType)
                .WithHeaders(objectMetadata);

            await _minioClient.PutObjectAsync(putObjectArgs, cancellationToken);

            _logger.LogInformation("Document stored successfully at path: {FilePath}", filePath);
            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store document: {FileName}", fileName);
            throw;
        }
    }

    public async Task<Stream> GetDocumentAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var memoryStream = new MemoryStream();
            
            var getObjectArgs = new GetObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(filePath)
                .WithCallbackStream(stream => stream.CopyTo(memoryStream));

            await _minioClient.GetObjectAsync(getObjectArgs, cancellationToken);
            
            memoryStream.Position = 0;
            return memoryStream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve document: {FilePath}", filePath);
            throw;
        }
    }

    public async Task<bool> DeleteDocumentAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var removeObjectArgs = new RemoveObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(filePath);

            await _minioClient.RemoveObjectAsync(removeObjectArgs, cancellationToken);
            
            _logger.LogInformation("Document deleted successfully: {FilePath}", filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete document: {FilePath}", filePath);
            return false;
        }
    }

    public async Task<string> SaveDocumentAsync(string filePath, Stream documentStream, string contentType, CancellationToken cancellationToken = default)
    {
        try
        {
            // Ensure bucket exists
            await EnsureBucketExistsAsync();

            // Upload to MinIO
            var putObjectArgs = new PutObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(filePath)
                .WithStreamData(documentStream)
                .WithObjectSize(documentStream.Length)
                .WithContentType(contentType);

            await _minioClient.PutObjectAsync(putObjectArgs, cancellationToken);

            _logger.LogInformation("Document saved successfully at path: {FilePath}", filePath);
            return filePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save document at path: {FilePath}", filePath);
            throw;
        }
    }

    public async Task<string> GetDocumentUrlAsync(string filePath, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        try
        {
            var presignedGetObjectArgs = new PresignedGetObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(filePath)
                .WithExpiry((int)expiry.TotalSeconds);

            var url = await _minioClient.PresignedGetObjectAsync(presignedGetObjectArgs);
            return url;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate document URL: {FilePath}", filePath);
            throw;
        }
    }

    public async Task<string> GenerateDownloadUrlAsync(string filePath, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        return await GetDocumentUrlAsync(filePath, expiry, cancellationToken);
    }

    public async Task<DocumentInfo> GetDocumentInfoAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var statObjectArgs = new StatObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(filePath);

            var objectStat = await _minioClient.StatObjectAsync(statObjectArgs, cancellationToken);

            return new DocumentInfo
            {
                FileName = Path.GetFileName(filePath),
                ContentType = objectStat.ContentType,
                Size = objectStat.Size,
                LastModified = objectStat.LastModified,
                ETag = objectStat.ETag,
                Metadata = objectStat.MetaData
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get document info: {FilePath}", filePath);
            throw;
        }
    }

    public async Task<bool> DocumentExistsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var statObjectArgs = new StatObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(filePath);

            await _minioClient.StatObjectAsync(statObjectArgs, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IEnumerable<string>> ListDocumentsAsync(string prefix, CancellationToken cancellationToken = default)
    {
        try
        {
            var documents = new List<string>();
            
            var listObjectsArgs = new ListObjectsArgs()
                .WithBucket(_bucketName)
                .WithPrefix(prefix)
                .WithRecursive(true);

            await foreach (var item in _minioClient.ListObjectsEnumAsync(listObjectsArgs, cancellationToken))
            {
                documents.Add(item.Key);
            }

            return documents;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list documents with prefix: {Prefix}", prefix);
            throw;
        }
    }

    public async Task<string> CopyDocumentAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default)
    {
        try
        {
            var copySourceObjectArgs = new CopySourceObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(sourcePath);

            var copyObjectArgs = new CopyObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(destinationPath)
                .WithCopyObjectSource(copySourceObjectArgs);

            await _minioClient.CopyObjectAsync(copyObjectArgs, cancellationToken);
            
            _logger.LogInformation("Document copied from {SourcePath} to {DestinationPath}", sourcePath, destinationPath);
            return destinationPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy document from {SourcePath} to {DestinationPath}", sourcePath, destinationPath);
            throw;
        }
    }

    private async Task EnsureBucketExistsAsync()
    {
        try
        {
            var bucketExistsArgs = new BucketExistsArgs().WithBucket(_bucketName);
            var exists = await _minioClient.BucketExistsAsync(bucketExistsArgs);
            
            if (!exists)
            {
                var makeBucketArgs = new MakeBucketArgs().WithBucket(_bucketName);
                await _minioClient.MakeBucketAsync(makeBucketArgs);
                _logger.LogInformation("Created MinIO bucket: {BucketName}", _bucketName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure bucket exists: {BucketName}", _bucketName);
            throw;
        }
    }

    private static async Task<string> CalculateFileHashAsync(Stream stream)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = await Task.Run(() => sha256.ComputeHash(stream));
        return Convert.ToBase64String(hashBytes);
    }
}