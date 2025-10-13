namespace IntelliFin.AdminService.Contracts.Responses;

public sealed record SodComplianceReportDto(
    DateTime ReportPeriodStart,
    DateTime ReportPeriodEnd,
    int TotalActiveExceptions,
    int ExpiredExceptionsCount,
    int BlockedAssignmentsCount,
    IReadOnlyCollection<SodExceptionSummaryDto> ActiveExceptions,
    DateTime GeneratedAt);
