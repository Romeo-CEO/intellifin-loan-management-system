# Story 1.4: Event Routing and Filtering Framework

## Status
Draft

## Story
**As a** system administrator
**I want** configurable event routing and filtering rules
**so that** I can control which events trigger notifications without code changes

## Acceptance Criteria
1. ✅ NotificationRoutingService supports dynamic routing rules
2. ✅ Event filtering based on business criteria (amount, branch, customer type)
3. ✅ Channel preferences per recipient type and event category
4. ✅ Time-based filtering for quiet hours and business days
5. ✅ Priority-based routing with escalation rules
6. ✅ Configuration management through database and admin interface
7. ✅ Real-time rule updates without service restart

## Tasks / Subtasks

- [ ] Task 1: Implement NotificationRoutingService Core (AC: 1, 2)
  - [ ] Create INotificationRoutingService interface
  - [ ] Implement NotificationRoutingService with rule engine
  - [ ] Add rule evaluation logic for business criteria
  - [ ] Implement event filtering based on configurable criteria
  - [ ] Add support for complex rule combinations (AND, OR, NOT logic)
  - [ ] Create rule validation and conflict detection

- [ ] Task 2: Implement Channel Preference Management (AC: 3)
  - [ ] Create recipient channel preference repository
  - [ ] Implement channel selection logic per recipient type
  - [ ] Add event category to channel mapping
  - [ ] Create default channel preference configuration
  - [ ] Implement customer communication preference override
  - [ ] Add multi-channel delivery rules (primary + fallback)

- [ ] Task 3: Implement Time-Based Filtering (AC: 4)
  - [ ] Create business hours configuration service
  - [ ] Implement quiet hours filtering logic
  - [ ] Add holiday calendar integration
  - [ ] Create time zone aware filtering for multi-branch operations
  - [ ] Implement delayed delivery scheduling for quiet hours
  - [ ] Add emergency override rules for critical notifications

- [ ] Task 4: Implement Priority-Based Routing (AC: 5)
  - [ ] Create notification priority classification system
  - [ ] Implement priority-based escalation rules
  - [ ] Add automatic priority elevation based on criteria
  - [ ] Create VIP customer priority handling
  - [ ] Implement high-value transaction priority routing
  - [ ] Add critical system event immediate delivery

- [ ] Task 5: Database Schema for Routing Configuration (AC: 6)
  - [ ] Create NotificationRoutingRules table
  - [ ] Create RecipientChannelPreferences table
  - [ ] Create BusinessHoursConfiguration table
  - [ ] Create NotificationPriorityRules table
  - [ ] Create EventFilteringCriteria table
  - [ ] Add EF Core configuration and migrations

- [ ] Task 6: Configuration Management Service (AC: 6, 7)
  - [ ] Create IRoutingConfigurationService interface
  - [ ] Implement database-backed configuration storage
  - [ ] Add configuration caching with refresh mechanism
  - [ ] Create configuration validation service
  - [ ] Implement configuration versioning and rollback
  - [ ] Add configuration change audit logging

- [ ] Task 7: Real-Time Configuration Updates (AC: 7)
  - [ ] Implement configuration change event publishing
  - [ ] Add configuration refresh consumer
  - [ ] Create in-memory configuration cache management
  - [ ] Implement distributed cache synchronization across instances
  - [ ] Add configuration change notification to administrators
  - [ ] Create configuration health monitoring

- [ ] Task 8: Rule Engine Implementation (AC: 1, 2)
  - [ ] Create flexible rule evaluation engine
  - [ ] Implement expression-based rule syntax
  - [ ] Add rule compilation and optimization
  - [ ] Create rule testing and simulation tools
  - [ ] Implement rule performance monitoring
  - [ ] Add rule conflict detection and resolution

