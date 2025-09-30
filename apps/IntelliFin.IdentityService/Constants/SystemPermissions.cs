using System.Reflection;

namespace IntelliFin.IdentityService.Constants;

/// <summary>
/// Static permission constants for compile-time safety and centralized management.
/// These are the atomic "Lego bricks" that tenants can compose into custom roles.
/// </summary>
public static class SystemPermissions
{
    #region Client Management
    public const string ClientsView = "clients:view";
    public const string ClientsCreate = "clients:create";
    public const string ClientsEdit = "clients:edit";
    public const string ClientsDelete = "clients:delete";
    public const string ClientsViewPii = "clients:view_pii";
    public const string ClientsEditContactInfo = "clients:edit_contact_info";
    public const string ClientsMerge = "clients:merge";
    public const string ClientsExport = "clients:export";
    #endregion

    #region Loan Management
    public const string LoansView = "loans:view";
    public const string LoansCreate = "loans:create";
    public const string LoansEdit = "loans:edit";
    public const string LoansApprove = "loans:approve";
    public const string LoansReject = "loans:reject";
    public const string LoansDisburse = "loans:disburse";
    public const string LoansClose = "loans:close";
    public const string LoansRestructure = "loans:restructure";
    public const string LoansWriteOff = "loans:write_off";
    public const string LoansApproveHighValue = "loans:approve_high_value";
    public const string LoansApproveEmergency = "loans:approve_emergency";
    #endregion

    #region Loan Applications
    public const string LoanApplicationsView = "loan_applications:view";
    public const string LoanApplicationsProcess = "loan_applications:process";
    public const string LoanApplicationsReview = "loan_applications:review";
    public const string LoanApplicationsAssign = "loan_applications:assign";
    #endregion

    #region Credit Assessment
    public const string CreditReportsView = "credit_reports:view";
    public const string CreditReportsRequest = "credit_reports:request";
    public const string CreditReportsExport = "credit_reports:export";
    public const string RiskAssessmentPerform = "risk_assessment:perform";
    public const string RiskAssessmentOverride = "risk_assessment:override";
    #endregion

    #region Collections
    public const string CollectionsView = "collections:view";
    public const string CollectionsProcess = "collections:process";
    public const string CollectionsSchedule = "collections:schedule";
    public const string CollectionsException = "collections:exception";
    public const string PaymentsRecord = "payments:record";
    public const string PaymentsReverse = "payments:reverse";
    public const string PaymentsAllocate = "payments:allocate";
    #endregion

    #region Financial/GL Management
    public const string GlView = "gl:view";
    public const string GlPost = "gl:post";
    public const string GlReverse = "gl:reverse";
    public const string GlClose = "gl:close";
    public const string GlAccountsManage = "gl:accounts_manage";
    public const string FinancialReportsView = "financial_reports:view";
    public const string FinancialReportsGenerate = "financial_reports:generate";
    #endregion

    #region Reporting
    public const string ReportsView = "reports:view";
    public const string ReportsGenerate = "reports:generate";
    public const string ReportsGeneratePortfolio = "reports:generate_portfolio";
    public const string ReportsViewSensitive = "reports:view_sensitive";
    public const string ReportsExport = "reports:export";
    public const string ReportsSchedule = "reports:schedule";
    public const string ReportsBoz = "reports:boz";
    #endregion

    #region Communications
    public const string CommunicationsView = "communications:view";
    public const string CommunicationsSend = "communications:send";
    public const string CommunicationsTemplateManage = "communications:template_manage";
    public const string CommunicationsPreferencesManage = "communications:preferences_manage";
    #endregion

    #region System Administration
    public const string SystemConfigView = "system:config_view";
    public const string SystemConfigEdit = "system:config_edit";
    public const string SystemUsersManage = "system:users_manage";
    public const string SystemRolesManage = "system:roles_manage";
    public const string SystemBackup = "system:backup";
    public const string SystemRestore = "system:restore";
    public const string SystemAdvancedConfig = "system:advanced_config";
    #endregion

    #region Audit and Compliance
    public const string AuditTrailView = "audit_trail:view";
    public const string AuditTrailCreate = "audit_trail:create";
    public const string AuditTrailAdvanced = "audit_trail:advanced";
    public const string ComplianceView = "compliance:view";
    public const string ComplianceManage = "compliance:manage";
    public const string ComplianceReports = "compliance:reports";
    #endregion

    #region Branch Operations
    public const string BranchView = "branch:view";
    public const string BranchManage = "branch:manage";
    public const string BranchSwitchContext = "branch:switch_context";
    public const string BranchReports = "branch:reports";
    #endregion

    #region Digital Banking (Future)
    public const string MobileApprove = "mobile:approve";
    public const string DigitalPaymentsProcess = "digital_payments:process";
    public const string MobileTransferApprove = "mobile:transfer_approve";
    #endregion

    #region Platform Administration (Platform Plane Only)
    public const string PlatformTenantsManage = "platform:tenants_manage";
    public const string PlatformSystemMonitor = "platform:system_monitor";
    public const string PlatformEmergencyAccess = "platform:emergency_access";
    public const string PlatformAnalytics = "platform:analytics";
    public const string PlatformConfigGlobal = "platform:config_global";
    #endregion

    /// <summary>
    /// Gets all permission constants defined in this class using reflection
    /// </summary>
    public static string[] GetAllPermissions()
    {
        return typeof(SystemPermissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => f.GetValue(null) as string)
            .Where(v => !string.IsNullOrEmpty(v))
            .OrderBy(v => v)
            .ToArray()!;
    }

    /// <summary>
    /// Validates if a permission string is a valid system permission
    /// </summary>
    public static bool IsValidPermission(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission))
            return false;

        return GetAllPermissions().Contains(permission);
    }

    /// <summary>
    /// Gets permissions by category (extracted from permission prefix)
    /// </summary>
    public static Dictionary<string, string[]> GetPermissionsByCategory()
    {
        var allPermissions = GetAllPermissions();
        return allPermissions
            .GroupBy(p => p.Split(':')[0])
            .ToDictionary(g => g.Key, g => g.ToArray());
    }
}