using IntelliFin.LoanOriginationService.Models;
using System.Collections.Concurrent;

namespace IntelliFin.LoanOriginationService.Services;

public class LoanProductService : ILoanProductService
{
    private readonly ILogger<LoanProductService> _logger;
    private readonly IVaultProductConfigService? _vaultConfigService;
    private readonly bool _useVault;
    
    // Legacy in-memory product catalog - used when Vault is not available
    private static readonly ConcurrentDictionary<string, LoanProduct> _products = new()
    {
        ["PL001"] = new()
        {
            Code = "PL001",
            Name = "Personal Loan Standard",
            Description = "Standard personal loan for general purposes",
            MinAmount = 5000m,
            MaxAmount = 200000m,
            MinTermMonths = 6,
            MaxTermMonths = 60,
            BaseInterestRate = 0.15m,
            IsActive = true,
            RequiredFields = new List<ApplicationField>
            {
                new() { Name = "purpose", Label = "Loan Purpose", Type = "select", IsRequired = true, Order = 1,
                       Options = new List<string> { "Home Improvement", "Education", "Medical", "Business", "Other" } },
                new() { Name = "monthly_income", Label = "Monthly Income", Type = "number", IsRequired = true, Order = 2,
                       ValidationPattern = "^[0-9]+(\\.[0-9]{1,2})?$", HelpText = "Enter your net monthly income in ZMW" },
                new() { Name = "employment_type", Label = "Employment Type", Type = "select", IsRequired = true, Order = 3,
                       Options = new List<string> { "Permanent", "Contract", "Self-employed", "Unemployed" } },
                new() { Name = "employer_name", Label = "Employer Name", Type = "text", IsRequired = true, Order = 4 },
                new() { Name = "employment_duration", Label = "Employment Duration (months)", Type = "number", IsRequired = true, Order = 5 },
                new() { Name = "payslip", Label = "Latest Payslip", Type = "file", IsRequired = true, Order = 6 },
                new() { Name = "bank_statements", Label = "3 Months Bank Statements", Type = "file", IsRequired = true, Order = 7 }
            },
            ValidationRules = new List<BusinessRule>
            {
                new() { Name = "MinimumIncome", Condition = "monthly_income >= 2000", 
                       ErrorMessage = "Minimum monthly income of K2,000 required", RuleType = "validation", Priority = 1 },
                new() { Name = "MinimumEmployment", Condition = "employment_duration >= 6", 
                       ErrorMessage = "Minimum 6 months employment required", RuleType = "validation", Priority = 2 },
                new() { Name = "ValidEmployment", Condition = "employment_type != 'Unemployed'", 
                       ErrorMessage = "Employment required for personal loan", RuleType = "validation", Priority = 3 }
            }
        },
        ["BL001"] = new()
        {
            Code = "BL001",
            Name = "Business Loan SME",
            Description = "Small and Medium Enterprise business loan",
            MinAmount = 10000m,
            MaxAmount = 500000m,
            MinTermMonths = 12,
            MaxTermMonths = 84,
            BaseInterestRate = 0.18m,
            IsActive = true,
            RequiredFields = new List<ApplicationField>
            {
                new() { Name = "business_name", Label = "Business Name", Type = "text", IsRequired = true, Order = 1 },
                new() { Name = "business_type", Label = "Business Type", Type = "select", IsRequired = true, Order = 2,
                       Options = new List<string> { "Manufacturing", "Trading", "Services", "Agriculture", "Other" } },
                new() { Name = "years_in_business", Label = "Years in Business", Type = "number", IsRequired = true, Order = 3 },
                new() { Name = "annual_turnover", Label = "Annual Turnover", Type = "number", IsRequired = true, Order = 4 },
                new() { Name = "monthly_profit", Label = "Monthly Profit", Type = "number", IsRequired = true, Order = 5 },
                new() { Name = "business_license", Label = "Business License", Type = "file", IsRequired = true, Order = 6 },
                new() { Name = "financial_statements", Label = "Financial Statements", Type = "file", IsRequired = true, Order = 7 },
                new() { Name = "collateral_type", Label = "Collateral Type", Type = "select", IsRequired = true, Order = 8,
                       Options = new List<string> { "Property", "Vehicle", "Equipment", "Inventory", "None" } },
                new() { Name = "collateral_value", Label = "Collateral Value", Type = "number", IsRequired = false, Order = 9 }
            },
            ValidationRules = new List<BusinessRule>
            {
                new() { Name = "MinimumBusinessAge", Condition = "years_in_business >= 2", 
                       ErrorMessage = "Business must be operational for at least 2 years", RuleType = "validation", Priority = 1 },
                new() { Name = "MinimumTurnover", Condition = "annual_turnover >= 50000", 
                       ErrorMessage = "Minimum annual turnover of K50,000 required", RuleType = "validation", Priority = 2 },
                new() { Name = "ProfitabilityCheck", Condition = "monthly_profit > 0", 
                       ErrorMessage = "Business must be profitable", RuleType = "validation", Priority = 3 }
            }
        },
        ["HL001"] = new()
        {
            Code = "HL001", 
            Name = "Home Loan Premium",
            Description = "Premium home loan for property purchase",
            MinAmount = 50000m,
            MaxAmount = 2000000m,
            MinTermMonths = 60,
            MaxTermMonths = 300,
            BaseInterestRate = 0.12m,
            IsActive = true,
            RequiredFields = new List<ApplicationField>
            {
                new() { Name = "property_value", Label = "Property Value", Type = "number", IsRequired = true, Order = 1 },
                new() { Name = "property_type", Label = "Property Type", Type = "select", IsRequired = true, Order = 2,
                       Options = new List<string> { "Apartment", "House", "Townhouse", "Land", "Commercial" } },
                new() { Name = "property_location", Label = "Property Location", Type = "text", IsRequired = true, Order = 3 },
                new() { Name = "down_payment", Label = "Down Payment", Type = "number", IsRequired = true, Order = 4 },
                new() { Name = "monthly_income", Label = "Monthly Income", Type = "number", IsRequired = true, Order = 5 },
                new() { Name = "property_valuation", Label = "Property Valuation Report", Type = "file", IsRequired = true, Order = 6 },
                new() { Name = "title_deed", Label = "Title Deed/Agreement of Sale", Type = "file", IsRequired = true, Order = 7 },
                new() { Name = "insurance_quote", Label = "Property Insurance Quote", Type = "file", IsRequired = true, Order = 8 }
            },
            ValidationRules = new List<BusinessRule>
            {
                new() { Name = "MinimumDownPayment", Condition = "down_payment >= property_value * 0.1", 
                       ErrorMessage = "Minimum 10% down payment required", RuleType = "validation", Priority = 1 },
                new() { Name = "MaximumLTV", Condition = "requested_amount <= property_value * 0.9", 
                       ErrorMessage = "Maximum 90% Loan-to-Value ratio", RuleType = "validation", Priority = 2 },
                new() { Name = "IncomeMultiple", Condition = "requested_amount <= monthly_income * 60", 
                       ErrorMessage = "Loan amount cannot exceed 5 years of monthly income", RuleType = "validation", Priority = 3 }
            }
        }
    };

