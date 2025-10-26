using IntelliFin.ClientManagement.Models;

namespace IntelliFin.ClientManagement.Services;

/// <summary>
/// Service for building personalization data for notification templates
/// Handles formatting and data transformation
/// </summary>
public class TemplatePersonalizer
{
    private readonly ILogger<TemplatePersonalizer> _logger;

    // Configuration (would come from appsettings in production)
    private const string DefaultBranchContact = "0977-123-456";
    private const string DefaultComplianceContact = "compliance@intellifin.zm";
    private const string CompanyName = "IntelliFin";

    public TemplatePersonalizer(ILogger<TemplatePersonalizer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Builds personalization data for KYC approved notification
    /// </summary>
    public Dictionary<string, object> BuildKycApprovedData(
        string clientName,
        DateTime completionDate,
        string? nextSteps = null)
    {
        var data = new Dictionary<string, object>
        {
            ["ClientName"] = clientName,
            ["CompletionDate"] = completionDate.ToString("MMMM dd, yyyy"),
            ["NextSteps"] = nextSteps ?? "Your loan application will proceed to the next stage",
            ["BranchContact"] = DefaultBranchContact,
            ["CompanyName"] = CompanyName
        };

        _logger.LogDebug("Built KYC approved personalization data for {ClientName}", clientName);
        return data;
    }

    /// <summary>
    /// Builds personalization data for KYC rejected notification
    /// </summary>
    public Dictionary<string, object> BuildKycRejectedData(
        string clientName,
        DateTime rejectionDate,
        string rejectionReason,
        string? applicationId = null)
    {
        // Sanitize rejection reason for customer communication
        var customerFriendlyReason = SanitizeRejectionReason(rejectionReason);

        var data = new Dictionary<string, object>
        {
            ["ClientName"] = clientName,
            ["RejectionDate"] = rejectionDate.ToString("MMMM dd, yyyy"),
            ["RejectionReason"] = customerFriendlyReason,
            ["ReapplyInstructions"] = "Please visit your branch for guidance on reapplication",
            ["BranchContact"] = DefaultBranchContact,
            ["ApplicationId"] = applicationId ?? "N/A",
            ["CompanyName"] = CompanyName
        };

        _logger.LogDebug("Built KYC rejected personalization data for {ClientName}", clientName);
        return data;
    }

    /// <summary>
    /// Builds personalization data for EDD escalation notification
    /// </summary>
    public Dictionary<string, object> BuildEddEscalationData(
        string clientName,
        DateTime escalationDate,
        string eddReason,
        string expectedTimeframe = "5-7 business days")
    {
        var data = new Dictionary<string, object>
        {
            ["ClientName"] = clientName,
            ["EscalationDate"] = escalationDate.ToString("MMMM dd, yyyy"),
            ["ExpectedTimeframe"] = expectedTimeframe,
            ["RequiredActions"] = "No action required at this time",
            ["ContactInformation"] = DefaultComplianceContact,
            ["CompanyName"] = CompanyName
        };

        _logger.LogDebug("Built EDD escalation personalization data for {ClientName}", clientName);
        return data;
    }

    /// <summary>
    /// Builds personalization data for EDD approved notification
    /// </summary>
    public Dictionary<string, object> BuildEddApprovedData(
        string clientName,
        DateTime approvalDate,
        string riskAcceptanceLevel,
        string? nextSteps = null)
    {
        var riskLevelDescription = riskAcceptanceLevel switch
        {
            "Standard" => "standard monitoring",
            "EnhancedMonitoring" => "enhanced monitoring",
            "RestrictedServices" => "restricted services",
            _ => "standard monitoring"
        };

        var data = new Dictionary<string, object>
        {
            ["ClientName"] = clientName,
            ["ApprovalDate"] = approvalDate.ToString("MMMM dd, yyyy"),
            ["RiskAcceptanceLevel"] = riskLevelDescription,
            ["NextSteps"] = nextSteps ?? "Your loan application will proceed",
            ["BranchContact"] = DefaultBranchContact,
            ["CompanyName"] = CompanyName
        };

        _logger.LogDebug("Built EDD approved personalization data for {ClientName}", clientName);
        return data;
    }

    /// <summary>
    /// Builds personalization data for EDD rejected notification
    /// </summary>
    public Dictionary<string, object> BuildEddRejectedData(
        string clientName,
        DateTime rejectionDate,
        string rejectionReason,
        string rejectionStage)
    {
        // Sanitize rejection reason
        var customerFriendlyReason = SanitizeRejectionReason(rejectionReason);

        var data = new Dictionary<string, object>
        {
            ["ClientName"] = clientName,
            ["RejectionDate"] = rejectionDate.ToString("MMMM dd, yyyy"),
            ["RejectionReason"] = customerFriendlyReason,
            ["SupportInformation"] = "Please contact your branch for more information",
            ["BranchContact"] = DefaultBranchContact,
            ["CompanyName"] = CompanyName
        };

        _logger.LogDebug("Built EDD rejected personalization data for {ClientName}", clientName);
        return data;
    }

    /// <summary>
    /// Sanitizes internal rejection reasons for customer-friendly messaging
    /// Removes technical details and provides helpful guidance
    /// </summary>
    private string SanitizeRejectionReason(string internalReason)
    {
        // Map internal reasons to customer-friendly messages
        var sanitizedReasons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Sanctions"] = "Additional documentation required",
            ["PEP"] = "Additional compliance review required",
            ["High Risk"] = "Additional verification required",
            ["Multiple Medium Risk"] = "Additional information needed",
            ["Incomplete Documents"] = "Missing required documents",
            ["Invalid NRC"] = "National Registration Card verification required",
            ["Invalid Address"] = "Proof of address verification required",
            ["Failed AML Screening"] = "Additional verification required",
            ["Compliance Review"] = "Additional review required",
            ["CEO Rejection"] = "Application not approved at this time"
        };

        // Check if we have a mapping
        foreach (var (key, value) in sanitizedReasons)
        {
            if (internalReason.Contains(key, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        // Default fallback for unknown reasons
        return "Additional verification required. Please contact your branch for details";
    }

    /// <summary>
    /// Formats a date for display in notifications
    /// </summary>
    public string FormatDate(DateTime date, string format = "MMMM dd, yyyy")
    {
        return date.ToString(format);
    }

    /// <summary>
    /// Validates that all required placeholders are present
    /// </summary>
    public bool ValidatePersonalizationData(
        Dictionary<string, object> data,
        string[] requiredFields)
    {
        foreach (var field in requiredFields)
        {
            if (!data.ContainsKey(field) || string.IsNullOrWhiteSpace(data[field]?.ToString()))
            {
                _logger.LogWarning("Missing required personalization field: {Field}", field);
                return false;
            }
        }

        return true;
    }
}
