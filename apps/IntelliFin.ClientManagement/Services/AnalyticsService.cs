using IntelliFin.ClientManagement.Domain.Enums;
using IntelliFin.ClientManagement.Infrastructure.Persistence;
using IntelliFin.ClientManagement.Models.Analytics;
using IntelliFin.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace IntelliFin.ClientManagement.Services;

/// <summary>
/// Implementation of analytics service
/// Calculates KYC performance metrics from database
/// </summary>
public class AnalyticsService : IAnalyticsService
{
    private readonly ClientManagementDbContext _context;
    private readonly ILogger<AnalyticsService> _logger;

    // SLA threshold in hours (24 hours for KYC completion)
    private const double SlaThresholdHours = 24.0;

    public AnalyticsService(
        ClientManagementDbContext context,
        ILogger<AnalyticsService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<KycPerformanceMetrics>> GetKycPerformanceAsync(
        AnalyticsRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Calculating KYC performance metrics: Period={Start} to {End}, BranchId={BranchId}",
                request.StartDate, request.EndDate, request.BranchId);

            var query = _context.KycStatuses
                .Include(k => k.Client)
                .Where(k => k.CreatedAt >= request.StartDate && k.CreatedAt <= request.EndDate);

            if (request.BranchId.HasValue)
            {
                query = query.Where(k => k.Client.BranchId == request.BranchId.Value);
            }

            var statuses = await query.ToListAsync(cancellationToken);

            var totalStarted = statuses.Count;
            var totalCompleted = statuses.Count(k => k.CurrentState == KycState.Completed);
            var totalRejected = statuses.Count(k => k.CurrentState == KycState.Rejected);
            var totalInProgress = statuses.Count(k => k.CurrentState == KycState.InProgress);
            var totalPending = statuses.Count(k => k.CurrentState == KycState.Pending);
            var totalEddEscalations = statuses.Count(k => k.RequiresEdd);

            // Calculate processing times for completed KYCs
            var completedKycs = statuses
                .Where(k => k.CurrentState == KycState.Completed && k.KycCompletedAt.HasValue)
                .Select(k => new
                {
                    ProcessingTimeHours = (k.KycCompletedAt!.Value - k.CreatedAt).TotalHours,
                    WithinSla = (k.KycCompletedAt!.Value - k.CreatedAt).TotalHours <= SlaThresholdHours
                })
                .ToList();

            var avgProcessingTime = completedKycs.Any() 
                ? completedKycs.Average(k => k.ProcessingTimeHours) 
                : 0;

            var medianProcessingTime = completedKycs.Any()
                ? CalculateMedian(completedKycs.Select(k => k.ProcessingTimeHours))
                : 0;

            var slaCompliant = completedKycs.Count(k => k.WithinSla);
            var slaBreaches = completedKycs.Count - slaCompliant;
            var slaComplianceRate = completedKycs.Any()
                ? (slaCompliant / (double)completedKycs.Count) * 100
                : 100;

            var avgSlaBreachTime = slaBreaches > 0
                ? completedKycs.Where(k => !k.WithinSla).Average(k => k.ProcessingTimeHours)
                : (double?)null;

            var metrics = new KycPerformanceMetrics
            {
                PeriodStart = request.StartDate,
                PeriodEnd = request.EndDate,
                BranchId = request.BranchId,
                TotalStarted = totalStarted,
                TotalCompleted = totalCompleted,
                TotalRejected = totalRejected,
                TotalInProgress = totalInProgress,
                TotalPending = totalPending,
                TotalEddEscalations = totalEddEscalations,
                CompletionRate = totalStarted > 0 ? (totalCompleted / (double)totalStarted) * 100 : 0,
                AverageProcessingTimeHours = avgProcessingTime,
                MedianProcessingTimeHours = medianProcessingTime,
                EddEscalationRate = totalStarted > 0 ? (totalEddEscalations / (double)totalStarted) * 100 : 0,
                SlaComplianceRate = slaComplianceRate,
                SlaBreaches = slaBreaches,
                AverageSlaBreachTimeHours = avgSlaBreachTime
            };

            _logger.LogInformation(
                "KYC performance calculated: Total={Total}, Completed={Completed}, CompletionRate={Rate:F2}%",
                totalStarted, totalCompleted, metrics.CompletionRate);

            return Result<KycPerformanceMetrics>.Success(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating KYC performance metrics");
            return Result<KycPerformanceMetrics>.Failure($"Failed to calculate KYC metrics: {ex.Message}");
        }
    }