    public LoanProductService(
        ILogger<LoanProductService> logger,
        IVaultProductConfigService? vaultConfigService = null)
    {
        _logger = logger;
        _vaultConfigService = vaultConfigService;
        _useVault = vaultConfigService != null;
        
        if (_useVault)
        {
            _logger.LogInformation("LoanProductService initialized with Vault configuration");
        }
        else
        {
            _logger.LogWarning("LoanProductService initialized with legacy in-memory configuration");
        }
    }

    public async Task<LoanProduct?> GetProductAsync(string productCode, CancellationToken cancellationToken = default)
    {
        try
        {
            // Use Vault if available, otherwise fall back to in-memory
            if (_useVault && _vaultConfigService != null)
            {
                var config = await _vaultConfigService.GetProductConfigAsync(productCode, cancellationToken);
                return MapVaultConfigToProduct(config, productCode);
            }
            
            // Legacy fallback
            _products.TryGetValue(productCode, out var product);
            return product;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving product {ProductCode}", productCode);
            return null;
        }
    }
    
    /// <summary>
    /// Maps Vault configuration to LoanProduct model for backward compatibility
    /// </summary>
    private LoanProduct MapVaultConfigToProduct(LoanProductConfig config, string productCode)
    {
        return new LoanProduct
        {
            Code = productCode,
            Name = config.ProductName,
            Description = config.ProductName,
            MinAmount = config.MinAmount,
            MaxAmount = config.MaxAmount,
            MinTermMonths = config.MinTermMonths,
            MaxTermMonths = config.MaxTermMonths,
            BaseInterestRate = config.BaseInterestRate,
            IsActive = true,
            RequiredFields = new List<ApplicationField>(), // Fields would be in Vault if needed
            ValidationRules = new List<BusinessRule>() // Rules would be in Vault if needed
        };
    }

