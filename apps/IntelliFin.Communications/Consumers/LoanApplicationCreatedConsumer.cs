using IntelliFin.Shared.Infrastructure.Messaging.Contracts;
using MassTransit;

namespace IntelliFin.Communications.Consumers;

public class LoanApplicationCreatedConsumer : IConsumer<LoanApplicationCreated>
{
    private readonly ILogger<LoanApplicationCreatedConsumer> _logger;

    public LoanApplicationCreatedConsumer(ILogger<LoanApplicationCreatedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<LoanApplicationCreated> context)
    {
        var message = context.Message;
        _logger.LogInformation(
            "Received LoanApplicationCreated: ApplicationId={ApplicationId}, ClientId={ClientId}, Amount={Amount}, ProductCode={ProductCode}",
            message.ApplicationId, message.ClientId, message.Amount, message.ProductCode);

        // TODO: Send notification email/SMS to client
        // TODO: Notify loan officers
        // TODO: Update communication preferences

        return Task.CompletedTask;
    }
}
