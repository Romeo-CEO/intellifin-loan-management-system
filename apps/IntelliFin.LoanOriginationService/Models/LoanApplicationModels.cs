using System.ComponentModel.DataAnnotations;

namespace IntelliFin.LoanOriginationService.Models;

public class LoanApplication
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public decimal RequestedAmount { get; set; }
    public int TermMonths { get; set; }
    public decimal InterestRate { get; set; }
    public LoanApplicationStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? ApprovedBy { get; set; }
    
    // Dynamic application data based on product type
    public Dictionary<string, object> ApplicationData { get; set; } = new();
    
    // Credit assessment results
    public CreditAssessment? CreditAssessment { get; set; }
    
    // Workflow tracking
    public string WorkflowInstanceId { get; set; } = string.Empty;
    public List<WorkflowStep> WorkflowSteps { get; set; } = new();
}

public class CreditAssessment
{
    public Guid Id { get; set; }
    public Guid LoanApplicationId { get; set; }
    public RiskGrade RiskGrade { get; set; }
    public decimal CreditScore { get; set; }
    public decimal DebtToIncomeRatio { get; set; }
    public decimal PaymentCapacity { get; set; }
    public bool HasCreditBureauData { get; set; }
    public List<CreditFactor> CreditFactors { get; set; } = new();
    public List<RiskIndicator> RiskIndicators { get; set; } = new();
    public string ScoreExplanation { get; set; } = string.Empty;
    public DateTime AssessedAt { get; set; }
    public string AssessedBy { get; set; } = string.Empty;
}

public class CreditFactor
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal Score { get; set; }
    public string Impact { get; set; } = string.Empty; // Positive, Negative, Neutral
}

public class RiskIndicator
{
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public RiskLevel Level { get; set; }
    public decimal Impact { get; set; }
}

public class WorkflowStep
{
    public Guid Id { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string AssignedTo { get; set; } = string.Empty;
    public WorkflowStepStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Comments { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
}

public class LoanProduct
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public int MinTermMonths { get; set; }
    public int MaxTermMonths { get; set; }
    public decimal BaseInterestRate { get; set; }
    public bool IsActive { get; set; }
    public List<ApplicationField> RequiredFields { get; set; } = new();
    public List<BusinessRule> ValidationRules { get; set; } = new();
}

public class ApplicationField
{
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // text, number, date, select, file
    public bool IsRequired { get; set; }
    public string? ValidationPattern { get; set; }
    public List<string>? Options { get; set; }
    public int Order { get; set; }
    public string? HelpText { get; set; }
}

public class BusinessRule
{
    public string Name { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty; // validation, calculation, approval
    public int Priority { get; set; }
}

// DTOs for API requests/responses
public class CreateLoanApplicationRequest
{
    [Required]
    public Guid ClientId { get; set; }
    
    [Required]
    public string ProductCode { get; set; } = string.Empty;
    
    [Range(1000, double.MaxValue, ErrorMessage = "Amount must be at least 1000")]
    public decimal RequestedAmount { get; set; }
    
    [Range(1, 360, ErrorMessage = "Term must be between 1 and 360 months")]
    public int TermMonths { get; set; }
    
    public Dictionary<string, object> ApplicationData { get; set; } = new();
}

public class LoanApplicationResponse
{
    public Guid Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal RequestedAmount { get; set; }
    public int TermMonths { get; set; }
    public LoanApplicationStatus Status { get; set; }
    public string StatusDescription { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public CreditAssessmentSummary? CreditAssessment { get; set; }
    public List<string> RequiredDocuments { get; set; } = new();
    public List<WorkflowStepSummary> WorkflowSteps { get; set; } = new();
}

public class CreditAssessmentSummary
{
    public RiskGrade RiskGrade { get; set; }
    public decimal CreditScore { get; set; }
    public string ScoreExplanation { get; set; } = string.Empty;
    public List<string> KeyFactors { get; set; } = new();
    public bool RecommendedForApproval { get; set; }
}

public class WorkflowStepSummary
{
    public string StepName { get; set; } = string.Empty;
    public WorkflowStepStatus Status { get; set; }
    public string? AssignedTo { get; set; }
    public DateTime? DueDate { get; set; }
}

public class CreditAssessmentRequest
{
    [Required]
    public Guid LoanApplicationId { get; set; }
    
    [Required]
    public Guid ClientId { get; set; }
    
    public Dictionary<string, object> ClientData { get; set; } = new();
}

// Enums
public enum LoanApplicationStatus
{
    Draft = 0,
    Submitted = 1,
    UnderReview = 2,
    CreditAssessment = 3,
    PendingApproval = 4,
    Approved = 5,
    Rejected = 6,
    Withdrawn = 7,
    Expired = 8
}

public enum RiskGrade
{
    A = 1, // Excellent (750+)
    B = 2, // Good (650-749)
    C = 3, // Fair (550-649)
    D = 4, // Poor (450-549)
    E = 5, // Very Poor (350-449)
    F = 6  // Unacceptable (<350)
}

public enum RiskLevel
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum WorkflowStepStatus
{
    Pending = 0,
    InProgress = 1,
    Completed = 2,
    Skipped = 3,
    Rejected = 4
}