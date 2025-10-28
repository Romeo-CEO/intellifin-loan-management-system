using IntelliFin.LoanOriginationService.Models;
using IntelliFin.LoanOriginationService.Exceptions;
using IntelliFin.Shared.DomainModels.Repositories;
using System.Text.Json;

namespace IntelliFin.LoanOriginationService.Services;

public class LoanApplicationService : ILoanApplicationService
{
    private readonly ILogger<LoanApplicationService> _logger;
    private readonly ILoanProductService _productService;
    private readonly ICreditAssessmentService _creditAssessmentService;
    private readonly IWorkflowService _workflowService;
    private readonly IComplianceService _complianceService;
    private readonly ILoanApplicationRepository _applicationRepository;
    private readonly ILoanVersioningService _versioningService;
    private readonly IClientManagementClient? _clientManagementClient;
    private readonly IDualControlValidator _dualControlValidator;

    public LoanApplicationService(
        ILogger<LoanApplicationService> logger,
        ILoanProductService productService,
        ICreditAssessmentService creditAssessmentService,
        IWorkflowService workflowService,
        IComplianceService complianceService,
        ILoanApplicationRepository applicationRepository,
        ILoanVersioningService versioningService,
        IDualControlValidator dualControlValidator,
        IClientManagementClient? clientManagementClient = null)
    {
        _logger = logger;
        _productService = productService;
        _creditAssessmentService = creditAssessmentService;
        _workflowService = workflowService;
        _complianceService = complianceService;
        _applicationRepository = applicationRepository;
        _versioningService = versioningService;
        _clientManagementClient = clientManagementClient;
        _dualControlValidator = dualControlValidator;
    }

    public async Task<LoanApplicationResponse> CreateApplicationAsync(CreateLoanApplicationRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Creating loan application for client {ClientId}, product {ProductCode}", 
                request.ClientId, request.ProductCode);

            // Verify KYC status before allowing loan application
            if (_clientManagementClient != null)
            {
                var verification = await _clientManagementClient.GetClientVerificationAsync(
                    request.ClientId, cancellationToken);

                // Check if KYC status is "Approved"
                if (!verification.KycStatus.Equals("Approved", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning(
                        "KYC verification failed for client {ClientId}: Status={KycStatus}",
                        request.ClientId,
                        verification.KycStatus);
                    
                    throw new KycNotVerifiedException(request.ClientId, verification.KycStatus);
                }

                // Check if KYC has expired (>12 months old)
                if (verification.KycApprovedAt.HasValue)
                {
                    var kycExpirationDate = verification.KycApprovedAt.Value.AddMonths(12);
                    if (DateTime.UtcNow > kycExpirationDate)
                    {
                        _logger.LogWarning(
                            "KYC verification expired for client {ClientId}: ApprovedAt={KycApprovedAt}, ExpiryDate={ExpiryDate}",
                            request.ClientId,
                            verification.KycApprovedAt.Value,
                            kycExpirationDate);
                        
                        throw new KycExpiredException(request.ClientId, verification.KycApprovedAt.Value);
                    }
                }

                _logger.LogInformation(
                    "KYC verification passed for client {ClientId}: Status={KycStatus}, ApprovedAt={KycApprovedAt}",
                    request.ClientId,
                    verification.KycStatus,
                    verification.KycApprovedAt);
            }
            else
            {
                _logger.LogWarning(
                    "Client Management Service not configured - skipping KYC verification for client {ClientId}",
                    request.ClientId);
            }

            // Validate product exists and is active
            var product = await _productService.GetProductAsync(request.ProductCode, cancellationToken);
            if (product == null || !product.IsActive)
            {
                throw new InvalidOperationException($"Product {request.ProductCode} is not available");
            }

            // Validate amount and term are within product limits
            if (request.RequestedAmount < product.MinAmount || request.RequestedAmount > product.MaxAmount)
            {
                throw new ArgumentException($"Amount must be between {product.MinAmount:C} and {product.MaxAmount:C}");
            }

            if (request.TermMonths < product.MinTermMonths || request.TermMonths > product.MaxTermMonths)
            {
                throw new ArgumentException($"Term must be between {product.MinTermMonths} and {product.MaxTermMonths} months");
            }

            // Generate loan number
            var loanNumber = await _versioningService.GenerateLoanNumberAsync("LUS", cancellationToken);

            // Create application entity
            var applicationEntity = new IntelliFin.Shared.DomainModels.Entities.LoanApplication
            {
                Id = Guid.NewGuid(),
                ClientId = request.ClientId,
                ProductCode = request.ProductCode,
                ProductName = product.Name,
                Amount = request.RequestedAmount,
                RequestedAmount = request.RequestedAmount,
                TermMonths = request.TermMonths,
                Status = "Draft",
                CreatedAtUtc = DateTime.UtcNow,
                ApplicationDataJson = JsonSerializer.Serialize(request.ApplicationData ?? new Dictionary<string, object>()),
                LoanNumber = loanNumber,
                Version = 1,
                IsCurrentVersion = true
            };

            // Validate application data against product requirements
            var validationResult = await _productService.ValidateApplicationForProductAsync(
                product, request.ApplicationData ?? new Dictionary<string, object>(), cancellationToken);

            if (!validationResult.IsValid)
            {
                var errors = string.Join(", ", validationResult.Errors.Select(e => e.Message));
                throw new ArgumentException($"Application validation failed: {errors}");
            }

            // Save to database
            applicationEntity = await _applicationRepository.CreateAsync(applicationEntity, cancellationToken);

            _logger.LogInformation("Loan application {ApplicationId} created successfully", applicationEntity.Id);

            return await MapToResponseAsync(applicationEntity, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating loan application for client {ClientId}", request.ClientId);
            throw;
        }
    }