- [ ] Task 9: Admin Interface Integration (AC: 6)
  - [ ] Create routing configuration API endpoints
  - [ ] Implement rule management CRUD operations
  - [ ] Add rule testing and preview functionality
  - [ ] Create configuration export/import capabilities
  - [ ] Implement role-based access control for configuration
  - [ ] Add configuration change approval workflow

- [ ] Task 10: Integration with Existing Consumers (AC: All)
  - [ ] Update BaseNotificationConsumer to use routing service
  - [ ] Modify existing consumers to support filtering
  - [ ] Add routing service dependency injection
  - [ ] Update event processing pipeline with filtering
  - [ ] Implement backward compatibility for existing rules
  - [ ] Add routing metrics and monitoring

- [ ] Task 11: Unit Testing (AC: All)
  - [ ] Write unit tests for NotificationRoutingService
  - [ ] Write unit tests for rule evaluation engine
  - [ ] Write unit tests for time-based filtering
  - [ ] Write unit tests for priority routing logic
  - [ ] Write unit tests for configuration management
  - [ ] Write unit tests for channel preference logic

- [ ] Task 12: Integration Testing (AC: All)
  - [ ] Write integration tests for end-to-end routing
  - [ ] Write integration tests for configuration updates
  - [ ] Write integration tests for time-based filtering
  - [ ] Write integration tests for multi-channel delivery
  - [ ] Write integration tests for priority escalation
  - [ ] Write integration tests for rule conflict handling

## Dev Notes

### Previous Story Dependencies
**Story 1.1 Foundation**: Builds on BaseNotificationConsumer to add routing capabilities
**Story 1.2 & 1.3 Consumers**: All existing consumers will be enhanced with routing functionality
**Epic 3 Database**: Requires database persistence for routing configuration

### Data Models
**NotificationRoutingRule Entity**: Core routing rule configuration
- Fields: Id, RuleName, EventType, RecipientType, Conditions, Actions, Priority, IsActive, CreatedAt, UpdatedAt, CreatedBy
- Purpose: Store flexible routing rules with business logic conditions
- Example Conditions: "LoanAmount > 100000 AND BranchId = 5", "CustomerType = 'VIP'"

**RecipientChannelPreference Entity**: Communication channel preferences
- Fields: Id, RecipientId, RecipientType, EventCategory, PreferredChannel, FallbackChannel, QuietHoursEnabled, IsActive
- Purpose: Store individual recipient communication preferences
- Supports: SMS, Email, InApp, Push notification channels

**BusinessHoursConfiguration Entity**: Time-based filtering configuration
- Fields: Id, BranchId, DayOfWeek, StartTime, EndTime, TimeZone, IsHoliday, IsActive
- Purpose: Define business hours and quiet hours for notification delivery
- Supports: Branch-specific hours, holiday calendar, time zone handling

**NotificationPriorityRule Entity**: Priority classification rules
- Fields: Id, RuleName, EventType, Conditions, PriorityLevel, EscalationMinutes, IsActive
- Purpose: Automatic priority assignment and escalation rules
- Supports: Dynamic priority elevation, VIP handling, emergency overrides

### API Specifications
**INotificationRoutingService Interface**: Core routing service contract
```csharp
public interface INotificationRoutingService
{
    Task<List<NotificationRoute>> GetRoutingRulesAsync(string eventType, string templateCategory);
    Task<bool> ShouldDeliverNotificationAsync(NotificationRequest request);
    Task<NotificationPriority> CalculatePriorityAsync(IBusinessEvent eventData);
    Task<List<string>> GetPreferredChannelsAsync(string recipientId, string eventCategory);
    Task<DateTime?> GetOptimalDeliveryTimeAsync(NotificationRequest request);
}
```

**NotificationRoute Model**: Routing decision result
```csharp
public class NotificationRoute
{
    public string RecipientId { get; set; }
    public string RecipientType { get; set; }
    public string Channel { get; set; }
    public NotificationPriority Priority { get; set; }
    public DateTime? ScheduledDeliveryTime { get; set; }
    public Dictionary<string, object> RouteMetadata { get; set; }
}
```

