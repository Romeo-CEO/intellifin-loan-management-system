# Database Architecture

## Overview
The Communications enhancement extends the existing LmsDbContext with communication-specific entities while maintaining complete compatibility with the current database infrastructure. This represents a **zero-to-database architectural change** requiring careful safety measures.

## Database Integration Strategy

### Existing Infrastructure Utilization
**Current State:**
- LmsDbContext in libs/IntelliFin.Shared.DomainModels
- SQL Server Always On with primary and read replica
- Entity Framework Core with migration pipeline
- Existing audit table (AuditEvents) for compliance

**Integration Approach:**
- Extend existing LmsDbContext with communication entities
- Leverage existing connection strings and infrastructure
- Maintain existing repository pattern consistency
- Use existing transaction management

## Entity Design

### NotificationLogs Entity
```csharp
public class NotificationLog
{
    public long Id { get; set; }
    public Guid EventId { get; set; }
    public string RecipientId { get; set; } = string.Empty;
    public string RecipientType { get; set; } = string.Empty; // Customer, LoanOfficer, etc.
    public string Channel { get; set; } = string.Empty; // SMS, Email, InApp, Push
    public int? TemplateId { get; set; }
    public string? Subject { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? PersonalizationData { get; set; } // JSON
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    public string? GatewayResponse { get; set; } // JSON
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public string? FailureReason { get; set; }
    public int RetryCount { get; set; } = 0;
    public int MaxRetries { get; set; } = 3;
    public decimal? Cost { get; set; }
    public string? ExternalId { get; set; } // Provider's message ID
    public int BranchId { get; set; }
    public string CreatedBy { get; set; } = string.Empty;

    // Navigation properties
    public NotificationTemplate? Template { get; set; }
}

public enum NotificationStatus
{
    Pending = 0,
    Queued = 1,
    Sent = 2,
    Delivered = 3,
    Failed = 4,
    Expired = 5
}
```

### NotificationTemplate Entity
```csharp
public class NotificationTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Language { get; set; } = "en";
    public string? Subject { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? PersonalizationTokens { get; set; } // JSON array
    public bool IsActive { get; set; } = true;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int Version { get; set; } = 1;

    // Navigation properties
    public List<NotificationLog> NotificationLogs { get; set; } = new();
}
```

### UserCommunicationPreferences Entity
```csharp
public class UserCommunicationPreferences
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserType { get; set; } = string.Empty;
    public string PreferenceType { get; set; } = string.Empty; // LoanUpdates, Collections, Marketing
    public bool Enabled { get; set; } = true;
    public string? Channels { get; set; } // JSON array: ["SMS", "Email"]
    public string? Frequency { get; set; } // Immediate, Daily, Weekly
    public DateTimeOffset? OptOutDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
```

### EventProcessingStatus Entity
```csharp
public class EventProcessingStatus
{
    public long Id { get; set; }
    public Guid EventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; set; } = DateTimeOffset.UtcNow;
    public string ProcessingResult { get; set; } = string.Empty; // Success, Failed, Skipped
    public string? ErrorDetails { get; set; }
}
```

## Database Schema (SQL Server)

### Table Creation Script
```sql
-- Comprehensive notification audit trail
CREATE TABLE NotificationLogs (
    Id BIGINT IDENTITY(1,1) NOT NULL,
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
    CreatedBy NVARCHAR(100) NOT NULL,

    CONSTRAINT PK_NotificationLogs PRIMARY KEY (Id),
    CONSTRAINT FK_NotificationLogs_Templates FOREIGN KEY (TemplateId)
        REFERENCES NotificationTemplates(Id)
);

-- Template management with versioning
CREATE TABLE NotificationTemplates (
    Id INT IDENTITY(1,1) NOT NULL,
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
    Version INT NOT NULL DEFAULT 1,

    CONSTRAINT PK_NotificationTemplates PRIMARY KEY (Id)
);

-- Customer communication preferences
CREATE TABLE UserCommunicationPreferences (
    Id INT IDENTITY(1,1) NOT NULL,
    UserId NVARCHAR(100) NOT NULL,
    UserType NVARCHAR(50) NOT NULL,
    PreferenceType NVARCHAR(50) NOT NULL,
    Enabled BIT NOT NULL DEFAULT 1,
    Channels NVARCHAR(100) NULL,
    Frequency NVARCHAR(20) NULL,
    OptOutDate DATETIMEOFFSET NULL,
    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    UpdatedAt DATETIMEOFFSET NULL,

    CONSTRAINT PK_UserCommunicationPreferences PRIMARY KEY (Id)
);

-- Event processing idempotency
CREATE TABLE EventProcessingStatus (
    Id BIGINT IDENTITY(1,1) NOT NULL,
    EventId UNIQUEIDENTIFIER NOT NULL,
    EventType NVARCHAR(100) NOT NULL,
    ProcessedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    ProcessingResult NVARCHAR(20) NOT NULL,
    ErrorDetails NVARCHAR(MAX) NULL,

    CONSTRAINT PK_EventProcessingStatus PRIMARY KEY (Id)
);
```

