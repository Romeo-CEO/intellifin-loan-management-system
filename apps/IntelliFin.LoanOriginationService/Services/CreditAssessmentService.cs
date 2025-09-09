using IntelliFin.LoanOriginationService.Models;

namespace IntelliFin.LoanOriginationService.Services;

public class CreditAssessmentService : ICreditAssessmentService
{
    private readonly ILogger<CreditAssessmentService> _logger;
    private readonly IRiskCalculationEngine _riskEngine;
    private readonly IComplianceService _complianceService;

    // **SPRINT 4: External Integrations** - Replace with Bank of Zambia Credit Reference Bureau API integration
    private readonly Dictionary<Guid, CreditBureauData> _mockBureauData = new()
    {
        {
            Guid.Parse("00000000-0000-0000-0000-000000000001"), 
            new CreditBureauData
            {
                BureauName = "Bank of Zambia Credit Reference Bureau",
                CreditScore = 720,
                TotalAccounts = 3,
                ActiveAccounts = 2,
                DefaultedAccounts = 0,
                TotalDebt = 45000,
                MonthlyObligations = 2800,
                LastUpdated = DateTime.UtcNow.AddDays(-30),
                Accounts = new List<CreditAccount>
                {
                    new() { 
                        AccountNumber = "ACC001", 
                        LenderName = "Standard Chartered Bank", 
                        AccountType = "Personal Loan",
                        CurrentBalance = 25000, 
                        CreditLimit = 50000, 
                        PaymentHistory = "Current",
                        DaysInArrears = 0,
                        OpenedDate = DateTime.UtcNow.AddYears(-2)
                    }
                }
            }
        }
    };

    public CreditAssessmentService(
        ILogger<CreditAssessmentService> logger,
        IRiskCalculationEngine riskEngine,
        IComplianceService complianceService)
    {
        _logger = logger;
        _riskEngine = riskEngine;
        _complianceService = complianceService;
    }

