using IntelliFin.LoanOriginationService.Models;

namespace IntelliFin.LoanOriginationService.Services;

public interface ILoanApplicationService
{
    Task<LoanApplicationResponse> CreateApplicationAsync(CreateLoanApplicationRequest request, CancellationToken cancellationToken = default);
    Task<LoanApplicationResponse?> GetApplicationAsync(Guid applicationId, CancellationToken cancellationToken = default);
    Task<IEnumerable<LoanApplicationResponse>> GetApplicationsByClientAsync(Guid clientId, CancellationToken cancellationToken = default);
    Task<LoanApplicationResponse> UpdateApplicationAsync(Guid applicationId, Dictionary<string, object> updates, CancellationToken cancellationToken = default);
    Task<bool> SubmitApplicationAsync(Guid applicationId, CancellationToken cancellationToken = default);
    Task<bool> WithdrawApplicationAsync(Guid applicationId, string reason, CancellationToken cancellationToken = default);
    Task<LoanApplicationResponse> ApproveApplicationAsync(Guid applicationId, string approvedBy, CancellationToken cancellationToken = default);
    Task<LoanApplicationResponse> RejectApplicationAsync(Guid applicationId, string rejectedBy, string reason, CancellationToken cancellationToken = default);
    Task<RuleEngineResult> ValidateApplicationAsync(Guid applicationId, CancellationToken cancellationToken = default);
    Task<ValidationResult> ValidateInitialApplicationAsync(LoanApplicationVariables variables, CancellationToken cancellationToken = default);
}

public interface ICreditAssessmentService
{
    Task<CreditAssessment> PerformAssessmentAsync(CreditAssessmentRequest request, CancellationToken cancellationToken = default);
    Task<RiskCalculationResult> CalculateRiskGradeAsync(Guid clientId, LoanApplication application, CancellationToken cancellationToken = default);
    Task<AffordabilityAssessment> AssessAffordabilityAsync(Guid clientId, decimal loanAmount, int termMonths, CancellationToken cancellationToken = default);
    Task<CreditBureauData?> GetCreditBureauDataAsync(Guid clientId, CancellationToken cancellationToken = default);
    Task<string> GenerateScoreExplanationAsync(RiskCalculationResult riskResult, CancellationToken cancellationToken = default);
    Task<bool> UpdateAssessmentAsync(Guid assessmentId, Dictionary<string, object> updates, CancellationToken cancellationToken = default);
}

public interface ILoanProductService
{
    Task<LoanProduct?> GetProductAsync(string productCode, CancellationToken cancellationToken = default);
    Task<IEnumerable<LoanProduct>> GetActiveProductsAsync(CancellationToken cancellationToken = default);
    Task<RuleEngineResult> ValidateApplicationForProductAsync(LoanProduct product, Dictionary<string, object> applicationData, CancellationToken cancellationToken = default);
    Task<decimal> CalculateInterestRateAsync(string productCode, RiskGrade riskGrade, CancellationToken cancellationToken = default);
    Task<bool> IsEligibleForProductAsync(string productCode, Guid clientId, CancellationToken cancellationToken = default);
}

public interface IWorkflowService
{
    /// <summary>
    /// Starts a new loan origination workflow instance in Camunda with all required variables.
    /// </summary>
    /// <param name="applicationId">Unique identifier for the loan application</param>
    /// <param name="clientId">Unique identifier for the client</param>
    /// <param name="loanAmount">Requested loan amount</param>
    /// <param name="riskGrade">Risk grade assigned to the application (A, B, C, D, F)</param>
    /// <param name="productCode">Loan product code</param>
    /// <param name="termMonths">Loan term in months</param>
    /// <param name="createdBy">User ID who created the application</param>
    /// <param name="loanNumber">Generated loan number</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Workflow instance key as a string</returns>
    Task<string> StartLoanOriginationWorkflowAsync(
        Guid applicationId,
        Guid clientId,
        decimal loanAmount,
        string riskGrade,
        string productCode,
        int termMonths,
        string createdBy,
        string loanNumber,
        CancellationToken cancellationToken = default);

    [Obsolete("Use StartLoanOriginationWorkflowAsync instead. This method has limited variable support.")]
    Task<string> StartApprovalWorkflowAsync(Guid applicationId, CancellationToken cancellationToken = default);
    
    [Obsolete("Human task completion is now handled directly by Camunda Tasklist API. This method is deprecated.")]
    Task<bool> CompleteWorkflowTaskAsync(string taskId, WorkflowDecision decision, CancellationToken cancellationToken = default);
    
    [Obsolete("Workflow steps are now tracked via Camunda History API. Use Camunda REST API directly for historical data.")]
    Task<List<WorkflowStep>> GetWorkflowStepsAsync(Guid applicationId, CancellationToken cancellationToken = default);
    
    [Obsolete("Workflow advancement is now handled by external task workers. This method is deprecated.")]
    Task<bool> AdvanceWorkflowAsync(Guid applicationId, string nextStep, CancellationToken cancellationToken = default);
    
    [Obsolete("Current workflow step should be queried via Camunda Tasklist API directly.")]
    Task<string?> GetCurrentWorkflowStepAsync(Guid applicationId, CancellationToken cancellationToken = default);
    
    [Obsolete("Task reassignment is now handled directly by Camunda Tasklist API. This method is deprecated.")]
    Task<bool> ReassignWorkflowTaskAsync(string taskId, string newAssignee, CancellationToken cancellationToken = default);
}

public interface IRiskCalculationEngine
{
    Task<RiskCalculationResult> CalculateRiskAsync(LoanApplication application, CreditBureauData? bureauData, AffordabilityAssessment affordability, CancellationToken cancellationToken = default);
    Task<List<RiskFactor>> ExtractRiskFactorsAsync(LoanApplication application, CreditBureauData? bureauData, CancellationToken cancellationToken = default);
    Task<RiskGrade> DetermineRiskGradeAsync(decimal score, CancellationToken cancellationToken = default);
    Task<bool> PassesMinimumCriteriaAsync(LoanApplication application, CreditBureauData? bureauData, CancellationToken cancellationToken = default);
    Task<decimal> CalculateScoreAsync(List<RiskFactor> factors, CancellationToken cancellationToken = default);
}

public interface IComplianceService
{
    Task<BoZComplianceCheck> ValidateBoZComplianceAsync(LoanApplication application, CreditAssessment assessment, CancellationToken cancellationToken = default);
    Task<bool> ValidateKYCComplianceAsync(Guid clientId, CancellationToken cancellationToken = default);
    Task<bool> ValidateAMLComplianceAsync(Guid clientId, CancellationToken cancellationToken = default);
    Task<List<string>> GetRequiredDocumentsAsync(string productCode, CancellationToken cancellationToken = default);
    Task<bool> ValidateDocumentComplianceAsync(Guid applicationId, CancellationToken cancellationToken = default);
}
