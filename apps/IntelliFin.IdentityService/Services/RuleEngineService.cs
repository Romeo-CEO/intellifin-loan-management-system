using IntelliFin.IdentityService.Constants;
using IntelliFin.IdentityService.Models;
using IntelliFin.Shared.DomainModels.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;

namespace IntelliFin.IdentityService.Services;

/// <summary>
/// High-performance rule evaluation engine for business authorization
/// </summary>
public class RuleEngineService : IRuleEngineService
{
    private readonly LmsDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RuleEngineService> _logger;
    private readonly ITenantResolver _tenantResolver;
    
    private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(15);
    private const string RULE_TEMPLATE_CACHE_KEY = "rule_templates";
    private const string TENANT_COMPLIANCE_CACHE_KEY = "tenant_compliance_{0}";

    public RuleEngineService(
        LmsDbContext context,
        IMemoryCache cache,
        ILogger<RuleEngineService> logger,
        ITenantResolver tenantResolver)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
        _tenantResolver = tenantResolver;
    }

    public async Task<RuleEvaluationResult> EvaluateRuleAsync(string ruleType, object contextValue, ClaimsPrincipal user)
    {
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            // Get rule value from user claims
            var ruleClaim = user.FindFirst(ruleType);
            if (ruleClaim == null)
            {
                return RuleEvaluationResult.NotApplicable($"Rule '{ruleType}' not configured for user");
            }

            var result = ruleType switch
            {
                SystemRules.LoanApprovalLimit => await EvaluateAmountRuleAsync(ruleClaim.Value, contextValue),
                SystemRules.DailyDisbursementLimit => await EvaluateAmountRuleAsync(ruleClaim.Value, contextValue),
                SystemRules.MaxTransactionAmount => await EvaluateAmountRuleAsync(ruleClaim.Value, contextValue),
                SystemRules.MonthlyLendingLimit => await EvaluateMonthlyLimitAsync(ruleClaim.Value, contextValue, user),
                SystemRules.CashHandlingLimit => await EvaluateAmountRuleAsync(ruleClaim.Value, contextValue),
                SystemRules.WriteOffLimit => await EvaluateAmountRuleAsync(ruleClaim.Value, contextValue),
                
                SystemRules.MaxRiskGrade => EvaluateRiskGradeRule(ruleClaim.Value, contextValue),
                SystemRules.RequiredApprovalCount => EvaluateCountRule(ruleClaim.Value, contextValue),
                SystemRules.MaxLoanToValueRatio => EvaluatePercentageRule(ruleClaim.Value, contextValue),
                SystemRules.MinCreditScore => EvaluateMinimumRule(ruleClaim.Value, contextValue),
                SystemRules.MaxDebtToIncomeRatio => EvaluatePercentageRule(ruleClaim.Value, contextValue),
                SystemRules.PortfolioConcentrationLimit => EvaluatePercentageRule(ruleClaim.Value, contextValue),
                
                SystemRules.MaxClientAssignments => EvaluateCountRule(ruleClaim.Value, contextValue),
                SystemRules.BranchAccessScope => EvaluateScopeRule(ruleClaim.Value, contextValue),
                SystemRules.WorkingHours => EvaluateTimeRangeRule(ruleClaim.Value, contextValue),
                SystemRules.MaxConcurrentSessions => EvaluateCountRule(ruleClaim.Value, contextValue),
                SystemRules.IpAddressRestrictions => EvaluateIpRestrictionRule(ruleClaim.Value, contextValue),
                SystemRules.GeographicRestrictions => EvaluateGeographicRule(ruleClaim.Value, contextValue),
                
                SystemRules.MandatoryApprovalDelay => EvaluateDelayRule(ruleClaim.Value, contextValue),
                SystemRules.AuditTrailLevel => EvaluateEnumRule(ruleClaim.Value, contextValue, ["basic", "detailed", "comprehensive"]),
                SystemRules.DataRetentionPeriod => EvaluateCountRule(ruleClaim.Value, contextValue),
                SystemRules.KycVerificationLevel => EvaluateEnumRule(ruleClaim.Value, contextValue, ["basic", "enhanced", "premium"]),
                SystemRules.AmlRiskThreshold => EvaluateEnumRule(ruleClaim.Value, contextValue, ["low", "medium", "high"]),
                SystemRules.RegulatoryReportingLevel => EvaluateEnumRule(ruleClaim.Value, contextValue, ["basic", "standard", "comprehensive"]),
                
                SystemRules.MaxPayrollDeductionPercent => EvaluatePercentageRule(ruleClaim.Value, contextValue),
                SystemRules.PmecVerificationLevel => EvaluateEnumRule(ruleClaim.Value, contextValue, ["auto", "manual", "enhanced"]),
                SystemRules.AllowedEmployeeGrades => EvaluateScopeRule(ruleClaim.Value, contextValue),
                SystemRules.MinistryAccessScope => EvaluateScopeRule(ruleClaim.Value, contextValue),
                
                SystemRules.AllowedLoanProducts => EvaluateScopeRule(ruleClaim.Value, contextValue),
                SystemRules.MaxLoanTerm => EvaluateCountRule(ruleClaim.Value, contextValue),
                SystemRules.InterestRateAdjustmentLimit => EvaluatePercentageRule(ruleClaim.Value, contextValue),
                SystemRules.MinCollateralValue => EvaluatePercentageRule(ruleClaim.Value, contextValue),
                SystemRules.MaxGracePeriodExtension => EvaluateCountRule(ruleClaim.Value, contextValue),
                
                SystemRules.MobileTransactionLimit => await EvaluateAmountRuleAsync(ruleClaim.Value, contextValue),
                SystemRules.DigitalPaymentThreshold => await EvaluateAmountRuleAsync(ruleClaim.Value, contextValue),
                SystemRules.ApiRateLimit => EvaluateCountRule(ruleClaim.Value, contextValue),
                SystemRules.DigitalChannelHours => EvaluateTimeRangeRule(ruleClaim.Value, contextValue),
                
                _ => RuleEvaluationResult.UnknownRule($"Rule type '{ruleType}' is not supported")
            };

            stopwatch.Stop();
            result.EvaluationTimeMs = (int)stopwatch.ElapsedMilliseconds;

            _logger.LogInformation("Rule evaluation completed: {RuleType} = {Result} in {ElapsedMs}ms", 
                ruleType, result.IsAllowed, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating rule {RuleType} with context {Context}", ruleType, contextValue);
            return RuleEvaluationResult.Error($"Rule evaluation failed: {ex.Message}");
        }
    }

    public async Task<RuleEvaluationResult> EvaluateRuleAsync(string ruleType, string ruleValue, object contextValue, CancellationToken cancellationToken = default)
    {
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var result = ruleType switch
            {
                SystemRules.LoanApprovalLimit => await EvaluateAmountRuleAsync(ruleValue, contextValue),
                SystemRules.DailyDisbursementLimit => await EvaluateAmountRuleAsync(ruleValue, contextValue),
                SystemRules.MaxTransactionAmount => await EvaluateAmountRuleAsync(ruleValue, contextValue),
                SystemRules.MonthlyLendingLimit => await EvaluateMonthlyLimitAsync(ruleValue, contextValue, null),
                SystemRules.CashHandlingLimit => await EvaluateAmountRuleAsync(ruleValue, contextValue),
                SystemRules.WriteOffLimit => await EvaluateAmountRuleAsync(ruleValue, contextValue),

                SystemRules.MaxRiskGrade => EvaluateRiskGradeRule(ruleValue, contextValue),
                SystemRules.RequiredApprovalCount => EvaluateCountRule(ruleValue, contextValue),
                SystemRules.MaxLoanToValueRatio => EvaluatePercentageRule(ruleValue, contextValue),
                SystemRules.MinCreditScore => EvaluateMinimumRule(ruleValue, contextValue),
                SystemRules.MaxDebtToIncomeRatio => EvaluatePercentageRule(ruleValue, contextValue),
                SystemRules.PortfolioConcentrationLimit => EvaluatePercentageRule(ruleValue, contextValue),

                SystemRules.MaxClientAssignments => EvaluateCountRule(ruleValue, contextValue),
                SystemRules.BranchAccessScope => EvaluateScopeRule(ruleValue, contextValue),
                SystemRules.WorkingHours => EvaluateTimeRangeRule(ruleValue, contextValue),
                SystemRules.MaxConcurrentSessions => EvaluateCountRule(ruleValue, contextValue),
                SystemRules.IpAddressRestrictions => EvaluateIpRestrictionRule(ruleValue, contextValue),
                SystemRules.GeographicRestrictions => EvaluateGeographicRule(ruleValue, contextValue),

                SystemRules.MandatoryApprovalDelay => EvaluateDelayRule(ruleValue, contextValue),
                SystemRules.AuditTrailLevel => EvaluateEnumRule(ruleValue, contextValue, new[] { "basic", "detailed", "comprehensive" }),
                SystemRules.DataRetentionPeriod => EvaluateCountRule(ruleValue, contextValue),
                SystemRules.KycVerificationLevel => EvaluateEnumRule(ruleValue, contextValue, new[] { "basic", "enhanced", "premium" }),
                SystemRules.AmlRiskThreshold => EvaluateEnumRule(ruleValue, contextValue, new[] { "low", "medium", "high" }),
                SystemRules.RegulatoryReportingLevel => EvaluateEnumRule(ruleValue, contextValue, new[] { "basic", "standard", "comprehensive" }),

                SystemRules.MaxPayrollDeductionPercent => EvaluatePercentageRule(ruleValue, contextValue),
                SystemRules.PmecVerificationLevel => EvaluateEnumRule(ruleValue, contextValue, new[] { "auto", "manual", "enhanced" }),
                SystemRules.AllowedEmployeeGrades => EvaluateScopeRule(ruleValue, contextValue),
                SystemRules.MinistryAccessScope => EvaluateScopeRule(ruleValue, contextValue),

                SystemRules.AllowedLoanProducts => EvaluateScopeRule(ruleValue, contextValue),
                SystemRules.MaxLoanTerm => EvaluateCountRule(ruleValue, contextValue),
                SystemRules.InterestRateAdjustmentLimit => EvaluatePercentageRule(ruleValue, contextValue),
                SystemRules.MinCollateralValue => EvaluatePercentageRule(ruleValue, contextValue),
                SystemRules.MaxGracePeriodExtension => EvaluateCountRule(ruleValue, contextValue),

                SystemRules.MobileTransactionLimit => await EvaluateAmountRuleAsync(ruleValue, contextValue),
                SystemRules.DigitalPaymentThreshold => await EvaluateAmountRuleAsync(ruleValue, contextValue),
                SystemRules.ApiRateLimit => EvaluateCountRule(ruleValue, contextValue),
                SystemRules.DigitalChannelHours => EvaluateTimeRangeRule(ruleValue, contextValue),

                _ => RuleEvaluationResult.UnknownRule($"Rule type '{ruleType}' is not supported")
            };

            stopwatch.Stop();
            result.EvaluationTimeMs = (int)stopwatch.ElapsedMilliseconds;

            _logger.LogInformation("Rule evaluation completed: {RuleType} = {Result} in {ElapsedMs}ms", 
                ruleType, result.IsAllowed, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating rule {RuleType} with context {Context}", ruleType, contextValue);
            return RuleEvaluationResult.Error($"Rule evaluation failed: {ex.Message}");
        }
    }

    public async Task<Dictionary<string, RuleEvaluationResult>> EvaluateRulesAsync(
        Dictionary<string, object> ruleContexts, 
        ClaimsPrincipal user)
    {
        var results = new Dictionary<string, RuleEvaluationResult>();
        
        // Evaluate rules in parallel for performance
        var evaluationTasks = ruleContexts.Select(async kvp =>
        {
            var result = await EvaluateRuleAsync(kvp.Key, kvp.Value, user);
            return new { Rule = kvp.Key, Result = result };
        });

        var evaluationResults = await Task.WhenAll(evaluationTasks);
        
        foreach (var eval in evaluationResults)
        {
            results[eval.Rule] = eval.Result;
        }

        return results;
    }

    public Dictionary<string, string> GetUserRules(ClaimsPrincipal user)
    {
        var rules = new Dictionary<string, string>();
        var allSystemRules = SystemRules.GetAllRules();

        foreach (var ruleType in allSystemRules)
        {
            var claim = user.FindFirst(ruleType);
            if (claim != null)
            {
                rules[ruleType] = claim.Value;
            }
        }

        return rules;
    }

    public async Task<RuleValidationResult> ValidateRuleConfigurationAsync(RoleRule ruleConfiguration, Guid? tenantId = null)
    {
        var result = new RuleValidationResult
        {
            IsValid = true,
            Errors = new List<string>(),
            Warnings = new List<string>()
        };

        try
        {
            // Validate rule type exists
            if (!SystemRules.IsValidRule(ruleConfiguration.RuleType))
            {
                result.IsValid = false;
                result.Errors.Add($"Invalid rule type: {ruleConfiguration.RuleType}");
                return result;
            }

            // Get rule template for validation
            var template = await GetRuleTemplateAsync(ruleConfiguration.RuleType);
            if (template == null)
            {
                result.Warnings.Add($"No template found for rule type: {ruleConfiguration.RuleType}");
                return result;
            }

            // Validate value type and constraints
            var valueValidation = ValidateRuleValue(ruleConfiguration.RuleValue, template);
            if (!valueValidation.IsValid)
            {
                result.IsValid = false;
                result.Errors.AddRange(valueValidation.Errors);
            }

            // Validate against tenant constraints if applicable
            if (tenantId.HasValue)
            {
                var tenantValidation = await ValidateTenantConstraintsAsync(ruleConfiguration, tenantId.Value);
                if (!tenantValidation.IsValid)
                {
                    result.IsValid = false;
                    result.Errors.AddRange(tenantValidation.Errors);
                }
                result.Warnings.AddRange(tenantValidation.Warnings);
            }

            // Validate conditions if present
            if (ruleConfiguration.Conditions?.Any() == true)
            {
                foreach (var condition in ruleConfiguration.Conditions)
                {
                    var conditionValidation = ValidateRuleCondition(condition);
                    if (!conditionValidation.IsValid)
                    {
                        result.IsValid = false;
                        result.Errors.AddRange(conditionValidation.Errors);
                    }
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating rule configuration for {RuleType}", ruleConfiguration.RuleType);
            result.IsValid = false;
            result.Errors.Add($"Validation error: {ex.Message}");
            return result;
        }
    }

    public async Task<RuleTestResult> TestRuleAsync(RuleTestScenario scenario)
    {
        var result = new RuleTestResult
        {
            ScenarioId = scenario.ScenarioId,
            TestCases = new List<RuleTestCaseResult>()
        };

        try
        {
            foreach (var testCase in scenario.TestCases)
            {
                var mockUser = CreateMockUser(scenario.RuleType, scenario.RuleValue, scenario.TenantId);
                var evaluation = await EvaluateRuleAsync(scenario.RuleType, testCase.ContextValue, mockUser);
                
                var testResult = new RuleTestCaseResult
                {
                    ContextValue = testCase.ContextValue,
                    ExpectedResult = testCase.ExpectedResult,
                    ActualResult = evaluation.IsAllowed ? "allowed" : "denied",
                    Passed = (evaluation.IsAllowed && testCase.ExpectedResult == "allowed") ||
                            (!evaluation.IsAllowed && testCase.ExpectedResult == "denied"),
                    Reason = evaluation.Reason,
                    EvaluationTimeMs = evaluation.EvaluationTimeMs
                };

                result.TestCases.Add(testResult);
            }

            result.OverallResult = result.TestCases.All(tc => tc.Passed) ? "passed" : "failed";
            result.TotalEvaluationTimeMs = result.TestCases.Sum(tc => tc.EvaluationTimeMs);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing rule {RuleType}", scenario.RuleType);
            result.OverallResult = "error";
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    public async Task<AuthorityCheckResult> CheckBusinessAuthorityAsync(string operation, object context, ClaimsPrincipal user)
    {
        var result = new AuthorityCheckResult
        {
            HasAuthority = false,
            Reasons = new List<string>(),
            RequiredActions = new List<string>()
        };

        try
        {
            // Map operation to relevant rules
            var relevantRules = GetRelevantRulesForOperation(operation);
            
            foreach (var ruleType in relevantRules)
            {
                var evaluation = await EvaluateRuleAsync(ruleType, context, user);
                
                if (!evaluation.IsAllowed)
                {
                    result.Reasons.Add($"{SystemRules.GetRuleDisplayName(ruleType)}: {evaluation.Reason}");
                    
                    // Suggest corrective actions
                    var actions = GetCorrectiveActions(ruleType, evaluation, context);
                    result.RequiredActions.AddRange(actions);
                }
            }

            result.HasAuthority = result.Reasons.Count == 0;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking business authority for {Operation}", operation);
            result.Reasons.Add($"Authority check failed: {ex.Message}");
            return result;
        }
    }

    public async Task<string?> GetEffectiveRuleValueAsync(string ruleType, ClaimsPrincipal user)
    {
        // Implementation would consider role hierarchy and inheritance
        // For now, return direct claim value
        return user.FindFirst(ruleType)?.Value;
    }

    public async Task<ComplianceValidationResult> ValidateComplianceAsync(List<RoleRule> rules, Guid tenantId)
    {
        var complianceIssues = new List<ComplianceIssue>();
        var result = new ComplianceValidationResult
        {
            IsCompliant = true,
            ComplianceIssues = Array.Empty<ComplianceIssue>(),
            Warnings = Array.Empty<ComplianceWarning>(),
            OverallScore = 100
        };

        try
        {
            // Get cached compliance constraints
            var cacheKey = string.Format(TENANT_COMPLIANCE_CACHE_KEY, tenantId);
            if (!_cache.TryGetValue(cacheKey, out ComplianceConstraints? constraints))
            {
                constraints = await LoadTenantComplianceConstraintsAsync(tenantId);
                _cache.Set(cacheKey, constraints, CacheExpiry);
            }

            var violationCount = 0;

            foreach (var rule in rules)
            {
                var ruleConstraints = constraints?.GetConstraintsForRule(rule.RuleType);
                if (ruleConstraints != null)
                {
                    var violation = ValidateRuleAgainstConstraints(rule, ruleConstraints);
                    if (violation != null)
                    {
                        complianceIssues.Add(new ComplianceIssue
                        {
                            Severity = IssueSeverity.Critical,
                            Issue = violation,
                            RoleId = rule.RoleId
                        });
                        violationCount++;
                    }
                }
            }

            // Calculate compliance score
            if (rules.Count > 0)
            {
                result.OverallScore = Math.Max(0, 100 - (violationCount * 100 / rules.Count));
            }

            result.IsCompliant = violationCount == 0;
            result.ComplianceIssues = complianceIssues.ToArray();

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating compliance for tenant {TenantId}", tenantId);
            result.IsCompliant = false;
            complianceIssues.Add(new ComplianceIssue
            {
                Severity = IssueSeverity.Critical,
                Issue = $"Compliance validation failed: {ex.Message}",
                RoleId = "system"
            });
            result.ComplianceIssues = complianceIssues.ToArray();
            return result;
        }
    }

    #region Private Helper Methods

    private async Task<RuleEvaluationResult> EvaluateAmountRuleAsync(string ruleValue, object contextValue)
    {
        if (!decimal.TryParse(ruleValue, out var limit))
        {
            return RuleEvaluationResult.Error($"Invalid amount rule value: {ruleValue}");
        }

        var amount = Convert.ToDecimal(contextValue);
        var isAllowed = amount <= limit;

        return new RuleEvaluationResult
        {
            IsAllowed = isAllowed,
            Reason = isAllowed 
                ? $"Amount ({amount:C}) is within limit ({limit:C})"
                : $"Amount ({amount:C}) exceeds limit ({limit:C})"
        };
    }

    private async Task<RuleEvaluationResult> EvaluateMonthlyLimitAsync(string ruleValue, object contextValue, ClaimsPrincipal user)
    {
        if (!decimal.TryParse(ruleValue, out var monthlyLimit))
        {
            return RuleEvaluationResult.Error($"Invalid monthly limit value: {ruleValue}");
        }

        var requestedAmount = Convert.ToDecimal(contextValue);
        var userId = user.FindFirst("sub")?.Value;
        
        // Get current month usage (this would query actual data)
        var currentMonthUsage = await GetCurrentMonthUsageAsync(userId);
        var totalAfterTransaction = currentMonthUsage + requestedAmount;
        
        var isAllowed = totalAfterTransaction <= monthlyLimit;

        return new RuleEvaluationResult
        {
            IsAllowed = isAllowed,
            Reason = isAllowed
                ? $"Monthly total ({totalAfterTransaction:C}) would be within limit ({monthlyLimit:C})"
                : $"Monthly total ({totalAfterTransaction:C}) would exceed limit ({monthlyLimit:C})",
            AdditionalData = new Dictionary<string, object>
            {
                ["currentMonthUsage"] = currentMonthUsage,
                ["monthlyLimit"] = monthlyLimit,
                ["remainingLimit"] = Math.Max(0, monthlyLimit - currentMonthUsage)
            }
        };
    }

    private RuleEvaluationResult EvaluateRiskGradeRule(string ruleValue, object contextValue)
    {
        var allowedGrades = new[] { "A", "B", "C", "D", "F" };
        var gradeValues = new Dictionary<string, int> { ["A"] = 1, ["B"] = 2, ["C"] = 3, ["D"] = 4, ["F"] = 5 };

        if (!gradeValues.ContainsKey(ruleValue))
        {
            return RuleEvaluationResult.Error($"Invalid risk grade rule value: {ruleValue}");
        }

        var contextGrade = contextValue.ToString()?.ToUpperInvariant();
        if (string.IsNullOrEmpty(contextGrade) || !gradeValues.ContainsKey(contextGrade))
        {
            return RuleEvaluationResult.Error($"Invalid context risk grade: {contextValue}");
        }

        var isAllowed = gradeValues[contextGrade] <= gradeValues[ruleValue];

        return new RuleEvaluationResult
        {
            IsAllowed = isAllowed,
            Reason = isAllowed
                ? $"Risk grade {contextGrade} is within allowed maximum {ruleValue}"
                : $"Risk grade {contextGrade} exceeds maximum allowed {ruleValue}"
        };
    }

    private RuleEvaluationResult EvaluateCountRule(string ruleValue, object contextValue)
    {
        if (!int.TryParse(ruleValue, out var limit))
        {
            return RuleEvaluationResult.Error($"Invalid count rule value: {ruleValue}");
        }

        var count = Convert.ToInt32(contextValue);
        var isAllowed = count <= limit;

        return new RuleEvaluationResult
        {
            IsAllowed = isAllowed,
            Reason = isAllowed
                ? $"Count ({count}) is within limit ({limit})"
                : $"Count ({count}) exceeds limit ({limit})"
        };
    }

    private RuleEvaluationResult EvaluatePercentageRule(string ruleValue, object contextValue)
    {
        if (!decimal.TryParse(ruleValue, out var limitPercent))
        {
            return RuleEvaluationResult.Error($"Invalid percentage rule value: {ruleValue}");
        }

        var percent = Convert.ToDecimal(contextValue);
        var isAllowed = percent <= limitPercent;

        return new RuleEvaluationResult
        {
            IsAllowed = isAllowed,
            Reason = isAllowed
                ? $"Percentage ({percent}%) is within limit ({limitPercent}%)"
                : $"Percentage ({percent}%) exceeds limit ({limitPercent}%)"
        };
    }

    private RuleEvaluationResult EvaluateMinimumRule(string ruleValue, object contextValue)
    {
        if (!decimal.TryParse(ruleValue, out var minimum))
        {
            return RuleEvaluationResult.Error($"Invalid minimum rule value: {ruleValue}");
        }

        var value = Convert.ToDecimal(contextValue);
        var isAllowed = value >= minimum;

        return new RuleEvaluationResult
        {
            IsAllowed = isAllowed,
            Reason = isAllowed
                ? $"Value ({value}) meets minimum requirement ({minimum})"
                : $"Value ({value}) below minimum requirement ({minimum})"
        };
    }

    private RuleEvaluationResult EvaluateScopeRule(string ruleValue, object contextValue)
    {
        var allowedScopes = ruleValue.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                   .Select(s => s.Trim())
                                   .ToHashSet();

        var requestedScope = contextValue.ToString()?.Trim();
        if (string.IsNullOrEmpty(requestedScope))
        {
            return RuleEvaluationResult.Error("Invalid scope context value");
        }

        var isAllowed = allowedScopes.Contains(requestedScope);

        return new RuleEvaluationResult
        {
            IsAllowed = isAllowed,
            Reason = isAllowed
                ? $"Scope '{requestedScope}' is within allowed scopes"
                : $"Scope '{requestedScope}' is not in allowed scopes: {string.Join(", ", allowedScopes)}"
        };
    }

    private RuleEvaluationResult EvaluateTimeRangeRule(string ruleValue, object contextValue)
    {
        // Format: "HH:MM-HH:MM" e.g., "08:00-17:00"
        var parts = ruleValue.Split('-');
        if (parts.Length != 2)
        {
            return RuleEvaluationResult.Error($"Invalid time range format: {ruleValue}");
        }

        if (!TimeSpan.TryParse(parts[0], out var startTime) || !TimeSpan.TryParse(parts[1], out var endTime))
        {
            return RuleEvaluationResult.Error($"Invalid time values in range: {ruleValue}");
        }

        var currentTime = contextValue is DateTime dt ? dt.TimeOfDay : DateTime.Now.TimeOfDay;
        var isAllowed = currentTime >= startTime && currentTime <= endTime;

        return new RuleEvaluationResult
        {
            IsAllowed = isAllowed,
            Reason = isAllowed
                ? $"Current time ({currentTime:hh\\:mm}) is within working hours ({ruleValue})"
                : $"Current time ({currentTime:hh\\:mm}) is outside working hours ({ruleValue})"
        };
    }

    private RuleEvaluationResult EvaluateEnumRule(string ruleValue, object contextValue, string[] allowedValues)
    {
        var contextStr = contextValue.ToString()?.ToLowerInvariant();
        var ruleStr = ruleValue.ToLowerInvariant();

        if (!allowedValues.Contains(ruleStr))
        {
            return RuleEvaluationResult.Error($"Invalid rule value '{ruleValue}'. Allowed: {string.Join(", ", allowedValues)}");
        }

        // For enum rules, we typically check if context meets the minimum level
        var ruleIndex = Array.IndexOf(allowedValues, ruleStr);
        var contextIndex = Array.IndexOf(allowedValues, contextStr);

        if (contextIndex == -1)
        {
            return RuleEvaluationResult.Error($"Invalid context value '{contextValue}'. Allowed: {string.Join(", ", allowedValues)}");
        }

        var isAllowed = contextIndex <= ruleIndex;

        return new RuleEvaluationResult
        {
            IsAllowed = isAllowed,
            Reason = isAllowed
                ? $"Level '{contextValue}' meets requirement '{ruleValue}'"
                : $"Level '{contextValue}' exceeds maximum '{ruleValue}'"
        };
    }

    private RuleEvaluationResult EvaluateIpRestrictionRule(string ruleValue, object contextValue)
    {
        var allowedIps = ruleValue.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                  .Select(ip => ip.Trim())
                                  .ToHashSet();

        var clientIp = contextValue.ToString()?.Trim();
        if (string.IsNullOrEmpty(clientIp))
        {
            return RuleEvaluationResult.Error("Invalid IP address context");
        }

        var isAllowed = allowedIps.Contains(clientIp) || allowedIps.Contains("*");

        return new RuleEvaluationResult
        {
            IsAllowed = isAllowed,
            Reason = isAllowed
                ? $"IP address '{clientIp}' is allowed"
                : $"IP address '{clientIp}' is not in allowed list: {string.Join(", ", allowedIps)}"
        };
    }

    private RuleEvaluationResult EvaluateGeographicRule(string ruleValue, object contextValue)
    {
        var allowedRegions = ruleValue.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                     .Select(r => r.Trim().ToLowerInvariant())
                                     .ToHashSet();

        var requestedRegion = contextValue.ToString()?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(requestedRegion))
        {
            return RuleEvaluationResult.Error("Invalid geographic context");
        }

        var isAllowed = allowedRegions.Contains(requestedRegion) || allowedRegions.Contains("all");

        return new RuleEvaluationResult
        {
            IsAllowed = isAllowed,
            Reason = isAllowed
                ? $"Geographic region '{requestedRegion}' is allowed"
                : $"Geographic region '{requestedRegion}' is not in allowed regions: {string.Join(", ", allowedRegions)}"
        };
    }

    private RuleEvaluationResult EvaluateDelayRule(string ruleValue, object contextValue)
    {
        if (!int.TryParse(ruleValue, out var requiredDelayHours))
        {
            return RuleEvaluationResult.Error($"Invalid delay rule value: {ruleValue}");
        }

        var requestTime = contextValue is DateTime dt ? dt : DateTime.UtcNow;
        var earliestAllowedTime = requestTime.AddHours(requiredDelayHours);
        var currentTime = DateTime.UtcNow;

        var isAllowed = currentTime >= earliestAllowedTime;

        return new RuleEvaluationResult
        {
            IsAllowed = isAllowed,
            Reason = isAllowed
                ? $"Required delay of {requiredDelayHours} hours has passed"
                : $"Must wait {requiredDelayHours} hours before approval (available at {earliestAllowedTime:yyyy-MM-dd HH:mm})"
        };
    }

    private async Task<RuleTemplate?> GetRuleTemplateAsync(string ruleType)
    {
        // This would typically load from database
        // For now, return a mock template
        return new RuleTemplate
        {
            Id = ruleType,
            Name = SystemRules.GetRuleDisplayName(ruleType),
            Description = SystemRules.GetRuleDescription(ruleType),
            ValueType = GetRuleValueType(ruleType),
            RequiresCompliance = IsComplianceRule(ruleType)
        };
    }

    private RuleValueType GetRuleValueType(string ruleType)
    {
        return ruleType switch
        {
            var r when r.Contains("limit") || r.Contains("amount") || r.Contains("threshold") => RuleValueType.Amount,
            var r when r.Contains("count") || r.Contains("term") || r.Contains("period") => RuleValueType.Count,
            var r when r.Contains("grade") => RuleValueType.Grade,
            var r when r.Contains("hours") || r.Contains("delay") => RuleValueType.Duration,
            var r when r.Contains("scope") || r.Contains("access") => RuleValueType.Enum,
            var r when r.Contains("level") || r.Contains("verification") => RuleValueType.Enum,
            _ => RuleValueType.Amount
        };
    }

    private bool IsComplianceRule(string ruleType)
    {
        return ruleType.Contains("audit") || ruleType.Contains("kyc") || ruleType.Contains("aml") || 
               ruleType.Contains("regulatory") || ruleType.Contains("retention");
    }

    private ClaimsPrincipal CreateMockUser(string ruleType, string ruleValue, Guid? tenantId)
    {
        var claims = new List<Claim>
        {
            new("sub", "test-user"),
            new(ruleType, ruleValue)
        };

        if (tenantId.HasValue)
        {
            claims.Add(new("tenant_id", tenantId.Value.ToString()));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private string[] GetRelevantRulesForOperation(string operation)
    {
        return operation.ToLowerInvariant() switch
        {
            "loan_approval" => new[] { SystemRules.LoanApprovalLimit, SystemRules.MaxRiskGrade, SystemRules.RequiredApprovalCount },
            "loan_disbursement" => new[] { SystemRules.DailyDisbursementLimit, SystemRules.MaxTransactionAmount },
            "client_assignment" => new[] { SystemRules.MaxClientAssignments, SystemRules.BranchAccessScope },
            "payment_processing" => new[] { SystemRules.MaxTransactionAmount, SystemRules.DigitalPaymentThreshold },
            _ => Array.Empty<string>()
        };
    }

    private string[] GetCorrectiveActions(string ruleType, RuleEvaluationResult evaluation, object context)
    {
        return ruleType switch
        {
            SystemRules.LoanApprovalLimit => new[] { "Request approval from higher authority", "Reduce loan amount" },
            SystemRules.MaxRiskGrade => new[] { "Require additional collateral", "Request risk assessment review" },
            SystemRules.RequiredApprovalCount => new[] { "Obtain additional approvals", "Escalate to senior management" },
            _ => new[] { "Review business rules", "Contact administrator" }
        };
    }

    private async Task<decimal> GetCurrentMonthUsageAsync(string? userId)
    {
        if (string.IsNullOrEmpty(userId)) return 0;

        // This would query actual transaction data
        // For now, return mock data
        return 25000m;
    }

    private RuleValidationResult ValidateRuleValue(string ruleValue, RuleTemplate template)
    {
        var result = new RuleValidationResult { IsValid = true, Errors = new(), Warnings = new() };

        switch (template.ValueType)
        {
            case RuleValueType.Amount:
                if (!decimal.TryParse(ruleValue, out var amount) || amount < 0)
                {
                    result.IsValid = false;
                    result.Errors.Add("Amount must be a positive number");
                }
                break;

            case RuleValueType.Count:
                if (!int.TryParse(ruleValue, out var count) || count < 0)
                {
                    result.IsValid = false;
                    result.Errors.Add("Count must be a positive integer");
                }
                break;

            case RuleValueType.Grade:
                if (!new[] { "A", "B", "C", "D", "F" }.Contains(ruleValue.ToUpperInvariant()))
                {
                    result.IsValid = false;
                    result.Errors.Add("Grade must be A, B, C, D, or F");
                }
                break;
        }

        return result;
    }

    private async Task<RuleValidationResult> ValidateTenantConstraintsAsync(RoleRule rule, Guid tenantId)
    {
        // This would validate against tenant-specific constraints
        return new RuleValidationResult { IsValid = true, Errors = new(), Warnings = new() };
    }

    private RuleValidationResult ValidateRuleCondition(RuleCondition condition)
    {
        // This would validate rule conditions
        return new RuleValidationResult { IsValid = true, Errors = new(), Warnings = new() };
    }

    private async Task<ComplianceConstraints> LoadTenantComplianceConstraintsAsync(Guid tenantId)
    {
        // This would load from database
        return new ComplianceConstraints();
    }

    private string? ValidateRuleAgainstConstraints(RoleRule rule, object ruleConstraints)
    {
        // This would validate against specific constraints
        return null;
    }

    #endregion
}

// Helper classes for compliance validation
public class ComplianceConstraints
{
    public object? GetConstraintsForRule(string ruleType) => null;
}