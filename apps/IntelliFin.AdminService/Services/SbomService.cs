using System.ComponentModel.DataAnnotations;
using System.Linq;
using IntelliFin.AdminService.Contracts.Requests;
using IntelliFin.AdminService.Contracts.Responses;
using IntelliFin.AdminService.Data;
using IntelliFin.AdminService.Models;
using IntelliFin.AdminService.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace IntelliFin.AdminService.Services;

public sealed class SbomService : ISbomService
{
    private static readonly IReadOnlyDictionary<string, string> FormatSuffixes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["spdx"] = "sbom.spdx.json",
            ["cyclonedx"] = "sbom.cyclonedx.json",
            ["syft"] = "sbom.syft.json"
        };

    private readonly AdminDbContext _dbContext;
    private readonly IMinioClient _minioClient;
    private readonly IOptionsMonitor<SbomOptions> _sbomOptions;
    private readonly ILogger<SbomService> _logger;

    public SbomService(
        AdminDbContext dbContext,
        IMinioClient minioClient,
        IOptionsMonitor<SbomOptions> sbomOptions,
        ILogger<SbomService> logger)
    {
        _dbContext = dbContext;
        _minioClient = minioClient;
        _sbomOptions = sbomOptions;
        _logger = logger;
    }

    public async Task<PagedResult<SBOMSummaryDto>> ListSBOMsAsync(string? serviceName, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _dbContext.ContainerImages.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(serviceName))
        {
            var normalized = serviceName.Trim();
            query = query.Where(ci => ci.ServiceName == normalized);
        }

        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 200);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(ci => ci.BuildTimestamp)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(ci => new SBOMSummaryDto(
                ci.ServiceName,
                ci.Version,
                ci.ImageDigest,
                ci.IsSigned,
                ci.SignatureVerified,
                ci.HasSbom,
                ci.BuildTimestamp,
                ci.CriticalCount,
                ci.HighCount,
                ci.MediumCount,
                ci.LowCount))
            .ToListAsync(cancellationToken);

        return new PagedResult<SBOMSummaryDto>(items, safePage, safePageSize, total);
    }

    public async Task<SBOMDto?> GetSBOMAsync(string serviceName, string version, CancellationToken cancellationToken)
    {
        var image = await _dbContext.ContainerImages
            .AsNoTracking()
            .Include(ci => ci.Vulnerabilities)
            .FirstOrDefaultAsync(ci => ci.ServiceName == serviceName && ci.Version == version, cancellationToken);

        if (image is null)
        {
            return null;
        }

        var vulnerabilities = image.Vulnerabilities
            .OrderByDescending(v => SeverityRank(v.Severity))
            .ThenBy(v => v.PackageName)
            .Select(v => new VulnerabilityDto(
                v.VulnerabilityId,
                v.PackageName,
                v.Severity,
                v.InstalledVersion,
                v.FixedVersion,
                v.Cvss3Score,
                v.Status,
                v.PublishedDate))
            .ToList();

        return new SBOMDto(
            image.ServiceName,
            image.Version,
            image.ImageDigest,
            image.Registry,
            image.BuildTimestamp,
            image.IsSigned,
            image.SignatureVerified,
            image.SignedBy,
            image.SignatureTimestamp,
            image.HasSbom,
            image.SbomPath,
            image.SbomFormat,
            vulnerabilities);
    }

    public async Task<VulnerabilityReportDto> GetVulnerabilitiesAsync(string serviceName, string version, CancellationToken cancellationToken)
    {
        var sbom = await GetSBOMAsync(serviceName, version, cancellationToken)
                   ?? throw new KeyNotFoundException($"SBOM not found for {serviceName}:{version}");

        return new VulnerabilityReportDto(
            sbom.ServiceName,
            sbom.Version,
            sbom.Vulnerabilities.Count(v => string.Equals(v.Severity, "CRITICAL", StringComparison.OrdinalIgnoreCase)),
            sbom.Vulnerabilities.Count(v => string.Equals(v.Severity, "HIGH", StringComparison.OrdinalIgnoreCase)),
            sbom.Vulnerabilities.Count(v => string.Equals(v.Severity, "MEDIUM", StringComparison.OrdinalIgnoreCase)),
            sbom.Vulnerabilities.Count(v => string.Equals(v.Severity, "LOW", StringComparison.OrdinalIgnoreCase)),
            sbom.Vulnerabilities);
    }

    public async Task<byte[]?> DownloadSBOMAsync(string serviceName, string version, string format, CancellationToken cancellationToken)
    {
        var image = await _dbContext.ContainerImages
            .AsNoTracking()
            .FirstOrDefaultAsync(ci => ci.ServiceName == serviceName && ci.Version == version, cancellationToken);

        if (image is null || !image.HasSbom || string.IsNullOrWhiteSpace(image.SbomPath))
        {
            return null;
        }

        var key = BuildObjectKey(image.SbomPath, format);
        var options = _sbomOptions.CurrentValue;
        var objectKey = string.IsNullOrWhiteSpace(options.Prefix) ? key : CombinePath(options.Prefix!, key);

        using var memoryStream = new MemoryStream();
        try
        {
            await _minioClient.GetObjectAsync(new GetObjectArgs()
                .WithBucket(options.BucketName)
                .WithObject(objectKey)
                .WithCallbackStream(stream => stream.CopyTo(memoryStream)), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download SBOM from MinIO. Bucket={Bucket} Key={Key}", options.BucketName, objectKey);
            return null;
        }

        return memoryStream.ToArray();
    }

    public async Task<VulnerabilityStatisticsDto> GetVulnerabilityStatisticsAsync(CancellationToken cancellationToken)
    {
        var images = await _dbContext.ContainerImages.AsNoTracking().ToListAsync(cancellationToken);
        var vulnerabilities = await _dbContext.Vulnerabilities.AsNoTracking().ToListAsync(cancellationToken);

        var byService = images
            .GroupBy(i => i.ServiceName)
            .Select(g => new VulnerabilityByServiceDto(
                g.Key,
                g.Sum(i => i.CriticalCount),
                g.Sum(i => i.HighCount),
                g.Sum(i => i.MediumCount),
                g.Sum(i => i.LowCount)))
            .OrderByDescending(dto => dto.Critical)
            .ThenByDescending(dto => dto.High)
            .ToList();

        var topPackages = vulnerabilities
            .GroupBy(v => new { v.PackageName, v.Severity })
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => new TopVulnerablePackageDto(g.Key.PackageName, g.Count(), g.Key.Severity))
            .ToList();

        return new VulnerabilityStatisticsDto(
            images.Count,
            images.Count(i => i.IsSigned),
            images.Count(i => i.HasSbom),
            images.Sum(i => i.CriticalCount),
            images.Sum(i => i.HighCount),
            images.Sum(i => i.MediumCount),
            images.Sum(i => i.LowCount),
            byService,
            topPackages);
    }

    public async Task<ComplianceReportDto> GenerateComplianceReportAsync(ComplianceReportRequest request, CancellationToken cancellationToken)
    {
        if (request.PeriodEnd < request.PeriodStart)
        {
            throw new ValidationException("PeriodEnd must be greater than or equal to PeriodStart");
        }

        var imageQuery = _dbContext.ContainerImages.AsNoTracking()
            .Where(ci => ci.BuildTimestamp >= request.PeriodStart && ci.BuildTimestamp <= request.PeriodEnd);

        if (request.Services is { Count: > 0 })
        {
            var set = request.Services.Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            imageQuery = imageQuery.Where(ci => set.Contains(ci.ServiceName));
        }

        var images = await imageQuery.ToListAsync(cancellationToken);

        var auditsQuery = _dbContext.SignatureVerificationAudits.AsNoTracking()
            .Where(a => a.VerificationTimestamp >= request.PeriodStart && a.VerificationTimestamp <= request.PeriodEnd);

        if (request.Services is { Count: > 0 })
        {
            var set = request.Services.Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            auditsQuery = auditsQuery.Where(a => set.Contains(a.ServiceName));
        }

        var audits = await auditsQuery.ToListAsync(cancellationToken);

        var imageDtos = images
            .Select(i => new ComplianceReportImageDto(
                i.ServiceName,
                i.Version,
                i.IsSigned,
                i.SignatureVerified,
                i.HasSbom,
                i.CriticalCount,
                i.HighCount,
                i.MediumCount,
                i.LowCount,
                i.BuildTimestamp,
                i.DeploymentTimestamp))
            .OrderBy(dto => dto.ServiceName)
            .ThenBy(dto => dto.Version)
            .ToList();

        var auditDtos = audits
            .OrderByDescending(a => a.VerificationTimestamp)
            .Select(a => new SignatureVerificationRecordDto(
                a.ImageDigest,
                a.ServiceName,
                a.Version,
                a.VerificationTimestamp,
                a.VerificationResult,
                a.VerificationMethod,
                a.VerifiedBy,
                a.VerificationContext))
            .ToList();

        return new ComplianceReportDto(
            DateTime.UtcNow,
            request.PeriodStart,
            request.PeriodEnd,
            imageDtos,
            imageDtos.Count(i => i.IsSigned),
            imageDtos.Count,
            imageDtos.Count(i => i.HasSbom),
            imageDtos.Sum(i => i.CriticalCount),
            auditDtos);
    }

    private static int SeverityRank(string severity)
        => severity?.ToUpperInvariant() switch
        {
            "CRITICAL" => 4,
            "HIGH" => 3,
            "MEDIUM" => 2,
            "LOW" => 1,
            _ => 0
        };

    private static string CombinePath(string prefix, string key)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return key;
        }

        if (prefix.EndsWith('/'))
        {
            return string.Concat(prefix, key);
        }

        return string.Concat(prefix, "/", key);
    }

    private static string BuildObjectKey(string basePath, string format)
    {
        if (string.IsNullOrWhiteSpace(basePath))
        {
            return FormatSuffixes.TryGetValue(format, out var fallback) ? fallback : FormatSuffixes["spdx"];
        }

        if (!FormatSuffixes.TryGetValue(format, out var suffix))
        {
            suffix = FormatSuffixes["spdx"];
        }

        if (basePath.EndsWith('.'))
        {
            return basePath + suffix;
        }

        if (basePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return basePath;
        }

        if (basePath.EndsWith('/'))
        {
            return basePath + suffix;
        }

        return string.Concat(basePath, "/", suffix);
    }
}
