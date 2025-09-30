# Technical Requirements

## Technology Stack Requirements

### Core Technologies
- **.NET 9**: Backend framework with ASP.NET Core Web API
- **Entity Framework Core**: Database ORM extending existing LmsDbContext
- **SQL Server**: Database persistence using existing infrastructure
- **Redis**: Caching layer (existing implementation)
- **MassTransit**: Message bus integration (existing RabbitMQ setup)
- **SignalR**: Real-time notifications (existing implementation)

### External Integrations
- **Africa's Talking API**: SMS provider replacement
- **Mailtrap**: Email testing and delivery
- **Existing LmsDbContext**: Database integration via shared library

## Architecture Requirements

### Microservice Integration
```csharp
// Existing service structure to maintain
- Controllers/SmsNotificationController.cs      ✅ Existing - enhance
- Controllers/EmailNotificationController.cs    ✅ Existing - enhance
- Controllers/InAppNotificationController.cs    ✅ Existing - enhance
- Services/ISmsService.cs                       ✅ Existing - enhance
- Services/IEmailService.cs                     ✅ Existing - enhance
- Hubs/NotificationHub.cs                       ✅ Existing - enhance
```

### New Components to Add
```csharp
// Database integration (extending existing LmsDbContext)
- Entities/NotificationLog.cs
- Entities/NotificationTemplate.cs
- Entities/UserCommunicationPreferences.cs
- Entities/EventProcessingStatus.cs
- Repositories/INotificationRepository.cs
- Repositories/NotificationRepository.cs

// Africa's Talking integration
- Providers/AfricasTalkingSmsProvider.cs
- Models/AfricasTalkingModels.cs
- Configuration/AfricasTalkingConfig.cs

// Event processing enhancement
- Consumers/LoanApplicationCreatedConsumer.cs   ✅ Enhance existing stub
- Consumers/LoanApprovedConsumer.cs             🆕 New implementation
- Consumers/LoanDeclinedConsumer.cs             🆕 New implementation
- Consumers/LoanDisbursedConsumer.cs            🆕 New implementation
- Consumers/PaymentOverdueConsumer.cs           🆕 New implementation

// Template management
- Services/TemplateRenderingEngine.cs
- Services/PersonalizationService.cs
- Services/TemplateManagementService.cs
- Models/TemplateContext.cs
- Validators/TemplateValidator.cs
```

## Database Requirements

### Schema Extensions (Add to existing LmsDbContext)
```sql
-- Core notification audit trail
CREATE TABLE NotificationLogs (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    EventId UNIQUEIDENTIFIER NOT NULL,
    RecipientId NVARCHAR(100) NOT NULL,
    RecipientType NVARCHAR(50) NOT NULL,
    Channel NVARCHAR(20) NOT NULL,
    TemplateId INT NULL,
    Subject NVARCHAR(500) NULL,
    Content NVARCHAR(MAX) NOT NULL,
    PersonalizationData NVARCHAR(MAX) NULL,
    Status NVARCHAR(20) NOT NULL,
    GatewayResponse NVARCHAR(MAX) NULL,
    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    SentAt DATETIMEOFFSET NULL,
    DeliveredAt DATETIMEOFFSET NULL,
    FailureReason NVARCHAR(1000) NULL,
    RetryCount INT NOT NULL DEFAULT 0,
    MaxRetries INT NOT NULL DEFAULT 3,
    Cost DECIMAL(10,4) NULL,
    ExternalId NVARCHAR(100) NULL,
    BranchId INT NOT NULL,
    CreatedBy NVARCHAR(100) NOT NULL
);

-- Template management with versioning
CREATE TABLE NotificationTemplates (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    Category NVARCHAR(50) NOT NULL,
    Channel NVARCHAR(20) NOT NULL,
    Language NVARCHAR(10) NOT NULL DEFAULT 'en',
    Subject NVARCHAR(500) NULL,
    Content NVARCHAR(MAX) NOT NULL,
    PersonalizationTokens NVARCHAR(MAX) NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedBy NVARCHAR(100) NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    UpdatedBy NVARCHAR(100) NULL,
    UpdatedAt DATETIMEOFFSET NULL,
    Version INT NOT NULL DEFAULT 1
);

-- User communication preferences
CREATE TABLE UserCommunicationPreferences (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    UserId NVARCHAR(100) NOT NULL,
    UserType NVARCHAR(50) NOT NULL,
    PreferenceType NVARCHAR(50) NOT NULL,
    Enabled BIT NOT NULL DEFAULT 1,
    Channels NVARCHAR(100) NULL,
    Frequency NVARCHAR(20) NULL,
    OptOutDate DATETIMEOFFSET NULL,
    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    UpdatedAt DATETIMEOFFSET NULL
);

-- Event processing idempotency
CREATE TABLE EventProcessingStatus (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    EventId UNIQUEIDENTIFIER NOT NULL,
    EventType NVARCHAR(100) NOT NULL,
    ProcessedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    ProcessingResult NVARCHAR(20) NOT NULL,
    ErrorDetails NVARCHAR(MAX) NULL
);
```

