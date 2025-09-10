using IntelliFin.LoanOriginationService.Models;

namespace IntelliFin.LoanOriginationService.Services;

public class RiskCalculationEngine : IRiskCalculationEngine
{
    private readonly ILogger<RiskCalculationEngine> _logger;
    
    // BoZ-compliant risk scoring model
    private readonly ScoringModel _scoringModel = new()
    {
        Name = "BoZ Standard Scoring Model",
        Version = "2.1",
        MinScore = 300,
        MaxScore = 850,
        IsActive = true,
        Factors = new List<ScoringFactor>
        {
            new() { 
                Name = "Payment History", 
                Weight = 0.35m, 
                DataSource = "bureau",
                Bands = new List<ScoringBand>
                {
                    new() { MinValue = 0, MaxValue = 30, Points = 120, Description = "Excellent payment history" },
                    new() { MinValue = 31, MaxValue = 90, Points = 85, Description = "Good payment history" },
                    new() { MinValue = 91, MaxValue = 180, Points = 50, Description = "Fair payment history" },
                    new() { MinValue = 181, MaxValue = 9999, Points = 10, Description = "Poor payment history" }
                }
            },
            new() { 
                Name = "Debt to Income Ratio", 
                Weight = 0.25m, 
                DataSource = "application",
                Bands = new List<ScoringBand>
                {
                    new() { MinValue = 0, MaxValue = 20, Points = 100, Description = "Very low debt burden" },
                    new() { MinValue = 20, MaxValue = 30, Points = 80, Description = "Low debt burden" },
                    new() { MinValue = 30, MaxValue = 40, Points = 60, Description = "Moderate debt burden" },
                    new() { MinValue = 40, MaxValue = 50, Points = 30, Description = "High debt burden" },
                    new() { MinValue = 50, MaxValue = 100, Points = 5, Description = "Very high debt burden" }
                }
            },
            new() { 
                Name = "Credit Utilization", 
                Weight = 0.20m, 
                DataSource = "bureau",
                Bands = new List<ScoringBand>
                {
                    new() { MinValue = 0, MaxValue = 30, Points = 90, Description = "Low utilization" },
                    new() { MinValue = 30, MaxValue = 50, Points = 70, Description = "Moderate utilization" },
                    new() { MinValue = 50, MaxValue = 80, Points = 40, Description = "High utilization" },
                    new() { MinValue = 80, MaxValue = 100, Points = 10, Description = "Very high utilization" }
                }
            },
            new() { 
                Name = "Length of Credit History", 
                Weight = 0.10m, 
                DataSource = "bureau",
                Bands = new List<ScoringBand>
                {
                    new() { MinValue = 0, MaxValue = 12, Points = 30, Description = "New credit history" },
                    new() { MinValue = 12, MaxValue = 36, Points = 60, Description = "Short credit history" },
                    new() { MinValue = 36, MaxValue = 72, Points = 80, Description = "Moderate credit history" },
                    new() { MinValue = 72, MaxValue = 9999, Points = 95, Description = "Long credit history" }
                }
            },
            new() { 
                Name = "Account Diversity", 
                Weight = 0.10m, 
                DataSource = "bureau",
                Bands = new List<ScoringBand>
                {
                    new() { MinValue = 1, MaxValue = 1, Points = 40, Description = "Single account type" },
                    new() { MinValue = 2, MaxValue = 2, Points = 70, Description = "Two account types" },
                    new() { MinValue = 3, MaxValue = 4, Points = 90, Description = "Multiple account types" },
                    new() { MinValue = 5, MaxValue = 99, Points = 95, Description = "Diverse credit mix" }
                }
            }
        },
        Rules = new List<ScoringRule>
        {
            new() { Name = "Current Default", Condition = "HasCurrentDefault", Action = "decline", Reason = "Active default on credit bureau" },
            new() { Name = "Recent Bankruptcy", Condition = "BankruptcyWithin24Months", Action = "decline", Reason = "Bankruptcy within 24 months" },
            new() { Name = "Excessive DTI", Condition = "DTI > 0.6", Action = "decline", Reason = "Debt-to-income ratio exceeds 60%" },
            new() { Name = "Insufficient Income", Condition = "MonthlyIncome < 2000", Action = "decline", Reason = "Monthly income below minimum threshold" }
        }
    };

    public RiskCalculationEngine(ILogger<RiskCalculationEngine> logger)
    {
        _logger = logger;
    }

