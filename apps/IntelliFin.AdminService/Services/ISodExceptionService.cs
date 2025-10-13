using IntelliFin.AdminService.Contracts.Requests;
using IntelliFin.AdminService.Contracts.Responses;

namespace IntelliFin.AdminService.Services;

public interface ISodExceptionService
{
    Task<SodExceptionResponse> RequestExceptionAsync(
        SodExceptionRequest request,
        string requestorId,
        string requestorName,
        CancellationToken cancellationToken);

    Task<SodExceptionStatusDto?> GetExceptionStatusAsync(Guid exceptionId, CancellationToken cancellationToken);

    Task ApproveExceptionAsync(
        Guid exceptionId,
        string reviewerId,
        string reviewerName,
        string comments,
        CancellationToken cancellationToken);

    Task RejectExceptionAsync(
        Guid exceptionId,
        string reviewerId,
        string reviewerName,
        string comments,
        CancellationToken cancellationToken);

    Task<SodComplianceReportDto> GenerateComplianceReportAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<SodPolicyDto>> GetAllPoliciesAsync(CancellationToken cancellationToken);

    Task UpdatePolicyAsync(int policyId, SodPolicyUpdateDto update, string adminId, CancellationToken cancellationToken);
}
