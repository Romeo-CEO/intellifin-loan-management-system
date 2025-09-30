using System.Reflection;

namespace IntelliFin.IdentityService.Constants;

/// <summary>
/// Static rule constants for rule-based authorization
/// These define the available business rules that can be configured by tenants
/// </summary>
public static class SystemRules
{
    #region Financial Rules
    /// <summary>
    /// Maximum loan amount a role can approve (ZMW)
    /// </summary>
    public const string LoanApprovalLimit = "loan_approval_limit";

    /// <summary>
    /// Maximum daily disbursement amount (ZMW)
    /// </summary>
    public const string DailyDisbursementLimit = "daily_disbursement_limit";

    /// <summary>
    /// Maximum single transaction amount (ZMW)
    /// </summary>
    public const string MaxTransactionAmount = "max_transaction_amount";

    /// <summary>
    /// Monthly lending volume limit (ZMW)
    /// </summary>
    public const string MonthlyLendingLimit = "monthly_lending_limit";

    /// <summary>
    /// Cash handling limit per transaction (ZMW)
    /// </summary>
    public const string CashHandlingLimit = "cash_handling_limit";

    /// <summary>
    /// Write-off approval limit (ZMW)
    /// </summary>
    public const string WriteOffLimit = "write_off_limit";
    #endregion

    #region Risk Management Rules
    /// <summary>
    /// Maximum risk grade that can be approved (A, B, C, D, F)
    /// </summary>
    public const string MaxRiskGrade = "max_risk_grade";

    /// <summary>
    /// Number of additional approvals required
    /// </summary>
    public const string RequiredApprovalCount = "required_approval_count";

    /// <summary>
    /// Maximum loan-to-value ratio (percentage)
    /// </summary>
    public const string MaxLoanToValueRatio = "max_ltv_ratio";

    /// <summary>
    /// Minimum credit score required
    /// </summary>
    public const string MinCreditScore = "min_credit_score";

    /// <summary>
    /// Maximum debt-to-income ratio (percentage)
    /// </summary>
    public const string MaxDebtToIncomeRatio = "max_dti_ratio";

    /// <summary>
    /// Portfolio concentration limit (percentage)
    /// </summary>
    public const string PortfolioConcentrationLimit = "portfolio_concentration_limit";
    #endregion

    #region Operational Rules
    /// <summary>
    /// Maximum number of clients assigned to a user
    /// </summary>
    public const string MaxClientAssignments = "max_client_assignments";

    /// <summary>
    /// Branch access scope (branch IDs)
    /// </summary>
    public const string BranchAccessScope = "branch_access_scope";

    /// <summary>
    /// Working hours constraint (HH:MM-HH:MM)
    /// </summary>
    public const string WorkingHours = "working_hours";

    /// <summary>
    /// Maximum concurrent sessions allowed
    /// </summary>
    public const string MaxConcurrentSessions = "max_concurrent_sessions";

    /// <summary>
    /// IP address restrictions
    /// </summary>
    public const string IpAddressRestrictions = "ip_address_restrictions";

    /// <summary>
    /// Geographic location restrictions
    /// </summary>
    public const string GeographicRestrictions = "geographic_restrictions";
    #endregion

    #region Compliance Rules
    /// <summary>
    /// Mandatory approval delay in hours
    /// </summary>
    public const string MandatoryApprovalDelay = "mandatory_approval_delay";

    /// <summary>
    /// Audit trail detail level (basic, detailed, comprehensive)
    /// </summary>
    public const string AuditTrailLevel = "audit_trail_level";

    /// <summary>
    /// Data retention period in days
    /// </summary>
    public const string DataRetentionPeriod = "data_retention_period";

    /// <summary>
    /// KYC verification level required (basic, enhanced, premium)
    /// </summary>
    public const string KycVerificationLevel = "kyc_verification_level";

    /// <summary>
    /// AML risk threshold (low, medium, high)
    /// </summary>
    public const string AmlRiskThreshold = "aml_risk_threshold";

    /// <summary>
    /// Regulatory reporting access level
    /// </summary>
    public const string RegulatoryReportingLevel = "regulatory_reporting_level";
    #endregion

