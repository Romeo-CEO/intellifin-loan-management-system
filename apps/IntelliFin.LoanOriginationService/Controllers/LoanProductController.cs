using IntelliFin.LoanOriginationService.Models;
using IntelliFin.LoanOriginationService.Services;
using Microsoft.AspNetCore.Mvc;

namespace IntelliFin.LoanOriginationService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LoanProductController : ControllerBase
{
    private readonly ILogger<LoanProductController> _logger;
    private readonly ILoanProductService _productService;

    public LoanProductController(
        ILogger<LoanProductController> logger,
        ILoanProductService productService)
    {
        _logger = logger;
        _productService = productService;
    }

    /// <summary>
    /// Get all active loan products
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<LoanProduct>>> GetProducts(CancellationToken cancellationToken = default)
    {
        try
        {
            var products = await _productService.GetActiveProductsAsync(cancellationToken);
            return Ok(products);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving loan products");
            return StatusCode(500, new { error = "An error occurred while retrieving products" });
        }
    }

    /// <summary>
    /// Get loan product by code
    /// </summary>
    [HttpGet("{productCode}")]
    public async Task<ActionResult<LoanProduct>> GetProduct(string productCode, CancellationToken cancellationToken = default)
    {
        try
        {
            var product = await _productService.GetProductAsync(productCode, cancellationToken);
            if (product == null)
            {
                return NotFound(new { error = "Product not found" });
            }

            return Ok(product);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving product {ProductCode}", productCode);
            return StatusCode(500, new { error = "An error occurred while retrieving the product" });
        }
    }

    /// <summary>
    /// Get dynamic application form for a product
    /// </summary>
    [HttpGet("{productCode}/form")]
    public async Task<ActionResult<ApplicationFormResponse>> GetApplicationForm(
        string productCode, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var product = await _productService.GetProductAsync(productCode, cancellationToken);
            if (product == null)
            {
                return NotFound(new { error = "Product not found" });
            }

            var form = new ApplicationFormResponse
            {
                ProductCode = product.Code,
                ProductName = product.Name,
                Description = product.Description,
                MinAmount = product.MinAmount,
                MaxAmount = product.MaxAmount,
                MinTermMonths = product.MinTermMonths,
                MaxTermMonths = product.MaxTermMonths,
                BaseInterestRate = product.BaseInterestRate,
                Fields = product.RequiredFields.OrderBy(f => f.Order).ToList(),
                ValidationRules = product.ValidationRules.Select(r => new FormValidationRule
                {
                    Name = r.Name,
                    ErrorMessage = r.ErrorMessage,
                    RuleType = r.RuleType
                }).ToList()
            };

            return Ok(form);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving application form for product {ProductCode}", productCode);
            return StatusCode(500, new { error = "An error occurred while retrieving the application form" });
        }
    }

    /// <summary>
    /// Validate application data against product rules
    /// </summary>
    [HttpPost("{productCode}/validate")]
    public async Task<ActionResult<RuleEngineResult>> ValidateApplicationData(
        string productCode,
        [FromBody] Dictionary<string, object> applicationData,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var product = await _productService.GetProductAsync(productCode, cancellationToken);
            if (product == null)
            {
                return NotFound(new { error = "Product not found" });
            }

            var validationResult = await _productService.ValidateApplicationForProductAsync(
                product, applicationData, cancellationToken);

            return Ok(validationResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating application data for product {ProductCode}", productCode);
            return StatusCode(500, new { error = "An error occurred while validating application data" });
        }
    }

    /// <summary>
    /// Calculate interest rate for client based on risk grade
    /// </summary>
    [HttpPost("{productCode}/calculate-rate")]
    public async Task<ActionResult<InterestRateResponse>> CalculateInterestRate(
        string productCode,
        [FromBody] InterestRateRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var rate = await _productService.CalculateInterestRateAsync(productCode, request.RiskGrade, cancellationToken);
            
            return Ok(new InterestRateResponse
            {
                ProductCode = productCode,
                RiskGrade = request.RiskGrade,
                InterestRate = rate,
                EffectiveApr = CalculateApr(rate), // Add fees and charges
                RateValidUntil = DateTime.UtcNow.AddDays(30)
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating interest rate for product {ProductCode}", productCode);
            return StatusCode(500, new { error = "An error occurred while calculating interest rate" });
        }
    }

    /// <summary>
    /// Check client eligibility for product
    /// </summary>
    [HttpGet("{productCode}/eligibility/{clientId:guid}")]
    public async Task<ActionResult<EligibilityResponse>> CheckEligibility(
        string productCode,
        Guid clientId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var isEligible = await _productService.IsEligibleForProductAsync(productCode, clientId, cancellationToken);
            
            return Ok(new EligibilityResponse
            {
                ClientId = clientId,
                ProductCode = productCode,
                IsEligible = isEligible,
                CheckedAt = DateTime.UtcNow,
                Reasons = isEligible ? new List<string> { "All eligibility criteria met" } 
                                    : new List<string> { "Eligibility criteria not met" }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking eligibility for product {ProductCode}, client {ClientId}", productCode, clientId);
            return StatusCode(500, new { error = "An error occurred while checking eligibility" });
        }
    }

    // Helper method
    private decimal CalculateApr(decimal baseRate)
    {
        // Add typical fees and charges to get APR
        var fees = 0.005m; // 0.5% fees
        return baseRate + fees;
    }
}

// Response DTOs
public class ApplicationFormResponse
{
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public int MinTermMonths { get; set; }
    public int MaxTermMonths { get; set; }
    public decimal BaseInterestRate { get; set; }
    public List<ApplicationField> Fields { get; set; } = new();
    public List<FormValidationRule> ValidationRules { get; set; } = new();
}

public class FormValidationRule
{
    public string Name { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty;
}

public class InterestRateRequest
{
    public RiskGrade RiskGrade { get; set; }
}

public class InterestRateResponse
{
    public string ProductCode { get; set; } = string.Empty;
    public RiskGrade RiskGrade { get; set; }
    public decimal InterestRate { get; set; }
    public decimal EffectiveApr { get; set; }
    public DateTime RateValidUntil { get; set; }
}

public class EligibilityResponse
{
    public Guid ClientId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public bool IsEligible { get; set; }
    public DateTime CheckedAt { get; set; }
    public List<string> Reasons { get; set; } = new();
}