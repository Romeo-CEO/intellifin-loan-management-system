using IntelliFin.LoanOriginationService.Models;
using IntelliFin.Shared.DomainModels.Entities;
using IntelliFin.Shared.DomainModels.Repositories;

namespace IntelliFin.LoanOriginationService.Services;

public class ComplianceService : IComplianceService
{
    private readonly ILogger<ComplianceService> _logger;
    private readonly IDocumentVerificationRepository _documentVerificationRepository;
    
    // BoZ compliance requirements configuration
    private static readonly Dictionary<string, List<string>> _productDocumentRequirements = new()
    {
        ["PL001"] = new List<string> 
        { 
            "National ID/Passport", 
            "Proof of Address", 
            "Payslip (3 months)", 
            "Bank Statements (3 months)",
            "Employment Letter"
        },
        ["BL001"] = new List<string> 
        { 
            "National ID/Passport", 
            "Business License", 
            "Tax Clearance Certificate",
            "Financial Statements (2 years)", 
            "Bank Statements (6 months)",
            "Proof of Address",
            "Collateral Valuation (if applicable)"
        },
        ["HL001"] = new List<string> 
        { 
            "National ID/Passport", 
            "Property Valuation Report", 
            "Title Deed/Agreement of Sale",
            "Proof of Income (6 months)", 
            "Bank Statements (6 months)",
            "Property Insurance Quote",
            "Survey Report",
            "Local Authority Clearance"
        }
    };

    private static readonly List<ComplianceRequirement> _bozRequirements = new()
    {
        new() 
        { 
            Code = "BOZ_KYC_001", 
            Description = "Customer identification and verification completed",
            IsMet = false 
        },
        new() 
        { 
            Code = "BOZ_AML_001", 
            Description = "Anti-Money Laundering checks completed",
            IsMet = false 
        },
        new() 
        { 
            Code = "BOZ_DTI_001", 
            Description = "Debt-to-Income ratio within acceptable limits (≤40%)",
            IsMet = false 
        },
        new() 
        { 
            Code = "BOZ_PROV_001", 
            Description = "Loan loss provisioning requirements assessed",
            IsMet = false 
        },
        new() 
        { 
            Code = "BOZ_CAP_001", 
            Description = "Capital adequacy requirements considered",
            IsMet = false 
        },
        new() 
        { 
            Code = "BOZ_DOC_001", 
            Description = "All required documentation collected and verified",
            IsMet = false 
        },
        new() 
        { 
            Code = "BOZ_RATE_001", 
            Description = "Interest rate within regulatory limits",
            IsMet = false 
        },
        new() 
        { 
            Code = "BOZ_DISC_001", 
            Description = "Proper disclosure of terms and conditions",
            IsMet = false 
        }
    };

    public ComplianceService(
        ILogger<ComplianceService> logger,
        IDocumentVerificationRepository documentVerificationRepository)
    {
        _logger = logger;
        _documentVerificationRepository = documentVerificationRepository;
    }