    #region PMEC Integration Rules
    /// <summary>
    /// Maximum payroll deduction percentage
    /// </summary>
    public const string MaxPayrollDeductionPercent = "max_payroll_deduction_percent";

    /// <summary>
    /// PMEC verification requirements (auto, manual, enhanced)
    /// </summary>
    public const string PmecVerificationLevel = "pmec_verification_level";

    /// <summary>
    /// Government employee grade restrictions
    /// </summary>
    public const string AllowedEmployeeGrades = "allowed_employee_grades";

    /// <summary>
    /// Ministry/department access restrictions
    /// </summary>
    public const string MinistryAccessScope = "ministry_access_scope";
    #endregion

    #region Loan Product Rules
    /// <summary>
    /// Allowed loan product types
    /// </summary>
    public const string AllowedLoanProducts = "allowed_loan_products";

    /// <summary>
    /// Maximum loan term in months
    /// </summary>
    public const string MaxLoanTerm = "max_loan_term";

    /// <summary>
    /// Interest rate adjustment authority (percentage points)
    /// </summary>
    public const string InterestRateAdjustmentLimit = "interest_rate_adjustment_limit";

    /// <summary>
    /// Collateral value requirements (percentage of loan)
    /// </summary>
    public const string MinCollateralValue = "min_collateral_value";

    /// <summary>
    /// Grace period extension authority (days)
    /// </summary>
    public const string MaxGracePeriodExtension = "max_grace_period_extension";
    #endregion

    #region Digital Banking Rules
    /// <summary>
    /// Mobile transaction limits (ZMW)
    /// </summary>
    public const string MobileTransactionLimit = "mobile_transaction_limit";

    /// <summary>
    /// Digital payment approval threshold (ZMW)
    /// </summary>
    public const string DigitalPaymentThreshold = "digital_payment_threshold";

    /// <summary>
    /// API rate limiting (requests per minute)
    /// </summary>
    public const string ApiRateLimit = "api_rate_limit";

    /// <summary>
    /// Digital channel access hours
    /// </summary>
    public const string DigitalChannelHours = "digital_channel_hours";
    #endregion