### Component Specifications
**NotificationRoutingService**: Core routing logic implementation
- Rule Evaluation: Flexible expression engine for business rules
- Channel Selection: Multi-channel preference with fallback logic
- Time Filtering: Business hours and quiet hours enforcement
- Priority Calculation: Dynamic priority assignment with escalation
- Dependencies: IRoutingConfigurationService, IBusinessHoursService, IRuleEngine

**RoutingConfigurationService**: Configuration management and caching
- Database Storage: Persistent storage of routing configuration
- Cache Management: In-memory caching with refresh mechanism
- Change Detection: Real-time configuration update handling
- Validation: Rule validation and conflict detection
- Dependencies: LmsDbContext, IMemoryCache, IConfiguration

**RuleEngine**: Flexible rule evaluation engine
- Expression Parsing: Support for complex business logic expressions
- Rule Compilation: Performance optimization through rule compilation
- Context Building: Dynamic context creation for rule evaluation
- Testing Support: Rule simulation and testing capabilities
- Dependencies: System.Linq.Dynamic.Core for expression evaluation

**BusinessHoursService**: Time-based filtering logic
- Calendar Integration: Holiday calendar and business day calculation
- Time Zone Support: Multi-branch time zone handling
- Quiet Hours: Customer communication preference enforcement
- Emergency Override: Critical notification immediate delivery
- Dependencies: IDateTimeProvider, IHolidayCalendarService

### File Locations
- **Core Services**: apps/IntelliFin.Communications/Services/NotificationRoutingService.cs
- **Configuration**: apps/IntelliFin.Communications/Services/RoutingConfigurationService.cs
- **Rule Engine**: apps/IntelliFin.Communications/Services/RuleEngine.cs
- **Business Hours**: apps/IntelliFin.Communications/Services/BusinessHoursService.cs
- **Database Models**: libs/IntelliFin.Shared.DomainModels/Communications/RoutingModels.cs
- **API Controllers**: apps/IntelliFin.Communications/Controllers/RoutingConfigurationController.cs
- **Migrations**: libs/IntelliFin.Shared.DomainModels/Migrations/
- **Unit Tests**: tests/IntelliFin.Communications.Tests/Services/Routing/
- **Integration Tests**: tests/IntelliFin.Communications.Tests/Integration/RoutingTests.cs

### Business Rules
**Event Filtering Criteria**: Configurable business logic filters
- Loan Amount Thresholds: Different notification rules based on loan amounts
- Customer Segmentation: VIP, standard, government employee specific rules
- Branch-based Routing: Branch manager and local staff notification rules
- Product Type Rules: PMEC loans vs. business loans notification differences
- Risk-based Filtering: High-risk customer special handling

**Channel Selection Logic**: Multi-channel preference management
- Primary Channel: Customer's preferred communication method
- Fallback Chain: SMS → Email → InApp for failed deliveries
- Channel Availability: Real-time channel status checking
- Cost Optimization: Prefer lower cost channels when appropriate
- Emergency Override: Critical notifications bypass channel preferences

**Time-Based Filtering Rules**: Business hours and quiet hours enforcement
- Standard Business Hours: 8:00 AM - 5:00 PM local branch time
- Quiet Hours: 10:00 PM - 7:00 AM (no non-critical notifications)
- Weekend Policy: Emergency notifications only on weekends
- Holiday Policy: Reduced notification schedule on public holidays
- Emergency Criteria: Loan default, fraud alerts, system failures

**Priority Escalation Rules**: Dynamic priority management
- Automatic Elevation: Increase priority based on time delay
- VIP Customer Handling: Higher priority for premium customers
- High-Value Transactions: Priority based on transaction amount
- Management Escalation: Automatic escalation to management levels
- Critical System Events: Immediate delivery regardless of filters