### Performance Requirements
- **Query Performance**: <500ms for notification history queries
- **Insert Performance**: <100ms for notification logging
- **Indexing Strategy**: Optimized for recipient queries and date ranges
- **Connection Pooling**: Max 10 connections with proper disposal

## API Requirements

### Backward Compatibility (CRITICAL)
- **ALL existing endpoints must remain functional**
- **NO breaking changes to request/response formats**
- **Existing client contracts preserved**
- **SignalR hub functionality unchanged**

### Enhanced API Endpoints
```http
# Existing endpoints to enhance (maintain compatibility)
POST /api/sms/send                           ✅ Enhance with database logging
GET /api/sms/status/{messageId}             ✅ Enhance with improved tracking
POST /api/email/send                        ✅ Enhance with template support
GET /api/notifications/in-app               ✅ Enhance with state management

# New endpoints to add
GET /api/communications/logs                 🆕 Notification history
GET /api/communications/templates           🆕 Template management
POST /api/communications/templates          🆕 Template creation
PUT /api/communications/templates/{id}      🆝 Template updates
GET /api/communications/preferences/{id}    🆕 User preferences
PUT /api/communications/preferences/{id}    🆕 Preference updates
POST /api/sms/delivery-webhook              🆕 Africa's Talking webhook
```

## Configuration Requirements

### Feature Flags (CRITICAL for safe deployment)
```json
{
  "FeatureFlags": {
    "EnableDatabaseInfrastructure": false,
    "EnableDatabaseLogging": false,
    "EnableDatabaseQueries": false,
    "EnableFullDatabaseIntegration": false,
    "EnableDatabaseFallback": true,
    "EnableAfricasTalking": false,
    "EnableTemplateRendering": false,
    "EnableEventConsumers": false
  }
}
```

### Provider Configuration
```json
{
  "AfricasTalking": {
    "ApiKey": "sandbox_key_for_development",
    "Username": "sandbox",
    "BaseUrl": "https://api.sandbox.africastalking.com/version1/messaging",
    "SenderId": "IntelliFin",
    "EnableDeliveryReports": true,
    "WebhookUrl": "/api/sms/delivery-webhook",
    "MaxRetryAttempts": 3,
    "RetryDelaySeconds": [30, 300, 1800]
  },
  "Mailtrap": {
    "Host": "sandbox.smtp.mailtrap.io",
    "Port": 587,
    "Username": "9f8c0a0a35eee6",
    "Password": "c412d0230ea1a0",
    "EnableSsl": true,
    "From": "IntelliFin <noreply@intellifin.com>"
  }
}
```

