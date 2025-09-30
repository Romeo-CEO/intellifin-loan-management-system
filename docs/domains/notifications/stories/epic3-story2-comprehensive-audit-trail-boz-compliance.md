# Epic 3, Story 2: Comprehensive Audit Trail for BoZ Compliance

**Status:** Draft
**Epic:** Database Persistence & Audit Trail
**Story Points:** 13
**Priority:** Critical
**Dependencies:** Epic 3 Story 1 (Database Infrastructure Migration)

## User Story

**As a** compliance officer and BoZ auditor
**I want** complete, immutable audit trails of all customer communications with comprehensive metadata
**So that** we can demonstrate regulatory compliance and provide evidence during BoZ inspections

## Problem Statement

Bank of Zambia (BoZ) regulations require financial institutions to maintain comprehensive audit trails of all customer communications for regulatory compliance. The current Communications service lacks the structured audit capabilities needed to:

- Track every communication attempt, success, and failure
- Maintain immutable records for 10-year retention periods
- Provide queryable history by customer, loan, communication type, and date ranges
- Support regulatory reporting and audit requirements
- Demonstrate compliance with fair lending and collections practices

This story implements the core audit trail infrastructure that will serve as the foundation for all BoZ compliance reporting.

## Acceptance Criteria

### Core Audit Trail Implementation
- [ ] **AC 2.1:** Every notification attempt creates an immutable audit record in NotificationLogs table
- [ ] **AC 2.2:** Audit records capture all required metadata: recipient, channel, content, timestamps, delivery status, costs
- [ ] **AC 2.3:** Support for both successful and failed communication attempts with detailed error information
- [ ] **AC 2.4:** Integration with existing IntelliFin audit system (AuditEvents table) for cross-system traceability
- [ ] **AC 2.5:** Append-only audit design prevents modification or deletion of historical records

### BoZ Compliance Requirements
- [ ] **AC 2.6:** Audit records include required BoZ data points: customer NRC (hashed), loan account numbers, communication purpose
- [ ] **AC 2.7:** Support for compliance classification: Collections, Notifications, Marketing, Regulatory
- [ ] **AC 2.8:** Tracking of opt-out requests and customer communication preferences with timestamps
- [ ] **AC 2.9:** Integration with branch context for multi-branch audit trail segregation
- [ ] **AC 2.10:** Correlation IDs link communications to specific business events (loan status changes, payment due dates)

### Audit Query Capabilities
- [ ] **AC 2.11:** Query audit trail by customer ID with pagination for large result sets
- [ ] **AC 2.12:** Filter by communication channel (SMS, Email, In-App), date ranges, and delivery status
- [ ] **AC 2.13:** Search by business event correlation (loan application, payment reminder, collections notice)
- [ ] **AC 2.14:** Export capabilities for regulatory reporting and external audit requirements
- [ ] **AC 2.15:** Real-time audit trail updates as communications are processed

### Data Integrity and Security
- [ ] **AC 2.16:** Audit records are cryptographically signed to prevent tampering
- [ ] **AC 2.17:** Sensitive customer data (PII) is encrypted at rest using existing TDE infrastructure
- [ ] **AC 2.18:** Access control ensures only authorized compliance and audit personnel can query full audit trails
- [ ] **AC 2.19:** Audit trail queries are themselves audited for compliance oversight
- [ ] **AC 2.20:** Data retention policies automatically applied with 10-year minimum retention

## Technical Implementation

### Enhanced NotificationLog Entity
```csharp
public class NotificationLog
{
    public long Id { get; set; }
    public Guid EventId { get; set; }
    public Guid CorrelationId { get; set; } // Links to business events

    // Recipient Information
    public string RecipientId { get; set; } = string.Empty;
    public string RecipientType { get; set; } = string.Empty; // Customer, LoanOfficer, Compliance
    public string? RecipientNrcHash { get; set; } // Hashed NRC for BoZ compliance
    public string? LoanAccountNumber { get; set; } // Associated loan account

    // Communication Details
    public string Channel { get; set; } = string.Empty; // SMS, Email, InApp, Push
    public int? TemplateId { get; set; }
    public string? Subject { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? PersonalizationData { get; set; } // JSON with customer-specific data

    // Delivery Tracking
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    public string? GatewayResponse { get; set; } // JSON response from SMS/Email provider
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public string? FailureReason { get; set; }
    public int RetryCount { get; set; } = 0;
    public int MaxRetries { get; set; } = 3;

    // Cost and Provider Tracking
    public decimal? Cost { get; set; }
    public string? ProviderId { get; set; } // SMS provider, email service
    public string? ExternalId { get; set; } // Provider's message ID
    public string? ExternalReference { get; set; } // Provider's additional reference

    // BoZ Compliance Fields
    public string ComplianceCategory { get; set; } = string.Empty; // Collections, Notifications, Marketing
    public string BusinessEventType { get; set; } = string.Empty; // LoanApproval, PaymentDue, Collections
    public int BranchId { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? ComplianceNotes { get; set; } // Optional compliance annotations

    // Audit Integrity
    public string? DataHash { get; set; } // Cryptographic hash for integrity verification
    public DateTime? RetentionDate { get; set; } // Calculated 10-year retention date

    // Navigation Properties
    public NotificationTemplate? Template { get; set; }
}

public enum NotificationStatus
{
    Pending = 0,
    Queued = 1,
    Sent = 2,
    Delivered = 3,
    Failed = 4,
    Expired = 5,
    OptedOut = 6,      // Customer opted out
    Blocked = 7        // Compliance block
}

public enum ComplianceCategory
{
    Collections = 1,    // Debt collection communications
    Notifications = 2,  // Service notifications
    Marketing = 3,      // Promotional communications
    Regulatory = 4,     // Required regulatory notices
    Support = 5        // Customer service communications
}
```