    public async Task<RiskCalculationResult> CalculateRiskAsync(
        LoanApplication application, 
        CreditBureauData? bureauData, 
        AffordabilityAssessment affordability, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Calculating risk for application {ApplicationId}", application.Id);

            // Check minimum criteria first
            var passesMinimum = await PassesMinimumCriteriaAsync(application, bureauData, cancellationToken);
            if (!passesMinimum)
            {
                return new RiskCalculationResult
                {
                    Grade = RiskGrade.F,
                    Score = 300,
                    Confidence = 0.95m,
                    RecommendApproval = false,
                    Explanation = "Application does not meet minimum lending criteria"
                };
            }

            // Extract risk factors
            var factors = await ExtractRiskFactorsAsync(application, bureauData, cancellationToken);
            
            // Add affordability factors
            factors.AddRange(ExtractAffordabilityFactors(affordability));

            // Calculate base score
            var baseScore = await CalculateScoreAsync(factors, cancellationToken);

            // Apply adjustments based on business rules
            var adjustedScore = ApplyBusinessRuleAdjustments(baseScore, application, bureauData, affordability);

            // Determine risk grade
            var riskGrade = await DetermineRiskGradeAsync(adjustedScore, cancellationToken);

            // Calculate confidence based on data availability
            var confidence = CalculateConfidence(bureauData, application);

            // Generate recommendation
            var recommendation = GenerateRecommendation(riskGrade, adjustedScore, affordability);

            var result = new RiskCalculationResult
            {
                Grade = riskGrade,
                Score = adjustedScore,
                Confidence = confidence,
                Factors = factors,
                Explanation = $"Risk Grade {riskGrade} based on score of {adjustedScore:F0}",
                RecommendApproval = recommendation.Approve,
                RecommendedAmount = recommendation.Amount,
                RecommendedRate = recommendation.Rate,
                Conditions = recommendation.Conditions
            };

            _logger.LogInformation("Risk calculation completed. Grade: {Grade}, Score: {Score}", 
                result.Grade, result.Score);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating risk for application {ApplicationId}", application.Id);
            throw;
        }
    }

    public Task<List<RiskFactor>> ExtractRiskFactorsAsync(
        LoanApplication application, 
        CreditBureauData? bureauData, 
        CancellationToken cancellationToken = default)
    {
        var factors = new List<RiskFactor>();

        // Payment History Factor
        if (bureauData != null)
        {
            var avgDaysInArrears = bureauData.Accounts.Any() 
                ? bureauData.Accounts.Average(a => a.DaysInArrears) 
                : 0;
            
            factors.Add(new RiskFactor
            {
                Name = "Payment History",
                Category = "Credit Bureau",
                Value = (decimal)avgDaysInArrears,
                Weight = 0.35m,
                Contribution = GetBandScore(_scoringModel.Factors[0], (decimal)avgDaysInArrears) * 0.35m,
                Description = $"Average {avgDaysInArrears} days in arrears across all accounts"
            });

            // Credit Utilization Factor
            var totalUtilization = bureauData.Accounts.Any() 
                ? bureauData.Accounts.Where(a => a.CreditLimit > 0)
                    .Average(a => (a.CurrentBalance / a.CreditLimit) * 100)
                : 0;

            factors.Add(new RiskFactor
            {
                Name = "Credit Utilization",
                Category = "Credit Bureau",
                Value = (decimal)totalUtilization,
                Weight = 0.20m,
                Contribution = GetBandScore(_scoringModel.Factors[2], (decimal)totalUtilization) * 0.20m,
                Description = $"Average credit utilization of {totalUtilization:F1}%"
            });

            // Credit History Length Factor
            var avgAccountAge = bureauData.Accounts.Any()
                ? bureauData.Accounts.Average(a => (DateTime.UtcNow - a.OpenedDate).Days / 30.0)
                : 0;

            factors.Add(new RiskFactor
            {
                Name = "Length of Credit History",
                Category = "Credit Bureau",
                Value = (decimal)avgAccountAge,
                Weight = 0.10m,
                Contribution = GetBandScore(_scoringModel.Factors[3], (decimal)avgAccountAge) * 0.10m,
                Description = $"Average account age of {avgAccountAge:F0} months"
            });

            // Account Diversity Factor
            var accountTypeCount = bureauData.Accounts.Select(a => a.AccountType).Distinct().Count();
            factors.Add(new RiskFactor
            {
                Name = "Account Diversity",
                Category = "Credit Bureau",
                Value = accountTypeCount,
                Weight = 0.10m,
                Contribution = GetBandScore(_scoringModel.Factors[4], accountTypeCount) * 0.10m,
                Description = $"{accountTypeCount} different account types"
            });
        }
        else
        {
            // No credit bureau data - apply penalties
            factors.Add(new RiskFactor
            {
                Name = "No Credit History",
                Category = "Credit Bureau",
                Value = 0,
                Weight = 0.70m,
                Contribution = -50,
                Description = "No credit bureau data available"
            });
        }

        return Task.FromResult(factors);
    }

    public Task<RiskGrade> DetermineRiskGradeAsync(decimal score, CancellationToken cancellationToken = default)
    {
        // BoZ-aligned risk grading based on credit score bands
        return Task.FromResult(score switch
        {
            >= 750 => RiskGrade.A, // Excellent
            >= 650 => RiskGrade.B, // Good  
            >= 550 => RiskGrade.C, // Fair
            >= 450 => RiskGrade.D, // Poor
            >= 350 => RiskGrade.E, // Very Poor
            _ => RiskGrade.F        // Unacceptable
        });
    }

    public Task<bool> PassesMinimumCriteriaAsync(
        LoanApplication application, 
        CreditBureauData? bureauData, 
        CancellationToken cancellationToken = default)
    {
        // Check for immediate declines based on business rules
        foreach (var rule in _scoringModel.Rules.Where(r => r.Action == "decline"))
        {
            if (await EvaluateRule(rule, application, bureauData))
            {
                _logger.LogInformation("Application {ApplicationId} fails minimum criteria: {Reason}", 
                    application.Id, rule.Reason);
                return Task.FromResult(false);
            }
        }

        return Task.FromResult(true);
    }

    public Task<decimal> CalculateScoreAsync(List<RiskFactor> factors, CancellationToken cancellationToken = default)
    {
        var baseScore = 500m; // Starting point
        var totalContribution = factors.Sum(f => f.Contribution);
        var finalScore = baseScore + totalContribution;
        
        // Ensure score is within bounds
        return Task.FromResult(Math.Max(_scoringModel.MinScore, Math.Min(_scoringModel.MaxScore, finalScore)));
    }

    // Helper methods
    private List<RiskFactor> ExtractAffordabilityFactors(AffordabilityAssessment affordability)
    {
        var factors = new List<RiskFactor>();

        // Debt-to-Income Factor
        var dtiPercentage = affordability.DebtToIncomeRatio * 100;
        factors.Add(new RiskFactor
        {
            Name = "Debt to Income Ratio",
            Category = "Affordability",
            Value = dtiPercentage,
            Weight = 0.25m,
            Contribution = GetBandScore(_scoringModel.Factors[1], dtiPercentage) * 0.25m,
            Description = $"DTI ratio of {affordability.DebtToIncomeRatio:P}"
        });

        return factors;
    }

    private decimal GetBandScore(ScoringFactor factor, decimal value)
    {
        var band = factor.Bands.FirstOrDefault(b => value >= b.MinValue && value <= b.MaxValue)
                  ?? factor.Bands.Last(); // Default to last band if out of range
        
        return band.Points;
    }

    private decimal ApplyBusinessRuleAdjustments(
        decimal baseScore, 
        LoanApplication application, 
        CreditBureauData? bureauData, 
        AffordabilityAssessment affordability)
    {
        var adjustedScore = baseScore;

        // Apply specific adjustments based on BoZ guidelines
        if (affordability.DebtToIncomeRatio > 0.5m)
        {
            adjustedScore -= 75; // Significant penalty for high DTI
        }

        if (bureauData?.DefaultedAccounts > 0)
        {
            adjustedScore -= bureauData.DefaultedAccounts * 25; // Penalty per defaulted account
        }

        // Loan amount risk adjustment
        if (application.RequestedAmount > 100000m)
        {
            adjustedScore -= 25; // Higher risk for large loans
        }

        return Math.Max(_scoringModel.MinScore, Math.Min(_scoringModel.MaxScore, adjustedScore));
    }

    private decimal CalculateConfidence(CreditBureauData? bureauData, LoanApplication application)
    {
        var confidence = 0.5m; // Base confidence

        if (bureauData != null)
        {
            confidence += 0.3m; // Boost for having bureau data
            if (bureauData.Accounts.Count >= 2)
                confidence += 0.1m; // Additional boost for multiple accounts
        }

        if (application.ApplicationData.Count > 5)
            confidence += 0.1m; // Boost for complete application data

        return Math.Min(1.0m, confidence);
    }

    private (bool Approve, decimal Amount, decimal Rate, List<string> Conditions) GenerateRecommendation(
        RiskGrade riskGrade, 
        decimal score, 
        AffordabilityAssessment affordability)
    {
        var conditions = new List<string>();
        
        var recommendation = riskGrade switch
        {
            RiskGrade.A => (true, affordability.RecommendedLoanAmount, 0.12m, conditions),
            RiskGrade.B => (true, affordability.RecommendedLoanAmount, 0.15m, conditions),
            RiskGrade.C => (true, Math.Min(affordability.RecommendedLoanAmount, 75000m), 0.18m, 
                new List<string> { "Require additional collateral", "Monthly income verification required" }),
            RiskGrade.D => (false, 0, 0.22m, 
                new List<string> { "Significant risk - recommend decline", "Consider secured loan option" }),
            RiskGrade.E => (false, 0, 0.25m, 
                new List<string> { "High risk - decline", "Refer to financial counseling" }),
            RiskGrade.F => (false, 0, 0m,
                new List<string> { "Unacceptable risk - decline" })
        };

        return recommendation;
    }

    private async Task<bool> EvaluateRule(ScoringRule rule, LoanApplication application, CreditBureauData? bureauData)
    {
        // Simplified rule evaluation - in production would use a proper rules engine
        return rule.Condition switch
        {
            "HasCurrentDefault" => bureauData?.Accounts.Any(a => a.DaysInArrears > 90) == true,
            "BankruptcyWithin24Months" => false, // Would check actual bankruptcy records
            _ => false
        };
    }
}