    /// <summary>
    /// Gets all rule constants defined in this class using reflection
    /// </summary>
    public static string[] GetAllRules()
    {
        return typeof(SystemRules)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => f.GetValue(null) as string)
            .Where(v => !string.IsNullOrEmpty(v))
            .OrderBy(v => v)
            .ToArray()!;
    }

    /// <summary>
    /// Validates if a rule string is a valid system rule
    /// </summary>
    public static bool IsValidRule(string rule)
    {
        if (string.IsNullOrWhiteSpace(rule))
            return false;

        return GetAllRules().Contains(rule);
    }

    /// <summary>
    /// Gets rules by category (extracted from rule prefix)
    /// </summary>
    public static Dictionary<string, string[]> GetRulesByCategory()
    {
        var allRules = GetAllRules();
        var categories = new Dictionary<string, List<string>>();

        foreach (var rule in allRules)
        {
            var category = GetRuleCategory(rule);
            if (!categories.ContainsKey(category))
            {
                categories[category] = new List<string>();
            }
            categories[category].Add(rule);
        }

        return categories.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray());
    }

    /// <summary>
    /// Gets the category for a specific rule
    /// </summary>
    public static string GetRuleCategory(string rule)
    {
        return rule switch
        {
            var r when r.Contains("approval_limit") || r.Contains("disbursement") || r.Contains("transaction") || 
                      r.Contains("lending") || r.Contains("cash") || r.Contains("write_off") => "Financial",
            var r when r.Contains("risk") || r.Contains("credit") || r.Contains("ltv") || 
                      r.Contains("dti") || r.Contains("portfolio") => "Risk Management",
            var r when r.Contains("client") || r.Contains("branch") || r.Contains("working") || 
                      r.Contains("sessions") || r.Contains("ip_address") || r.Contains("geographic") => "Operational",
            var r when r.Contains("approval_delay") || r.Contains("audit") || r.Contains("retention") || 
                      r.Contains("kyc") || r.Contains("aml") || r.Contains("regulatory") => "Compliance",
            var r when r.Contains("payroll") || r.Contains("pmec") || r.Contains("employee") || 
                      r.Contains("ministry") => "PMEC Integration",
            var r when r.Contains("loan_products") || r.Contains("loan_term") || r.Contains("interest") || 
                      r.Contains("collateral") || r.Contains("grace_period") => "Loan Products",
            var r when r.Contains("mobile") || r.Contains("digital") || r.Contains("api") => "Digital Banking",
            _ => "General"
        };
    }

    /// <summary>
    /// Gets the display name for a rule
    /// </summary>
    public static string GetRuleDisplayName(string rule)
    {
        return rule switch
        {
            LoanApprovalLimit => "Loan Approval Limit",
            DailyDisbursementLimit => "Daily Disbursement Limit",
            MaxTransactionAmount => "Maximum Transaction Amount",
            MonthlyLendingLimit => "Monthly Lending Limit",
            CashHandlingLimit => "Cash Handling Limit",
            WriteOffLimit => "Write-off Approval Limit",
            MaxRiskGrade => "Maximum Risk Grade",
            RequiredApprovalCount => "Required Approval Count",
            MaxLoanToValueRatio => "Maximum Loan-to-Value Ratio",
            MinCreditScore => "Minimum Credit Score",
            MaxDebtToIncomeRatio => "Maximum Debt-to-Income Ratio",
            PortfolioConcentrationLimit => "Portfolio Concentration Limit",
            MaxClientAssignments => "Maximum Client Assignments",
            BranchAccessScope => "Branch Access Scope",
            WorkingHours => "Working Hours",
            MaxConcurrentSessions => "Maximum Concurrent Sessions",
            IpAddressRestrictions => "IP Address Restrictions",
            GeographicRestrictions => "Geographic Restrictions",
            MandatoryApprovalDelay => "Mandatory Approval Delay",
            AuditTrailLevel => "Audit Trail Level",
            DataRetentionPeriod => "Data Retention Period",
            KycVerificationLevel => "KYC Verification Level",
            AmlRiskThreshold => "AML Risk Threshold",
            RegulatoryReportingLevel => "Regulatory Reporting Level",
            MaxPayrollDeductionPercent => "Maximum Payroll Deduction %",
            PmecVerificationLevel => "PMEC Verification Level",
            AllowedEmployeeGrades => "Allowed Employee Grades",
            MinistryAccessScope => "Ministry Access Scope",
            AllowedLoanProducts => "Allowed Loan Products",
            MaxLoanTerm => "Maximum Loan Term",
            InterestRateAdjustmentLimit => "Interest Rate Adjustment Limit",
            MinCollateralValue => "Minimum Collateral Value",
            MaxGracePeriodExtension => "Maximum Grace Period Extension",
            MobileTransactionLimit => "Mobile Transaction Limit",
            DigitalPaymentThreshold => "Digital Payment Threshold",
            ApiRateLimit => "API Rate Limit",
            DigitalChannelHours => "Digital Channel Hours",
            _ => rule.Replace("_", " ").Replace(" ", " ") // Convert snake_case to Title Case
        };
    }

    /// <summary>
    /// Gets the description for a rule
    /// </summary>
    public static string GetRuleDescription(string rule)
    {
        return rule switch
        {
            LoanApprovalLimit => "Maximum loan amount (in ZMW) that this role can approve without additional authorization",
            DailyDisbursementLimit => "Maximum daily disbursement amount (in ZMW) for this role",
            MaxRiskGrade => "Highest risk grade (A, B, C, D, F) that this role can approve",
            RequiredApprovalCount => "Number of additional approvals required for this role's actions",
            BranchAccessScope => "Branches that this role can access (comma-separated branch IDs)",
            WorkingHours => "Time restrictions for role access (format: HH:MM-HH:MM)",
            MandatoryApprovalDelay => "Minimum delay (in hours) before approval can be granted",
            AuditTrailLevel => "Level of audit detail required (basic, detailed, comprehensive)",
            MaxPayrollDeductionPercent => "Maximum percentage of salary that can be deducted for loan payments",
            AllowedLoanProducts => "Loan product types that this role can process",
            MaxLoanTerm => "Maximum loan term (in months) that this role can approve",
            MobileTransactionLimit => "Maximum transaction amount (in ZMW) for mobile banking operations",
            _ => $"Business rule configuration for {GetRuleDisplayName(rule).ToLower()}"
        };
    }
}