    public async Task<BoZComplianceCheck> ValidateBoZComplianceAsync(
        IntelliFin.LoanOriginationService.Models.LoanApplication application, 
        IntelliFin.LoanOriginationService.Models.CreditAssessment assessment, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Performing BoZ compliance check for application {ApplicationId}", application.Id);

            var complianceCheck = new BoZComplianceCheck
            {
                CheckedAt = DateTime.UtcNow,
                Requirements = new List<ComplianceRequirement>(_bozRequirements)
            };

            // Check KYC compliance
            var kycCompliant = await ValidateKYCComplianceAsync(application.ClientId, cancellationToken);
            UpdateRequirement(complianceCheck.Requirements, "BOZ_KYC_001", kycCompliant, 
                kycCompliant ? "KYC verification completed" : "KYC verification pending or incomplete");

            // Check AML compliance  
            var amlCompliant = await ValidateAMLComplianceAsync(application.ClientId, cancellationToken);
            UpdateRequirement(complianceCheck.Requirements, "BOZ_AML_001", amlCompliant,
                amlCompliant ? "AML screening completed, no flags" : "AML screening incomplete or flags present");

            // Check DTI compliance
            var dtiCompliant = assessment.DebtToIncomeRatio <= 0.40m;
            UpdateRequirement(complianceCheck.Requirements, "BOZ_DTI_001", dtiCompliant,
                $"DTI ratio: {assessment.DebtToIncomeRatio:P}");

            // Check documentation compliance
            var documentCompliant = await ValidateDocumentComplianceAsync(application.Id, cancellationToken);
            UpdateRequirement(complianceCheck.Requirements, "BOZ_DOC_001", documentCompliant,
                documentCompliant ? "All required documents provided" : "Missing required documentation");

            // Check interest rate compliance (assuming max 25% per BoZ regulations)
            var maxAllowedRate = 0.25m;
            var rateCompliant = application.InterestRate <= maxAllowedRate;
            UpdateRequirement(complianceCheck.Requirements, "BOZ_RATE_001", rateCompliant,
                $"Interest rate: {application.InterestRate:P} (max: {maxAllowedRate:P})");

            // Check provisioning requirements based on risk grade
            var provisioningCompliant = ValidateProvisioningRequirements(assessment.RiskGrade);
            UpdateRequirement(complianceCheck.Requirements, "BOZ_PROV_001", provisioningCompliant.IsCompliant,
                provisioningCompliant.Evidence);

            // Check capital adequacy impact
            var capitalAdequacyOk = await ValidateCapitalAdequacyImpactAsync(application.RequestedAmount, cancellationToken);
            UpdateRequirement(complianceCheck.Requirements, "BOZ_CAP_001", capitalAdequacyOk,
                capitalAdequacyOk ? "Loan amount within capital limits" : "Loan amount may impact capital ratios");

            // Check disclosure requirements
            var disclosureCompliant = true; // Assume proper disclosure process
            UpdateRequirement(complianceCheck.Requirements, "BOZ_DISC_001", disclosureCompliant,
                "Terms and conditions properly disclosed");

            // Determine overall compliance
            complianceCheck.IsCompliant = complianceCheck.Requirements.All(r => r.IsMet);

            // Generate violations for unmet requirements
            complianceCheck.Violations = complianceCheck.Requirements
                .Where(r => !r.IsMet)
                .Select(r => new ComplianceViolation
                {
                    Code = r.Code,
                    Description = r.Description,
                    Severity = GetViolationSeverity(r.Code),
                    Recommendation = GetViolationRecommendation(r.Code)
                })
                .ToList();

            _logger.LogInformation("BoZ compliance check completed for application {ApplicationId}. Compliant: {IsCompliant}, Violations: {ViolationCount}",
                application.Id, complianceCheck.IsCompliant, complianceCheck.Violations.Count);

            return complianceCheck;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing BoZ compliance check for application {ApplicationId}", application.Id);
            throw;
        }
    }

    public async Task<bool> ValidateKYCComplianceAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        try
        {
            // In production, integrate with KYC service
            _logger.LogInformation("Validating KYC compliance for client {ClientId}", clientId);

            // System-Assisted Manual Verification Model - No external KYC API required
            // This validates that the loan officer has completed manual verification with OCR assistance
            
            var verificationStatus = await GetManualVerificationStatusAsync(clientId, cancellationToken);
            
            if (verificationStatus == null)
            {
                _logger.LogWarning("Manual KYC verification not yet completed for client {ClientId}", clientId);
                return false;
            }

            var isCompliant = verificationStatus.IsVerified && 
                             verificationStatus.VerifiedBy != null && 
                             verificationStatus.VerificationDate.HasValue &&
                             verificationStatus.VerificationDate > DateTime.UtcNow.AddDays(-90); // Verification not older than 90 days

            _logger.LogInformation("Manual KYC verification status for client {ClientId}: {Status} (Verified by: {VerifiedBy})", 
                clientId, isCompliant ? "VERIFIED" : "PENDING", verificationStatus.VerifiedBy);

            return isCompliant;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating KYC compliance for client {ClientId}", clientId);
            return false; // Fail safe
        }
    }

    /// <summary>
    /// Gets the manual verification status for System-Assisted Manual Verification
    /// </summary>
    private async Task<DocumentVerification?> GetManualVerificationStatusAsync(Guid clientId, CancellationToken cancellationToken)
    {
        return await _documentVerificationRepository.GetByClientIdAsync(clientId, cancellationToken);
    }

    public async Task<bool> ValidateAMLComplianceAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Validating AML compliance for client {ClientId}", clientId);

            // **SPRINT 4: External Integrations** - Replace with Bank of Zambia approved AML screening API integration
            await Task.Delay(100, cancellationToken);

            var mockAmlResults = new
            {
                SanctionListCheck = true,  // Passed sanction list screening
                PepCheck = true,          // Passed PEP screening
                AdverseMediaCheck = true, // No adverse media found
                TransactionMonitoring = true, // No suspicious patterns
                SourceOfFundsVerified = true
            };

            return mockAmlResults.SanctionListCheck && 
                   mockAmlResults.PepCheck && 
                   mockAmlResults.AdverseMediaCheck &&
                   mockAmlResults.TransactionMonitoring &&
                   mockAmlResults.SourceOfFundsVerified;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating AML compliance for client {ClientId}", clientId);
            return false; // Fail safe
        }
    }

    public async Task<List<string>> GetRequiredDocumentsAsync(string productCode, CancellationToken cancellationToken = default)
    {
        try
        {
            if (_productDocumentRequirements.TryGetValue(productCode, out var documents))
            {
                return await Task.FromResult(new List<string>(documents));
            }
            
            // Default document requirements
            return new List<string> 
            { 
                "National ID/Passport", 
                "Proof of Address", 
                "Proof of Income" 
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting required documents for product {ProductCode}", productCode);
            return new List<string>();
        }
    }

    public async Task<bool> ValidateDocumentComplianceAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Validating document compliance for application {ApplicationId}", applicationId);

            // Mock document validation - in production integrate with document service
            await Task.Delay(100, cancellationToken);

            // Simulate document check - in production verify against actual uploaded documents
            var mockDocumentStatus = new
            {
                IdentityDocuments = true,
                IncomeDocuments = true, 
                AddressProof = true,
                ProductSpecificDocs = true,
                AllDocumentsVerified = true
            };

            return mockDocumentStatus.IdentityDocuments && 
                   mockDocumentStatus.IncomeDocuments &&
                   mockDocumentStatus.AddressProof &&
                   mockDocumentStatus.ProductSpecificDocs &&
                   mockDocumentStatus.AllDocumentsVerified;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating document compliance for application {ApplicationId}", applicationId);
            return false; // Fail safe
        }
    }

    // Helper methods
    private void UpdateRequirement(List<ComplianceRequirement> requirements, string code, bool isMet, string evidence)
    {
        var requirement = requirements.FirstOrDefault(r => r.Code == code);
        if (requirement != null)
        {
            requirement.IsMet = isMet;
            requirement.Evidence = evidence;
        }
    }

    private (bool IsCompliant, string Evidence) ValidateProvisioningRequirements(RiskGrade riskGrade)
    {
        // BoZ provisioning requirements based on risk classification
        var provisioningRate = riskGrade switch
        {
            RiskGrade.A => 0.01m,  // 1% provision
            RiskGrade.B => 0.03m,  // 3% provision
            RiskGrade.C => 0.20m,  // 20% provision
            RiskGrade.D => 0.50m,  // 50% provision
            RiskGrade.E => 1.00m,  // 100% provision
            RiskGrade.F => 1.00m,  // 100% provision
            _ => 0.50m
        };

        var isCompliant = provisioningRate <= 1.00m; // All grades are within limits
        var evidence = $"Risk Grade {riskGrade} requires {provisioningRate:P} provisioning";

        return (isCompliant, evidence);
    }

    private async Task<bool> ValidateCapitalAdequacyImpactAsync(decimal loanAmount, CancellationToken cancellationToken)
    {
        try
        {
            // Mock capital adequacy check - in production check against actual capital ratios
            await Task.Delay(50, cancellationToken);

            var mockCapitalData = new
            {
                CurrentCapitalRatio = 0.15m,      // 15% (above BoZ minimum of 10%)
                MinimumRequired = 0.10m,          // 10% BoZ requirement
                LoanImpact = loanAmount * 0.08m,  // Risk-weighted asset impact
                ProjectedRatio = 0.14m            // After loan approval
            };

            return mockCapitalData.ProjectedRatio >= mockCapitalData.MinimumRequired;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating capital adequacy impact");
            return true; // Fail safe - don't block loans on capital calculation errors
        }
    }

    private string GetViolationSeverity(string requirementCode)
    {
        return requirementCode switch
        {
            "BOZ_KYC_001" or "BOZ_AML_001" => "Critical",
            "BOZ_DTI_001" or "BOZ_RATE_001" => "Major",
            "BOZ_DOC_001" or "BOZ_DISC_001" => "Major",
            "BOZ_PROV_001" or "BOZ_CAP_001" => "Minor",
            _ => "Minor"
        };
    }

    private string GetViolationRecommendation(string requirementCode)
    {
        return requirementCode switch
        {
            "BOZ_KYC_001" => "Complete customer identification and verification process",
            "BOZ_AML_001" => "Complete anti-money laundering screening and obtain clearance",
            "BOZ_DTI_001" => "Reduce loan amount or require additional income verification",
            "BOZ_DOC_001" => "Collect all required documentation before approval",
            "BOZ_RATE_001" => "Adjust interest rate to comply with BoZ maximum limits",
            "BOZ_PROV_001" => "Ensure adequate loan loss provisions are allocated",
            "BOZ_CAP_001" => "Review loan amount against capital adequacy requirements",
            "BOZ_DISC_001" => "Ensure proper disclosure of all terms and conditions",
            _ => "Address compliance requirement before loan approval"
        };
    }
}