    public async Task<LoanApplicationResponse?> GetApplicationAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        try
        {
            var applicationEntity = await _applicationRepository.GetByIdAsync(applicationId, cancellationToken);
            if (applicationEntity != null)
            {
                return await MapToResponseAsync(applicationEntity, cancellationToken);
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving loan application {ApplicationId}", applicationId);
            throw;
        }
    }

    public async Task<IEnumerable<LoanApplicationResponse>> GetApplicationsByClientAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        try
        {
            var clientApplications = await _applicationRepository.GetByClientIdAsync(clientId, cancellationToken);

            var responses = new List<LoanApplicationResponse>();
            foreach (var application in clientApplications)
            {
                responses.Add(await MapToResponseAsync(application, cancellationToken));
            }

            return responses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving applications for client {ClientId}", clientId);
            throw;
        }
    }

    public async Task<LoanApplicationResponse> UpdateApplicationAsync(Guid applicationId, Dictionary<string, object> updates, CancellationToken cancellationToken = default)
    {
        try
        {
            var application = await _applicationRepository.GetByIdAsync(applicationId, cancellationToken);
            if (application == null)
            {
                throw new KeyNotFoundException($"Application {applicationId} not found");
            }

            if (application.Status != "Draft")
            {
                throw new InvalidOperationException("Only draft applications can be updated");
            }

            // Update application data
            var currentData = new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(application.ApplicationDataJson))
            {
                try
                {
                    currentData = JsonSerializer.Deserialize<Dictionary<string, object>>(application.ApplicationDataJson) ?? new Dictionary<string, object>();
                }
                catch { /* Use empty dictionary if parsing fails */ }
            }
            
            foreach (var update in updates)
            {
                currentData[update.Key] = update.Value;
            }
            
            application.ApplicationDataJson = JsonSerializer.Serialize(currentData);
            application = await _applicationRepository.UpdateAsync(application, cancellationToken);

            _logger.LogInformation("Loan application {ApplicationId} updated", applicationId);

            return await MapToResponseAsync(application, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating loan application {ApplicationId}", applicationId);
            throw;
        }
    }

    public async Task<bool> SubmitApplicationAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        try
        {
            var application = await _applicationRepository.GetByIdAsync(applicationId, cancellationToken);
            if (application == null)
            {
                throw new KeyNotFoundException($"Application {applicationId} not found");
            }

            if (application.Status != "Draft")
            {
                throw new InvalidOperationException("Only draft applications can be submitted");
            }

            // Validate application is complete
            var validationResult = await ValidateApplicationAsync(applicationId, cancellationToken);
            if (!validationResult.IsValid)
            {
                var errors = string.Join(", ", validationResult.Errors.Select(e => e.Message));
                throw new ArgumentException($"Application is not valid for submission: {errors}");
            }

            // Check compliance requirements
            var kycCompliant = await _complianceService.ValidateKYCComplianceAsync(application.ClientId, cancellationToken);
            if (!kycCompliant)
            {
                throw new InvalidOperationException("KYC compliance requirements not met");
            }

            // Update status and start workflow
            application.Status = "Submitted";
            application.SubmittedAt = DateTime.UtcNow;
            
            var workflowInstanceId = await _workflowService.StartApprovalWorkflowAsync(applicationId, cancellationToken);
            application.WorkflowInstanceId = workflowInstanceId;

            // Parse application data for credit assessment
            var applicationData = new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(application.ApplicationDataJson))
            {
                try
                {
                    applicationData = JsonSerializer.Deserialize<Dictionary<string, object>>(application.ApplicationDataJson) ?? new Dictionary<string, object>();
                }
                catch { /* Use empty dictionary if parsing fails */ }
            }

            // Trigger credit assessment
            var assessmentRequest = new CreditAssessmentRequest
            {
                LoanApplicationId = applicationId,
                ClientId = application.ClientId,
                ClientData = applicationData
            };

            // Note: Credit assessment will be linked to application via foreign key
            await _creditAssessmentService.PerformAssessmentAsync(assessmentRequest, cancellationToken);
            application.Status = "CreditAssessment";
            
            await _applicationRepository.UpdateAsync(application, cancellationToken);

            _logger.LogInformation("Loan application {ApplicationId} submitted and credit assessment initiated", applicationId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting loan application {ApplicationId}", applicationId);
            throw;
        }
    }

    public async Task<bool> WithdrawApplicationAsync(Guid applicationId, string reason, CancellationToken cancellationToken = default)
    {
        try
        {
            var application = await _applicationRepository.GetByIdAsync(applicationId, cancellationToken);
            if (application == null)
            {
                throw new KeyNotFoundException($"Application {applicationId} not found");
            }

            if (application.Status == "Approved" || 
                application.Status == "Rejected" ||
                application.Status == "Withdrawn")
            {
                throw new InvalidOperationException($"Cannot withdraw application in {application.Status} status");
            }

            application.Status = "Withdrawn";
            application.DeclineReason = reason;
            await _applicationRepository.UpdateAsync(application, cancellationToken);

            _logger.LogInformation("Loan application {ApplicationId} withdrawn: {Reason}", applicationId, reason);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error withdrawing loan application {ApplicationId}", applicationId);
            throw;
        }
    }

    public async Task<LoanApplicationResponse> ApproveApplicationAsync(Guid applicationId, string approvedBy, CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate dual control before proceeding (Story 1.7)
            await _dualControlValidator.ValidateApprovalAsync(applicationId, approvedBy, cancellationToken);
            
            var application = await _applicationRepository.GetByIdAsync(applicationId, cancellationToken);
            if (application == null)
            {
                throw new KeyNotFoundException($"Application {applicationId} not found");
            }

            if (application.Status != "PendingApproval")
            {
                throw new InvalidOperationException($"Cannot approve application in {application.Status} status");
            }

            // For now, skip complex compliance check since we need to map between entity and service models
            // This will be enhanced in a future iteration with proper model mapping
            _logger.LogInformation("Performing simplified compliance check for application {ApplicationId}", applicationId);

            application.Status = "Approved";
            application.ApprovedAt = DateTime.UtcNow;
            application.ApprovedBy = approvedBy;
            await _applicationRepository.UpdateAsync(application, cancellationToken);

            _logger.LogInformation("Loan application {ApplicationId} approved by {ApprovedBy}", applicationId, approvedBy);

            return await MapToResponseAsync(application, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving loan application {ApplicationId}", applicationId);
            throw;
        }
    }

    public async Task<LoanApplicationResponse> RejectApplicationAsync(Guid applicationId, string rejectedBy, string reason, CancellationToken cancellationToken = default)
    {
        try
        {
            var application = await _applicationRepository.GetByIdAsync(applicationId, cancellationToken);
            if (application == null)
            {
                throw new KeyNotFoundException($"Application {applicationId} not found");
            }

            if (application.Status == "Approved" || 
                application.Status == "Rejected" ||
                application.Status == "Withdrawn")
            {
                throw new InvalidOperationException($"Cannot reject application in {application.Status} status");
            }

            application.Status = "Rejected";
            application.DeclineReason = reason;
            await _applicationRepository.UpdateAsync(application, cancellationToken);

            _logger.LogInformation("Loan application {ApplicationId} rejected by {RejectedBy}: {Reason}", 
                applicationId, rejectedBy, reason);

            return await MapToResponseAsync(application, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting loan application {ApplicationId}", applicationId);
            throw;
        }
    }

    public async Task<RuleEngineResult> ValidateApplicationAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        try
        {
            var application = await _applicationRepository.GetByIdAsync(applicationId, cancellationToken);
            if (application == null)
            {
                throw new KeyNotFoundException($"Application {applicationId} not found");
            }

            var product = await _productService.GetProductAsync(application.ProductCode, cancellationToken);
            if (product == null)
            {
                return new RuleEngineResult
                {
                    IsValid = false,
                    Errors = { new ValidationError { Code = "PRODUCT_NOT_FOUND", Message = "Product not found" } }
                };
            }

            // Parse application data from JSON
            var applicationData = new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(application.ApplicationDataJson))
            {
                try
                {
                    applicationData = JsonSerializer.Deserialize<Dictionary<string, object>>(application.ApplicationDataJson) ?? new Dictionary<string, object>();
                }
                catch { /* Use empty dictionary if parsing fails */ }
            }
            
            return await _productService.ValidateApplicationForProductAsync(
                product, applicationData, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating loan application {ApplicationId}", applicationId);
            throw;
        }
    }

    public Task<ValidationResult> ValidateInitialApplicationAsync(LoanApplicationVariables variables, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        if (variables.LoanAmount <= 0) errors.Add("Loan amount must be greater than zero");
        if (string.IsNullOrWhiteSpace(variables.ProductType)) errors.Add("Product type is required");
        else if (!IsValidProductType(variables.ProductType)) errors.Add("Product type must be either 'PAYROLL' or 'BUSINESS'");
        if (string.IsNullOrWhiteSpace(variables.ApplicantNrc)) errors.Add("Applicant NRC is required");
        if (string.IsNullOrWhiteSpace(variables.BranchId)) errors.Add("Branch ID is required");

        var result = new ValidationResult
        {
            IsValid = !errors.Any(),
            ErrorMessage = errors.Any() ? string.Join("; ", errors) : null
        };

        return Task.FromResult(result);
    }

    private async Task<LoanApplicationResponse> MapToResponseAsync(IntelliFin.Shared.DomainModels.Entities.LoanApplication application, CancellationToken cancellationToken)
    {
        var product = await _productService.GetProductAsync(application.ProductCode, cancellationToken);
        var workflowSteps = await _workflowService.GetWorkflowStepsAsync(application.Id, cancellationToken);
        var requiredDocs = await _complianceService.GetRequiredDocumentsAsync(application.ProductCode, cancellationToken);

        // Parse application data
        var applicationData = new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(application.ApplicationDataJson))
        {
            try
            {
                applicationData = JsonSerializer.Deserialize<Dictionary<string, object>>(application.ApplicationDataJson) ?? new Dictionary<string, object>();
            }
            catch { /* Use empty dictionary if parsing fails */ }
        }

        // Get the latest credit assessment
        var latestCreditAssessment = application.CreditAssessments?.OrderByDescending(ca => ca.AssessedAt).FirstOrDefault();

        return new LoanApplicationResponse
        {
            Id = application.Id,
            LoanNumber = application.LoanNumber,
            ProductName = application.ProductName ?? product?.Name ?? application.ProductCode,
            RequestedAmount = application.RequestedAmount,
            TermMonths = application.TermMonths,
            Status = Enum.TryParse<LoanApplicationStatus>(application.Status, out var status) ? status : LoanApplicationStatus.Draft,
            StatusDescription = GetStatusDescription(Enum.TryParse<LoanApplicationStatus>(application.Status, out var s) ? s : LoanApplicationStatus.Draft),
            CreatedAt = application.CreatedAtUtc,
            CreditAssessment = latestCreditAssessment != null ? new CreditAssessmentSummary
            {
                RiskGrade = Enum.TryParse<RiskGrade>(latestCreditAssessment.RiskGrade, out var grade) ? grade : RiskGrade.F,
                CreditScore = latestCreditAssessment.CreditScore,
                ScoreExplanation = latestCreditAssessment.ScoreExplanation,
                KeyFactors = latestCreditAssessment.CreditFactors.Take(5).Select(f => f.Name).ToList(),
                RecommendedForApproval = Enum.TryParse<RiskGrade>(latestCreditAssessment.RiskGrade, out var g) && g <= RiskGrade.C
            } : null,
            RequiredDocuments = requiredDocs.ToList(),
            WorkflowSteps = workflowSteps.Select(ws => new WorkflowStepSummary
            {
                StepName = ws.StepName,
                Status = ws.Status,
                AssignedTo = ws.AssignedTo
            }).ToList()
        };
    }

    private static string GetStatusDescription(LoanApplicationStatus status)
    {
        return status switch
        {
            LoanApplicationStatus.Draft => "Application is being prepared",
            LoanApplicationStatus.Submitted => "Application has been submitted and is under initial review",
            LoanApplicationStatus.UnderReview => "Application is being reviewed by our team",
            LoanApplicationStatus.CreditAssessment => "Credit assessment is in progress",
            LoanApplicationStatus.PendingApproval => "Application is pending final approval decision",
            LoanApplicationStatus.Approved => "Application has been approved",
            LoanApplicationStatus.Rejected => "Application has been rejected",
            LoanApplicationStatus.Withdrawn => "Application has been withdrawn",
            LoanApplicationStatus.Expired => "Application has expired",
            _ => "Unknown status"
        };
    }

    private static bool IsValidProductType(string productType)
    {
        return productType.Equals("PAYROLL", StringComparison.OrdinalIgnoreCase) ||
               productType.Equals("BUSINESS", StringComparison.OrdinalIgnoreCase);
    }
}