    public async Task<Result<DocumentMetrics>> GetDocumentMetricsAsync(
        AnalyticsRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Calculating document metrics: Period={Start} to {End}",
                request.StartDate, request.EndDate);

            var query = _context.KycDocuments
                .Include(d => d.Client)
                .Where(d => d.UploadedAt >= request.StartDate && d.UploadedAt <= request.EndDate);

            if (request.BranchId.HasValue)
            {
                query = query.Where(d => d.Client.BranchId == request.BranchId.Value);
            }

            var documents = await query.ToListAsync(cancellationToken);

            var totalUploaded = documents.Count;
            var totalVerified = documents.Count(d => d.VerificationStatus == "Verified");
            var totalRejected = documents.Count(d => d.VerificationStatus == "Rejected");
            var totalPending = documents.Count(d => d.VerificationStatus == "Pending");

            // Calculate verification times
            var verifiedDocs = documents
                .Where(d => d.VerificationStatus == "Verified" && d.VerifiedAt.HasValue)
                .Select(d => (d.VerifiedAt!.Value - d.UploadedAt).TotalHours)
                .ToList();

            var avgVerificationTime = verifiedDocs.Any() ? verifiedDocs.Average() : 0;

            // Dual-control compliance (uploader != verifier)
            var dualControlCompliant = documents
                .Where(d => d.VerificationStatus == "Verified" && !string.IsNullOrEmpty(d.VerifiedBy))
                .Count(d => d.UploadedBy != d.VerifiedBy);

            var dualControlRate = totalVerified > 0
                ? (dualControlCompliant / (double)totalVerified) * 100
                : 0;

            // Top rejection reasons
            var rejectionReasons = documents
                .Where(d => d.VerificationStatus == "Rejected" && !string.IsNullOrEmpty(d.RejectionReason))
                .GroupBy(d => d.RejectionReason)
                .Select(g => new RejectionReasonStat
                {
                    Reason = g.Key!,
                    Count = g.Count(),
                    Percentage = totalRejected > 0 ? (g.Count() / (double)totalRejected) * 100 : 0
                })
                .OrderByDescending(r => r.Count)
                .Take(5)
                .ToList();

            var metrics = new DocumentMetrics
            {
                TotalUploaded = totalUploaded,
                TotalVerified = totalVerified,
                TotalRejected = totalRejected,
                TotalPending = totalPending,
                AverageVerificationTimeHours = avgVerificationTime,
                VerificationRate = totalUploaded > 0 ? (totalVerified / (double)totalUploaded) * 100 : 0,
                RejectionRate = totalUploaded > 0 ? (totalRejected / (double)totalUploaded) * 100 : 0,
                DualControlComplianceRate = dualControlRate,
                TopRejectionReasons = rejectionReasons
            };

            _logger.LogInformation(
                "Document metrics calculated: Total={Total}, Verified={Verified}, DualControl={DualControl:F2}%",
                totalUploaded, totalVerified, dualControlRate);

