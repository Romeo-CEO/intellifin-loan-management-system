# IntelliFin Communications Enhancement PRD

## Project Overview

### Project Type
**BROWNFIELD ENHANCEMENT** - Enhancing existing, fully operational IntelliFin.Communications microservice with SMS provider migration and feature completion.

### Current System Analysis

The **IntelliFin.Communications** service is a **fully implemented notification microservice** that handles multi-channel communications (SMS, Email, In-App) for the IntelliFin loan management system.

**EXISTING PRODUCTION-READY CAPABILITIES:**
- **SMS Infrastructure**: Complete implementation with Airtel and MTN providers, sophisticated routing logic, rate limiting, and cost tracking
- **Real-time Notifications**: Fully operational SignalR hub with connection management, user groups, and real-time event delivery
- **API Layer**: Comprehensive REST controllers (SmsNotificationController, EmailNotificationController, InAppNotificationController) with 30+ endpoints
- **Resilience Patterns**: Polly-based circuit breakers, retry policies, and HTTP client configurations implemented
- **Caching Layer**: Redis integration for delivery status, rate limiting, analytics, and user preferences
- **Template System**: Foundation exists with SmsTemplateService, EmailTemplateService, and basic rendering capabilities
- **Event Infrastructure**: MassTransit configured with RabbitMQ, consumer registration, and event routing setup
- **Database Integration**: Uses existing LmsDbContext from libs/IntelliFin.Shared.DomainModels with SQL Server Entity Framework Core

**Architecture Stack**: .NET 9, ASP.NET Core Web API, Entity Framework Core, MassTransit with RabbitMQ, Redis, SignalR, Polly.

### Enhancement Scope Definition

**Enhancement Type**: ☑️ SMS Provider Migration + ☑️ Event Consumer Implementation + ☑️ Database Persistence Enhancement
**Impact Assessment**: ☑️ Moderate Impact (targeted enhancements to existing production system)

**Enhancement Requirements Identified**:
1. **SMS Provider Migration**: Migrate from Airtel/MTN direct integration to unified Africa's Talking API
2. **Event Consumer Completion**: Transform stub LoanApplicationCreatedConsumer and add new business event consumers
3. **Database Persistence Enhancement**: Extend existing LmsDbContext with communication-specific entities
4. **Template System Completion**: Enhance existing template services with full rendering capabilities
5. **Business Event Workflow**: Complete end-to-end event processing from business events to multi-channel notifications

### Goals and Background Context

**Goals:**
• **Migrate SMS infrastructure from Zambian carriers to Africa's Talking API**
• Transform stub event consumers into fully functional business event processors
• Implement comprehensive database persistence layer for notification tracking and auditing
• Complete template rendering system with dynamic content support
• Maintain 100% backward compatibility with existing SignalR infrastructure
• Enable end-to-end notification workflows triggered by business events

**Provider Integration Details:**
- **Current**: Individual Zambian carrier integrations (Airtel, MTN, Zamtel) with complete HTTP client setup and Polly resilience policies
- **Target**: Unified Africa's Talking API integration while preserving existing provider interfaces
- **Migration Strategy**: Phased migration maintaining 100% backward compatibility during transition
- **Benefit**: Single API for multi-carrier delivery, better reliability, comprehensive delivery tracking
- **Sandbox Available**: Sandbox credentials provided for development and testing

### Backward Compatibility Requirements

**API Compatibility:**
- All existing SMS API endpoints must remain functional during and after migration
- Existing service contracts and response formats must be preserved
- SignalR hub functionality must remain completely intact
- No breaking changes to existing notification workflows

**SMS Provider Migration Strategy:**
- Phase 1: Add Africa's Talking provider alongside existing providers
- Phase 2: Route new traffic through Africa's Talking while maintaining existing routes
- Phase 3: Gradual migration of existing clients with fallback capabilities
- Phase 4: Deprecation of old providers only after complete validation

**Database Integration Safety:**
- **CRITICAL**: Current Communications service has NO database integration - this is a zero-to-database architectural change
- New communication entities extend existing LmsDbContext without affecting current tables
- All new tables use separate namespace to avoid conflicts
- Existing audit trail in AuditEvents table remains untouched
- Migration scripts designed for zero-downtime deployment
- Feature flags enable gradual database integration rollout

## Database Integration Safety Framework

### Critical Assessment: Zero-to-Database Architectural Change

**Current State Analysis:**
The IntelliFin.Communications service currently operates as a **database-free microservice** using:
- Redis for caching and rate limiting
- In-memory processing for notifications
- SignalR for real-time communication
- HTTP APIs for external provider integration