### Audit Service Implementation
```csharp
public interface IAuditTrailService
{
    Task<NotificationLog> CreateAuditRecordAsync(CreateAuditRecordRequest request, CancellationToken cancellationToken = default);
    Task UpdateDeliveryStatusAsync(long auditId, NotificationStatus status, string? providerResponse = null, CancellationToken cancellationToken = default);
    Task<PagedResult<NotificationLog>> GetAuditTrailAsync(AuditTrailQuery query, CancellationToken cancellationToken = default);
    Task<AuditTrailSummary> GetComplianceSummaryAsync(ComplianceSummaryQuery query, CancellationToken cancellationToken = default);
    Task<byte[]> ExportAuditTrailAsync(AuditExportRequest request, CancellationToken cancellationToken = default);
}

public class AuditTrailService : IAuditTrailService
{
    private readonly INotificationRepository _repository;
    private readonly IDataHashingService _hashingService;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<AuditTrailService> _logger;

    public async Task<NotificationLog> CreateAuditRecordAsync(CreateAuditRecordRequest request, CancellationToken cancellationToken = default)
    {
        var auditRecord = new NotificationLog
        {
            EventId = request.EventId,
            CorrelationId = request.CorrelationId,
            RecipientId = request.RecipientId,
            RecipientType = request.RecipientType,
            RecipientNrcHash = await _hashingService.HashNrcAsync(request.RecipientNrc),
            LoanAccountNumber = request.LoanAccountNumber,
            Channel = request.Channel,
            TemplateId = request.TemplateId,
            Subject = request.Subject,
            Content = request.Content,
            PersonalizationData = JsonSerializer.Serialize(request.PersonalizationData),
            ComplianceCategory = request.ComplianceCategory.ToString(),
            BusinessEventType = request.BusinessEventType,
            BranchId = request.BranchId,
            CreatedBy = request.CreatedBy,
            ComplianceNotes = request.ComplianceNotes,
            RetentionDate = DateTime.UtcNow.AddYears(10) // BoZ 10-year retention requirement
        };

        // Calculate cryptographic hash for integrity
        auditRecord.DataHash = await _hashingService.CalculateRecordHashAsync(auditRecord);

        return await _repository.CreateAsync(auditRecord, cancellationToken);
    }

    public async Task<PagedResult<NotificationLog>> GetAuditTrailAsync(AuditTrailQuery query, CancellationToken cancellationToken = default)
    {
        // Log the audit query itself for compliance oversight
        await LogAuditQueryAsync(query);

        return await _repository.GetPagedAsync(query, cancellationToken);
    }

    public async Task<AuditTrailSummary> GetComplianceSummaryAsync(ComplianceSummaryQuery query, CancellationToken cancellationToken = default)
    {
        return new AuditTrailSummary
        {
            TotalCommunications = await _repository.GetCountAsync(query.FromDate, query.ToDate, query.BranchId),
            ByChannel = await _repository.GetChannelBreakdownAsync(query.FromDate, query.ToDate, query.BranchId),
            ByComplianceCategory = await _repository.GetComplianceCategoryBreakdownAsync(query.FromDate, query.ToDate, query.BranchId),
            DeliverySuccessRate = await _repository.GetDeliverySuccessRateAsync(query.FromDate, query.ToDate, query.BranchId),
            CostSummary = await _repository.GetCostSummaryAsync(query.FromDate, query.ToDate, query.BranchId)
        };
    }
}
```

