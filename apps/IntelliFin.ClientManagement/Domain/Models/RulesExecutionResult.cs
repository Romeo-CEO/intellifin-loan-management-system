namespace IntelliFin.ClientManagement.Domain.Models;

/// <summary>
/// Result of executing risk scoring rules
/// </summary>
public class RulesExecutionResult
{
    /// <summary>
    /// Total score calculated from all rules
    /// </summary>
    public int TotalScore { get; set; }

    /// <summary>
    /// Detailed log of each rule execution
    /// </summary>
    public List<RuleExecution> ExecutionLog { get; set; } = new();

    /// <summary>
    /// Number of rules evaluated
    /// </summary>
    public int RulesEvaluated { get; set; }

    /// <summary>
    /// Total execution time
    /// </summary>
    public TimeSpan ExecutionTime { get; set; }

    /// <summary>
    /// Number of rules that fired (condition met)
    /// </summary>
    public int RulesFired => ExecutionLog.Count(e => e.ConditionMet);

    /// <summary>
    /// Whether any errors occurred during execution
    /// </summary>
    public bool HasErrors => ExecutionLog.Any(e => !string.IsNullOrEmpty(e.Error));
}

/// <summary>
/// Execution details for a single rule
/// </summary>
public class RuleExecution
{
    /// <summary>
    /// Rule name
    /// </summary>
    public string RuleName { get; set; } = string.Empty;

    /// <summary>
    /// Rule condition (JSONLogic expression)
    /// </summary>
    public string Condition { get; set; } = string.Empty;

    /// <summary>
    /// Whether the condition was met
    /// </summary>
    public bool ConditionMet { get; set; }

    /// <summary>
    /// Points awarded (0 if condition not met)
    /// </summary>
    public int PointsAwarded { get; set; }

    /// <summary>
    /// When the rule was executed
    /// </summary>
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Error message if rule execution failed
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Rule category (if applicable)
    /// </summary>
    public string? Category { get; set; }
}