**Introducing database integration represents a fundamental architectural change** that requires extreme caution to avoid disrupting the stable, production-ready service.

### Production Safety Requirements

#### Phase 0: Infrastructure Preparation (Zero-Risk Phase)
**Objective**: Add database infrastructure without using it
- Add LmsDbContext registration to DI container (feature flagged OFF)
- Deploy communication entities and migrations (tables created but unused)
- Add repository interfaces and implementations (not called)
- **Safety**: Service continues operating exactly as before - no behavioral changes

#### Phase 1: Read-Only Database Integration (Low-Risk Phase)
**Objective**: Enable database logging without affecting core functionality
- Feature flag: `EnableDatabaseLogging = false` (default)
- When enabled: Log notifications to database in background (fire-and-forget)
- Core notification logic remains unchanged (Redis + in-memory)
- **Safety**: Database failures do not affect notification delivery

#### Phase 2: Hybrid Operation (Controlled Risk Phase)
**Objective**: Enable full database integration with automatic fallback
- Feature flag: `EnableDatabaseQueries = false` (default)
- When enabled: Query database for history, fallback to "not available" if fails
- Notification sending still uses original Redis-based logic
- **Safety**: Graceful degradation - service works normally if database unavailable

#### Phase 3: Full Database Integration (Production Phase)
**Objective**: Complete migration with comprehensive monitoring
- All database features enabled after validation in previous phases
- Original in-memory logic retained as emergency fallback
- **Safety**: Rollback capability to previous phases if issues detected

### Feature Flag Configuration

```json
{
  "FeatureFlags": {
    "EnableDatabaseInfrastructure": false,    // Phase 0: Add DB registration
    "EnableDatabaseLogging": false,           // Phase 1: Background logging
    "EnableDatabaseQueries": false,           // Phase 2: Read operations
    "EnableFullDatabaseIntegration": false,   // Phase 3: Complete integration
    "EnableDatabaseFallback": true            // Emergency fallback to Redis
  }
}
```

### Rollback Strategy

#### Immediate Rollback (< 5 minutes)
- Disable feature flags via configuration update
- Service reverts to original Redis-only behavior
- No code deployment required

#### Full Rollback (< 30 minutes)
- Deploy previous service version
- Database tables remain (for audit) but unused
- Complete restoration to pre-enhancement state

#### Emergency Procedures
- Database connection failures automatically trigger fallback mode
- Health checks monitor database integration performance
- Automated alerts for database operation failures
- Circuit breaker pattern prevents cascading failures

### Monitoring and Validation

#### Database Integration Metrics
- Database operation success rate (target: >99%)
- Database query performance (target: <500ms)
- Fallback activation frequency (target: <0.1%)
- Service availability during database issues (target: >99.9%)

#### Validation Criteria per Phase
- **Phase 0**: No performance impact, all existing functionality works
- **Phase 1**: Background logging succeeds, no notification delivery impact
- **Phase 2**: Database queries enhance functionality without breaking existing features
- **Phase 3**: Full integration maintains performance and reliability standards

### Risk Mitigation

#### Technical Risks
- **Database Unavailability**: Graceful degradation to Redis-only mode
- **Performance Impact**: Connection pooling and read replicas
- **Transaction Failures**: Optional database operations, service continues
- **Migration Issues**: Database changes are additive only

#### Operational Risks
- **Deployment Risk**: Phased rollout with immediate rollback capability
- **Data Loss Risk**: All critical data remains in Redis during transition
- **Service Disruption**: Original notification paths preserved throughout migration
- **Compliance Risk**: Audit trail enhanced, not replaced

## User Stories and Requirements

### Epic 1: Business Event Processing
**As a** loan officer
**I want** automatic notifications to be sent when loan applications are submitted
**So that** I can respond quickly to new applications without manually monitoring the system

**As a** customer
**I want** to receive SMS notifications about my loan status changes
**So that** I stay informed about my application progress

**As a** collections officer
**I want** automated overdue payment notifications to be sent to customers
**So that** I can focus on complex cases rather than routine reminders

### Epic 2: SMS Provider Migration
**As a** system administrator
**I want** SMS delivery through Africa's Talking instead of multiple Zambian carriers
**So that** we have unified delivery reporting and reduced integration complexity

**As a** finance manager
**I want** consolidated SMS cost tracking through a single provider
**So that** I can better manage communication budgets

