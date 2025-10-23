using IntelliFin.ClientManagement.Infrastructure.Persistence;
using IntelliFin.ClientManagement.Services;
using IntelliFin.Shared.KycDocuments;
using Microsoft.EntityFrameworkCore;
using System.Text;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;

namespace IntelliFin.ClientManagement.Workflows.CamundaWorkers;

/// <summary>
/// Camunda worker for generating EDD (Enhanced Due Diligence) reports
/// Creates comprehensive PDF report and stores in MinIO
/// </summary>
public class EddReportGenerationWorker : ICamundaJobHandler
{
    private readonly ILogger<EddReportGenerationWorker> _logger;
    private readonly ClientManagementDbContext _context;
    private readonly EddReportGenerator _reportGenerator;
    private readonly IKycDocumentService _kycDocumentService;

    public EddReportGenerationWorker(
        ILogger<EddReportGenerationWorker> logger,
        ClientManagementDbContext context,
        EddReportGenerator reportGenerator,
        IKycDocumentService kycDocumentService)
    {
        _logger = logger;
        _context = context;
        _reportGenerator = reportGenerator;
        _kycDocumentService = kycDocumentService;
    }

    public string GetTopicName() => "client.edd.generate-report";

    public string GetJobType() => "io.intellifin.edd.generate-report";

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
            // Extract variables from job
            var clientIdStr = job.Variables.GetValueOrDefault("clientId")?.ToString();
            if (string.IsNullOrWhiteSpace(clientIdStr) || !Guid.TryParse(clientIdStr, out var clientId))
            {
                throw new ArgumentException($"Invalid or missing clientId in job variables: {clientIdStr}");
            }

            var kycStatusIdStr = job.Variables.GetValueOrDefault("kycStatusId")?.ToString();
            if (string.IsNullOrWhiteSpace(kycStatusIdStr) || !Guid.TryParse(kycStatusIdStr, out var kycStatusId))
            {
                throw new ArgumentException($"Invalid or missing kycStatusId in job variables: {kycStatusIdStr}");
            }

            _logger.LogInformation(
                "Starting EDD report generation for client {ClientId}, KycStatus {KycStatusId}",
                clientId, kycStatusId);

            // Generate report
            var reportResult = await _reportGenerator.GenerateReportAsync(clientId, kycStatusId, correlationId);

            if (reportResult.IsFailure)
            {
                throw new Exception($"Report generation failed: {reportResult.Error}");
            }

            var reportData = reportResult.Value!;

            // Convert report content to bytes
            var reportBytes = Encoding.UTF8.GetBytes(reportData.ReportContent);

            // Generate MinIO object key
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var fileName = $"edd-report-{clientId}-{timestamp}.txt"; // Would be .pdf in production
            var objectKey = $"edd-reports/{clientId}/{fileName}";

            // Upload to MinIO
            _logger.LogInformation(
                "Uploading EDD report to MinIO: {ObjectKey} ({Size} bytes)",
                objectKey, reportBytes.Length);

            var uploadResult = await _kycDocumentService.UploadDocumentAsync(
                reportBytes,
                fileName,
                "text/plain", // Would be "application/pdf" in production
                "kyc-documents",
                objectKey,
                correlationId);

            if (uploadResult.IsFailure)
            {
                throw new Exception($"MinIO upload failed: {uploadResult.Error}");
            }

            // Update KYC status with report object key
            var kycStatus = await _context.KycStatuses.FindAsync(kycStatusId);
            if (kycStatus != null)
            {
                kycStatus.EddReportObjectKey = objectKey;
                kycStatus.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation(
                "EDD report generated and uploaded successfully for client {ClientId}: {ObjectKey}",
                clientId, objectKey);

            // Complete job with workflow variables
            await jobClient.NewCompleteJobCommand(job.Key)
                .Variables(new Dictionary<string, object>
                {
                    ["eddReportGenerated"] = true,
                    ["reportObjectKey"] = objectKey,
                    ["reportGeneratedAt"] = DateTime.UtcNow.ToString("o"),
                    ["clientName"] = reportData.ClientName,
                    ["overallRiskLevel"] = reportData.OverallRiskLevel,
                    ["eddReason"] = reportData.EddReason
                })
                .Send();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error generating EDD report for job {JobKey}",
                job.Key);

            // Fail job with retry
            await jobClient.NewFailCommand(job.Key)
                .Retries(job.Retries - 1)
                .ErrorMessage($"EDD report generation failed: {ex.Message}")
                .Send();
        }
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
}
