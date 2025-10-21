using IntelliFin.ClientManagement.Domain.Enums;
using IntelliFin.ClientManagement.Infrastructure.Persistence;
using IntelliFin.ClientManagement.Services;
using Microsoft.EntityFrameworkCore;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;

namespace IntelliFin.ClientManagement.Workflows.CamundaWorkers;

/// <summary>
/// Camunda worker for checking KYC document completeness
/// Validates that all required documents are uploaded and verified
/// </summary>
public class KycDocumentCheckWorker : ICamundaJobHandler
{
    private readonly ILogger<KycDocumentCheckWorker> _logger;
    private readonly ClientManagementDbContext _context;
    private readonly IKycWorkflowService _kycService;

    public KycDocumentCheckWorker(
        ILogger<KycDocumentCheckWorker> logger,
        ClientManagementDbContext context,
        IKycWorkflowService kycService)
    {
        _logger = logger;
        _context = context;
        _kycService = kycService;
    }

    public string GetTopicName() => "client.kyc.check-documents";

    public string GetJobType() => "io.intellifin.kyc.check-documents";

    public async Task HandleJobAsync(IJobClient jobClient, IJob job)
    {
        var correlationId = ExtractCorrelationId(job);

        using var logScope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["JobKey"] = job.Key,
            ["ProcessInstanceKey"] = job.ProcessInstanceKey
        });

        try
        {
            // Extract client ID from job variables
            var clientIdStr = job.Variables.GetValueOrDefault("clientId")?.ToString();
            if (string.IsNullOrWhiteSpace(clientIdStr) || !Guid.TryParse(clientIdStr, out var clientId))
            {
                throw new ArgumentException($"Invalid or missing clientId in job variables: {clientIdStr}");
            }

            _logger.LogInformation(
                "Starting document completeness check for client {ClientId}",
                clientId);

            // Check document completeness
            var result = await CheckDocumentCompletenessAsync(clientId);

            // Update KYC status with document flags
            await UpdateKycDocumentStatusAsync(clientId, result);

            // Complete job with workflow variables
            await jobClient.NewCompleteJobCommand(job.Key)
                .Variables(result.Variables)
                .Send();

            _logger.LogInformation(
                "Document check completed for client {ClientId}: Complete={IsComplete}, " +
                "NRC={HasNrc}, Address={HasAddress}, Employment={HasEmployment}",
                clientId, result.IsComplete, result.HasNrc, result.HasProofOfAddress,
                result.HasPayslip || result.HasEmploymentLetter);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error checking documents for job {JobKey}",
                job.Key);

            // Fail job with retry
            await jobClient.NewFailCommand(job.Key)
                .Retries(job.Retries - 1)
                .ErrorMessage($"Document check failed: {ex.Message}")
                .Send();
        }
    }

    private async Task<DocumentCheckResult> CheckDocumentCompletenessAsync(Guid clientId)
    {
        // Query verified documents for the client
        var documents = await _context.ClientDocuments
            .Where(d => d.ClientId == clientId && d.UploadStatus == UploadStatus.Verified)
            .Select(d => d.DocumentType)
            .ToListAsync();

        // Check for required document types
        var hasNrc = documents.Contains("NRC");
        var hasProofOfAddress = documents.Contains("ProofOfAddress");
        var hasPayslip = documents.Contains("Payslip");
        var hasEmploymentLetter = documents.Contains("EmploymentLetter");

        // Document is complete if:
        // - Has NRC AND
        // - Has Proof of Address AND
        // - Has (Payslip OR Employment Letter)
        var isComplete = hasNrc && hasProofOfAddress && (hasPayslip || hasEmploymentLetter);

        return new DocumentCheckResult
        {
            HasNrc = hasNrc,
            HasProofOfAddress = hasProofOfAddress,
            HasPayslip = hasPayslip,
            HasEmploymentLetter = hasEmploymentLetter,
            IsComplete = isComplete,
            Variables = new Dictionary<string, object>
            {
                ["hasNrc"] = hasNrc,
                ["hasProofOfAddress"] = hasProofOfAddress,
                ["hasPayslip"] = hasPayslip,
                ["hasEmploymentLetter"] = hasEmploymentLetter,
                ["documentComplete"] = isComplete
            }
        };
    }

    private async Task UpdateKycDocumentStatusAsync(Guid clientId, DocumentCheckResult result)
    {
        var kycStatus = await _context.KycStatuses
            .FirstOrDefaultAsync(k => k.ClientId == clientId);

        if (kycStatus == null)
        {
            _logger.LogWarning(
                "KYC status not found for client {ClientId}, skipping update",
                clientId);
            return;
        }

        // Update document flags
        kycStatus.HasNrc = result.HasNrc;
        kycStatus.HasProofOfAddress = result.HasProofOfAddress;
        kycStatus.HasPayslip = result.HasPayslip;
        kycStatus.HasEmploymentLetter = result.HasEmploymentLetter;
        kycStatus.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogDebug(
            "Updated KYC status document flags for client {ClientId}",
            clientId);
    }

    private static string ExtractCorrelationId(IJob job)
    {
        try
        {
            var corrId = job.Variables.GetValueOrDefault("correlationId")?.ToString();
            return !string.IsNullOrWhiteSpace(corrId) ? corrId : $"job-{job.Key}";
        }
        catch
        {
            return $"job-{job.Key}";
        }
    }

    private class DocumentCheckResult
    {
        public bool HasNrc { get; set; }
        public bool HasProofOfAddress { get; set; }
        public bool HasPayslip { get; set; }
        public bool HasEmploymentLetter { get; set; }
        public bool IsComplete { get; set; }
        public Dictionary<string, object> Variables { get; set; } = new();
    }
}