### Epic 3: Database Persistence & Audit Trail
**As a** compliance officer
**I want** complete audit trails of all customer communications
**So that** we can demonstrate regulatory compliance

**As a** customer service representative
**I want** to view a customer's notification history
**So that** I can provide better support and avoid duplicate communications

### Epic 4: Template Management System
**As a** communications manager
**I want** to create and modify message templates without code changes
**So that** we can quickly adapt communications for different scenarios

**As a** loan officer
**I want** personalized messages with customer-specific information
**So that** communications feel more professional and relevant

### Epic 5: Real-time Staff Notifications Enhancement
**As a** branch manager
**I want** real-time dashboard notifications for urgent loan approvals
**So that** I can prioritize high-value decisions

**As a** system administrator
**I want** immediate alerts for communication system failures
**So that** I can maintain service reliability

### Requirements Priority Matrix

**P0 (Must Have - Release Blockers):**
- Business event consumer implementation (loan events → notifications)
- Africa's Talking SMS provider integration
- Database persistence layer for notification tracking
- Basic template rendering system

**P1 (Should Have - High Value):**
- Complete audit trail and compliance reporting
- Advanced template management UI
- Real-time staff notification enhancements
- Cost tracking and analytics

**P2 (Could Have - Nice to Have):**
- A/B testing framework for message optimization
- Multi-language template support
- Advanced analytics dashboard
- WhatsApp Business API readiness

## Technical Implementation Details

### Architecture Overview

**Current Architecture:** .NET 9 Web API + SignalR + MassTransit + Redis
**Enhancement Pattern:** Layered enhancement preserving existing functionality
**Database Strategy:** Extend existing LmsDbContext with communication entities (leveraging existing EF Core infrastructure)

### Core Components to Implement

#### 1. Database Persistence Layer Enhancement
**New Components (extending existing LmsDbContext):**
```csharp
// Add to existing LmsDbContext
- Entities/NotificationLog.cs               // Audit trail entity
- Entities/NotificationTemplate.cs          // Template storage
- Entities/UserCommunicationPreferences.cs  // User preferences
- Repositories/INotificationRepository.cs   // Repository pattern
- Extensions/LmsDbContextExtensions.cs      // Communication-specific DbSets
```

**Database Schema (SQL Server):**
- NotificationLogs table - Complete audit trail of all communications
- NotificationTemplates table - Centralized template management
- UserCommunicationPreferences table - Opt-out and preference management
- EventProcessingStatus table - Idempotent event processing tracking

#### 2. Africa's Talking SMS Provider Implementation
**Provider Migration:**
```csharp
// Replace existing providers
- Providers/AfricasTalkingSmsProvider.cs    // New unified provider
- Models/AfricasTalkingModels.cs           // Provider-specific DTOs
- Configuration/AfricasTalkingConfig.cs     // Provider settings
```

**Configuration Updates (appsettings.json):**
```json
{
  "AfricasTalking": {
    "ApiKey": "atsk_17ff1222a840ca0d8e4ba32df08ee8ad76f258213a3456d1e2649006f43b2bcc0b8fe5e5",
    "Username": "sandbox",
    "BaseUrl": "https://api.sandbox.africastalking.com/version1/messaging",
    "SenderId": "IntelliFin",
    "EnableDeliveryReports": true,
    "WebhookUrl": "/api/sms/delivery-webhook"
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

#### 3. Business Event Processing Implementation
**Event Consumers to Complete:**
```csharp
// Enhance existing stub
- Consumers/LoanApplicationCreatedConsumer.cs     // → Full implementation
- Consumers/LoanApprovedConsumer.cs              // → New consumer
- Consumers/PaymentOverdueConsumer.cs            // → New consumer
- Consumers/LoanDisbursedConsumer.cs             // → New consumer
```

**Event Processing Pattern:**
1. Receive business event via MassTransit
2. Query templates based on event type + user preferences
3. Render personalized message content
4. Send via appropriate channel (SMS/In-App/Email)
5. Log to database with full audit trail
6. Handle delivery status updates

#### 4. Template Rendering System
**Template Engine Components:**
```csharp
- Services/TemplateRenderingEngine.cs        // Core rendering logic
- Services/PersonalizationService.cs         // Token replacement
- Models/TemplateContext.cs                  // Rendering context
- Validators/TemplateValidator.cs            // Template validation
```

**Template Features:**
- Token-based personalization ({{customer_name}}, {{loan_amount}}, etc.)
- Conditional content rendering
- Multi-format support (SMS, Email HTML/Text, In-App)
- Template versioning and A/B testing support

### Integration Points

#### Database Integration
**Existing LmsDbContext Extension:**
```csharp
// Extend existing LmsDbContext in libs/IntelliFin.Shared.DomainModels
public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
public DbSet<UserCommunicationPreferences> UserCommunicationPreferences => Set<UserCommunicationPreferences>();
```

**Entity Framework Integration:**
- Leverage existing connection string and migrations
- Add new entities to existing LmsDbContext
- Maintain existing repository pattern consistency
- Use existing transaction management infrastructure

#### Message Bus Enhancement
**MassTransit Configuration Updates:**
```csharp
// Add new consumers to Program.cs
x.AddConsumer<LoanApprovedConsumer>();
x.AddConsumer<PaymentOverdueConsumer>();
x.AddConsumer<LoanDisbursedConsumer>();

