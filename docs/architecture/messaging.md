# Messaging Architecture

## Overview

The IntelliFin Loan Management System uses MassTransit with RabbitMQ for asynchronous messaging between microservices. This enables loose coupling, scalability, and reliable message delivery.

## Message Broker

- **Technology**: RabbitMQ 3.x
- **Management UI**: http://localhost:15672 (guest/guest)
- **AMQP Port**: 5672
- **Management Port**: 15672

## Message Contracts

### LoanApplicationCreated

Published when a new loan application is created.

```csharp
public record LoanApplicationCreated(
    Guid ApplicationId,
    Guid ClientId,
    decimal Amount,
    int TermMonths,
    string ProductCode,
    DateTime CreatedAtUtc
);
```

**Publisher**: LoanOrigination Service
**Consumers**: Communications Service

## Configuration

### Publisher Configuration (LoanOrigination)

```csharp
services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", 35672, "/", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });
        cfg.ConfigureEndpoints(context);
    });
});
```

### Consumer Configuration (Communications)

```csharp
services.AddMassTransit(x =>
{
    x.AddConsumer<LoanApplicationCreatedConsumer>();
    x.SetKebabCaseEndpointNameFormatter();
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", 35672, "/", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });
        cfg.ConfigureEndpoints(context);
    });
});
```

## Message Flow

1. **Loan Application Creation**
   - User submits loan application via API Gateway
   - LoanOrigination service processes the request
   - LoanApplicationCreated message is published to RabbitMQ
   - Communications service receives the message
   - Notification workflows are triggered

## Queue Naming Convention

MassTransit uses kebab-case naming:
- Exchange: `loan-application-created`
- Queue: `communications_loan-application-created`

## Error Handling

- **Retry Policy**: 3 retries with exponential backoff
- **Dead Letter Queue**: Failed messages after retries
- **Poison Message Handling**: Automatic quarantine

## Monitoring

- **Health Checks**: `/health` endpoint includes MassTransit status
- **RabbitMQ Management**: Queue depths, message rates
- **Application Logs**: Message publishing/consumption events

## Future Message Types

Planned message contracts for upcoming sprints:

- `ClientCreated`
- `LoanApproved`
- `LoanDisbursed`
- `PaymentReceived`
- `LoanDefaulted`

## Best Practices

1. **Idempotency**: All message handlers should be idempotent
2. **Versioning**: Use message versioning for backward compatibility
3. **Correlation**: Include correlation IDs for tracing
4. **Timeouts**: Set appropriate message TTL values
5. **Monitoring**: Log all message operations for debugging