### Audit Trail Query Models
```csharp
public class AuditTrailQuery
{
    public string? RecipientId { get; set; }
    public string? LoanAccountNumber { get; set; }
    public List<string>? Channels { get; set; }
    public List<ComplianceCategory>? ComplianceCategories { get; set; }
    public List<NotificationStatus>? Statuses { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int? BranchId { get; set; }
    public string? BusinessEventType { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string? SortBy { get; set; } = "CreatedAt";
    public string? SortDirection { get; set; } = "DESC";
}

public class AuditTrailSummary
{
    public int TotalCommunications { get; set; }
    public Dictionary<string, int> ByChannel { get; set; } = new();
    public Dictionary<ComplianceCategory, int> ByComplianceCategory { get; set; } = new();
    public double DeliverySuccessRate { get; set; }
    public CostSummary CostSummary { get; set; } = new();
}

public class CostSummary
{
    public decimal TotalCost { get; set; }
    public Dictionary<string, decimal> ByChannel { get; set; } = new();
    public Dictionary<string, decimal> ByProvider { get; set; } = new();
}
```

### Integration with Existing Audit System
```csharp
public class NotificationAuditIntegrationService
{
    private readonly IAuditService _existingAuditService; // From IntelliFin.Shared
    private readonly IAuditTrailService _notificationAuditService;

    public async Task LogBusinessEventAsync(BusinessEvent businessEvent, NotificationLog notificationLog)
    {
        // Create cross-reference in existing AuditEvents table
        var auditEvent = new AuditEvent
        {
            EventType = "CommunicationSent",
            EntityType = "NotificationLog",
            EntityId = notificationLog.Id.ToString(),
            UserId = notificationLog.CreatedBy,
            BranchId = notificationLog.BranchId,
            Details = JsonSerializer.Serialize(new
            {
                RecipientId = notificationLog.RecipientId,
                Channel = notificationLog.Channel,
                BusinessEventType = notificationLog.BusinessEventType,
                ComplianceCategory = notificationLog.ComplianceCategory,
                CorrelationId = notificationLog.CorrelationId
            }),
            Timestamp = notificationLog.CreatedAt
        };

        await _existingAuditService.LogEventAsync(auditEvent);
    }
}
```

## Compliance API Endpoints

### Audit Trail Query API
```csharp
[ApiController]
[Route("api/compliance/audit-trail")]
[Authorize(Policy = "ComplianceOfficer")]
public class AuditTrailController : ControllerBase
{
    private readonly IAuditTrailService _auditTrailService;

    [HttpGet]
    public async Task<ActionResult<PagedResult<NotificationLog>>> GetAuditTrail(
        [FromQuery] AuditTrailQuery query,
        CancellationToken cancellationToken = default)
    {
        var result = await _auditTrailService.GetAuditTrailAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("summary")]
    public async Task<ActionResult<AuditTrailSummary>> GetComplianceSummary(
        [FromQuery] ComplianceSummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        var summary = await _auditTrailService.GetComplianceSummaryAsync(query, cancellationToken);
        return Ok(summary);
    }

    [HttpPost("export")]
    public async Task<ActionResult> ExportAuditTrail(
        [FromBody] AuditExportRequest request,
        CancellationToken cancellationToken = default)
    {
        var exportData = await _auditTrailService.ExportAuditTrailAsync(request, cancellationToken);
        return File(exportData, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                   $"audit-trail-{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }
}
```

## Database Indexes for Performance

```sql
-- Audit trail query performance indexes
CREATE INDEX IX_NotificationLogs_Recipient_Date ON NotificationLogs (RecipientId, CreatedAt DESC);
CREATE INDEX IX_NotificationLogs_LoanAccount_Date ON NotificationLogs (LoanAccountNumber, CreatedAt DESC) WHERE LoanAccountNumber IS NOT NULL;
CREATE INDEX IX_NotificationLogs_Compliance_Category ON NotificationLogs (ComplianceCategory, CreatedAt DESC);
CREATE INDEX IX_NotificationLogs_Business_Event ON NotificationLogs (BusinessEventType, CreatedAt DESC);
CREATE INDEX IX_NotificationLogs_Branch_Date ON NotificationLogs (BranchId, CreatedAt DESC);
CREATE INDEX IX_NotificationLogs_Status_Date ON NotificationLogs (Status, CreatedAt DESC);
CREATE INDEX IX_NotificationLogs_Channel_Date ON NotificationLogs (Channel, CreatedAt DESC);

-- Composite indexes for common compliance queries
CREATE INDEX IX_NotificationLogs_Compliance_Branch_Date ON NotificationLogs (ComplianceCategory, BranchId, CreatedAt DESC);
CREATE INDEX IX_NotificationLogs_Recipient_Channel_Date ON NotificationLogs (RecipientId, Channel, CreatedAt DESC);

-- Retention policy index
CREATE INDEX IX_NotificationLogs_Retention_Date ON NotificationLogs (RetentionDate) WHERE RetentionDate IS NOT NULL;
```

## Integration Testing

