using IntelliFin.Shared.Infrastructure.Messaging;
using IntelliFin.Shared.Infrastructure.Messaging.Contracts;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddIntelliFinMassTransit(builder.Environment);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapHealthChecks("/health");

// Publish a LoanApplicationCreated message
app.MapPost("/loan-applications", async (CreateLoanApplicationRequest req, IPublishEndpoint publisher) =>
{
    var applicationId = Guid.NewGuid();
    var message = new LoanApplicationCreated(
        applicationId,
        req.ClientId,
        req.Amount,
        req.TermMonths,
        req.ProductCode,
        DateTime.UtcNow
    );

    await publisher.Publish(message);
    return Results.Ok(new { applicationId, message = "Loan application created and published" });
});

app.MapGet("/", () => Results.Ok(new { name = "IntelliFin.LoanOrigination", status = "OK" }));

app.Run();

record CreateLoanApplicationRequest(Guid ClientId, decimal Amount, int TermMonths, string ProductCode);