            return Result<DocumentMetrics>.Success(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating document metrics");
            return Result<DocumentMetrics>.Failure($"Failed to calculate document metrics: {ex.Message}");
        }
    }

    public async Task<Result<AmlMetrics>> GetAmlMetricsAsync(
        AnalyticsRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Calculating AML metrics: Period={Start} to {End}",
                request.StartDate, request.EndDate);

            var query = _context.AmlScreenings
                .Include(a => a.KycStatus)
                .ThenInclude(k => k.Client)
                .Where(a => a.ScreenedAt >= request.StartDate && a.ScreenedAt <= request.EndDate);

            if (request.BranchId.HasValue)
            {
                query = query.Where(a => a.KycStatus.Client.BranchId == request.BranchId.Value);
            }

            var screenings = await query.ToListAsync(cancellationToken);

            var totalScreenings = screenings.Count;
            var sanctionsHits = screenings.Count(s => s.SanctionsHit);
            var pepMatches = screenings.Count(s => s.PepMatch);

            // Risk level distribution
            var riskDistribution = screenings
                .GroupBy(s => s.OverallRiskLevel)
                .ToDictionary(g => g.Key, g => g.Count());

            // AML-triggered EDD
            var amlTriggeredEdd = screenings.Count(s => s.KycStatus.RequiresEdd);

            var metrics = new AmlMetrics
            {
                TotalScreenings = totalScreenings,
                SanctionsHits = sanctionsHits,
                PepMatches = pepMatches,
                SanctionsHitRate = totalScreenings > 0 ? (sanctionsHits / (double)totalScreenings) * 100 : 0,
                PepMatchRate = totalScreenings > 0 ? (pepMatches / (double)totalScreenings) * 100 : 0,
                AverageScreeningTimeSeconds = 0.5, // Placeholder - would calculate from timing data
                RiskLevelDistribution = riskDistribution,
                AmlTriggeredEdd = amlTriggeredEdd
            };

            _logger.LogInformation(
                "AML metrics calculated: Screenings={Total}, Sanctions={Sanctions}, PEP={Pep}",
                totalScreenings, sanctionsHits, pepMatches);

            return Result<AmlMetrics>.Success(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating AML metrics");
            return Result<AmlMetrics>.Failure($"Failed to calculate AML metrics: {ex.Message}");
        }
    }

    public async Task<Result<EddMetrics>> GetEddMetricsAsync(
        AnalyticsRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Calculating EDD metrics: Period={Start} to {End}",
                request.StartDate, request.EndDate);

            var query = _context.KycStatuses
                .Include(k => k.Client)
                .Where(k => k.RequiresEdd 
                    && k.EddEscalatedAt.HasValue 
                    && k.EddEscalatedAt.Value >= request.StartDate 
                    && k.EddEscalatedAt.Value <= request.EndDate);

            if (request.BranchId.HasValue)
            {
                query = query.Where(k => k.Client.BranchId == request.BranchId.Value);
            }

            var eddCases = await query.ToListAsync(cancellationToken);

            var totalInitiated = eddCases.Count;
            var totalApproved = eddCases.Count(e => e.CurrentState == KycState.Completed && e.EddApprovedBy != null);
            var totalRejected = eddCases.Count(e => e.CurrentState == KycState.Rejected);
            var totalInProgress = eddCases.Count(e => e.CurrentState == KycState.EDD_Required);

            // Processing times for completed EDD
            var completedEdd = eddCases
                .Where(e => e.EddApprovedAt.HasValue && e.EddEscalatedAt.HasValue)
                .Select(e => (e.EddApprovedAt!.Value - e.EddEscalatedAt!.Value).TotalDays)
                .ToList();

            var avgProcessingDays = completedEdd.Any() ? completedEdd.Average() : 0;

            // Risk acceptance distribution
            var riskAcceptance = eddCases
                .Where(e => !string.IsNullOrEmpty(e.RiskAcceptanceLevel))
                .GroupBy(e => e.RiskAcceptanceLevel)
                .ToDictionary(g => g.Key!, g => g.Count());

            // Top escalation reasons
            var escalationReasons = eddCases
                .Where(e => !string.IsNullOrEmpty(e.EddReason))
                .GroupBy(e => e.EddReason)
                .Select(g => new EscalationReasonStat
                {
                    Reason = g.Key!,
                    Count = g.Count(),
                    Percentage = totalInitiated > 0 ? (g.Count() / (double)totalInitiated) * 100 : 0
                })
                .OrderByDescending(r => r.Count)
                .Take(5)
                .ToList();

            var metrics = new EddMetrics
            {
                TotalInitiated = totalInitiated,
                TotalApproved = totalApproved,
                TotalRejected = totalRejected,
                TotalInProgress = totalInProgress,
                AverageProcessingTimeDays = avgProcessingDays,
                ApprovalRate = totalInitiated > 0 ? (totalApproved / (double)totalInitiated) * 100 : 0,
                RejectionRate = totalInitiated > 0 ? (totalRejected / (double)totalInitiated) * 100 : 0,
                AverageTimeToComplianceHours = 24.0, // Placeholder
                AverageTimeToCeoHours = 12.0, // Placeholder
                RiskAcceptanceDistribution = riskAcceptance,
                TopEscalationReasons = escalationReasons
            };

            _logger.LogInformation(
                "EDD metrics calculated: Total={Total}, Approved={Approved}, Rejected={Rejected}",
                totalInitiated, totalApproved, totalRejected);

            return Result<EddMetrics>.Success(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating EDD metrics");
            return Result<EddMetrics>.Failure($"Failed to calculate EDD metrics: {ex.Message}");
        }
    }

    public async Task<Result<List<OfficerPerformanceMetrics>>> GetOfficerPerformanceAsync(
        OfficerPerformanceRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Calculating officer performance: Period={Start} to {End}",
                request.StartDate, request.EndDate);

            var kycQuery = _context.KycStatuses
                .Include(k => k.Client)
                .Where(k => k.CreatedAt >= request.StartDate && k.CreatedAt <= request.EndDate);

            if (request.BranchId.HasValue)
            {
                kycQuery = kycQuery.Where(k => k.Client.BranchId == request.BranchId.Value);
            }

            var kycData = await kycQuery.ToListAsync(cancellationToken);

            // Group by CreatedBy (officer who initiated KYC)
            var officerGroups = kycData
                .GroupBy(k => k.CreatedBy)
                .Where(g => !string.IsNullOrEmpty(g.Key));

            if (!string.IsNullOrEmpty(request.OfficerId))
            {
                officerGroups = officerGroups.Where(g => g.Key == request.OfficerId);
            }

            var officerMetrics = new List<OfficerPerformanceMetrics>();

            foreach (var group in officerGroups)
            {
                var officerId = group.Key;
                var kycCases = group.ToList();

                if (kycCases.Count < request.MinimumProcessed)
                    continue;

                var completed = kycCases.Count(k => k.CurrentState == KycState.Completed);
                var rejected = kycCases.Count(k => k.CurrentState == KycState.Rejected);

                // Calculate processing times
                var processingTimes = kycCases
                    .Where(k => k.CurrentState == KycState.Completed && k.KycCompletedAt.HasValue)
                    .Select(k => (k.KycCompletedAt!.Value - k.CreatedAt).TotalHours)
                    .ToList();

                var avgProcessingTime = processingTimes.Any() ? processingTimes.Average() : 0;

                var slaCompliant = processingTimes.Count(t => t <= SlaThresholdHours);
                var slaRate = processingTimes.Any()
                    ? (slaCompliant / (double)processingTimes.Count) * 100
                    : 100;

                // Get document counts for this officer
                var documentsUploaded = await _context.KycDocuments
                    .Where(d => d.UploadedBy == officerId 
                        && d.UploadedAt >= request.StartDate 
                        && d.UploadedAt <= request.EndDate)
                    .CountAsync(cancellationToken);

                var documentsVerified = await _context.KycDocuments
                    .Where(d => d.VerifiedBy == officerId 
                        && d.VerifiedAt >= request.StartDate 
                        && d.VerifiedAt <= request.EndDate)
                    .CountAsync(cancellationToken);

                var metrics = new OfficerPerformanceMetrics
                {
                    OfficerId = officerId,
                    OfficerName = officerId, // Would map to actual names from user service
                    TotalProcessed = kycCases.Count,
                    TotalCompleted = completed,
                    TotalRejected = rejected,
                    AverageProcessingTimeHours = avgProcessingTime,
                    SlaComplianceRate = slaRate,
                    DocumentsUploaded = documentsUploaded,
                    DocumentsVerified = documentsVerified,
                    CompletionRate = kycCases.Count > 0 ? (completed / (double)kycCases.Count) * 100 : 0
                };

                officerMetrics.Add(metrics);
            }

            // Sort results
            officerMetrics = request.SortBy switch
            {
                OfficerSortBy.TotalProcessed => request.SortDirection == SortDirection.Descending
                    ? officerMetrics.OrderByDescending(o => o.TotalProcessed).ToList()
                    : officerMetrics.OrderBy(o => o.TotalProcessed).ToList(),
                OfficerSortBy.CompletionRate => request.SortDirection == SortDirection.Descending
                    ? officerMetrics.OrderByDescending(o => o.CompletionRate).ToList()
                    : officerMetrics.OrderBy(o => o.CompletionRate).ToList(),
                OfficerSortBy.AverageProcessingTime => request.SortDirection == SortDirection.Descending
                    ? officerMetrics.OrderByDescending(o => o.AverageProcessingTimeHours).ToList()
                    : officerMetrics.OrderBy(o => o.AverageProcessingTimeHours).ToList(),
                OfficerSortBy.SlaComplianceRate => request.SortDirection == SortDirection.Descending
                    ? officerMetrics.OrderByDescending(o => o.SlaComplianceRate).ToList()
                    : officerMetrics.OrderBy(o => o.SlaComplianceRate).ToList(),
                _ => officerMetrics
            };

            _logger.LogInformation(
                "Officer performance calculated: Officers={Count}",
                officerMetrics.Count);

            return Result<List<OfficerPerformanceMetrics>>.Success(officerMetrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating officer performance");
            return Result<List<OfficerPerformanceMetrics>>.Failure($"Failed to calculate officer performance: {ex.Message}");
        }
    }

    public async Task<Result<RiskDistributionMetrics>> GetRiskDistributionAsync(
        AnalyticsRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Calculating risk distribution: Period={Start} to {End}",
                request.StartDate, request.EndDate);

            var query = _context.RiskProfiles
                .Include(r => r.Client)
                .Where(r => r.IsCurrent 
                    && r.ComputedAt >= request.StartDate 
                    && r.ComputedAt <= request.EndDate);

            if (request.BranchId.HasValue)
            {
                query = query.Where(r => r.Client.BranchId == request.BranchId.Value);
            }

            var riskProfiles = await query.ToListAsync(cancellationToken);

            var lowRisk = riskProfiles.Count(r => r.RiskRating == "Low");
            var mediumRisk = riskProfiles.Count(r => r.RiskRating == "Medium");
            var highRisk = riskProfiles.Count(r => r.RiskRating == "High");

            var riskScores = riskProfiles.Select(r => (double)r.RiskScore).ToList();
            var avgRiskScore = riskScores.Any() ? riskScores.Average() : 0;
            var medianRiskScore = riskScores.Any() ? CalculateMedian(riskScores) : 0;

            // EDD required percentage
            var clientIds = riskProfiles.Select(r => r.ClientId).ToList();
            var eddRequired = await _context.KycStatuses
                .Where(k => clientIds.Contains(k.ClientId) && k.RequiresEdd)
                .CountAsync(cancellationToken);

            var eddPercentage = clientIds.Any() 
                ? (eddRequired / (double)clientIds.Count) * 100 
                : 0;

            var metrics = new RiskDistributionMetrics
            {
                LowRisk = lowRisk,
                MediumRisk = mediumRisk,
                HighRisk = highRisk,
                AverageRiskScore = avgRiskScore,
                MedianRiskScore = medianRiskScore,
                EddRequiredPercentage = eddPercentage
            };

            _logger.LogInformation(
                "Risk distribution calculated: Low={Low}, Medium={Medium}, High={High}",
                lowRisk, mediumRisk, highRisk);

            return Result<RiskDistributionMetrics>.Success(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating risk distribution");
            return Result<RiskDistributionMetrics>.Failure($"Failed to calculate risk distribution: {ex.Message}");
        }
    }

    public async Task<Result<KycFunnelMetrics>> GetKycFunnelMetricsAsync(
        AnalyticsRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Calculating KYC funnel: Period={Start} to {End}",
                request.StartDate, request.EndDate);

            var clientQuery = _context.Clients
                .Where(c => c.CreatedAt >= request.StartDate && c.CreatedAt <= request.EndDate);

            if (request.BranchId.HasValue)
            {
                clientQuery = clientQuery.Where(c => c.BranchId == request.BranchId.Value);
            }

            var clients = await clientQuery.Select(c => c.Id).ToListAsync(cancellationToken);
            var clientsCreated = clients.Count;

            // Documents uploaded
            var clientsWithDocs = await _context.KycDocuments
                .Where(d => clients.Contains(d.ClientId))
                .Select(d => d.ClientId)
                .Distinct()
                .CountAsync(cancellationToken);

            // Documents verified
            var clientsDocsVerified = await _context.KycDocuments
                .Where(d => clients.Contains(d.ClientId) && d.VerificationStatus == "Verified")
                .Select(d => d.ClientId)
                .Distinct()
                .CountAsync(cancellationToken);

            // AML screening passed (no hits)
            var clientsAmlPassed = await _context.AmlScreenings
                .Where(a => clients.Contains(a.KycStatus.ClientId) && !a.SanctionsHit)
                .Select(a => a.KycStatus.ClientId)
                .Distinct()
                .CountAsync(cancellationToken);

            // Risk assessment complete
            var clientsRiskComplete = await _context.RiskProfiles
                .Where(r => clients.Contains(r.ClientId) && r.IsCurrent)
                .CountAsync(cancellationToken);

            // KYC completed
            var clientsKycCompleted = await _context.KycStatuses
                .Where(k => clients.Contains(k.ClientId) && k.CurrentState == KycState.Completed)
                .CountAsync(cancellationToken);

            var metrics = new KycFunnelMetrics
            {
                ClientsCreated = clientsCreated,
                DocumentsUploaded = clientsWithDocs,
                DocumentsVerified = clientsDocsVerified,
                AmlScreeningPassed = clientsAmlPassed,
                RiskAssessmentComplete = clientsRiskComplete,
                KycCompleted = clientsKycCompleted,
                DocumentUploadConversion = clientsCreated > 0 ? (clientsWithDocs / (double)clientsCreated) * 100 : 0,
                VerificationConversion = clientsWithDocs > 0 ? (clientsDocsVerified / (double)clientsWithDocs) * 100 : 0,
                AmlPassConversion = clientsDocsVerified > 0 ? (clientsAmlPassed / (double)clientsDocsVerified) * 100 : 0,
                OverallConversionRate = clientsCreated > 0 ? (clientsKycCompleted / (double)clientsCreated) * 100 : 0
            };

            _logger.LogInformation(
                "KYC funnel calculated: Created={Created}, Completed={Completed}, Conversion={Rate:F2}%",
                clientsCreated, clientsKycCompleted, metrics.OverallConversionRate);

            return Result<KycFunnelMetrics>.Success(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating KYC funnel");
            return Result<KycFunnelMetrics>.Failure($"Failed to calculate KYC funnel: {ex.Message}");
        }
    }

    public async Task<Result<List<TimeSeriesDataPoint>>> GetKycTimeSeriesAsync(
        AnalyticsRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Calculating KYC time series: Period={Start} to {End}, Granularity={Granularity}",
                request.StartDate, request.EndDate, request.Granularity);

            var granularity = request.Granularity ?? TimeGranularity.Daily;

            var query = _context.KycStatuses
                .Include(k => k.Client)
                .Where(k => k.CreatedAt >= request.StartDate && k.CreatedAt <= request.EndDate);

            if (request.BranchId.HasValue)
            {
                query = query.Where(k => k.Client.BranchId == request.BranchId.Value);
            }

            var statuses = await query.ToListAsync(cancellationToken);

            // Group by period based on granularity
            var groupedData = statuses.GroupBy(k => TruncateDate(k.CreatedAt, granularity));

            var dataPoints = groupedData.Select(g =>
            {
                var started = g.Count();
                var completed = g.Count(k => k.CurrentState == KycState.Completed);
                var rejected = g.Count(k => k.CurrentState == KycState.Rejected);
                var eddEscalations = g.Count(k => k.RequiresEdd);

                return new TimeSeriesDataPoint
                {
                    Period = g.Key,
                    Started = started,
                    Completed = completed,
                    Rejected = rejected,
                    EddEscalations = eddEscalations,
                    CompletionRate = started > 0 ? (completed / (double)started) * 100 : 0
                };
            })
            .OrderBy(d => d.Period)
            .ToList();

            _logger.LogInformation(
                "Time series calculated: DataPoints={Count}",
                dataPoints.Count);

            return Result<List<TimeSeriesDataPoint>>.Success(dataPoints);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating KYC time series");
            return Result<List<TimeSeriesDataPoint>>.Failure($"Failed to calculate time series: {ex.Message}");
        }
    }

    #region Helper Methods

    private static double CalculateMedian(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        var count = sorted.Count;

        if (count == 0)
            return 0;

        if (count % 2 == 0)
        {
            // Even count - average of two middle values
            return (sorted[count / 2 - 1] + sorted[count / 2]) / 2.0;
        }
        else
        {
            // Odd count - middle value
            return sorted[count / 2];
        }
    }

    private static DateTime TruncateDate(DateTime date, TimeGranularity granularity)
    {
        return granularity switch
        {
            TimeGranularity.Daily => date.Date,
            TimeGranularity.Weekly => date.Date.AddDays(-(int)date.DayOfWeek),
            TimeGranularity.Monthly => new DateTime(date.Year, date.Month, 1),
            _ => date.Date
        };
    }

    #endregion
}
