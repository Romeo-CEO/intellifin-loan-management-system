using IntelliFin.Desktop.OfflineCenter.Data;
using IntelliFin.Desktop.OfflineCenter.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace IntelliFin.Desktop.OfflineCenter.Services;

/// <summary>
/// Offline loan origination service implementation
/// </summary>
public class OfflineLoanOriginationService : IOfflineLoanOriginationService
{
    private readonly OfflineDbContext _dbContext;
    private readonly IOfflineBusinessRuleEngine _businessRuleEngine;
    private readonly IOfflineAuthorizationService _authorizationService;
    private readonly ILogger<OfflineLoanOriginationService> _logger;

    // CEO approval thresholds (configurable)
    private readonly decimal _ceoApprovalThreshold = 50000m; // ZMW 50,000
    private readonly decimal _managerApprovalThreshold = 20000m; // ZMW 20,000

    public OfflineLoanOriginationService(
        OfflineDbContext dbContext,
        IOfflineBusinessRuleEngine businessRuleEngine,
        IOfflineAuthorizationService authorizationService,
        ILogger<OfflineLoanOriginationService> logger)
    {
        _dbContext = dbContext;
        _businessRuleEngine = businessRuleEngine;
        _authorizationService = authorizationService;
        _logger = logger;
    }

    #region Loan Application Management

    public async Task<OfflineLoanApplication> CreateApplicationAsync(OfflineLoanApplication application)
    {
        try
        {
            // Validate the application
            var validationResult = await ValidateApplicationAsync(application);
            if (!validationResult.IsValid)
            {
                throw new InvalidOperationException($"Application validation failed: {string.Join(", ", validationResult.Errors)}");
            }

            // Set application properties
            application.ApplicationId = Guid.NewGuid().ToString();
            application.Status = OfflineLoanApplicationStatus.Draft;
            application.ApplicationDate = DateTime.UtcNow;
            application.CreatedAt = DateTime.UtcNow;
            application.LastUpdated = DateTime.UtcNow;
            application.IsSynced = false;

            // Determine if CEO approval is required
            application.RequiresCeoApproval = application.RequestedAmount >= _ceoApprovalThreshold;

            // Save to local database
            _dbContext.OfflineLoanApplications.Add(application);
            await _dbContext.SaveChangesAsync();

            // Initialize approval workflow
            await InitializeApprovalWorkflowAsync(application.ApplicationId);

            _logger.LogInformation("Created offline loan application {ApplicationId} for client {ClientId}", 
                application.ApplicationId, application.ClientId);

            return application;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating offline loan application");
            throw;
        }
    }

    public async Task<OfflineLoanApplication> UpdateApplicationAsync(OfflineLoanApplication application)
    {
        try
        {
            var existingApplication = await _dbContext.OfflineLoanApplications
                .FirstOrDefaultAsync(a => a.ApplicationId == application.ApplicationId);

            if (existingApplication == null)
            {
                throw new InvalidOperationException($"Application {application.ApplicationId} not found");
            }

            // Validate the updated application
            var validationResult = await ValidateApplicationAsync(application);
            if (!validationResult.IsValid)
            {
                throw new InvalidOperationException($"Application validation failed: {string.Join(", ", validationResult.Errors)}");
            }

            // Update properties
            existingApplication.RequestedAmount = application.RequestedAmount;
            existingApplication.TermMonths = application.TermMonths;
            existingApplication.Purpose = application.Purpose;
            existingApplication.LastUpdated = DateTime.UtcNow;
            existingApplication.IsSynced = false;

            // Re-check CEO approval requirement
            existingApplication.RequiresCeoApproval = application.RequestedAmount >= _ceoApprovalThreshold;

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Updated offline loan application {ApplicationId}", application.ApplicationId);

            return existingApplication;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating offline loan application {ApplicationId}", application.ApplicationId);
            throw;
        }
    }

