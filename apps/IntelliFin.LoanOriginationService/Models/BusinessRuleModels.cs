namespace IntelliFin.LoanOriginationService.Models;

public class RuleEngineResult
{
    public bool IsValid { get; set; }
    public List<ValidationError> Errors { get; set; } = new();
    public List<ValidationWarning> Warnings { get; set; } = new();
    public Dictionary<string, object> CalculatedValues { get; set; } = new();
    public string RuleSetUsed { get; set; } = string.Empty;
}

public class ValidationError
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public string Severity { get; set; } = "Error";
}

public class ValidationWarning
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
}

public class RiskCalculationResult
{
    public RiskGrade Grade { get; set; }
    public decimal Score { get; set; }
    public decimal Confidence { get; set; }
    public List<RiskFactor> Factors { get; set; } = new();
    public string Explanation { get; set; } = string.Empty;
    public bool RecommendApproval { get; set; }
    public decimal RecommendedAmount { get; set; }
    public decimal RecommendedRate { get; set; }
    public List<string> Conditions { get; set; } = new();
}

public class RiskFactor
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public decimal Weight { get; set; }
    public decimal Contribution { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class CreditBureauData
{
    public string BureauName { get; set; } = string.Empty;
    public decimal CreditScore { get; set; }
    public int TotalAccounts { get; set; }
    public int ActiveAccounts { get; set; }
    public int DefaultedAccounts { get; set; }
    public decimal TotalDebt { get; set; }
    public decimal MonthlyObligations { get; set; }
    public List<CreditAccount> Accounts { get; set; } = new();
    public DateTime LastUpdated { get; set; }
}

public class CreditAccount
{
    public string AccountNumber { get; set; } = string.Empty;
    public string LenderName { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public decimal CurrentBalance { get; set; }
    public decimal CreditLimit { get; set; }
    public string PaymentHistory { get; set; } = string.Empty;
    public int DaysInArrears { get; set; }
    public DateTime OpenedDate { get; set; }
}

public class AffordabilityAssessment
{
    public decimal MonthlyIncome { get; set; }
    public decimal MonthlyExpenses { get; set; }
    public decimal ExistingDebtPayments { get; set; }
    public decimal DisposableIncome { get; set; }
    public decimal DebtToIncomeRatio { get; set; }
    public decimal MaxAffordablePayment { get; set; }
    public decimal RecommendedLoanAmount { get; set; }
    public bool PassesAffordabilityTest { get; set; }
    public List<string> AffordabilityNotes { get; set; } = new();
}

// BoZ Compliance Models
public class BoZComplianceCheck
{
    public bool IsCompliant { get; set; }
    public List<ComplianceRequirement> Requirements { get; set; } = new();
    public List<ComplianceViolation> Violations { get; set; } = new();
    public DateTime CheckedAt { get; set; }
}

public class ComplianceRequirement
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsMet { get; set; }
    public string Evidence { get; set; } = string.Empty;
}

public class ComplianceViolation
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty; // Critical, Major, Minor
    public string Recommendation { get; set; } = string.Empty;
}

// Camunda Integration Models
public class CamundaProcessInstance
{
    public string ProcessDefinitionKey { get; set; } = "loan-approval-process";
    public string BusinessKey { get; set; } = string.Empty;
    public Dictionary<string, object> Variables { get; set; } = new();
    public string TenantId { get; set; } = string.Empty;
}

public class CamundaProcessInstanceResponse
{
    public string Id { get; set; } = string.Empty;
    public string DefinitionId { get; set; } = string.Empty;
    public string BusinessKey { get; set; } = string.Empty;
    public bool Ended { get; set; }
    public bool Suspended { get; set; }
}

public class CamundaTaskRequest
{
    public string TaskId { get; set; } = string.Empty;
    public Dictionary<string, object> Variables { get; set; } = new();
    public string AssigneeId { get; set; } = string.Empty;
}

public class CamundaHistoricTask
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Assignee { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string DeleteReason { get; set; } = string.Empty;
}

public class CamundaTask
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Assignee { get; set; } = string.Empty;
    public DateTime Created { get; set; }
    public string ProcessInstanceId { get; set; } = string.Empty;
}

public class WorkflowDecision
{
    public Guid ApplicationId { get; set; }
    public string Decision { get; set; } = string.Empty; // approve, reject, request_more_info
    public string Reason { get; set; } = string.Empty;
    public string DecisionMaker { get; set; } = string.Empty;
    public DateTime DecisionDate { get; set; }
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}

// Scoring Models
public class ScoringModel
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public List<ScoringFactor> Factors { get; set; } = new();
    public List<ScoringRule> Rules { get; set; } = new();
    public decimal MinScore { get; set; }
    public decimal MaxScore { get; set; }
    public bool IsActive { get; set; }
}

public class ScoringFactor
{
    public string Name { get; set; } = string.Empty;
    public string DataSource { get; set; } = string.Empty; // application, bureau, internal
    public decimal Weight { get; set; }
    public string Calculation { get; set; } = string.Empty;
    public List<ScoringBand> Bands { get; set; } = new();
}

public class ScoringBand
{
    public decimal MinValue { get; set; }
    public decimal MaxValue { get; set; }
    public decimal Points { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class ScoringRule
{
    public string Name { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // decline, refer, approve
    public decimal? ScoreAdjustment { get; set; }
    public string Reason { get; set; } = string.Empty;
}