    public async Task<CreditAssessment> PerformAssessmentAsync(CreditAssessmentRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting credit assessment for application {ApplicationId}", request.LoanApplicationId);

            // Get application details (in production, from database)
            var application = await GetLoanApplicationAsync(request.LoanApplicationId, cancellationToken);
            if (application == null)
            {
                throw new KeyNotFoundException($"Loan application {request.LoanApplicationId} not found");
            }

            // Get credit bureau data
            var bureauData = await GetCreditBureauDataAsync(request.ClientId, cancellationToken);

            // Perform affordability assessment
            var affordability = await AssessAffordabilityAsync(
                request.ClientId, application.RequestedAmount, application.TermMonths, cancellationToken);

            // Calculate risk grade and score
            var riskResult = await _riskEngine.CalculateRiskAsync(application, bureauData, affordability, cancellationToken);

            // Generate explanation
            var explanation = await GenerateScoreExplanationAsync(riskResult, cancellationToken);

            // Create credit assessment
            var assessment = new CreditAssessment
            {
                Id = Guid.NewGuid(),
                LoanApplicationId = request.LoanApplicationId,
                RiskGrade = riskResult.Grade,
                CreditScore = riskResult.Score,
                DebtToIncomeRatio = affordability.DebtToIncomeRatio,
                PaymentCapacity = affordability.MaxAffordablePayment,
                HasCreditBureauData = bureauData != null,
                ScoreExplanation = explanation,
                AssessedAt = DateTime.UtcNow,
                AssessedBy = "CreditAssessmentEngine_v2.1",
                CreditFactors = riskResult.Factors.Select(f => new CreditFactor
                {
                    Name = f.Name,
                    Value = f.Value.ToString(),
                    Weight = f.Weight,
                    Score = f.Contribution,
                    Impact = f.Contribution > 0 ? "Positive" : f.Contribution < 0 ? "Negative" : "Neutral"
                }).ToList(),
                RiskIndicators = GenerateRiskIndicators(riskResult, affordability)
            };

            _logger.LogInformation("Credit assessment completed for application {ApplicationId}. Risk Grade: {RiskGrade}, Score: {Score}",
                request.LoanApplicationId, assessment.RiskGrade, assessment.CreditScore);

            return assessment;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing credit assessment for application {ApplicationId}", request.LoanApplicationId);
            throw;
        }
    }

    public async Task<RiskCalculationResult> CalculateRiskGradeAsync(Guid clientId, LoanApplication application, CancellationToken cancellationToken = default)
    {
        try
        {
            var bureauData = await GetCreditBureauDataAsync(clientId, cancellationToken);
            var affordability = await AssessAffordabilityAsync(clientId, application.RequestedAmount, application.TermMonths, cancellationToken);
            
            return await _riskEngine.CalculateRiskAsync(application, bureauData, affordability, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating risk grade for client {ClientId}", clientId);
            throw;
        }
    }

    public async Task<AffordabilityAssessment> AssessAffordabilityAsync(Guid clientId, decimal loanAmount, int termMonths, CancellationToken cancellationToken = default)
    {
        try
        {
            // In production, this would pull from client financial data and income verification
            var assessment = new AffordabilityAssessment();

            // Mock client financial data - in production get from client service
            var clientFinancials = await GetClientFinancialDataAsync(clientId, cancellationToken);
            
            assessment.MonthlyIncome = clientFinancials.GetValueOrDefault("monthly_income", 15000m);
            assessment.MonthlyExpenses = clientFinancials.GetValueOrDefault("monthly_expenses", 8000m);
            assessment.ExistingDebtPayments = clientFinancials.GetValueOrDefault("existing_debt_payments", 2500m);

            assessment.DisposableIncome = assessment.MonthlyIncome - assessment.MonthlyExpenses - assessment.ExistingDebtPayments;
            
            // Calculate debt-to-income ratio including proposed loan
            var proposedMonthlyPayment = CalculateMonthlyPayment(loanAmount, 0.15m / 12, termMonths);
            var totalDebtPayments = assessment.ExistingDebtPayments + proposedMonthlyPayment;
            assessment.DebtToIncomeRatio = totalDebtPayments / assessment.MonthlyIncome;

            // BoZ guidelines: DTI should not exceed 40%
            assessment.MaxAffordablePayment = Math.Max(0, assessment.MonthlyIncome * 0.40m - assessment.ExistingDebtPayments);
            
            // Calculate recommended loan amount based on affordability
            assessment.RecommendedLoanAmount = CalculateLoanAmountFromPayment(assessment.MaxAffordablePayment, 0.15m / 12, termMonths);
            
            assessment.PassesAffordabilityTest = assessment.DebtToIncomeRatio <= 0.40m && proposedMonthlyPayment <= assessment.MaxAffordablePayment;

            if (!assessment.PassesAffordabilityTest)
            {
                assessment.AffordabilityNotes.Add($"Debt-to-income ratio of {assessment.DebtToIncomeRatio:P} exceeds 40% limit");
                assessment.AffordabilityNotes.Add($"Proposed payment of {proposedMonthlyPayment:C} exceeds affordable amount of {assessment.MaxAffordablePayment:C}");
            }

            return assessment;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assessing affordability for client {ClientId}", clientId);
            throw;
        }
    }

    public async Task<CreditBureauData?> GetCreditBureauDataAsync(Guid clientId, CancellationToken cancellationToken = default)
    {
        try
        {
            // In production, integrate with actual credit bureau APIs
            await Task.Delay(100, cancellationToken); // Simulate API call
            
            _mockBureauData.TryGetValue(clientId, out var bureauData);
            return bureauData;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not retrieve credit bureau data for client {ClientId}", clientId);
            return null;
        }
    }

    public async Task<string> GenerateScoreExplanationAsync(RiskCalculationResult riskResult, CancellationToken cancellationToken = default)
    {
        try
        {
            var explanation = $"Credit Score: {riskResult.Score:F0}/1000 (Risk Grade {riskResult.Grade})\n\n";
            
            explanation += "Key Factors:\n";
            foreach (var factor in riskResult.Factors.OrderByDescending(f => Math.Abs(f.Contribution)).Take(5))
            {
                var impact = factor.Contribution > 0 ? "positively" : "negatively";
                explanation += $"• {factor.Name}: {impact} impacted score by {Math.Abs(factor.Contribution):F0} points\n";
            }

            explanation += $"\nRecommendation: {(riskResult.RecommendApproval ? "APPROVE" : "DECLINE")}";
            
            if (riskResult.Conditions.Any())
            {
                explanation += "\n\nConditions:\n";
                foreach (var condition in riskResult.Conditions)
                {
                    explanation += $"• {condition}\n";
                }
            }

            return explanation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating score explanation");
            return "Unable to generate explanation";
        }
    }

    public async Task<bool> UpdateAssessmentAsync(Guid assessmentId, Dictionary<string, object> updates, CancellationToken cancellationToken = default)
    {
        try
        {
            // In production, update assessment in database
            _logger.LogInformation("Assessment {AssessmentId} updated", assessmentId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating assessment {AssessmentId}", assessmentId);
            return false;
        }
    }

    // Helper methods
    private async Task<LoanApplication?> GetLoanApplicationAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        // In production, get from database via repository
        await Task.Delay(10, cancellationToken);
        return new LoanApplication
        {
            Id = applicationId,
            RequestedAmount = 50000m,
            TermMonths = 24,
            ProductCode = "PL001"
        };
    }

    private async Task<Dictionary<string, decimal>> GetClientFinancialDataAsync(Guid clientId, CancellationToken cancellationToken)
    {
        // Mock client financial data - in production get from client service/database
        await Task.Delay(10, cancellationToken);
        return new Dictionary<string, decimal>
        {
            ["monthly_income"] = 15000m,
            ["monthly_expenses"] = 8000m,
            ["existing_debt_payments"] = 2500m
        };
    }

    private static decimal CalculateMonthlyPayment(decimal principal, decimal monthlyRate, int termMonths)
    {
        if (monthlyRate == 0) return principal / termMonths;
        
        var payment = (double)principal * ((double)monthlyRate * Math.Pow(1 + (double)monthlyRate, termMonths)) / 
                     (Math.Pow(1 + (double)monthlyRate, termMonths) - 1);
        return (decimal)payment;
    }

    private static decimal CalculateLoanAmountFromPayment(decimal monthlyPayment, decimal monthlyRate, int termMonths)
    {
        if (monthlyRate == 0) return monthlyPayment * termMonths;
        
        var principal = (double)monthlyPayment * (Math.Pow(1 + (double)monthlyRate, termMonths) - 1) / 
                       ((double)monthlyRate * Math.Pow(1 + (double)monthlyRate, termMonths));
        return (decimal)principal;
    }

    private static List<RiskIndicator> GenerateRiskIndicators(RiskCalculationResult riskResult, AffordabilityAssessment affordability)
    {
        var indicators = new List<RiskIndicator>();

        // DTI Risk Indicator
        if (affordability.DebtToIncomeRatio > 0.40m)
        {
            indicators.Add(new RiskIndicator
            {
                Category = "Affordability",
                Description = $"High debt-to-income ratio: {affordability.DebtToIncomeRatio:P}",
                Level = affordability.DebtToIncomeRatio > 0.50m ? RiskLevel.High : RiskLevel.Medium,
                Impact = (decimal)(affordability.DebtToIncomeRatio - 0.40m) * 100
            });
        }

        // Credit Score Risk Indicator
        if (riskResult.Score < 600)
        {
            indicators.Add(new RiskIndicator
            {
                Category = "Credit History",
                Description = $"Below average credit score: {riskResult.Score:F0}",
                Level = riskResult.Score < 500 ? RiskLevel.High : RiskLevel.Medium,
                Impact = (600 - riskResult.Score) / 10
            });
        }

        return indicators;
    }
}