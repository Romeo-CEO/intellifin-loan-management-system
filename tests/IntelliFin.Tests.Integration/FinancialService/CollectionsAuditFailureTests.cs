using System.Net;
using System.Net.Http.Json;
using IntelliFin.FinancialService.Models;
using IntelliFin.Tests.Integration.FinancialService.Stubs;

namespace IntelliFin.Tests.Integration.FinancialService;

public class CollectionsAuditFailureTests
{
    [Fact]
    public async Task RecordPayment_WhenAuditUnavailable_Returns503()
    {
        var auditClient = new TestAuditClient(shouldThrow: true);
        using var factory = new FinancialServiceWebApplicationFactory(auditClient);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/collections/payments", new RecordPaymentRequest
        {
            LoanId = "LN-5",
            Amount = 25m,
            Method = PaymentMethod.Cash,
            PaymentDate = DateTime.UtcNow,
            ExternalReference = "EXT-1"
        });

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }
}
