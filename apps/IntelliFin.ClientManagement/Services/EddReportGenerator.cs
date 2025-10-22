using IntelliFin.ClientManagement.Common;
using IntelliFin.ClientManagement.Domain.Entities;
using IntelliFin.ClientManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace IntelliFin.ClientManagement.Services;

/// <summary>
/// Generates Enhanced Due Diligence (EDD) reports in text format
/// NOTE: In production, this would use a PDF library (iTextSharp, PuppeteerSharp)
/// For Phase 1, generates structured text reports for validation
/// </summary>
public class EddReportGenerator
{
    private readonly ClientManagementDbContext _context;
    private readonly ILogger<EddReportGenerator> _logger;

    public EddReportGenerator(
        ClientManagementDbContext context,
        ILogger<EddReportGenerator> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Generates comprehensive EDD report for a client
    /// Returns report content as text (would be PDF in production)
    /// </summary>
    public async Task<Result<EddReportData>> GenerateReportAsync(
        Guid clientId,
        Guid kycStatusId,
        string correlationId)
    {
        try
        {
            _logger.LogInformation(
                "Generating EDD report for client {ClientId}, KycStatus {KycStatusId}",
                clientId, kycStatusId);

            // Load all required data
            var client = await _context.Clients
                .FirstOrDefaultAsync(c => c.Id == clientId);

            if (client == null)
            {
                return Result<EddReportData>.Failure($"Client not found: {clientId}");
            }

            var kycStatus = await _context.KycStatuses
                .FirstOrDefaultAsync(k => k.Id == kycStatusId);

            if (kycStatus == null)
            {
                return Result<EddReportData>.Failure($"KYC status not found: {kycStatusId}");
            }

            var documents = await _context.ClientDocuments
                .Where(d => d.ClientId == clientId)
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();

            var amlScreenings = await _context.AmlScreenings
                .Where(a => a.KycStatusId == kycStatusId)
                .OrderBy(a => a.ScreenedAt)
                .ToListAsync();

            // Generate report sections
            var reportContent = new StringBuilder();

            AppendExecutiveSummary(reportContent, client, kycStatus, amlScreenings);
            AppendClientProfile(reportContent, client);
            AppendDocumentAnalysis(reportContent, documents, kycStatus);
            AppendAmlScreeningResults(reportContent, amlScreenings);
            AppendRiskAssessment(reportContent, kycStatus, amlScreenings);
            AppendComplianceRecommendation(reportContent, kycStatus);

            var reportData = new EddReportData
            {
                ClientId = clientId,
                KycStatusId = kycStatusId,
                GeneratedAt = DateTime.UtcNow,
                ReportContent = reportContent.ToString(),
                CorrelationId = correlationId,
                ClientName = $"{client.FirstName} {client.LastName}",
                OverallRiskLevel = DetermineOverallRiskLevel(amlScreenings),
                EddReason = kycStatus.EddReason ?? "Not specified"
            };

            _logger.LogInformation(
                "EDD report generated successfully for client {ClientId}: {Length} characters",
                clientId, reportData.ReportContent.Length);

            return Result<EddReportData>.Success(reportData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating EDD report for client {ClientId}", clientId);
            return Result<EddReportData>.Failure($"Report generation failed: {ex.Message}");
        }
    }

    private void AppendExecutiveSummary(
        StringBuilder report,
        Client client,
        KycStatus kycStatus,
        List<AmlScreening> amlScreenings)
    {
        report.AppendLine("═══════════════════════════════════════════════════════════");
        report.AppendLine("        ENHANCED DUE DILIGENCE (EDD) REPORT");
        report.AppendLine("═══════════════════════════════════════════════════════════");
        report.AppendLine();
        report.AppendLine("EXECUTIVE SUMMARY");
        report.AppendLine("─────────────────────────────────────────────────────────");
        report.AppendLine();
        report.AppendLine($"Client Name:        {client.FirstName} {client.LastName}");
        report.AppendLine($"NRC:                {client.Nrc}");
        report.AppendLine($"Report Date:        {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
        report.AppendLine($"EDD Trigger:        {kycStatus.EddReason ?? "Not specified"}");
        report.AppendLine($"Escalation Date:    {kycStatus.EddEscalatedAt:yyyy-MM-dd HH:mm} UTC");
        report.AppendLine();

        var riskLevel = DetermineOverallRiskLevel(amlScreenings);
        report.AppendLine($"OVERALL RISK LEVEL: {riskLevel}");
        report.AppendLine();

        // Key findings
        report.AppendLine("KEY FINDINGS:");
        var sanctionsHit = amlScreenings.Any(a => a.ScreeningType == "Sanctions" && a.IsMatch);
        var pepMatch = amlScreenings.Any(a => a.ScreeningType == "PEP" && a.IsMatch);

        if (sanctionsHit)
            report.AppendLine("  ⚠ SANCTIONS LIST MATCH DETECTED");
        if (pepMatch)
            report.AppendLine("  ⚠ POLITICALLY EXPOSED PERSON (PEP) IDENTIFIED");
        if (kycStatus.IsDocumentComplete)
            report.AppendLine("  ✓ All required documents verified");
        else
            report.AppendLine("  ✗ Document verification incomplete");

        report.AppendLine();
        report.AppendLine();
    }

    private void AppendClientProfile(StringBuilder report, Client client)
    {
        report.AppendLine("CLIENT PROFILE ANALYSIS");
        report.AppendLine("─────────────────────────────────────────────────────────");
        report.AppendLine();
        report.AppendLine("PERSONAL INFORMATION:");
        report.AppendLine($"  Full Name:           {client.FirstName} {client.LastName}");
        report.AppendLine($"  NRC Number:          {client.Nrc}");
        report.AppendLine($"  Date of Birth:       {client.DateOfBirth:yyyy-MM-dd} (Age: {DateTime.UtcNow.Year - client.DateOfBirth.Year})");
        report.AppendLine($"  Gender:              {client.Gender}");
        report.AppendLine($"  Marital Status:      {client.MaritalStatus}");
        report.AppendLine();
        report.AppendLine("CONTACT INFORMATION:");
        report.AppendLine($"  Primary Phone:       {client.PrimaryPhone}");
        report.AppendLine($"  Email:               {client.EmailAddress ?? "Not provided"}");
        report.AppendLine($"  Physical Address:    {client.PhysicalAddress}");
        report.AppendLine($"  City:                {client.City}");
        report.AppendLine($"  Province:            {client.Province}");
        report.AppendLine();
        report.AppendLine("BUSINESS ACTIVITIES:");
        report.AppendLine($"  Employment Status:   {(string.IsNullOrEmpty(client.Employer) ? "Not specified" : "Employed")}");
        report.AppendLine($"  Employer:            {client.Employer ?? "Not provided"}");
        report.AppendLine($"  Source of Funds:     {client.SourceOfFunds ?? "Not specified"}");
        report.AppendLine();
        report.AppendLine();
    }

    private void AppendDocumentAnalysis(
        StringBuilder report,
        List<ClientDocument> documents,
        KycStatus kycStatus)
    {
        report.AppendLine("DOCUMENT VERIFICATION RESULTS");
        report.AppendLine("─────────────────────────────────────────────────────────");
        report.AppendLine();
        report.AppendLine("DOCUMENT COMPLETENESS STATUS:");
        report.AppendLine($"  NRC Document:           {(kycStatus.HasNrc ? "✓ Verified" : "✗ Missing")}");
        report.AppendLine($"  Proof of Address:       {(kycStatus.HasProofOfAddress ? "✓ Verified" : "✗ Missing")}");
        report.AppendLine($"  Payslip:                {(kycStatus.HasPayslip ? "✓ Verified" : "✗ Missing")}");
        report.AppendLine($"  Employment Letter:      {(kycStatus.HasEmploymentLetter ? "✓ Verified" : "✗ Missing")}");
        report.AppendLine($"  Overall Completeness:   {(kycStatus.IsDocumentComplete ? "✓ COMPLETE" : "✗ INCOMPLETE")}");
        report.AppendLine();

        if (documents.Any())
        {
            report.AppendLine("UPLOADED DOCUMENTS:");
            foreach (var doc in documents)
            {
                var status = doc.UploadStatus.ToString();
                var icon = doc.UploadStatus == Domain.Enums.UploadStatus.Verified ? "✓" : 
                          doc.UploadStatus == Domain.Enums.UploadStatus.Rejected ? "✗" : "⏳";

                report.AppendLine($"  {icon} {doc.DocumentType,-20} {status,-20} Uploaded: {doc.UploadedAt:yyyy-MM-dd}");
                if (!string.IsNullOrEmpty(doc.RejectionReason))
                    report.AppendLine($"      Rejection: {doc.RejectionReason}");
            }
        }
        else
        {
            report.AppendLine("  No documents uploaded.");
        }

        report.AppendLine();
        report.AppendLine();
    }

    private void AppendAmlScreeningResults(StringBuilder report, List<AmlScreening> amlScreenings)
    {
        report.AppendLine("AML SCREENING DETAILED RESULTS");
        report.AppendLine("─────────────────────────────────────────────────────────");
        report.AppendLine();

        if (!amlScreenings.Any())
        {
            report.AppendLine("  No AML screening performed.");
            report.AppendLine();
            return;
        }

        foreach (var screening in amlScreenings)
        {
            report.AppendLine($"SCREENING TYPE: {screening.ScreeningType}");
            report.AppendLine($"  Provider:        {screening.ScreeningProvider}");
            report.AppendLine($"  Screened At:     {screening.ScreenedAt:yyyy-MM-dd HH:mm} UTC");
            report.AppendLine($"  Screened By:     {screening.ScreenedBy}");
            report.AppendLine($"  Match Status:    {(screening.IsMatch ? "⚠ MATCH FOUND" : "✓ No Match")}");
            report.AppendLine($"  Risk Level:      {screening.RiskLevel}");

            if (screening.IsMatch && !string.IsNullOrEmpty(screening.MatchDetails))
            {
                report.AppendLine($"  Match Details:");
                report.AppendLine($"    {screening.MatchDetails}");
            }

            if (!string.IsNullOrEmpty(screening.Notes))
            {
                report.AppendLine($"  Notes: {screening.Notes}");
            }

            report.AppendLine();
        }

        report.AppendLine();
    }

    private void AppendRiskAssessment(
        StringBuilder report,
        KycStatus kycStatus,
        List<AmlScreening> amlScreenings)
    {
        report.AppendLine("RISK ASSESSMENT BREAKDOWN");
        report.AppendLine("─────────────────────────────────────────────────────────");
        report.AppendLine();

        // Calculate risk score
        var riskScore = CalculateRiskScore(kycStatus, amlScreenings);

        report.AppendLine("RISK SCORING:");
        report.AppendLine($"  Overall Risk Score:  {riskScore}/100");
        report.AppendLine($"  Risk Rating:         {MapScoreToRating(riskScore)}");
        report.AppendLine();

        report.AppendLine("CONTRIBUTING RISK FACTORS:");

        var sanctionsHit = amlScreenings.Any(a => a.ScreeningType == "Sanctions" && a.IsMatch);
        var pepMatch = amlScreenings.Any(a => a.ScreeningType == "PEP" && a.IsMatch);
        var highRiskAml = amlScreenings.Any(a => a.RiskLevel == "High");

        if (sanctionsHit)
            report.AppendLine("  ⚠ Sanctions list match (+50 points)");
        if (pepMatch)
            report.AppendLine("  ⚠ PEP match (+25-50 points depending on position)");
        if (highRiskAml)
            report.AppendLine("  ⚠ High-risk AML screening result");
        if (!kycStatus.IsDocumentComplete)
            report.AppendLine("  ⚠ Incomplete document verification (+20 points)");

        report.AppendLine();
        report.AppendLine("MITIGATING FACTORS:");
        if (kycStatus.IsDocumentComplete)
            report.AppendLine("  ✓ All required documents verified");
        if (kycStatus.AmlScreeningComplete)
            report.AppendLine("  ✓ AML screening completed");
        
        report.AppendLine();
        report.AppendLine();
    }

    private void AppendComplianceRecommendation(StringBuilder report, KycStatus kycStatus)
    {
        report.AppendLine("COMPLIANCE RECOMMENDATION");
        report.AppendLine("─────────────────────────────────────────────────────────");
        report.AppendLine();

        if (kycStatus.RequiresEdd)
        {
            report.AppendLine("RECOMMENDATION:");
            report.AppendLine("  This client requires Enhanced Due Diligence review due to:");
            report.AppendLine($"  • {kycStatus.EddReason ?? "Unspecified reason"}");
            report.AppendLine();
            report.AppendLine("REQUIRED ACTIONS:");
            report.AppendLine("  1. Compliance Officer review and preliminary decision");
            report.AppendLine("  2. CEO final approval for client onboarding");
            report.AppendLine("  3. If approved, determine appropriate monitoring level:");
            report.AppendLine("     - Standard: Regular monitoring procedures");
            report.AppendLine("     - Enhanced Monitoring: Increased transaction monitoring");
            report.AppendLine("     - Restricted Services: Limited product offerings");
            report.AppendLine();
            report.AppendLine("MONITORING REQUIREMENTS:");
            report.AppendLine("  • Annual KYC review mandatory");
            report.AppendLine("  • Transaction monitoring with lower thresholds");
            report.AppendLine("  • Periodic re-screening against sanctions/PEP lists");
            report.AppendLine("  • Executive approval required for high-value transactions");
        }

        report.AppendLine();
        report.AppendLine("═══════════════════════════════════════════════════════════");
        report.AppendLine("                    END OF REPORT");
        report.AppendLine("═══════════════════════════════════════════════════════════");
    }

    private static string DetermineOverallRiskLevel(List<AmlScreening> amlScreenings)
    {
        if (!amlScreenings.Any())
            return "Medium"; // Default for EDD cases

        var hasHighRisk = amlScreenings.Any(a => a.RiskLevel == "High");
        var hasMediumRisk = amlScreenings.Any(a => a.RiskLevel == "Medium");

        if (hasHighRisk)
            return "High";
        if (hasMediumRisk)
            return "Medium";

        return "Low";
    }

    private static int CalculateRiskScore(KycStatus kycStatus, List<AmlScreening> amlScreenings)
    {
        var score = 0;

        // Document completeness factor
        if (!kycStatus.IsDocumentComplete)
            score += 20;

        // AML screening factors
        foreach (var screening in amlScreenings)
        {
            if (screening.ScreeningType == "Sanctions" && screening.IsMatch)
                score += 50; // Sanctions is highest risk
            else if (screening.ScreeningType == "PEP" && screening.IsMatch)
            {
                score += screening.RiskLevel == "High" ? 40 : 25;
            }
            else if (screening.IsMatch)
            {
                score += screening.RiskLevel switch
                {
                    "High" => 30,
                    "Medium" => 15,
                    _ => 5
                };
            }
        }

        return Math.Min(score, 100);
    }

    private static string MapScoreToRating(int score)
    {
        return score switch
        {
            <= 25 => "Low",
            <= 50 => "Medium",
            _ => "High"
        };
    }
}

/// <summary>
/// EDD report data structure
/// </summary>
public class EddReportData
{
    public Guid ClientId { get; set; }
    public Guid KycStatusId { get; set; }
    public DateTime GeneratedAt { get; set; }
    public string ReportContent { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string OverallRiskLevel { get; set; } = string.Empty;
    public string EddReason { get; set; } = string.Empty;
}
