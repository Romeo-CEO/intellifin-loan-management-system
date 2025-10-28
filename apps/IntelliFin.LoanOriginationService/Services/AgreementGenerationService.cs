using System.Security.Cryptography;
using IntelliFin.LoanOriginationService.Events;
using IntelliFin.LoanOriginationService.Exceptions;
using IntelliFin.LoanOriginationService.Models;
using IntelliFin.Shared.DomainModels.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Minio;
using Minio.DataModel.Args;

namespace IntelliFin.LoanOriginationService.Services;

/// <summary>
/// Service for generating loan agreements using JasperReports and storing them in MinIO.
/// Handles PDF generation, SHA256 hashing for integrity, MinIO storage, and audit event publishing.
/// </summary>
public class AgreementGenerationService : IAgreementGenerationService
{
    private readonly HttpClient _jasperClient;
    private readonly IMinioClient _minioClient;
    private readonly LmsDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IVaultProductConfigService? _vaultProductConfigService;
    private readonly ILogger<AgreementGenerationService> _logger;
    private const string MinioBucket = "intellifin-documents";
    
    /// <summary>
    /// Initializes a new instance of the AgreementGenerationService.
    /// </summary>
    public AgreementGenerationService(
        HttpClient jasperClient,
        IMinioClient minioClient,
        LmsDbContext dbContext,
        IPublishEndpoint publishEndpoint,
        ILogger<AgreementGenerationService> logger,
        IVaultProductConfigService? vaultProductConfigService = null)
    {
        _jasperClient = jasperClient;
        _minioClient = minioClient;
        _dbContext = dbContext;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
        _vaultProductConfigService = vaultProductConfigService;
    }
    
    /// <inheritdoc />
    public async Task<AgreementDocument> GenerateAgreementAsync(
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Generating agreement for loan application {ApplicationId}",
            applicationId);
        
        // Fetch loan application with all required data
        var application = await _dbContext.LoanApplications
            .Include(la => la.CreditAssessments)
            .Include(la => la.Client)
            .FirstOrDefaultAsync(la => la.Id == applicationId, cancellationToken);
        
        if (application == null)
        {
            _logger.LogWarning("Loan application {ApplicationId} not found", applicationId);
            throw new KeyNotFoundException($"Loan application {applicationId} not found");
        }
        
        // Get latest credit assessment for interest rate and risk grade
        var latestAssessment = application.CreditAssessments?
            .OrderByDescending(ca => ca.AssessedAt)
            .FirstOrDefault();
        
        // Fetch product configuration from Vault for template version and EAR
        LoanProductConfig? productConfig = null;
        if (_vaultProductConfigService != null)
        {
            try
            {
                productConfig = await _vaultProductConfigService
                    .GetProductConfigAsync(application.ProductCode, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, 
                    "Failed to fetch product config from Vault for {ProductCode}, using defaults",
                    application.ProductCode);
            }
        }
        
        // Calculate interest rate (use product config or default from application)
        var interestRate = productConfig?.BaseInterestRate ?? 0.12m; // 12% default
        var adminFee = productConfig?.AdminFee ?? 0.02m;
        var managementFee = productConfig?.ManagementFee ?? 0.01m;
        var calculatedEAR = productConfig?.CalculatedEAR ?? 0.152m;
        var templateVersion = productConfig?.AgreementTemplateVersion ?? "GEPL-v1.0";
        
        // Calculate monthly payment
        var monthlyPayment = CalculateMonthlyPayment(
            application.RequestedAmount,
            interestRate,
            application.TermMonths);
        
        // Generate repayment schedule
        var repaymentSchedule = GenerateRepaymentSchedule(
            application.RequestedAmount,
            interestRate,
            application.TermMonths);
        