// Add outbox pattern for reliability using existing LmsDbContext
x.AddEntityFrameworkOutbox<LmsDbContext>(o => {
    o.UseSqlServer();
    o.UseBusOutbox();
});
```

#### Africa's Talking Webhook Handling
**New Endpoint for Delivery Status:**
```csharp
// Add to SmsNotificationController
[HttpPost("delivery-webhook")]
[AllowAnonymous] // Africa's Talking callback
public async Task<IActionResult> HandleDeliveryStatusAsync([FromBody] AfricasTalkingDeliveryReport report)
```

### Enhanced Migration Strategy

#### Phase 0: Infrastructure Preparation (Week 1 - Zero-Risk)
1. Create communication entities and extend LmsDbContext (feature flagged OFF)
2. Add repository interfaces and implementations (not registered)
3. Generate and deploy EF migrations (tables created but unused)
4. **Validation**: Service operates identically to before - no behavioral changes

#### Phase 1: Database Foundation (Week 2 - Low-Risk)
1. Enable database infrastructure via feature flag (`EnableDatabaseInfrastructure = true`)
2. Register repositories in DI container
3. Begin background database logging (feature flagged OFF by default)
4. **Validation**: Database connectivity works, no impact on notification delivery

#### Phase 2: Africa's Talking Migration (Week 2-3)
1. Implement AfricasTalkingSmsProvider
2. Add configuration and webhook handling
3. Update SmsService to use new provider
4. Parallel testing with sandbox
5. Switch over from existing providers

#### Phase 3: Event Processing (Week 3-4)
1. Complete LoanApplicationCreatedConsumer implementation
2. Add new event consumers (LoanApproved, PaymentOverdue, etc.)
3. Implement template rendering engine
4. Add personalization service
5. End-to-end testing of event → notification flow

#### Phase 4: Template Management (Week 4-5)
1. Complete template CRUD operations
2. Add template validation and testing endpoints
3. Implement versioning system
4. Add basic template management UI
5. Migration tools for existing hardcoded templates

### Performance and Scalability Considerations

**Database Optimization:**
- Indexed queries on frequently accessed columns (UserId, EventType, CreatedDate)
- Partitioning strategy for NotificationLogs by date
- Connection pooling and read replicas for high-volume operations

**Caching Strategy:**
- Template caching in Redis (1-hour TTL)
- User preference caching (15-minute TTL)
- Rate limiting counters (existing Redis implementation)

**Monitoring and Observability:**
- Application Insights integration for performance tracking
- Custom metrics for notification delivery rates
- Health checks for database, Africa's Talking API, and MassTransit

### Security Considerations

**API Security:**
- Africa's Talking webhook signature verification
- Rate limiting on public webhook endpoints
- Input validation for all template rendering

**Data Protection:**
- Encryption for sensitive PII in database
- Audit logging for all template and preference changes
- GDPR compliance for notification history retention

## Success Metrics and Acceptance Criteria

### Key Performance Indicators (KPIs)

#### Technical Performance Metrics
**SMS Delivery Performance:**
- **SMS Delivery Rate**: ≥95% successful delivery via Africa's Talking
- **SMS Delivery Speed**: ≤30 seconds from event trigger to customer receipt
- **API Response Time**: ≤2 seconds for SMS send requests
- **System Uptime**: ≥99.5% availability for notification services

**Event Processing Performance:**
- **Event Processing Latency**: ≤5 seconds from business event to notification trigger
- **Event Processing Success Rate**: ≥99% successful processing without manual intervention
- **Template Rendering Speed**: ≤1 second for complex personalized templates
- **Database Query Performance**: ≤500ms for notification history queries

#### Business Impact Metrics
**Operational Efficiency:**
- **Loan Processing Time**: 20% reduction in average loan processing time due to automated notifications
- **Customer Response Time**: 50% improvement in customer response to loan status updates
- **Staff Productivity**: 30% reduction in manual communication tasks for loan officers
- **Compliance Reporting**: 100% automated generation of communication audit reports

**Customer Experience:**
- **Communication Relevance Score**: ≥4.5/5 based on customer feedback surveys
- **Notification Opt-out Rate**: ≤5% across all communication types
- **Customer Complaint Resolution**: 25% reduction in communication-related complaints
- **Multi-channel Engagement**: 80% of customers engage with notifications within 24 hours

### Acceptance Criteria by Epic

#### Epic 1: Business Event Processing
**GIVEN** a loan application is submitted
**WHEN** the LoanApplicationCreated event is published
**THEN** the system must:
- ✅ Process the event within 5 seconds
- ✅ Send SMS notification to customer with application reference number
- ✅ Send in-app notification to assigned loan officer
- ✅ Log all communications to database with full audit trail
- ✅ Handle event idempotency (no duplicate notifications)

#### Epic 2: SMS Provider Migration
**GIVEN** Africa's Talking is configured as SMS provider
**WHEN** any SMS is sent through the system
**THEN** the system must:
- ✅ Route all SMS through Africa's Talking API successfully
- ✅ Handle delivery status webhooks and update notification logs
- ✅ Maintain 100% backward compatibility with existing SMS API contracts
- ✅ Support Zambian phone number formats (+260XXXXXXXXX)
- ✅ Provide detailed delivery reporting and cost tracking

#### Epic 3: Database Persistence & Audit Trail
**GIVEN** any notification is sent
**WHEN** the communication is processed
**THEN** the system must:
- ✅ Store complete audit trail in existing LmsDbContext
- ✅ Enable queryable notification history by customer, type, date range
- ✅ Support compliance reporting with 10-year data retention
- ✅ Maintain data integrity and transaction consistency
- ✅ Enable notification replay and analysis capabilities

#### Epic 4: Template Management System
**GIVEN** a communication template exists
**WHEN** a notification is triggered
**THEN** the system must:
- ✅ Render personalized content with customer-specific data
- ✅ Support all required personalization tokens (customer_name, loan_amount, etc.)
- ✅ Validate template syntax before saving
- ✅ Enable A/B testing with template versions
- ✅ Support multi-format templates (SMS, Email, In-App)

#### Epic 5: Real-time Staff Notifications Enhancement
**GIVEN** a staff member is logged into the system
**WHEN** a relevant business event occurs
**THEN** the system must:
- ✅ Deliver real-time in-app notifications via SignalR
- ✅ Support role-based notification routing
- ✅ Maintain notification state (read/unread/dismissed)
- ✅ Enable notification action buttons (approve, review, etc.)
- ✅ Handle offline users with notification queuing

## Timeline and Delivery Milestones

### Development Timeline (5-Week Sprint Plan)

**Sprint 1 (Week 1-2): Foundation**
- ✅ Database schema design and migration
- ✅ Entity Framework Core integration
- ✅ Africa's Talking provider implementation
- ✅ Basic webhook handling

**Sprint 2 (Week 2-3): Event Processing**
- ✅ Complete LoanApplicationCreated consumer
- ✅ Add LoanApproved and PaymentOverdue consumers
- ✅ Template rendering engine implementation
- ✅ End-to-end event flow testing

**Sprint 3 (Week 3-4): Template Management**
- ✅ Template CRUD operations
- ✅ Template validation and testing
- ✅ Basic template management UI
- ✅ Migration of existing templates

**Sprint 4 (Week 4-5): Integration & Polish**
- ✅ Performance optimization
- ✅ Security audit and fixes
- ✅ Comprehensive testing
- ✅ Documentation and deployment prep

**Sprint 5 (Week 5): Deployment & Validation**
- ✅ Production deployment
- ✅ Monitoring and alerting setup
- ✅ User acceptance testing
- ✅ Go-live validation

### Quality Gates
- **Test Coverage**: ≥85% unit test coverage for all new code
- **Integration Testing**: ≥90% coverage for API endpoints and event consumers
- **Security Audit**: External security audit passed
- **Performance Testing**: Load testing for 1000+ concurrent SMS requests
- **Compliance**: GDPR and audit trail requirements validated

---

**Document Version**: 1.0
**Created**: Today
**Status**: Ready for Architecture Phase
**Next Steps**: Architect agent creates detailed technical architecture document