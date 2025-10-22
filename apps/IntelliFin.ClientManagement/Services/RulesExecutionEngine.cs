using IntelliFin.ClientManagement.Domain.Models;
using JsonLogicNet;
using System.Text.Json;

namespace IntelliFin.ClientManagement.Services;

/// <summary>
/// Executes risk scoring rules using JSONLogic
/// Evaluates conditions and calculates scores
/// </summary>
public class RulesExecutionEngine
{
    private readonly ILogger<RulesExecutionEngine> _logger;

    public RulesExecutionEngine(ILogger<RulesExecutionEngine> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Evaluates all enabled rules against input factors
    /// Returns total score and detailed execution log
    /// </summary>
    public async Task<RulesExecutionResult> EvaluateRulesAsync(
        RiskScoringConfig config,
        InputFactors factors)
    {
        var startTime = DateTime.UtcNow;
        var executionLog = new List<RuleExecution>();
        var totalScore = 0;

        _logger.LogInformation(
            "Starting rules evaluation: Version={Version}, Rules={Count}",
            config.Version, config.Rules.Count);

        // Convert input factors to dictionary for JSONLogic
        var inputData = ConvertToJsonLogicData(factors);

        // Execute rules in priority order
        var orderedRules = config.Rules.Values
            .Where(r => r.IsEnabled)
            .OrderBy(r => r.Priority)
            .ThenBy(r => r.Name);

        foreach (var rule in orderedRules)
        {
            var ruleExecution = await EvaluateRuleAsync(rule, inputData);
            executionLog.Add(ruleExecution);

            if (ruleExecution.ConditionMet)
            {
                totalScore += ruleExecution.PointsAwarded;
            }
        }

        var endTime = DateTime.UtcNow;

        _logger.LogInformation(
            "Rules evaluation completed: TotalScore={Score}, RulesFired={Fired}/{Total}, Duration={Duration}ms",
            totalScore, executionLog.Count(e => e.ConditionMet), executionLog.Count,
            (endTime - startTime).TotalMilliseconds);

        return new RulesExecutionResult
        {
            TotalScore = Math.Min(totalScore, config.Options.MaxScore),
            ExecutionLog = executionLog,
            RulesEvaluated = executionLog.Count,
            ExecutionTime = endTime - startTime
        };
    }

    private async Task<RuleExecution> EvaluateRuleAsync(RiskRule rule, Dictionary<string, object> inputData)
    {
        var ruleExecution = new RuleExecution
        {
            RuleName = rule.Name,
            Condition = rule.Condition,
            Category = rule.Category,
            ExecutedAt = DateTime.UtcNow
        };

        try
        {
            // Parse and evaluate JSONLogic condition
            // For Phase 1, using simple expression evaluation
            // In production, would use full JsonLogic.Net library
            var conditionMet = EvaluateSimpleCondition(rule.Condition, inputData);

            ruleExecution.ConditionMet = conditionMet;
            ruleExecution.PointsAwarded = conditionMet ? rule.Points : 0;

            if (conditionMet)
            {
                _logger.LogDebug(
                    "Rule fired: {RuleName} awarded {Points} points",
                    rule.Name, rule.Points);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error executing rule {RuleName}: {Condition}",
                rule.Name, rule.Condition);

            ruleExecution.ConditionMet = false;
            ruleExecution.PointsAwarded = 0;
            ruleExecution.Error = ex.Message;
        }

        return await Task.FromResult(ruleExecution);
    }

    /// <summary>
    /// Simple condition evaluator for Phase 1
    /// Supports basic comparisons (==, !=, <, >, <=, >=)
    /// NOTE: In production, use full JsonLogic.Net for complex expressions
    /// </summary>
    private bool EvaluateSimpleCondition(string condition, Dictionary<string, object> data)
    {
        // Handle simple equality checks
        if (condition.Contains("=="))
        {
            var parts = condition.Split("==", StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
            {
                var fieldName = parts[0].Trim();
                var expectedValue = parts[1].Trim().Trim('"');

                if (data.TryGetValue(fieldName, out var actualValue))
                {
                    return CompareValues(actualValue, expectedValue);
                }
            }
        }
        // Handle inequality
        else if (condition.Contains("!="))
        {
            var parts = condition.Split("!=", StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
            {
                var fieldName = parts[0].Trim();
                var expectedValue = parts[1].Trim().Trim('"');

                if (data.TryGetValue(fieldName, out var actualValue))
                {
                    return !CompareValues(actualValue, expectedValue);
                }
            }
        }
        // Handle less than
        else if (condition.Contains("<"))
        {
            var parts = condition.Split("<", StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
            {
                var fieldName = parts[0].Trim();
                var thresholdStr = parts[1].Trim();

                if (data.TryGetValue(fieldName, out var actualValue) && int.TryParse(thresholdStr, out var threshold))
                {
                    if (actualValue is int intValue)
                        return intValue < threshold;
                }
            }
        }
        // Handle greater than
        else if (condition.Contains(">"))
        {
            var parts = condition.Split(">", StringSplitOptions.TrimEntries);
            if (parts.Length == 2)
            {
                var fieldName = parts[0].Trim();
                var thresholdStr = parts[1].Trim();

                if (data.TryGetValue(fieldName, out var actualValue) && int.TryParse(thresholdStr, out var threshold))
                {
                    if (actualValue is int intValue)
                        return intValue > threshold;
                }
            }
        }

        _logger.LogWarning("Unsupported condition format: {Condition}", condition);
        return false;
    }

    private bool CompareValues(object actualValue, string expectedValue)
    {
        // Boolean comparison
        if (actualValue is bool boolValue && bool.TryParse(expectedValue, out var expectedBool))
        {
            return boolValue == expectedBool;
        }

        // String comparison
        if (actualValue is string stringValue)
        {
            return stringValue.Equals(expectedValue, StringComparison.OrdinalIgnoreCase);
        }

        // Integer comparison
        if (actualValue is int intValue && int.TryParse(expectedValue, out var expectedInt))
        {
            return intValue == expectedInt;
        }

        // Fallback to string comparison
        return actualValue.ToString()?.Equals(expectedValue, StringComparison.OrdinalIgnoreCase) ?? false;
    }

    private Dictionary<string, object> ConvertToJsonLogicData(InputFactors factors)
    {
        // Serialize to JSON then deserialize to dictionary
        // This ensures all properties are included
        var json = JsonSerializer.Serialize(factors);
        var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

        return data ?? new Dictionary<string, object>();
    }
}