        // Prepare JSON payload for JasperReports template
        var jasperPayload = new
        {
            loanNumber = application.LoanNumber ?? $"DRAFT-{application.Id}",
            clientName = $"{application.Client?.FirstName ?? "Unknown"} {application.Client?.LastName ?? "Client"}",
            clientNrc = application.Client?.NationalId ?? "N/A",
            clientAddress = "Lusaka, Zambia", // TODO: Add address field to Client entity
            principal = application.RequestedAmount,
            termMonths = application.TermMonths,
            interestRate = interestRate,
            adminFee = adminFee,
            managementFee = managementFee,
            calculatedEAR = calculatedEAR,
            monthlyPayment = monthlyPayment,
            repaymentSchedule = repaymentSchedule.Select(r => new
            {
                month = r.Month,
                dueDate = r.DueDate.ToString("yyyy-MM-dd"),
                principalPayment = r.PrincipalPayment,
                interestPayment = r.InterestPayment,
                totalPayment = r.TotalPayment,
                balance = r.Balance
            }).ToList(),
            disbursementDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            agreementDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            templateVersion = templateVersion
        };
        
        // Determine template path based on product code
        var templatePath = GetTemplatePath(application.ProductCode);
        
        // Call JasperReports Server API
        var jasperUrl = $"/rest_v2/reports/intellifin/loan-agreements/{templatePath}.pdf";
        
        _logger.LogInformation(
            "Calling JasperReports API: {JasperUrl} for loan {LoanNumber}",
            jasperUrl, application.LoanNumber);
        
        byte[] pdfBytes;
        try
        {
            var response = await _jasperClient.PostAsJsonAsync(
                jasperUrl,
                jasperPayload,
                cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "JasperReports generation failed for loan {LoanNumber}. Status: {StatusCode}, Error: {Error}",
                    application.LoanNumber, response.StatusCode, errorContent);
                
                throw new AgreementGenerationException(
                    applicationId,
                    templatePath,
                    $"JasperReports generation failed: {response.StatusCode}",
                    new Exception(errorContent));
            }
            