### Technical Constraints
- **Rule Evaluation Performance**: ≤100ms per routing decision
- **Configuration Cache TTL**: 5-minute cache refresh interval
- **Rule Complexity Limit**: Maximum 10 conditions per rule
- **Channel Failover Time**: ≤30 seconds for channel fallback
- **Configuration Deployment**: Zero-downtime configuration updates
- **Database Performance**: Optimized queries for routing decisions
[Source: docs/domains/notifications/prd/epic-1-business-event-processing.md#success-metrics]

### Configuration Examples
**High-Value Loan Notification Rule**: Automatic branch manager notification
```json
{
  "ruleName": "High-Value Loan Alert",
  "eventType": "LoanApplicationCreated",
  "conditions": "LoanAmount > 100000",
  "actions": {
    "addRecipient": {
      "type": "BranchManager",
      "channel": "InApp",
      "priority": "High"
    }
  }
}
```

**VIP Customer Channel Preference**: Multi-channel delivery
```json
{
  "recipientId": "VIP-12345",
  "recipientType": "Customer",
  "eventCategory": "LoanOrigination",
  "preferredChannel": "SMS",
  "fallbackChannel": "Email",
  "quietHoursEnabled": false
}
```

**Business Hours Configuration**: Branch-specific hours
```json
{
  "branchId": 5,
  "timeZone": "Africa/Lusaka",
  "businessHours": {
    "monday": { "start": "08:00", "end": "17:00" },
    "friday": { "start": "08:00", "end": "16:00" },
    "saturday": "closed"
  }
}
```

### Integration Points
**BaseNotificationConsumer Enhancement**: Routing service integration
- Pre-processing: Event filtering before notification creation
- Route Calculation: Dynamic recipient and channel selection
- Priority Assignment: Automatic priority calculation
- Delivery Scheduling: Optimal delivery time calculation
- Backward Compatibility: Gradual migration from hardcoded rules

**Admin Interface Integration**: Configuration management UI
- Rule Builder: Visual rule creation and editing interface
- Testing Tools: Rule simulation and validation tools
- Monitoring Dashboard: Routing performance and rule effectiveness
- Approval Workflow: Configuration change approval process
- Audit Trail: Complete change history tracking

### Testing Requirements
**Unit Testing Standards**: Comprehensive routing logic validation
- Rule evaluation with various business scenarios
- Channel preference selection logic
- Time-based filtering edge cases
- Priority calculation accuracy
- Configuration caching behavior

**Integration Testing**: End-to-end routing functionality
- Event processing with dynamic routing
- Configuration updates without service restart
- Multi-channel delivery with fallback testing
- Time zone and business hours validation
- Database performance under load

**Performance Testing**: Routing scalability validation
- High-volume event routing performance
- Configuration cache performance under load
- Rule engine performance with complex rules
- Concurrent routing decision handling
- Memory usage optimization validation

### Testing
**Test File Location**: tests/IntelliFin.Communications.Tests/Services/Routing/
**Testing Frameworks**: xUnit for unit tests, TestContainers for integration tests, NBomber for performance tests
**Testing Patterns**: Rule engine testing, configuration management testing, time-based filtering testing
**Specific Testing Requirements**:
- Rule conflict detection and resolution testing
- Real-time configuration update validation
- Multi-branch time zone handling verification
- Channel failover mechanism testing
- Priority escalation scenario validation

## Change Log
| Date | Version | Description | Author |
|------|---------|-------------|---------|
| 2025-09-16 | 1.0 | Initial story creation for Epic 1 | SM Agent |

## Dev Agent Record
*This section will be populated by the development agent during implementation*

### Agent Model Used
*To be filled by dev agent*

### Debug Log References
*To be filled by dev agent*

### Completion Notes List
*To be filled by dev agent*

### File List
*To be filled by dev agent*

## QA Results
*Results from QA Agent review will be added here*