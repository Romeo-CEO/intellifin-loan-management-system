namespace IntelliFin.AdminService.Contracts.Responses;

public sealed record SodExceptionResponse(
    Guid ExceptionId,
    string Status,
    string Message,
    DateTime? EstimatedReviewTime);
