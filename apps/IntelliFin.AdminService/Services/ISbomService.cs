using IntelliFin.AdminService.Contracts.Requests;
using IntelliFin.AdminService.Contracts.Responses;

namespace IntelliFin.AdminService.Services;

public interface ISbomService
{
    Task<PagedResult<SBOMSummaryDto>> ListSBOMsAsync(string? serviceName, int page, int pageSize, CancellationToken cancellationToken);

    Task<SBOMDto?> GetSBOMAsync(string serviceName, string version, CancellationToken cancellationToken);

    Task<VulnerabilityReportDto> GetVulnerabilitiesAsync(string serviceName, string version, CancellationToken cancellationToken);

    Task<byte[]?> DownloadSBOMAsync(string serviceName, string version, string format, CancellationToken cancellationToken);

    Task<VulnerabilityStatisticsDto> GetVulnerabilityStatisticsAsync(CancellationToken cancellationToken);

    Task<ComplianceReportDto> GenerateComplianceReportAsync(ComplianceReportRequest request, CancellationToken cancellationToken);
}
