using IntelliFin.Desktop.OfflineCenter.Models;

namespace IntelliFin.Desktop.OfflineCenter.Services;

/// <summary>
/// Interface for offline loan origination operations
/// </summary>
public interface IOfflineLoanOriginationService
{
    // Loan Application Management
    Task<OfflineLoanApplication> CreateApplicationAsync(OfflineLoanApplication application);
    Task<OfflineLoanApplication> UpdateApplicationAsync(OfflineLoanApplication application);
    Task<OfflineLoanApplication?> GetApplicationAsync(string applicationId);
    Task<List<OfflineLoanApplication>> GetApplicationsAsync(OfflineLoanApplicationStatus? status = null);
    Task<bool> DeleteApplicationAsync(string applicationId);

    // Loan Product Management
    Task<List<OfflineLoanProduct>> GetLoanProductsAsync();
    Task<OfflineLoanProduct?> GetLoanProductAsync(string productId);

    // Credit Assessment
    Task<OfflineCreditAssessment> CreateCreditAssessmentAsync(OfflineCreditAssessment assessment);
    Task<OfflineCreditAssessment> UpdateCreditAssessmentAsync(OfflineCreditAssessment assessment);
    Task<OfflineCreditAssessment?> GetCreditAssessmentAsync(string applicationId);

    // Document Management
    Task<OfflineLoanDocument> AddDocumentAsync(OfflineLoanDocument document);
    Task<List<OfflineLoanDocument>> GetApplicationDocumentsAsync(string applicationId);
    Task<bool> RemoveDocumentAsync(string documentId);
    Task<bool> VerifyDocumentAsync(string documentId, string verifiedBy, string? notes = null);

    // Approval Workflow
    Task<List<OfflineApprovalStep>> InitializeApprovalWorkflowAsync(string applicationId);
    Task<OfflineApprovalStep> CompleteApprovalStepAsync(string stepId, string completedBy, string decision, string? comments = null);
    Task<List<OfflineApprovalStep>> GetApprovalStepsAsync(string applicationId);

    // CEO Operations
    Task<OfflineLoanApplication> CeoApproveApplicationAsync(string applicationId, string ceoUserId, string? notes = null);
    Task<OfflineVoucher> CreateVoucherAsync(OfflineVoucher voucher);
    Task<List<OfflineVoucher>> GetVouchersAsync(OfflineVoucherStatus? status = null);
    Task<OfflineVoucher> ApproveVoucherAsync(string voucherId, string approvedBy);

    // Business Rule Validation
    Task<OfflineValidationResult> ValidateApplicationAsync(OfflineLoanApplication application);
    Task<OfflineValidationResult> ValidateCreditAssessmentAsync(OfflineCreditAssessment assessment);
    Task<OfflineValidationResult> ValidateVoucherAsync(OfflineVoucher voucher);

    // Offline Operations
    Task<bool> CanProcessOfflineAsync();
    Task<List<string>> GetOfflineCapabilitiesAsync();
    Task<int> GetPendingSyncCountAsync();
    Task<DateTime?> GetLastSyncDateAsync();

    // Conflict Resolution
    Task<List<OfflineConflictResolution>> GetPendingConflictsAsync();
    Task<OfflineConflictResolution> ResolveConflictAsync(string conflictId, OfflineConflictResolutionStrategy strategy, string? resolvedValue = null, string? notes = null);

    // Reporting
    Task<Dictionary<string, object>> GetOfflineStatisticsAsync();
    Task<List<OfflineLoanApplication>> GetApplicationsRequiringAttentionAsync();
}

/// <summary>
/// Interface for offline business rule engine
/// </summary>
public interface IOfflineBusinessRuleEngine
{
    Task<OfflineValidationResult> ValidateAsync<T>(T entity, string ruleSet) where T : class;
    Task LoadRulesAsync();
    Task<List<string>> GetAvailableRuleSetsAsync();
    Task<Dictionary<string, object>> GetRuleConfigurationAsync(string ruleSet);
}

/// <summary>
/// Interface for offline authorization service
/// </summary>
public interface IOfflineAuthorizationService
{
    Task<bool> CanCreateLoanApplicationAsync(string userId);
    Task<bool> CanApproveLoanApplicationAsync(string userId, decimal amount);
    Task<bool> CanPerformCeoOperationsAsync(string userId);
    Task<bool> CanCreateVoucherAsync(string userId, decimal amount);
    Task<bool> RequiresCeoApprovalAsync(decimal amount, string productType);
    Task<List<string>> GetUserRolesAsync(string userId);
    Task<Dictionary<string, decimal>> GetApprovalLimitsAsync(string userId);
}