### Audit Trail Creation Test
```csharp
[Test]
public async Task CreateAuditRecord_ShouldCreateComprehensiveRecord()
{
    // Arrange
    var request = new CreateAuditRecordRequest
    {
        EventId = Guid.NewGuid(),
        CorrelationId = Guid.NewGuid(),
        RecipientId = "CUST001",
        RecipientNrc = "123456/78/9",
        LoanAccountNumber = "LN-2024-001",
        Channel = "SMS",
        ComplianceCategory = ComplianceCategory.Collections,
        BusinessEventType = "PaymentOverdue",
        BranchId = 1,
        CreatedBy = "system"
    };

    // Act
    var auditRecord = await _auditTrailService.CreateAuditRecordAsync(request);

    // Assert
    Assert.That(auditRecord.Id, Is.GreaterThan(0));
    Assert.That(auditRecord.RecipientNrcHash, Is.Not.Null);
    Assert.That(auditRecord.DataHash, Is.Not.Null);
    Assert.That(auditRecord.RetentionDate, Is.EqualTo(DateTime.UtcNow.AddYears(10)).Within(TimeSpan.FromMinutes(1)));
    Assert.That(auditRecord.ComplianceCategory, Is.EqualTo("Collections"));
}
```

### Compliance Query Test
```csharp
[Test]
public async Task GetAuditTrail_ShouldFilterByMultipleCriteria()
{
    // Arrange
    var query = new AuditTrailQuery
    {
        RecipientId = "CUST001",
        Channels = new List<string> { "SMS", "Email" },
        ComplianceCategories = new List<ComplianceCategory> { ComplianceCategory.Collections },
        FromDate = DateTime.UtcNow.AddDays(-30),
        ToDate = DateTime.UtcNow,
        BranchId = 1
    };

    // Act
    var result = await _auditTrailService.GetAuditTrailAsync(query);

    // Assert
    Assert.That(result.Items, Is.Not.Empty);
    Assert.That(result.Items.All(x => x.RecipientId == "CUST001"), Is.True);
    Assert.That(result.Items.All(x => query.Channels.Contains(x.Channel)), Is.True);
    Assert.That(result.Items.All(x => x.ComplianceCategory == "Collections"), Is.True);
}
```

## Success Metrics

### Compliance Metrics
- **Audit Coverage**: 100% of communications captured in audit trail
- **Data Integrity**: 100% of audit records pass cryptographic verification
- **Query Performance**: <2 seconds for standard compliance queries
- **Retention Compliance**: 100% adherence to 10-year retention policy

### Operational Metrics
- **Audit Record Creation**: <100ms additional latency per notification
- **Query Response Time**: <5 seconds for complex multi-criteria queries
- **Export Generation**: <30 seconds for monthly compliance reports
- **Storage Growth**: Predictable growth pattern aligned with communication volume

## Risk Mitigation

### Data Protection
- **Encryption**: All PII encrypted using existing TDE infrastructure
- **Access Control**: Role-based access for compliance queries
- **Data Masking**: Sensitive fields masked in non-production environments
- **Backup**: Integrated with existing backup and disaster recovery

### Performance Protection
- **Query Limits**: Maximum result set sizes to prevent database overload
- **Caching**: Frequently accessed audit summaries cached in Redis
- **Read Replicas**: Compliance queries use read replicas to protect transactional database
- **Archival**: Automated archival of older records to maintain performance

## Dependencies

### Technical Dependencies
- **Epic 3 Story 1**: Database infrastructure must be in place
- **Existing Audit System**: Integration with current AuditEvents table
- **Encryption Services**: Existing TDE and application-level encryption
- **Authorization System**: Role-based access control for compliance officers

### Regulatory Dependencies
- **BoZ Guidelines**: Compliance with Bank of Zambia audit requirements
- **Data Protection Laws**: Adherence to Zambian data protection regulations
- **Industry Standards**: Following banking industry audit trail best practices

## Definition of Done

- [ ] Complete audit trail implementation capturing all required BoZ compliance data
- [ ] Integration with existing IntelliFin audit system for cross-system traceability
- [ ] Comprehensive query capabilities supporting all compliance use cases
- [ ] Performance-optimized database indexes and query patterns
- [ ] Data integrity and encryption measures protecting sensitive information
- [ ] API endpoints for compliance officers and external auditors
- [ ] Export capabilities for regulatory reporting requirements
- [ ] Integration tests validating all audit trail scenarios
- [ ] Documentation for compliance procedures and audit access
- [ ] Performance testing confirming query response times meet requirements

## Notes

This audit trail implementation is **fundamental to BoZ compliance** and serves as the foundation for all regulatory reporting requirements. The comprehensive metadata capture ensures that IntelliFin can demonstrate full transparency in customer communications during regulatory inspections.

The integration with existing audit systems maintains consistency across the platform while providing the specialized capabilities needed for communication compliance monitoring.