### Index Strategy
```sql
-- NotificationLogs indexes for performance
CREATE INDEX IX_NotificationLogs_Recipient ON NotificationLogs (RecipientId, RecipientType);
CREATE INDEX IX_NotificationLogs_CreatedAt ON NotificationLogs (CreatedAt DESC);
CREATE INDEX IX_NotificationLogs_Status ON NotificationLogs (Status) WHERE Status IN ('Pending', 'Failed');
CREATE INDEX IX_NotificationLogs_ExternalId ON NotificationLogs (ExternalId) WHERE ExternalId IS NOT NULL;
CREATE INDEX IX_NotificationLogs_Branch_Date ON NotificationLogs (BranchId, CreatedAt DESC);

-- NotificationTemplates indexes
CREATE UNIQUE INDEX UX_NotificationTemplates_Name_Version ON NotificationTemplates (Name, Version);
CREATE INDEX IX_NotificationTemplates_Category_Channel ON NotificationTemplates (Category, Channel, IsActive);

-- UserCommunicationPreferences indexes
CREATE UNIQUE INDEX UX_UserCommunicationPreferences ON UserCommunicationPreferences (UserId, UserType, PreferenceType);
CREATE INDEX IX_UserCommunicationPreferences_UserId ON UserCommunicationPreferences (UserId);

-- EventProcessingStatus indexes
CREATE UNIQUE INDEX UX_EventProcessingStatus_EventId ON EventProcessingStatus (EventId);
CREATE INDEX IX_EventProcessingStatus_EventType ON EventProcessingStatus (EventType, ProcessedAt DESC);
```

## Entity Framework Configuration

### DbContext Extension
```csharp
public partial class LmsDbContext : DbContext
{
    // Existing entities...

    // New communication entities
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<UserCommunicationPreferences> UserCommunicationPreferences => Set<UserCommunicationPreferences>();
    public DbSet<EventProcessingStatus> EventProcessingStatus => Set<EventProcessingStatus>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure communication entities
        ConfigureCommunicationEntities(modelBuilder);
    }

    private void ConfigureCommunicationEntities(ModelBuilder modelBuilder)
    {
        // NotificationLog configuration
        modelBuilder.Entity<NotificationLog>(entity =>
        {
            entity.ToTable("NotificationLogs");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.RecipientId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.RecipientType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Channel).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Subject).HasMaxLength(500);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.FailureReason).HasMaxLength(1000);
            entity.Property(e => e.ExternalId).HasMaxLength(100);
            entity.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();

            entity.HasOne(e => e.Template)
                  .WithMany(t => t.NotificationLogs)
                  .HasForeignKey(e => e.TemplateId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // NotificationTemplate configuration
        modelBuilder.Entity<NotificationTemplate>(entity =>
        {
            entity.ToTable("NotificationTemplates");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Channel).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Language).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Subject).HasMaxLength(500);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.CreatedBy).HasMaxLength(100).IsRequired();
            entity.Property(e => e.UpdatedBy).HasMaxLength(100);

            entity.HasIndex(e => new { e.Name, e.Version }).IsUnique();
            entity.HasIndex(e => new { e.Category, e.Channel, e.IsActive });
        });

        // UserCommunicationPreferences configuration
        modelBuilder.Entity<UserCommunicationPreferences>(entity =>
        {
            entity.ToTable("UserCommunicationPreferences");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.UserId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.UserType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.PreferenceType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Channels).HasMaxLength(100);
            entity.Property(e => e.Frequency).HasMaxLength(20);

            entity.HasIndex(e => new { e.UserId, e.UserType, e.PreferenceType }).IsUnique();
        });

        // EventProcessingStatus configuration
        modelBuilder.Entity<EventProcessingStatus>(entity =>
        {
            entity.ToTable("EventProcessingStatus");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.EventType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ProcessingResult).HasMaxLength(20).IsRequired();

            entity.HasIndex(e => e.EventId).IsUnique();
        });
    }
}
```

## Repository Pattern Implementation