    public Task<IEnumerable<LoanProduct>> GetActiveProductsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IEnumerable<LoanProduct> products = _products.Values.Where(p => p.IsActive).OrderBy(p => p.Name).ToList();
            return Task.FromResult(products);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active products");
            return Task.FromResult(Enumerable.Empty<LoanProduct>());
        }
    }

    public async Task<RuleEngineResult> ValidateApplicationForProductAsync(
        LoanProduct product, 
        Dictionary<string, object> applicationData, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = new RuleEngineResult { RuleSetUsed = $"{product.Code}_ValidationRules" };

            // Validate required fields
            foreach (var field in product.RequiredFields.Where(f => f.IsRequired))
            {
                if (!applicationData.ContainsKey(field.Name) || 
                    string.IsNullOrWhiteSpace(applicationData[field.Name]?.ToString()))
                {
                    result.Errors.Add(new ValidationError
                    {
                        Code = "REQUIRED_FIELD_MISSING",
                        Message = $"{field.Label} is required",
                        Field = field.Name
                    });
                }
            }

            // Validate field formats using validation patterns
            foreach (var field in product.RequiredFields.Where(f => !string.IsNullOrEmpty(f.ValidationPattern)))
            {
                if (applicationData.ContainsKey(field.Name))
                {
                    var value = applicationData[field.Name]?.ToString() ?? string.Empty;
                    if (!string.IsNullOrEmpty(value))
                    {
                        var regex = new System.Text.RegularExpressions.Regex(field.ValidationPattern!);
                        if (!regex.IsMatch(value))
                        {
                            result.Errors.Add(new ValidationError
                            {
                                Code = "INVALID_FORMAT",
                                Message = $"{field.Label} format is invalid",
                                Field = field.Name
                            });
                        }
                    }
                }
            }

            // Apply business rules
            foreach (var rule in product.ValidationRules.OrderBy(r => r.Priority))
            {
                var ruleResult = await EvaluateBusinessRule(rule, applicationData, cancellationToken);
                if (!ruleResult.IsValid)
                {
                    result.Errors.Add(new ValidationError
                    {
                        Code = rule.Name.ToUpper(),
                        Message = rule.ErrorMessage,
                        Field = ExtractFieldFromCondition(rule.Condition)
                    });
                }
            }

            result.IsValid = result.Errors.Count == 0;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating application for product {ProductCode}", product.Code);
            throw;
        }
    }

    public async Task<decimal> CalculateInterestRateAsync(string productCode, RiskGrade riskGrade, CancellationToken cancellationToken = default)
    {
        try
        {
            var product = await GetProductAsync(productCode, cancellationToken);
            if (product == null)
                throw new ArgumentException($"Product {productCode} not found");

            var baseRate = product.BaseInterestRate;
            
            // Risk-based pricing adjustments
            var riskAdjustment = riskGrade switch
            {
                RiskGrade.A => -0.02m, // 2% discount for excellent credit
                RiskGrade.B => 0.00m,  // Base rate
                RiskGrade.C => 0.03m,  // 3% premium
                RiskGrade.D => 0.07m,  // 7% premium
                RiskGrade.E => 0.10m,  // 10% premium
                RiskGrade.F => 0.15m,  // 15% premium (if approved)
                _ => 0.00m
            };

            return baseRate + riskAdjustment;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating interest rate for product {ProductCode}", productCode);
            throw;
        }
    }

    public async Task<bool> IsEligibleForProductAsync(string productCode, Guid clientId, CancellationToken cancellationToken = default)
    {
        try
        {
            var product = await GetProductAsync(productCode, cancellationToken);
            if (product == null || !product.IsActive)
                return false;

            // In production, check client eligibility criteria:
            // - Age requirements
            // - Citizenship/residence
            // - Income requirements
            // - Credit history requirements
            // - Existing loan limits

            // For now, return true (basic eligibility check)
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking eligibility for product {ProductCode}, client {ClientId}", 
                productCode, clientId);
            return false;
        }
    }

    // Helper methods
    private async Task<(bool IsValid, string? ErrorMessage)> EvaluateBusinessRule(
        BusinessRule rule, 
        Dictionary<string, object> applicationData, 
        CancellationToken cancellationToken)
    {
        try
        {
            // Simplified rule evaluation - in production use a proper rules engine like Drools or Rules Engine
            var condition = rule.Condition.ToLower();
            
            // Handle different condition patterns
            if (condition.Contains(">="))
            {
                return EvaluateComparisonRule(condition, ">=", applicationData);
            }
            else if (condition.Contains("<="))
            {
                return EvaluateComparisonRule(condition, "<=", applicationData);
            }
            else if (condition.Contains(">"))
            {
                return EvaluateComparisonRule(condition, ">", applicationData);
            }
            else if (condition.Contains("<"))
            {
                return EvaluateComparisonRule(condition, "<", applicationData);
            }
            else if (condition.Contains("!="))
            {
                return EvaluateEqualityRule(condition, "!=", applicationData);
            }
            else if (condition.Contains("=="))
            {
                return EvaluateEqualityRule(condition, "==", applicationData);
            }

            // Default to valid if rule cannot be evaluated
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating business rule: {RuleName}", rule.Name);
            return (true, null); // Fail open for business rules
        }
    }

    private (bool IsValid, string? ErrorMessage) EvaluateComparisonRule(
        string condition, 
        string operatorStr, 
        Dictionary<string, object> applicationData)
    {
        var parts = condition.Split(operatorStr);
        if (parts.Length != 2) return (true, null);

        var fieldName = parts[0].Trim();
        var targetValueStr = parts[1].Trim();

        if (!applicationData.ContainsKey(fieldName))
            return (false, $"Field {fieldName} is required for this rule");

        if (!decimal.TryParse(applicationData[fieldName]?.ToString(), out var actualValue) ||
            !decimal.TryParse(targetValueStr, out var targetValue))
            return (true, null); // Cannot parse values, skip rule

        return operatorStr switch
        {
            ">=" => (actualValue >= targetValue, null),
            "<=" => (actualValue <= targetValue, null),
            ">" => (actualValue > targetValue, null),
            "<" => (actualValue < targetValue, null),
            _ => (true, null)
        };
    }

    private (bool IsValid, string? ErrorMessage) EvaluateEqualityRule(
        string condition, 
        string operatorStr, 
        Dictionary<string, object> applicationData)
    {
        var parts = condition.Split(operatorStr);
        if (parts.Length != 2) return (true, null);

        var fieldName = parts[0].Trim();
        var targetValue = parts[1].Trim().Trim('\'', '"');

        if (!applicationData.ContainsKey(fieldName))
            return (false, $"Field {fieldName} is required for this rule");

        var actualValue = applicationData[fieldName]?.ToString() ?? string.Empty;

        return operatorStr switch
        {
            "==" => (actualValue.Equals(targetValue, StringComparison.OrdinalIgnoreCase), null),
            "!=" => (!actualValue.Equals(targetValue, StringComparison.OrdinalIgnoreCase), null),
            _ => (true, null)
        };
    }

    private static string ExtractFieldFromCondition(string condition)
    {
        // Simple extraction - in production use proper parsing
        var operators = new[] { ">=", "<=", "!=", "==", ">", "<" };
        foreach (var op in operators)
        {
            if (condition.Contains(op))
            {
                return condition.Split(op)[0].Trim();
            }
        }
        return string.Empty;
    }
}