            // Read PDF bytes from response
            pdfBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            
            _logger.LogInformation(
                "JasperReports generated PDF for loan {LoanNumber}, size: {Size} bytes",
                application.LoanNumber, pdfBytes.Length);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "JasperReports request timed out for loan {LoanNumber}", application.LoanNumber);
            throw new AgreementGenerationException(
                applicationId,
                templatePath,
                "JasperReports request timed out",
                ex);
        }
        catch (Exception ex) when (ex is not AgreementGenerationException)
        {
            _logger.LogError(ex, "Unexpected error calling JasperReports for loan {LoanNumber}", application.LoanNumber);
            throw new AgreementGenerationException(
                applicationId,
                templatePath,
                $"Failed to call JasperReports API: {ex.Message}",
                ex);
        }
        
        // Calculate SHA256 hash for integrity verification
        var documentHash = ComputeSha256Hash(pdfBytes);
        
        // Store PDF in MinIO
        var minioPath = $"loan-agreements/{application.ClientId}/{application.LoanNumber}_v{application.Version}.pdf";
        
        try
        {
            await _minioClient.PutObjectAsync(new PutObjectArgs()
                .WithBucket(MinioBucket)
                .WithObject(minioPath)
                .WithStreamData(new MemoryStream(pdfBytes))
                .WithContentType("application/pdf")
                .WithObjectSize(pdfBytes.Length),
                cancellationToken);
            
            _logger.LogInformation(
                "Agreement PDF stored in MinIO at {MinioPath} for loan {LoanNumber}",
                minioPath, application.LoanNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store PDF in MinIO for loan {LoanNumber}", application.LoanNumber);
            throw new AgreementGenerationException(
                applicationId,
                templatePath,
                $"Failed to store PDF in MinIO: {ex.Message}",
                ex);
        }
        
        // Update loan application record with agreement details
        application.AgreementFileHash = documentHash;
        application.AgreementMinioPath = minioPath;
        application.AgreementGeneratedAt = DateTime.UtcNow;
        
        await _dbContext.SaveChangesAsync(cancellationToken);
        
        // Publish audit event to AdminService
        var correlationId = Guid.NewGuid();
        await _publishEndpoint.Publish(new LoanAgreementGenerated
        {
            LoanApplicationId = applicationId,
            LoanNumber = application.LoanNumber ?? $"DRAFT-{application.Id}",
            ClientId = application.ClientId,
            DocumentHash = documentHash,
            MinioPath = minioPath,
            TemplateVersion = templateVersion,
            GeneratedAt = DateTime.UtcNow,
            GeneratedBy = "SYSTEM",
            CorrelationId = correlationId
        }, cancellationToken);
        
        _logger.LogInformation(
            "Agreement generation completed successfully for loan {LoanNumber}, CorrelationId: {CorrelationId}",
            application.LoanNumber, correlationId);
        
        return new AgreementDocument
        {
            LoanNumber = application.LoanNumber ?? $"DRAFT-{application.Id}",
            FileHash = documentHash,
            MinioPath = minioPath,
            GeneratedAt = DateTime.UtcNow
        };
    }
    
    /// <summary>
    /// Computes SHA256 hash of the PDF bytes for document integrity verification.
    /// </summary>
    private string ComputeSha256Hash(byte[] data)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(data);
        return Convert.ToHexString(hashBytes);
    }
    
    /// <summary>
    /// Gets the JasperReports template path based on the product code.
    /// </summary>
    private string GetTemplatePath(string productCode)
    {
        return productCode switch
        {
            "GEPL-001" => "gepl-agreement",
            "SMEABL-001" => "smeabl-agreement",
            "SALARY" => "salary-agreement",
            "PAYROLL" => "payroll-agreement",
            "SME" => "sme-agreement",
            _ => "gepl-agreement" // Default to GEPL template
        };
    }
    
    /// <summary>
    /// Calculates the monthly payment amount using the standard amortization formula.
    /// </summary>
    private decimal CalculateMonthlyPayment(decimal principal, decimal annualRate, int termMonths)
    {
        if (termMonths <= 0)
            throw new ArgumentException("Term months must be greater than zero", nameof(termMonths));
        
        var monthlyRate = annualRate / 12m;
        
        // Handle zero interest rate case
        if (monthlyRate == 0)
            return principal / termMonths;
        
        // Standard amortization formula: P * (r * (1 + r)^n) / ((1 + r)^n - 1)
        var rateFactorPower = (decimal)Math.Pow((double)(1 + monthlyRate), termMonths);
        var payment = principal * (monthlyRate * rateFactorPower) / (rateFactorPower - 1);
        
        return Math.Round(payment, 2);
    }
    
    /// <summary>
    /// Generates a complete repayment schedule with principal, interest, and balance for each period.
    /// </summary>
    private List<RepaymentScheduleEntry> GenerateRepaymentSchedule(
        decimal principal,
        decimal annualRate,
        int termMonths)
    {
        var schedule = new List<RepaymentScheduleEntry>();
        var balance = principal;
        var monthlyPayment = CalculateMonthlyPayment(principal, annualRate, termMonths);
        var monthlyRate = annualRate / 12m;
        var dueDate = DateTime.UtcNow.AddMonths(1);
        
        for (int month = 1; month <= termMonths; month++)
        {
            var interestPayment = balance * monthlyRate;
            var principalPayment = monthlyPayment - interestPayment;
            balance -= principalPayment;
            
            // Ensure final balance is exactly zero (handle rounding)
            if (month == termMonths)
                balance = 0;
            
            schedule.Add(new RepaymentScheduleEntry
            {
                Month = month,
                DueDate = dueDate,
                PrincipalPayment = Math.Round(principalPayment, 2),
                InterestPayment = Math.Round(interestPayment, 2),
                TotalPayment = Math.Round(monthlyPayment, 2),
                Balance = Math.Round(Math.Max(balance, 0), 2)
            });
            
            dueDate = dueDate.AddMonths(1);
        }
        
        return schedule;
    }
}
