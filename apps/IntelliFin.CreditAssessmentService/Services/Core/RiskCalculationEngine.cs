using IntelliFin.CreditAssessmentService.Models.Responses;

namespace IntelliFin.CreditAssessmentService.Services.Core;

/// <summary>
/// Basic risk calculation engine (Story 1.4 - will be enhanced with Vault rules in Story 1.9).
/// </summary>
public class RiskCalculationEngine : IRiskCalculationEngine
{
    private readonly ILogger<RiskCalculationEngine> _logger;

    public RiskCalculationEngine(ILogger<RiskCalculationEngine> logger)
    {
        _logger = logger;
    }

    public async Task<RiskCalculationResult> CalculateRiskAsync(
        AssessmentData assessmentData,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Calculating risk for {ProductType} loan of {Amount}",
            assessmentData.ProductType, assessmentData.RequestedAmount);

        var result = new RiskCalculationResult();
        var rules = new List<RuleEvaluationDto>();

        // Calculate DTI
        var monthlyPayment = CalculateMonthlyPayment(assessmentData.RequestedAmount, assessmentData.TermMonths);
        var totalMonthlyObligation = assessmentData.ExistingDebt + monthlyPayment;
        result.DebtToIncomeRatio = assessmentData.MonthlyIncome > 0
            ? totalMonthlyObligation / assessmentData.MonthlyIncome
            : 1.0m;

        // Rule 1: DTI Ratio Check
        var dtiRule = new RuleEvaluationDto
        {
            RuleId = "BASIC-001",
            RuleName = "Debt-to-Income Ratio",
            Passed = result.DebtToIncomeRatio <= 0.40m,
            Score = result.DebtToIncomeRatio <= 0.40m ? 100 : -150,
            Weight = 0.30m,
            WeightedScore = (result.DebtToIncomeRatio <= 0.40m ? 100 : -150) * 0.30m,
            Explanation = $"DTI ratio is {result.DebtToIncomeRatio:P2} (threshold: 40%)",
            Impact = result.DebtToIncomeRatio <= 0.40m ? "Positive" : "Negative"
        };
        rules.Add(dtiRule);

        // Rule 2: Credit Score Check (if available)
        if (assessmentData.CreditScore.HasValue)
        {
            var creditScoreRule = new RuleEvaluationDto
            {
                RuleId = "BASIC-002",
                RuleName = "Credit Bureau Score",
                Passed = assessmentData.CreditScore.Value >= 550,
                Score = assessmentData.CreditScore.Value >= 550 ? 120 : -180,
                Weight = 0.25m,
                WeightedScore = (assessmentData.CreditScore.Value >= 550 ? 120 : -180) * 0.25m,
                Explanation = $"Credit score is {assessmentData.CreditScore.Value} (minimum: 550)",
                Impact = assessmentData.CreditScore.Value >= 550 ? "Positive" : "Negative"
            };
            rules.Add(creditScoreRule);
        }

        // Rule 3: Loan-to-Income Ratio
        var loanToIncome = assessmentData.MonthlyIncome > 0
            ? assessmentData.RequestedAmount / (assessmentData.MonthlyIncome * 12)
            : 99;
        var ltiRule = new RuleEvaluationDto
        {
            RuleId = "BASIC-003",
            RuleName = "Loan-to-Income Ratio",
            Passed = loanToIncome <= 10.0m,
            Score = loanToIncome <= 10.0m ? 100 : -100,
            Weight = 0.25m,
            WeightedScore = (loanToIncome <= 10.0m ? 100 : -100) * 0.25m,
            Explanation = $"LTI ratio is {loanToIncome:F2}x (maximum: 10x)",
            Impact = loanToIncome <= 10.0m ? "Positive" : "Negative"
        };
        rules.Add(ltiRule);

        // Rule 4: Employment Tenure
        var tenureRule = new RuleEvaluationDto
        {
            RuleId = "BASIC-004",
            RuleName = "Employment Tenure",
            Passed = assessmentData.EmploymentMonths >= 12,
            Score = assessmentData.EmploymentMonths >= 12 ? 50 : -50,
            Weight = 0.20m,
            WeightedScore = (assessmentData.EmploymentMonths >= 12 ? 50 : -50) * 0.20m,
            Explanation = $"Employment tenure: {assessmentData.EmploymentMonths} months (minimum: 12)",
            Impact = assessmentData.EmploymentMonths >= 12 ? "Positive" : "Negative"
        };
        rules.Add(tenureRule);

        // Calculate composite score
        result.Score = rules.Sum(r => r.WeightedScore);
        result.RulesFired = rules;

        // Determine risk grade based on score
        result.Grade = result.Score switch
        {
            >= 750 => "A",
            >= 650 => "B",
            >= 550 => "C",
            >= 450 => "D",
            _ => "F"
        };

        // Determine decision based on grade
        result.Decision = result.Grade switch
        {
            "A" or "B" => "Approved",
            "C" or "D" => "ManualReview",
            _ => "Rejected"
        };

        // Calculate affordable amounts
        var maxDtiPayment = assessmentData.MonthlyIncome * 0.40m - assessmentData.ExistingDebt;
        result.AffordablePayment = Math.Max(0, maxDtiPayment);
        result.AffordableAmount = result.AffordablePayment * assessmentData.TermMonths;

        // Generate explanation
        var passedRules = rules.Count(r => r.Passed);
        var totalRules = rules.Count;
        result.Explanation = $"Assessment Result: {result.Decision} with Risk Grade {result.Grade}. " +
                           $"Composite score: {result.Score:F0}. " +
                           $"Passed {passedRules} of {totalRules} evaluation rules. " +
                           $"DTI ratio: {result.DebtToIncomeRatio:P2}. " +
                           $"Maximum affordable amount: {result.AffordableAmount:C2} ZMW.";

        _logger.LogInformation("Risk calculation complete: Grade={Grade}, Decision={Decision}, Score={Score}",
            result.Grade, result.Decision, result.Score);

        return await Task.FromResult(result);
    }

    private static decimal CalculateMonthlyPayment(decimal principal, int termMonths)
    {
        if (termMonths == 0) return 0;
        // Simple interest calculation (will be enhanced with actual interest rates later)
        var annualRate = 0.24m; // 24% default
        var monthlyRate = annualRate / 12;
        var factor = (decimal)Math.Pow((double)(1 + monthlyRate), termMonths);
        return principal * (monthlyRate * factor) / (factor - 1);
    }
}