### INotificationRepository Interface
```csharp
public interface INotificationRepository
{
    // Create operations
    Task<NotificationLog> CreateAsync(NotificationLog log, CancellationToken cancellationToken = default);
    Task<List<NotificationLog>> CreateBulkAsync(List<NotificationLog> logs, CancellationToken cancellationToken = default);

    // Read operations
    Task<NotificationLog?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<NotificationLog?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default);
    Task<List<NotificationLog>> GetByRecipientAsync(string recipientId, string? channel = null,
        DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);
    Task<PagedResult<NotificationLog>> GetPagedAsync(NotificationLogQuery query, CancellationToken cancellationToken = default);

    // Update operations
    Task UpdateStatusAsync(long id, NotificationStatus status, string? gatewayResponse = null,
        string? failureReason = null, CancellationToken cancellationToken = default);
    Task IncrementRetryCountAsync(long id, CancellationToken cancellationToken = default);

    // Event processing
    Task<bool> IsEventProcessedAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task MarkEventProcessedAsync(Guid eventId, string eventType, bool success,
        string? error = null, CancellationToken cancellationToken = default);

    // Analytics
    Task<NotificationStats> GetStatsAsync(DateTime fromDate, DateTime toDate,
        string? branchId = null, CancellationToken cancellationToken = default);
}
```

### Repository Implementation
```csharp
public class NotificationRepository : INotificationRepository
{
    private readonly LmsDbContext _context;
    private readonly ILogger<NotificationRepository> _logger;

    public NotificationRepository(LmsDbContext context, ILogger<NotificationRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<NotificationLog> CreateAsync(NotificationLog log, CancellationToken cancellationToken = default)
    {
        _context.NotificationLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);
        return log;
    }

    public async Task<NotificationLog?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken = default)
    {
        return await _context.NotificationLogs
            .Include(n => n.Template)
            .FirstOrDefaultAsync(n => n.ExternalId == externalId, cancellationToken);
    }

    public async Task<List<NotificationLog>> GetByRecipientAsync(string recipientId, string? channel = null,
        DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
    {
        var query = _context.NotificationLogs
            .Where(n => n.RecipientId == recipientId);

        if (!string.IsNullOrEmpty(channel))
            query = query.Where(n => n.Channel == channel);

        if (fromDate.HasValue)
            query = query.Where(n => n.CreatedAt >= fromDate);

        if (toDate.HasValue)
            query = query.Where(n => n.CreatedAt <= toDate);

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(100) // Limit to prevent large result sets
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateStatusAsync(long id, NotificationStatus status, string? gatewayResponse = null,
        string? failureReason = null, CancellationToken cancellationToken = default)
    {
        var notification = await _context.NotificationLogs.FindAsync(new object[] { id }, cancellationToken);
        if (notification != null)
        {
            notification.Status = status;
            notification.GatewayResponse = gatewayResponse;
            notification.FailureReason = failureReason;

            if (status == NotificationStatus.Sent)
                notification.SentAt = DateTimeOffset.UtcNow;
            else if (status == NotificationStatus.Delivered)
                notification.DeliveredAt = DateTimeOffset.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> IsEventProcessedAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        return await _context.EventProcessingStatus
            .AnyAsync(e => e.EventId == eventId, cancellationToken);
    }

    public async Task MarkEventProcessedAsync(Guid eventId, string eventType, bool success,
        string? error = null, CancellationToken cancellationToken = default)
    {
        var status = new EventProcessingStatus
        {
            EventId = eventId,
            EventType = eventType,
            ProcessingResult = success ? "Success" : "Failed",
            ErrorDetails = error
        };

        _context.EventProcessingStatus.Add(status);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
```

## Migration Strategy

### Entity Framework Migration
```csharp
public partial class AddCommunicationsEntities : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "NotificationTemplates",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Channel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                Language = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "en"),
                Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                PersonalizationTokens = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                Version = table.Column<int>(type: "int", nullable: false, defaultValue: 1)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_NotificationTemplates", x => x.Id);
            });

        // Create remaining tables and indexes...
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "NotificationLogs");
        migrationBuilder.DropTable(name: "UserCommunicationPreferences");
        migrationBuilder.DropTable(name: "EventProcessingStatus");
        migrationBuilder.DropTable(name: "NotificationTemplates");
    }
}
```

## Performance Considerations

### Query Optimization
- **Pagination**: Implement cursor-based pagination for large result sets
- **Indexing**: Strategic indexes on frequently queried columns
- **Caching**: Redis caching for frequently accessed templates
- **Read Replicas**: Use read replicas for reporting queries

### Connection Management
- **Connection Pooling**: Leverage existing EF Core connection pooling
- **Timeout Configuration**: Appropriate command timeouts
- **Retry Policies**: Automatic retry for transient failures
- **Health Checks**: Database connectivity monitoring

### Data Archival
```csharp
public class NotificationArchivalService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Archive notifications older than 2 years to separate table
            await ArchiveOldNotificationsAsync();

            // Wait 24 hours before next archival
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}
```

This database architecture provides a solid foundation for the Communications enhancement while maintaining safety, performance, and compatibility with existing infrastructure.