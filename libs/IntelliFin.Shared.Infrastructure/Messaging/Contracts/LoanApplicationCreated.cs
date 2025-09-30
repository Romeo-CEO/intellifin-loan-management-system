namespace IntelliFin.Shared.Infrastructure.Messaging.Contracts;

public interface IBusinessEvent
{
    Guid EventId { get; }
    DateTime EventTimestamp { get; }
    string EventType { get; }
    string SourceService { get; }
}

public record LoanApplicationCreated(
    Guid ApplicationId,
    Guid ClientId,
    decimal Amount,
    int TermMonths,
    string ProductCode,
    DateTime CreatedAtUtc
) : IBusinessEvent
{
    public Guid EventId => Guid.NewGuid(); // TODO: Should be passed from calling service
    public DateTime EventTimestamp => CreatedAtUtc;
    public string EventType => "LoanApplicationCreated";
    public string SourceService => "LoanOriginationService";
}
