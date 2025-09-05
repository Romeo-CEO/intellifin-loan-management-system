namespace IntelliFin.Shared.Infrastructure.Messaging.Contracts;

public record LoanApplicationCreated(
    Guid ApplicationId,
    Guid ClientId,
    decimal Amount,
    int TermMonths,
    string ProductCode,
    DateTime CreatedAtUtc
);