### Database Configuration (Existing)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "existing_lms_connection_string"
  },
  "DatabaseOptions": {
    "CommandTimeout": 30,
    "EnableRetryOnFailure": true,
    "MaxRetryCount": 3,
    "MaxRetryDelay": "00:00:30"
  }
}
```

## Performance Requirements

### Response Time Targets
- **SMS Send API**: ≤2 seconds
- **Email Send API**: ≤3 seconds
- **Notification History**: ≤500ms
- **Template Rendering**: ≤1 second
- **Real-time Notifications**: ≤3 seconds end-to-end

### Throughput Requirements
- **SMS Sending**: 100 messages/minute sustained
- **Email Sending**: 50 messages/minute sustained
- **Event Processing**: 1000 events/minute peak
- **Database Operations**: 500 queries/minute sustained

### Availability Requirements
- **Service Availability**: >99.5% uptime
- **Database Availability**: >99% (with fallback)
- **External Provider**: >95% (with retry logic)
- **Real-time Notifications**: >99% delivery rate

## Security Requirements

### Authentication & Authorization
- **Existing JWT authentication maintained**
- **Role-based access control preserved**
- **Branch context validation required**
- **API key security for Africa's Talking**

### Data Protection
- **PII encryption in database**
- **Audit trail immutability**
- **GDPR compliance for preferences**
- **Secure webhook verification**

### Input Validation
- **Template content sanitization**
- **Personalization token validation**
- **Phone number format validation**
- **Rate limiting on public endpoints**

## Integration Requirements

### Message Bus Integration (Existing MassTransit)
```csharp
// Consumer registration pattern
services.AddMassTransit(x =>
{
    x.AddConsumer<LoanApplicationCreatedConsumer>();
    x.AddConsumer<LoanApprovedConsumer>();
    x.AddConsumer<PaymentOverdueConsumer>();
    x.AddConsumer<LoanDisbursedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.ConfigureEndpoints(context);
    });
});
```

### Database Integration (Existing LmsDbContext)
```csharp
// Extend existing context
public partial class LmsDbContext : DbContext
{
    // Existing entities...

    // New communication entities
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<UserCommunicationPreferences> UserCommunicationPreferences => Set<UserCommunicationPreferences>();
    public DbSet<EventProcessingStatus> EventProcessingStatus => Set<EventProcessingStatus>();
}
```

### Health Check Requirements
```csharp
services.AddHealthChecks()
    .AddDbContextCheck<LmsDbContext>("database")
    .AddRedis(connectionString, "redis")
    .AddRabbitMQ(connectionString, "message-bus")
    .AddCheck<AfricasTalkingHealthCheck>("africas-talking")
    .AddCheck<TemplateRenderingHealthCheck>("template-engine");
```

## Testing Requirements

### Unit Testing
- **Test Coverage**: ≥85% for all new code
- **Framework**: xUnit with FluentAssertions
- **Mocking**: Moq for dependencies
- **Database Testing**: In-memory provider for EF Core

### Integration Testing
- **API Testing**: All endpoints with realistic data
- **Database Testing**: Real SQL Server with test containers
- **Message Bus Testing**: RabbitMQ integration tests
- **External API Testing**: Africa's Talking sandbox

### Performance Testing
- **Load Testing**: NBomber for concurrent SMS sending
- **Database Performance**: Query execution time validation
- **Memory Testing**: Memory leaks and garbage collection
- **Connection Testing**: Database connection pooling

## Deployment Requirements

### Containerization
- **Docker**: Continue using existing Dockerfile
- **Kubernetes**: Existing deployment manifests
- **Health Checks**: Liveness and readiness probes
- **Resource Limits**: CPU and memory constraints

### Database Migrations
- **EF Core Migrations**: Automated deployment pipeline
- **Zero-Downtime**: Additive schema changes only
- **Rollback**: Migration rollback procedures
- **Validation**: Post-deployment validation scripts

### Configuration Management
- **Environment Variables**: Sensitive configuration
- **ConfigMaps**: Non-sensitive configuration
- **Secrets**: API keys and connection strings
- **Feature Flags**: Runtime configuration changes

## Monitoring Requirements

### Application Monitoring
- **Metrics**: Custom metrics for business KPIs
- **Logging**: Structured logging with correlation IDs
- **Tracing**: Distributed tracing for request flows
- **Alerts**: Automated alerting for critical failures

### Business Monitoring
- **SMS Delivery Rates**: Real-time tracking
- **Event Processing Success**: Business event metrics
- **Template Usage**: Template rendering statistics
- **User Engagement**: Notification interaction metrics

This technical requirements document ensures all enhancement work integrates seamlessly with existing infrastructure while adding the required new capabilities.