    public async Task<OfflineLoanApplication?> GetApplicationAsync(string applicationId)
    {
        return await _dbContext.OfflineLoanApplications
            .Include(a => a.Documents)
            .Include(a => a.CreditAssessment)
            .FirstOrDefaultAsync(a => a.ApplicationId == applicationId);
    }

    public async Task<List<OfflineLoanApplication>> GetApplicationsAsync(OfflineLoanApplicationStatus? status = null)
    {
        var query = _dbContext.OfflineLoanApplications
            .Include(a => a.Documents)
            .Include(a => a.CreditAssessment)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(a => a.Status == status.Value);
        }

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> DeleteApplicationAsync(string applicationId)
    {
        try
        {
            var application = await _dbContext.OfflineLoanApplications
                .FirstOrDefaultAsync(a => a.ApplicationId == applicationId);

            if (application == null)
            {
                return false;
            }

            // Can only delete draft applications
            if (application.Status != OfflineLoanApplicationStatus.Draft)
            {
                throw new InvalidOperationException("Can only delete draft applications");
            }

            _dbContext.OfflineLoanApplications.Remove(application);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Deleted offline loan application {ApplicationId}", applicationId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting offline loan application {ApplicationId}", applicationId);
            throw;
        }
    }

    #endregion

    #region Loan Product Management

    public async Task<List<OfflineLoanProduct>> GetLoanProductsAsync()
    {
        return await _dbContext.OfflineLoanProducts
            .Where(p => p.IsActive)
            .OrderBy(p => p.ProductName)
            .ToListAsync();
    }

    public async Task<OfflineLoanProduct?> GetLoanProductAsync(string productId)
    {
        return await _dbContext.OfflineLoanProducts
            .FirstOrDefaultAsync(p => p.ProductId == productId);
    }

    #endregion

    #region Credit Assessment

    public async Task<OfflineCreditAssessment> CreateCreditAssessmentAsync(OfflineCreditAssessment assessment)
    {
        try
        {
            assessment.AssessmentId = Guid.NewGuid().ToString();
            assessment.AssessmentDate = DateTime.UtcNow;
            assessment.IsSynced = false;

            // Calculate key metrics
            assessment.NetDisposableIncome = assessment.MonthlySalary - assessment.MonthlyExpenses;
            assessment.DebtToIncomeRatio = assessment.MonthlySalary > 0 ? 
                (assessment.MonthlyPayment + assessment.ExistingLoanBalance) / assessment.MonthlySalary * 100 : 0;

            // Determine risk grade based on assessment
            assessment.RiskGrade = CalculateRiskGrade(assessment);
            assessment.IsApproved = DetermineApproval(assessment);
            assessment.RecommendedAmount = CalculateRecommendedAmount(assessment);

            _dbContext.OfflineCreditAssessments.Add(assessment);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Created credit assessment {AssessmentId} for application {ApplicationId}", 
                assessment.AssessmentId, assessment.ApplicationId);

            return assessment;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating credit assessment");
            throw;
        }
    }

    public async Task<OfflineCreditAssessment> UpdateCreditAssessmentAsync(OfflineCreditAssessment assessment)
    {
        try
        {
            var existing = await _dbContext.OfflineCreditAssessments
                .FirstOrDefaultAsync(a => a.AssessmentId == assessment.AssessmentId);

            if (existing == null)
            {
                throw new InvalidOperationException($"Credit assessment {assessment.AssessmentId} not found");
            }

            // Update fields
            existing.MonthlySalary = assessment.MonthlySalary;
            existing.MonthlyExpenses = assessment.MonthlyExpenses;
            existing.RequestedAmount = assessment.RequestedAmount;
            existing.MonthlyPayment = assessment.MonthlyPayment;
            existing.HasExistingLoans = assessment.HasExistingLoans;
            existing.ExistingLoanBalance = assessment.ExistingLoanBalance;
            existing.HasDefaultHistory = assessment.HasDefaultHistory;
            existing.AssessmentNotes = assessment.AssessmentNotes;

            // Recalculate metrics
            existing.NetDisposableIncome = existing.MonthlySalary - existing.MonthlyExpenses;
            existing.DebtToIncomeRatio = existing.MonthlySalary > 0 ? 
                (existing.MonthlyPayment + existing.ExistingLoanBalance) / existing.MonthlySalary * 100 : 0;
            existing.RiskGrade = CalculateRiskGrade(existing);
            existing.IsApproved = DetermineApproval(existing);
            existing.RecommendedAmount = CalculateRecommendedAmount(existing);
            existing.IsSynced = false;

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Updated credit assessment {AssessmentId}", assessment.AssessmentId);
            return existing;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating credit assessment {AssessmentId}", assessment.AssessmentId);
            throw;
        }
    }

    public async Task<OfflineCreditAssessment?> GetCreditAssessmentAsync(string applicationId)
    {
        return await _dbContext.OfflineCreditAssessments
            .FirstOrDefaultAsync(a => a.ApplicationId == applicationId);
    }

    #endregion

    #region Document Management

    public async Task<OfflineLoanDocument> AddDocumentAsync(OfflineLoanDocument document)
    {
        try
        {
            document.DocumentId = Guid.NewGuid().ToString();
            document.UploadedDate = DateTime.UtcNow;
            document.IsSynced = false;

            _dbContext.OfflineLoanDocuments.Add(document);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Added document {DocumentId} for application {ApplicationId}", 
                document.DocumentId, document.ApplicationId);

            return document;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding document");
            throw;
        }
    }

    public async Task<List<OfflineLoanDocument>> GetApplicationDocumentsAsync(string applicationId)
    {
        return await _dbContext.OfflineLoanDocuments
            .Where(d => d.ApplicationId == applicationId)
            .OrderBy(d => d.DocumentType)
            .ToListAsync();
    }

    public async Task<bool> RemoveDocumentAsync(string documentId)
    {
        try
        {
            var document = await _dbContext.OfflineLoanDocuments
                .FirstOrDefaultAsync(d => d.DocumentId == documentId);

            if (document == null)
            {
                return false;
            }

            // Delete physical file if exists
            if (File.Exists(document.FilePath))
            {
                File.Delete(document.FilePath);
            }

            _dbContext.OfflineLoanDocuments.Remove(document);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Removed document {DocumentId}", documentId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing document {DocumentId}", documentId);
            throw;
        }
    }

    public async Task<bool> VerifyDocumentAsync(string documentId, string verifiedBy, string? notes = null)
    {
        try
        {
            var document = await _dbContext.OfflineLoanDocuments
                .FirstOrDefaultAsync(d => d.DocumentId == documentId);

            if (document == null)
            {
                return false;
            }

            document.IsVerified = true;
            document.VerifiedDate = DateTime.UtcNow;
            document.VerifiedBy = verifiedBy;
            document.VerificationNotes = notes;
            document.IsSynced = false;

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Verified document {DocumentId} by {VerifiedBy}", documentId, verifiedBy);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying document {DocumentId}", documentId);
            throw;
        }
    }

    #endregion

    #region Approval Workflow

    public async Task<List<OfflineApprovalStep>> InitializeApprovalWorkflowAsync(string applicationId)
    {
        try
        {
            var application = await GetApplicationAsync(applicationId);
            if (application == null)
            {
                throw new InvalidOperationException($"Application {applicationId} not found");
            }

            var steps = new List<OfflineApprovalStep>();

            // Step 1: Loan Officer Review
            steps.Add(new OfflineApprovalStep
            {
                ApplicationId = applicationId,
                StepName = "Loan Officer Review",
                StepOrder = 1,
                AssignedTo = "LoanOfficer",
                Status = OfflineApprovalStepStatus.Pending,
                CreatedDate = DateTime.UtcNow
            });

            // Step 2: Credit Assessment
            steps.Add(new OfflineApprovalStep
            {
                ApplicationId = applicationId,
                StepName = "Credit Assessment",
                StepOrder = 2,
                AssignedTo = "CreditAnalyst",
                Status = OfflineApprovalStepStatus.Pending,
                CreatedDate = DateTime.UtcNow
            });

            // Step 3: Manager Approval (if required)
            if (application.RequestedAmount >= _managerApprovalThreshold)
            {
                steps.Add(new OfflineApprovalStep
                {
                    ApplicationId = applicationId,
                    StepName = "Manager Approval",
                    StepOrder = 3,
                    AssignedTo = "Manager",
                    Status = OfflineApprovalStepStatus.Pending,
                    CreatedDate = DateTime.UtcNow
                });
            }

            // Step 4: CEO Approval (if required)
            if (application.RequiresCeoApproval)
            {
                steps.Add(new OfflineApprovalStep
                {
                    ApplicationId = applicationId,
                    StepName = "CEO Approval",
                    StepOrder = steps.Count + 1,
                    AssignedTo = "CEO",
                    Status = OfflineApprovalStepStatus.Pending,
                    CreatedDate = DateTime.UtcNow
                });
            }

            _dbContext.OfflineApprovalSteps.AddRange(steps);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Initialized {StepCount} approval steps for application {ApplicationId}", 
                steps.Count, applicationId);

            return steps;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing approval workflow for application {ApplicationId}", applicationId);
            throw;
        }
    }

    public async Task<OfflineApprovalStep> CompleteApprovalStepAsync(string stepId, string completedBy, string decision, string? comments = null)
    {
        try
        {
            var step = await _dbContext.OfflineApprovalSteps
                .FirstOrDefaultAsync(s => s.StepId == stepId);

            if (step == null)
            {
                throw new InvalidOperationException($"Approval step {stepId} not found");
            }

            step.Status = decision.ToLower() == "approved" ? OfflineApprovalStepStatus.Approved : OfflineApprovalStepStatus.Rejected;
            step.CompletedDate = DateTime.UtcNow;
            step.CompletedBy = completedBy;
            step.Decision = decision;
            step.Comments = comments;
            step.IsSynced = false;

            await _dbContext.SaveChangesAsync();

            // Update application status if workflow is complete
            await UpdateApplicationStatusFromWorkflowAsync(step.ApplicationId);

            _logger.LogInformation("Completed approval step {StepId} with decision {Decision} by {CompletedBy}", 
                stepId, decision, completedBy);

            return step;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing approval step {StepId}", stepId);
            throw;
        }
    }

    public async Task<List<OfflineApprovalStep>> GetApprovalStepsAsync(string applicationId)
    {
        return await _dbContext.OfflineApprovalSteps
            .Where(s => s.ApplicationId == applicationId)
            .OrderBy(s => s.StepOrder)
            .ToListAsync();
    }

    #endregion

    #region CEO Operations

    public async Task<OfflineLoanApplication> CeoApproveApplicationAsync(string applicationId, string ceoUserId, string? notes = null)
    {
        try
        {
            var application = await GetApplicationAsync(applicationId);
            if (application == null)
            {
                throw new InvalidOperationException($"Application {applicationId} not found");
            }

            // Verify CEO authorization
            var canPerformCeoOps = await _authorizationService.CanPerformCeoOperationsAsync(ceoUserId);
            if (!canPerformCeoOps)
            {
                throw new UnauthorizedAccessException("User is not authorized for CEO operations");
            }

            application.Status = OfflineLoanApplicationStatus.Approved;
            application.CeoApprovedDate = DateTime.UtcNow;
            application.CeoApprovedBy = ceoUserId;
            application.CeoApprovalNotes = notes;
            application.LastUpdated = DateTime.UtcNow;
            application.IsSynced = false;

            // Complete CEO approval step if it exists
            var ceoStep = await _dbContext.OfflineApprovalSteps
                .FirstOrDefaultAsync(s => s.ApplicationId == applicationId && s.StepName == "CEO Approval");

            if (ceoStep != null)
            {
                ceoStep.Status = OfflineApprovalStepStatus.Approved;
                ceoStep.CompletedDate = DateTime.UtcNow;
                ceoStep.CompletedBy = ceoUserId;
                ceoStep.Decision = "Approved";
                ceoStep.Comments = notes;
                ceoStep.IsSynced = false;
            }

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("CEO approved application {ApplicationId} by {CeoUserId}", applicationId, ceoUserId);

            return application;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CEO approval for application {ApplicationId}", applicationId);
            throw;
        }
    }

    public async Task<OfflineVoucher> CreateVoucherAsync(OfflineVoucher voucher)
    {
        try
        {
            voucher.VoucherId = Guid.NewGuid().ToString();
            voucher.VoucherNumber = await GenerateVoucherNumberAsync();
            voucher.Status = OfflineVoucherStatus.Draft;
            voucher.CreatedDate = DateTime.UtcNow;
            voucher.IsSynced = false;

            _dbContext.OfflineVouchers.Add(voucher);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Created voucher {VoucherId} for amount {Amount}", voucher.VoucherId, voucher.Amount);

            return voucher;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating voucher");
            throw;
        }
    }

    public async Task<List<OfflineVoucher>> GetVouchersAsync(OfflineVoucherStatus? status = null)
    {
        var query = _dbContext.OfflineVouchers.AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(v => v.Status == status.Value);
        }

        return await query
            .OrderByDescending(v => v.CreatedDate)
            .ToListAsync();
    }

    public async Task<OfflineVoucher> ApproveVoucherAsync(string voucherId, string approvedBy)
    {
        try
        {
            var voucher = await _dbContext.OfflineVouchers
                .FirstOrDefaultAsync(v => v.VoucherId == voucherId);

            if (voucher == null)
            {
                throw new InvalidOperationException($"Voucher {voucherId} not found");
            }

            voucher.Status = OfflineVoucherStatus.Approved;
            voucher.ApprovedDate = DateTime.UtcNow;
            voucher.ApprovedBy = approvedBy;
            voucher.IsSynced = false;

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Approved voucher {VoucherId} by {ApprovedBy}", voucherId, approvedBy);

            return voucher;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving voucher {VoucherId}", voucherId);
            throw;
        }
    }

    #endregion

    #region Business Rule Validation

    public async Task<OfflineValidationResult> ValidateApplicationAsync(OfflineLoanApplication application)
    {
        var result = new OfflineValidationResult();

        try
        {
            // Basic validation
            if (string.IsNullOrEmpty(application.ClientId))
            {
                result.Errors.Add("Client ID is required");
            }

            if (application.RequestedAmount <= 0)
            {
                result.Errors.Add("Requested amount must be greater than zero");
            }

            if (application.TermMonths <= 0)
            {
                result.Errors.Add("Loan term must be greater than zero");
            }

            // Get loan product for validation
            var product = await GetLoanProductAsync(application.LoanProductId);
            if (product != null)
            {
                if (application.RequestedAmount < product.MinimumAmount)
                {
                    result.Errors.Add($"Requested amount is below minimum of {product.MinimumAmount:C}");
                }

                if (application.RequestedAmount > product.MaximumAmount)
                {
                    result.Errors.Add($"Requested amount exceeds maximum of {product.MaximumAmount:C}");
                }

                if (application.TermMonths < product.MinimumTermMonths)
                {
                    result.Errors.Add($"Loan term is below minimum of {product.MinimumTermMonths} months");
                }

                if (application.TermMonths > product.MaximumTermMonths)
                {
                    result.Errors.Add($"Loan term exceeds maximum of {product.MaximumTermMonths} months");
                }
            }

            // Business rule engine validation
            var ruleResult = await _businessRuleEngine.ValidateAsync(application, "LoanApplication");
            result.Errors.AddRange(ruleResult.Errors);
            result.Warnings.AddRange(ruleResult.Warnings);
            result.Information.AddRange(ruleResult.Information);

            result.IsValid = !result.Errors.Any();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating loan application");
            result.Errors.Add("Validation service error");
            result.IsValid = false;
        }

        return result;
    }

    public async Task<OfflineValidationResult> ValidateCreditAssessmentAsync(OfflineCreditAssessment assessment)
    {
        var result = new OfflineValidationResult();

        try
        {
            if (assessment.MonthlySalary <= 0)
            {
                result.Errors.Add("Monthly salary must be greater than zero");
            }

            if (assessment.MonthlyExpenses < 0)
            {
                result.Errors.Add("Monthly expenses cannot be negative");
            }

            if (assessment.RequestedAmount <= 0)
            {
                result.Errors.Add("Requested amount must be greater than zero");
            }

            // Check debt-to-income ratio
            if (assessment.DebtToIncomeRatio > 60)
            {
                result.Warnings.Add($"High debt-to-income ratio: {assessment.DebtToIncomeRatio:F1}%");
            }

            // Check net disposable income
            if (assessment.NetDisposableIncome < assessment.MonthlyPayment)
            {
                result.Errors.Add("Insufficient disposable income to cover monthly payment");
            }

            result.IsValid = !result.Errors.Any();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating credit assessment");
            result.Errors.Add("Validation service error");
            result.IsValid = false;
        }

        return result;
    }

    public async Task<OfflineValidationResult> ValidateVoucherAsync(OfflineVoucher voucher)
    {
        var result = new OfflineValidationResult();

        try
        {
            if (voucher.Amount <= 0)
            {
                result.Errors.Add("Voucher amount must be greater than zero");
            }

            if (string.IsNullOrEmpty(voucher.Description))
            {
                result.Errors.Add("Voucher description is required");
            }

            if (string.IsNullOrEmpty(voucher.Payee))
            {
                result.Errors.Add("Payee is required");
            }

            // Business limits
            const decimal maxVoucherAmount = 100000m; // ZMW 100,000
            if (voucher.Amount > maxVoucherAmount)
            {
                result.Errors.Add($"Voucher amount exceeds maximum limit of {maxVoucherAmount:C}");
            }

            result.IsValid = !result.Errors.Any();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating voucher");
            result.Errors.Add("Validation service error");
            result.IsValid = false;
        }

        return result;
    }

    #endregion

    #region Offline Operations

    public async Task<bool> CanProcessOfflineAsync()
    {
        // Check if essential data is available for offline processing
        var hasProducts = await _dbContext.OfflineLoanProducts.AnyAsync(p => p.IsActive);
        var hasBusinessRules = true; // Assume rules are loaded
        
        return hasProducts && hasBusinessRules;
    }

    public async Task<List<string>> GetOfflineCapabilitiesAsync()
    {
        return new List<string>
        {
            "Create loan applications",
            "Perform credit assessments",
            "Upload documents",
            "CEO approvals",
            "Create vouchers",
            "Generate reports",
            "View client information",
            "Process payments",
            "Business rule validation"
        };
    }

    public async Task<int> GetPendingSyncCountAsync()
    {
        var applications = await _dbContext.OfflineLoanApplications.CountAsync(a => !a.IsSynced);
        var documents = await _dbContext.OfflineLoanDocuments.CountAsync(d => !d.IsSynced);
        var assessments = await _dbContext.OfflineCreditAssessments.CountAsync(a => !a.IsSynced);
        var vouchers = await _dbContext.OfflineVouchers.CountAsync(v => !v.IsSynced);

        return applications + documents + assessments + vouchers;
    }

    public async Task<DateTime?> GetLastSyncDateAsync()
    {
        var lastSyncDates = new List<DateTime?>
        {
            await _dbContext.OfflineLoanApplications.MaxAsync(a => (DateTime?)a.LastSyncDate),
            await _dbContext.OfflineLoanDocuments.MaxAsync(d => (DateTime?)d.LastSyncDate),
            await _dbContext.OfflineCreditAssessments.MaxAsync(a => (DateTime?)a.LastSyncDate),
            await _dbContext.OfflineVouchers.MaxAsync(v => (DateTime?)v.LastSyncDate)
        };

        return lastSyncDates.Where(d => d.HasValue).MaxBy(d => d.Value);
    }

    #endregion

    #region Conflict Resolution

    public async Task<List<OfflineConflictResolution>> GetPendingConflictsAsync()
    {
        return await _dbContext.OfflineConflictResolutions
            .Where(c => c.Status == OfflineConflictStatus.Pending)
            .OrderBy(c => c.CreatedDate)
            .ToListAsync();
    }

    public async Task<OfflineConflictResolution> ResolveConflictAsync(string conflictId, OfflineConflictResolutionStrategy strategy, string? resolvedValue = null, string? notes = null)
    {
        try
        {
            var conflict = await _dbContext.OfflineConflictResolutions
                .FirstOrDefaultAsync(c => c.ConflictId == conflictId);

            if (conflict == null)
            {
                throw new InvalidOperationException($"Conflict {conflictId} not found");
            }

            conflict.Strategy = strategy;
            conflict.ResolvedValue = resolvedValue ?? (strategy == OfflineConflictResolutionStrategy.UseLocal ? conflict.LocalValue : conflict.ServerValue);
            conflict.ResolvedDate = DateTime.UtcNow;
            conflict.Status = OfflineConflictStatus.Resolved;
            conflict.ResolutionNotes = notes;

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Resolved conflict {ConflictId} using strategy {Strategy}", conflictId, strategy);

            return conflict;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving conflict {ConflictId}", conflictId);
            throw;
        }
    }

    #endregion

    #region Reporting

    public async Task<Dictionary<string, object>> GetOfflineStatisticsAsync()
    {
        var totalApplications = await _dbContext.OfflineLoanApplications.CountAsync();
        var pendingApplications = await _dbContext.OfflineLoanApplications.CountAsync(a => a.Status == OfflineLoanApplicationStatus.Submitted);
        var approvedApplications = await _dbContext.OfflineLoanApplications.CountAsync(a => a.Status == OfflineLoanApplicationStatus.Approved);
        var totalDocuments = await _dbContext.OfflineLoanDocuments.CountAsync();
        var verifiedDocuments = await _dbContext.OfflineLoanDocuments.CountAsync(d => d.IsVerified);
        var totalVouchers = await _dbContext.OfflineVouchers.CountAsync();
        var pendingVouchers = await _dbContext.OfflineVouchers.CountAsync(v => v.Status == OfflineVoucherStatus.Submitted);
        var pendingSyncCount = await GetPendingSyncCountAsync();
        var lastSyncDate = await GetLastSyncDateAsync();

        return new Dictionary<string, object>
        {
            ["TotalApplications"] = totalApplications,
            ["PendingApplications"] = pendingApplications,
            ["ApprovedApplications"] = approvedApplications,
            ["ApprovalRate"] = totalApplications > 0 ? (double)approvedApplications / totalApplications * 100 : 0,
            ["TotalDocuments"] = totalDocuments,
            ["VerifiedDocuments"] = verifiedDocuments,
            ["DocumentVerificationRate"] = totalDocuments > 0 ? (double)verifiedDocuments / totalDocuments * 100 : 0,
            ["TotalVouchers"] = totalVouchers,
            ["PendingVouchers"] = pendingVouchers,
            ["PendingSyncCount"] = pendingSyncCount,
            ["LastSyncDate"] = lastSyncDate,
            ["CanProcessOffline"] = await CanProcessOfflineAsync(),
            ["GeneratedAt"] = DateTime.UtcNow
        };
    }

    public async Task<List<OfflineLoanApplication>> GetApplicationsRequiringAttentionAsync()
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-3); // Applications older than 3 days

        return await _dbContext.OfflineLoanApplications
            .Where(a => a.Status == OfflineLoanApplicationStatus.Submitted && a.ApplicationDate < cutoffDate)
            .OrderBy(a => a.ApplicationDate)
            .ToListAsync();
    }

    #endregion

    #region Private Helper Methods

    private string CalculateRiskGrade(OfflineCreditAssessment assessment)
    {
        var score = 0;

        // Debt-to-income ratio scoring
        if (assessment.DebtToIncomeRatio <= 30) score += 25;
        else if (assessment.DebtToIncomeRatio <= 45) score += 15;
        else if (assessment.DebtToIncomeRatio <= 60) score += 5;

        // Net disposable income scoring
        if (assessment.NetDisposableIncome >= assessment.MonthlyPayment * 2) score += 25;
        else if (assessment.NetDisposableIncome >= assessment.MonthlyPayment * 1.5) score += 15;
        else if (assessment.NetDisposableIncome >= assessment.MonthlyPayment) score += 10;

        // Existing loans penalty
        if (assessment.HasExistingLoans) score -= 10;

        // Default history penalty
        if (assessment.HasDefaultHistory) score -= 20;

        // Credit score bonus (if available)
        if (assessment.CreditScore > 0)
        {
            if (assessment.CreditScore >= 700) score += 20;
            else if (assessment.CreditScore >= 600) score += 10;
            else if (assessment.CreditScore >= 500) score += 5;
        }

        return score switch
        {
            >= 80 => "A",
            >= 60 => "B",
            >= 40 => "C",
            >= 20 => "D",
            _ => "E"
        };
    }

    private bool DetermineApproval(OfflineCreditAssessment assessment)
    {
        return assessment.RiskGrade switch
        {
            "A" or "B" => true,
            "C" => assessment.DebtToIncomeRatio <= 50,
            _ => false
        };
    }

    private decimal CalculateRecommendedAmount(OfflineCreditAssessment assessment)
    {
        if (!assessment.IsApproved)
        {
            return 0;
        }

        var maxAffordablePayment = assessment.NetDisposableIncome * 0.6m; // 60% of disposable income
        var recommendedAmount = Math.Min(assessment.RequestedAmount, maxAffordablePayment * assessment.RequestedAmount / assessment.MonthlyPayment);

        return Math.Round(recommendedAmount, 0);
    }

    private async Task UpdateApplicationStatusFromWorkflowAsync(string applicationId)
    {
        var steps = await GetApprovalStepsAsync(applicationId);
        var application = await GetApplicationAsync(applicationId);

        if (application == null) return;

        var allStepsComplete = steps.All(s => s.Status != OfflineApprovalStepStatus.Pending);
        var anyStepRejected = steps.Any(s => s.Status == OfflineApprovalStepStatus.Rejected);

        if (anyStepRejected)
        {
            application.Status = OfflineLoanApplicationStatus.Declined;
        }
        else if (allStepsComplete)
        {
            application.Status = application.RequiresCeoApproval && 
                                steps.Any(s => s.StepName == "CEO Approval" && s.Status == OfflineApprovalStepStatus.Approved) ?
                                OfflineLoanApplicationStatus.Approved : OfflineLoanApplicationStatus.UnderReview;
        }

        application.LastUpdated = DateTime.UtcNow;
        application.IsSynced = false;

        await _dbContext.SaveChangesAsync();
    }

    private async Task<string> GenerateVoucherNumberAsync()
    {
        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var count = await _dbContext.OfflineVouchers
            .CountAsync(v => v.CreatedDate.Date == DateTime.UtcNow.Date);

        return $"VCH-{today}-{(count + 1):D4}";
    }

    #endregion
}