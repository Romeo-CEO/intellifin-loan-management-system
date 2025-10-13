using IntelliFin.AdminService.Models;
using Microsoft.EntityFrameworkCore;

namespace IntelliFin.AdminService.Data;

public class AdminDbContext(DbContextOptions<AdminDbContext> options) : DbContext(options)
{
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<AuditChainVerification> AuditChainVerifications => Set<AuditChainVerification>();
    public DbSet<SecurityIncident> SecurityIncidents => Set<SecurityIncident>();
    public DbSet<UserIdMapping> UserIdMappings => Set<UserIdMapping>();
    public DbSet<AuditArchiveMetadata> AuditArchiveMetadata => Set<AuditArchiveMetadata>();
    public DbSet<OfflineMergeHistory> OfflineMergeHistory => Set<OfflineMergeHistory>();
    public DbSet<ElevationRequest> ElevationRequests => Set<ElevationRequest>();
    public DbSet<MfaConfiguration> MfaConfiguration => Set<MfaConfiguration>();
    public DbSet<MfaChallenge> MfaChallenges => Set<MfaChallenge>();
    public DbSet<MfaEnrollment> MfaEnrollments => Set<MfaEnrollment>();
    public DbSet<SodPolicy> SodPolicies => Set<SodPolicy>();
    public DbSet<SodException> SodExceptions => Set<SodException>();
    public DbSet<RoleDefinition> RoleDefinitions => Set<RoleDefinition>();
    public DbSet<RoleHierarchyEntry> RoleHierarchy => Set<RoleHierarchyEntry>();
    public DbSet<ConfigurationPolicy> ConfigurationPolicies => Set<ConfigurationPolicy>();
    public DbSet<ConfigurationChange> ConfigurationChanges => Set<ConfigurationChange>();
    public DbSet<ConfigurationRollback> ConfigurationRollbacks => Set<ConfigurationRollback>();
    public DbSet<VaultLeaseRecord> VaultLeaseRecords => Set<VaultLeaseRecord>();
    public DbSet<RecertificationCampaign> RecertificationCampaigns => Set<RecertificationCampaign>();
    public DbSet<RecertificationTask> RecertificationTasks => Set<RecertificationTask>();
    public DbSet<RecertificationReview> RecertificationReviews => Set<RecertificationReview>();
    public DbSet<RecertificationEscalation> RecertificationEscalations => Set<RecertificationEscalation>();
    public DbSet<RecertificationReport> RecertificationReports => Set<RecertificationReport>();
    public DbSet<ContainerImage> ContainerImages => Set<ContainerImage>();
    public DbSet<Vulnerability> Vulnerabilities => Set<Vulnerability>();
    public DbSet<SignatureVerificationAudit> SignatureVerificationAudits => Set<SignatureVerificationAudit>();
    public DbSet<BastionAccessRequest> BastionAccessRequests => Set<BastionAccessRequest>();
    public DbSet<BastionSession> BastionSessions => Set<BastionSession>();
    public DbSet<EmergencyAccessLog> EmergencyAccessLogs => Set<EmergencyAccessLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.ToTable("AuditEvents");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.EventId)
                  .HasDefaultValueSql("NEWID()");

            entity.Property(e => e.Timestamp)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.Actor)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.Action)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.EntityType)
                  .HasMaxLength(100);

            entity.Property(e => e.EntityId)
                  .HasMaxLength(100);

            entity.Property(e => e.CorrelationId)
                  .HasMaxLength(100);

            entity.Property(e => e.IpAddress)
                  .HasMaxLength(45);

            entity.Property(e => e.UserAgent)
                  .HasMaxLength(500);

            entity.Property(e => e.MigrationSource)
                  .HasMaxLength(50);

            entity.Property(e => e.PreviousEventHash)
                  .HasMaxLength(64);

            entity.Property(e => e.CurrentEventHash)
                  .HasMaxLength(64);

            entity.Property(e => e.IntegrityStatus)
                  .HasMaxLength(20)
                  .HasDefaultValue("UNVERIFIED")
                  .IsRequired();

            entity.Property(e => e.IsGenesisEvent)
                  .HasDefaultValue(false);

            entity.Property(e => e.LastVerifiedAt);

            entity.Property(e => e.EventData)
                  .HasColumnType("nvarchar(max)");

            entity.Property(e => e.IsOfflineEvent)
                  .HasDefaultValue(false);

            entity.Property(e => e.OfflineDeviceId)
                  .HasMaxLength(100);

            entity.Property(e => e.OfflineSessionId)
                  .HasMaxLength(100);

            entity.Property(e => e.OfflineMergeId);

            entity.Property(e => e.OriginalHash)
                  .HasMaxLength(64);

            entity.HasIndex(e => e.Timestamp).HasDatabaseName("IX_AuditEvents_Timestamp");
            entity.HasIndex(e => e.Actor).HasDatabaseName("IX_AuditEvents_Actor");
            entity.HasIndex(e => e.CorrelationId).HasDatabaseName("IX_AuditEvents_CorrelationId");
            entity.HasIndex(e => new { e.EntityType, e.EntityId }).HasDatabaseName("IX_AuditEvents_Entity");
            entity.HasIndex(e => e.EventId).IsUnique().HasDatabaseName("IX_AuditEvents_EventId");
            entity.HasIndex(e => new { e.Timestamp, e.CurrentEventHash }).HasDatabaseName("IX_AuditEvents_Timestamp_Hash");
            entity.HasIndex(e => e.OfflineMergeId).HasDatabaseName("IX_AuditEvents_OfflineMergeId");
        });

        modelBuilder.Entity<AuditChainVerification>(entity =>
        {
            entity.ToTable("AuditChainVerifications");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.VerificationId)
                  .HasDefaultValueSql("NEWID()");

            entity.Property(e => e.StartTime)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.ChainStatus)
                  .HasMaxLength(20)
                  .IsRequired();

            entity.Property(e => e.InitiatedBy)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.EndTime)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.VerificationId).HasDatabaseName("IX_AuditChainVerifications_VerificationId");
            entity.HasIndex(e => e.StartTime).HasDatabaseName("IX_AuditChainVerifications_StartTime");
        });

        modelBuilder.Entity<SecurityIncident>(entity =>
        {
            entity.ToTable("SecurityIncidents");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.IncidentId)
                  .HasDefaultValueSql("NEWID()");

            entity.Property(e => e.IncidentType)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.Severity)
                  .HasMaxLength(20)
                  .IsRequired();

            entity.Property(e => e.DetectedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.ResolutionStatus)
                  .HasMaxLength(20)
                  .HasDefaultValue("OPEN")
                  .IsRequired();

            entity.Property(e => e.AffectedEntityType)
                  .HasMaxLength(100);

            entity.Property(e => e.AffectedEntityId)
                  .HasMaxLength(100);

            entity.Property(e => e.ResolvedBy)
                  .HasMaxLength(100);

            entity.HasIndex(e => e.DetectedAt).HasDatabaseName("IX_SecurityIncidents_DetectedAt");
            entity.HasIndex(e => e.IncidentType).HasDatabaseName("IX_SecurityIncidents_Type");
        });

        modelBuilder.Entity<UserIdMapping>(entity =>
        {
            entity.ToTable("UserIdMapping");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.AspNetUserId)
                  .HasMaxLength(450)
                  .IsRequired();

            entity.Property(e => e.KeycloakUserId)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.MigrationDate)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.AspNetUserId)
                  .IsUnique()
                  .HasDatabaseName("IX_AspNetUserId");

            entity.HasIndex(e => e.KeycloakUserId)
                  .IsUnique()
                  .HasDatabaseName("IX_KeycloakUserId");
        });

        modelBuilder.Entity<AuditArchiveMetadata>(entity =>
        {
            entity.ToTable("AuditArchiveMetadata");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ArchiveId)
                  .HasDefaultValueSql("NEWID()");

            entity.Property(e => e.FileName)
                  .HasMaxLength(500)
                  .IsRequired();

            entity.Property(e => e.ObjectKey)
                  .HasMaxLength(1000)
                  .IsRequired();

            entity.Property(e => e.CompressionRatio)
                  .HasColumnType("decimal(5,2)")
                  .HasDefaultValue(0m);

            entity.Property(e => e.ChainStartHash)
                  .HasMaxLength(64);

            entity.Property(e => e.ChainEndHash)
                  .HasMaxLength(64);

            entity.Property(e => e.PreviousDayEndHash)
                  .HasMaxLength(64);

            entity.Property(e => e.StorageLocation)
                  .HasMaxLength(100)
                  .HasDefaultValue("PRIMARY")
                  .IsRequired();

            entity.Property(e => e.ReplicationStatus)
                  .HasMaxLength(20)
                  .HasDefaultValue("PENDING")
                  .IsRequired();

            entity.Property(e => e.LastAccessedBy)
                  .HasMaxLength(100);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.ExportDate).HasDatabaseName("IX_AuditArchiveMetadata_ExportDate");
            entity.HasIndex(e => new { e.EventDateStart, e.EventDateEnd }).HasDatabaseName("IX_AuditArchiveMetadata_EventDateRange");
            entity.HasIndex(e => e.ObjectKey).HasDatabaseName("IX_AuditArchiveMetadata_ObjectKey");
        });

        modelBuilder.Entity<OfflineMergeHistory>(entity =>
        {
            entity.ToTable("OfflineMergeHistory");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.MergeId)
                  .HasDefaultValueSql("NEWID()");

            entity.Property(e => e.MergeTimestamp)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.UserId)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.DeviceId)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.OfflineSessionId)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.Status)
                  .HasMaxLength(20)
                  .IsRequired();

            entity.Property(e => e.ErrorDetails)
                  .HasColumnType("nvarchar(max)");

            entity.HasIndex(e => e.MergeTimestamp).HasDatabaseName("IX_OfflineMergeHistory_MergeTimestamp");
            entity.HasIndex(e => e.UserId).HasDatabaseName("IX_OfflineMergeHistory_UserId");
            entity.HasIndex(e => e.MergeId).IsUnique().HasDatabaseName("IX_OfflineMergeHistory_MergeId");
        });

        modelBuilder.Entity<ElevationRequest>(entity =>
        {
            entity.ToTable("ElevationRequests");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.ElevationId)
                  .HasDefaultValueSql("NEWID()");

            entity.Property(e => e.UserId)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.UserName)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.RequestedRoles)
                  .HasColumnType("nvarchar(max)")
                  .IsRequired();

            entity.Property(e => e.Justification)
                  .HasMaxLength(1000)
                  .IsRequired();

            entity.Property(e => e.ManagerId)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.ManagerName)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.Status)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.CamundaProcessInstanceId)
                  .HasMaxLength(100);

            entity.Property(e => e.ApprovedBy)
                  .HasMaxLength(100);

            entity.Property(e => e.RejectedBy)
                  .HasMaxLength(100);

            entity.Property(e => e.RejectionReason)
                  .HasMaxLength(500);

            entity.Property(e => e.RevokedBy)
                  .HasMaxLength(100);

            entity.Property(e => e.RevocationReason)
                  .HasMaxLength(500);

            entity.Property(e => e.CorrelationId)
                  .HasMaxLength(100);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.UpdatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.ElevationId).IsUnique().HasDatabaseName("IX_ElevationRequests_ElevationId");
            entity.HasIndex(e => e.UserId).HasDatabaseName("IX_ElevationRequests_UserId");
            entity.HasIndex(e => e.ManagerId).HasDatabaseName("IX_ElevationRequests_ManagerId");
            entity.HasIndex(e => e.Status).HasDatabaseName("IX_ElevationRequests_Status");
            entity.HasIndex(e => e.ExpiresAt).HasDatabaseName("IX_ElevationRequests_ExpiresAt");
        });

        modelBuilder.Entity<MfaConfiguration>(entity =>
        {
            entity.ToTable("MfaConfiguration");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.OperationName)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.RequiresMfa)
                  .HasDefaultValue(true)
                  .IsRequired();

            entity.Property(e => e.TimeoutMinutes)
                  .HasDefaultValue(15)
                  .IsRequired();

            entity.Property(e => e.Description)
                  .HasMaxLength(500);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.UpdatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.UpdatedBy)
                  .HasMaxLength(100);

            entity.HasIndex(e => e.OperationName)
                  .IsUnique()
                  .HasDatabaseName("IX_MfaConfiguration_OperationName");

            entity.HasData(
                new MfaConfiguration { Id = 1, OperationName = "LoanApproval.HighValue", RequiresMfa = true, TimeoutMinutes = 15, Description = "Loan approvals over $50,000" },
                new MfaConfiguration { Id = 2, OperationName = "RoleManagement.Assign", RequiresMfa = true, TimeoutMinutes = 15, Description = "Role assignment to users" },
                new MfaConfiguration { Id = 3, OperationName = "RoleManagement.Remove", RequiresMfa = true, TimeoutMinutes = 15, Description = "Role removal from users" },
                new MfaConfiguration { Id = 4, OperationName = "UserManagement.Create", RequiresMfa = true, TimeoutMinutes = 15, Description = "User account creation" },
                new MfaConfiguration { Id = 5, OperationName = "UserManagement.Delete", RequiresMfa = true, TimeoutMinutes = 15, Description = "User account deletion" },
                new MfaConfiguration { Id = 6, OperationName = "Configuration.Update", RequiresMfa = true, TimeoutMinutes = 15, Description = "Sensitive configuration changes" },
                new MfaConfiguration { Id = 7, OperationName = "DataExport.CustomerPII", RequiresMfa = true, TimeoutMinutes = 15, Description = "Customer PII data export" },
                new MfaConfiguration { Id = 8, OperationName = "AuditLog.Export", RequiresMfa = true, TimeoutMinutes = 15, Description = "Audit log export" }
            );
        });

        modelBuilder.Entity<MfaChallenge>(entity =>
        {
            entity.ToTable("MfaChallenges");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ChallengeId)
                  .HasDefaultValueSql("NEWID()");

            entity.Property(e => e.UserId)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.UserName)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.Operation)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.ChallengeCode)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.Status)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.CamundaProcessInstanceId)
                  .HasMaxLength(100);

            entity.Property(e => e.IpAddress)
                  .HasMaxLength(45);

            entity.Property(e => e.UserAgent)
                  .HasMaxLength(500);

            entity.Property(e => e.CorrelationId)
                  .HasMaxLength(100);

            entity.Property(e => e.InitiatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.ExpiresAt)
                  .IsRequired();

            entity.HasIndex(e => e.ChallengeId)
                  .IsUnique()
                  .HasDatabaseName("IX_MfaChallenges_ChallengeId");

            entity.HasIndex(e => e.UserId)
                  .HasDatabaseName("IX_MfaChallenges_UserId");

            entity.HasIndex(e => e.Status)
                  .HasDatabaseName("IX_MfaChallenges_Status");

            entity.HasIndex(e => e.InitiatedAt)
                  .HasDatabaseName("IX_MfaChallenges_InitiatedAt");

            entity.HasIndex(e => e.ExpiresAt)
                  .HasDatabaseName("IX_MfaChallenges_ExpiresAt");
        });

        modelBuilder.Entity<MfaEnrollment>(entity =>
        {
            entity.ToTable("MfaEnrollments");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.UserId)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.UserName)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.SecretKey)
                  .HasMaxLength(200);

            entity.Property(e => e.Enrolled)
                  .HasDefaultValue(false)
                  .IsRequired();

            entity.HasIndex(e => e.UserId)
                  .IsUnique()
                  .HasDatabaseName("IX_MfaEnrollments_UserId");
        });

        modelBuilder.Entity<RoleDefinition>(entity =>
        {
            entity.ToTable("RoleDefinitions");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.RoleName)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.DisplayName)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.Description)
                  .HasMaxLength(1000);

            entity.Property(e => e.Category)
                  .HasMaxLength(50);

            entity.Property(e => e.RiskLevel)
                  .HasMaxLength(20);

            entity.Property(e => e.ApprovalWorkflow)
                  .HasMaxLength(100);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.RoleName)
                  .IsUnique()
                  .HasDatabaseName("IX_RoleDefinitions_RoleName");

            entity.HasData(
                new RoleDefinition { Id = 1, RoleName = "Loan Officer", DisplayName = "Loan Officer", Description = "Originates and manages loan applications", Category = "Business", RiskLevel = "Medium", RequiresApproval = false },
                new RoleDefinition { Id = 2, RoleName = "Loan Processor", DisplayName = "Loan Processor", Description = "Processes loan documentation and verification", Category = "Business", RiskLevel = "Medium", RequiresApproval = false },
                new RoleDefinition { Id = 3, RoleName = "Loan Approver", DisplayName = "Loan Approver", Description = "Approves or rejects loan applications", Category = "Business", RiskLevel = "High", RequiresApproval = true },
                new RoleDefinition { Id = 4, RoleName = "Credit Analyst", DisplayName = "Credit Analyst", Description = "Analyzes creditworthiness and risk", Category = "Business", RiskLevel = "Medium", RequiresApproval = false },
                new RoleDefinition { Id = 5, RoleName = "System Administrator", DisplayName = "System Administrator", Description = "Technical system administration", Category = "Technical", RiskLevel = "Critical", RequiresApproval = true },
                new RoleDefinition { Id = 6, RoleName = "CEO", DisplayName = "Chief Executive Officer", Description = "Executive authority over all operations", Category = "Management", RiskLevel = "Critical", RequiresApproval = true },
                new RoleDefinition { Id = 7, RoleName = "Collections Officer", DisplayName = "Collections Officer", Description = "Manages overdue loans and collections", Category = "Business", RiskLevel = "Medium", RequiresApproval = false },
                new RoleDefinition { Id = 8, RoleName = "Compliance Officer", DisplayName = "Compliance Officer", Description = "Ensures regulatory compliance", Category = "Management", RiskLevel = "High", RequiresApproval = true },
                new RoleDefinition { Id = 9, RoleName = "Treasury Officer", DisplayName = "Treasury Officer", Description = "Manages disbursements and cash flow", Category = "Business", RiskLevel = "High", RequiresApproval = true },
                new RoleDefinition { Id = 10, RoleName = "GL Accountant", DisplayName = "General Ledger Accountant", Description = "Records journal entries and financial transactions", Category = "Business", RiskLevel = "High", RequiresApproval = false },
                new RoleDefinition { Id = 11, RoleName = "Auditor", DisplayName = "Auditor", Description = "Reviews audit logs and compliance (read-only)", Category = "Management", RiskLevel = "Medium", RequiresApproval = true },
                new RoleDefinition { Id = 12, RoleName = "Risk Manager", DisplayName = "Risk Manager", Description = "Assesses and manages institutional risk", Category = "Management", RiskLevel = "High", RequiresApproval = true },
                new RoleDefinition { Id = 13, RoleName = "Branch Manager", DisplayName = "Branch Manager", Description = "Supervises branch operations", Category = "Management", RiskLevel = "High", RequiresApproval = true }
            );
        });

        modelBuilder.Entity<RoleHierarchyEntry>(entity =>
        {
            entity.ToTable("RoleHierarchy");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ParentRole)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.ChildRole)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => new { e.ParentRole, e.ChildRole })
                  .IsUnique()
                  .HasDatabaseName("IX_RoleHierarchy_ParentChild");

            entity.HasIndex(e => e.ParentRole)
                  .HasDatabaseName("IX_RoleHierarchy_ParentRole");

            entity.HasIndex(e => e.ChildRole)
                  .HasDatabaseName("IX_RoleHierarchy_ChildRole");

            entity.HasData(
                new RoleHierarchyEntry { Id = 1, ParentRole = "CEO", ChildRole = "Branch Manager" },
                new RoleHierarchyEntry { Id = 2, ParentRole = "CEO", ChildRole = "Compliance Officer" },
                new RoleHierarchyEntry { Id = 3, ParentRole = "CEO", ChildRole = "Risk Manager" },
                new RoleHierarchyEntry { Id = 4, ParentRole = "CEO", ChildRole = "Loan Approver" },
                new RoleHierarchyEntry { Id = 5, ParentRole = "Branch Manager", ChildRole = "Loan Officer" },
                new RoleHierarchyEntry { Id = 6, ParentRole = "Branch Manager", ChildRole = "Loan Processor" },
                new RoleHierarchyEntry { Id = 7, ParentRole = "Branch Manager", ChildRole = "Collections Officer" },
                new RoleHierarchyEntry { Id = 8, ParentRole = "Compliance Officer", ChildRole = "Auditor" }
            );
        });

        modelBuilder.Entity<SodPolicy>(entity =>
        {
            entity.ToTable("SodPolicies");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Role1)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.Role2)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.ConflictDescription)
                  .HasMaxLength(500)
                  .IsRequired();

            entity.Property(e => e.Severity)
                  .HasMaxLength(20)
                  .IsRequired();

            entity.Property(e => e.UpdatedBy)
                  .HasMaxLength(100);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.UpdatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => new { e.Role1, e.Role2 })
                  .IsUnique()
                  .HasDatabaseName("UQ_SodPolicies_RolePair");

            entity.HasIndex(e => e.Role1)
                  .HasDatabaseName("IX_SodPolicies_Role1");

            entity.HasIndex(e => e.Role2)
                  .HasDatabaseName("IX_SodPolicies_Role2");

            entity.HasIndex(e => e.Enabled)
                  .HasDatabaseName("IX_SodPolicies_Enabled");

            entity.HasData(
                new SodPolicy { Id = 1, Role1 = "Loan Processor", Role2 = "Loan Approver", ConflictDescription = "Cannot create and approve own loans", Severity = "Critical", Enabled = true },
                new SodPolicy { Id = 2, Role1 = "Treasury Officer", Role2 = "GL Accountant", ConflictDescription = "Cannot disburse and record the same transaction", Severity = "Critical", Enabled = true },
                new SodPolicy { Id = 3, Role1 = "Collections Officer", Role2 = "Loan Officer", ConflictDescription = "Cannot originate and collect same loans", Severity = "High", Enabled = true },
                new SodPolicy { Id = 4, Role1 = "Loan Officer", Role2 = "Auditor", ConflictDescription = "Cannot create loans and audit own work", Severity = "High", Enabled = true },
                new SodPolicy { Id = 5, Role1 = "System Administrator", Role2 = "CEO", ConflictDescription = "Technical vs business role separation", Severity = "Medium", Enabled = true },
                new SodPolicy { Id = 6, Role1 = "Loan Processor", Role2 = "Collections Officer", ConflictDescription = "Cannot process and collect the same loans", Severity = "Medium", Enabled = true }
            );
        });

        modelBuilder.Entity<SodException>(entity =>
        {
            entity.ToTable("SodExceptions");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ExceptionId)
                  .HasDefaultValueSql("NEWID()");

            entity.Property(e => e.UserId)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.UserName)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.RequestedRole)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.ConflictingRolesJson)
                  .HasColumnName("ConflictingRoles")
                  .HasColumnType("nvarchar(max)")
                  .IsRequired();

            entity.Property(e => e.BusinessJustification)
                  .HasMaxLength(1000)
                  .IsRequired();

            entity.Property(e => e.Status)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.RequestedBy)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.ReviewedBy)
                  .HasMaxLength(100);

            entity.Property(e => e.ReviewComments)
                  .HasMaxLength(1000);

            entity.Property(e => e.CamundaProcessInstanceId)
                  .HasMaxLength(100);

            entity.Property(e => e.CorrelationId)
                  .HasMaxLength(100);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.UpdatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.ExceptionId)
                  .IsUnique()
                  .HasDatabaseName("IX_SodExceptions_ExceptionId");

            entity.HasIndex(e => e.UserId)
                  .HasDatabaseName("IX_SodExceptions_UserId");

            entity.HasIndex(e => e.Status)
                  .HasDatabaseName("IX_SodExceptions_Status");

            entity.HasIndex(e => e.ExpiresAt)
                  .HasDatabaseName("IX_SodExceptions_ExpiresAt");
        });

        modelBuilder.Entity<ConfigurationPolicy>(entity =>
        {
            entity.ToTable("ConfigurationPolicies");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ConfigKey)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.Category)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.ApprovalWorkflow)
                  .HasMaxLength(100);

            entity.Property(e => e.Sensitivity)
                  .HasMaxLength(20)
                  .IsRequired();

            entity.Property(e => e.AllowedValuesRegex)
                  .HasMaxLength(500);

            entity.Property(e => e.Description)
                  .HasMaxLength(1000);

            entity.Property(e => e.KubernetesNamespace)
                  .HasMaxLength(100);

            entity.Property(e => e.KubernetesConfigMap)
                  .HasMaxLength(100);

            entity.Property(e => e.ConfigMapKey)
                  .HasMaxLength(200);

            entity.Property(e => e.UpdatedBy)
                  .HasMaxLength(100);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.UpdatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.ConfigKey)
                  .IsUnique()
                  .HasDatabaseName("IX_ConfigurationPolicies_ConfigKey");

            entity.HasIndex(e => e.Category)
                  .HasDatabaseName("IX_ConfigurationPolicies_Category");

            entity.HasIndex(e => e.Sensitivity)
                  .HasDatabaseName("IX_ConfigurationPolicies_Sensitivity");

            entity.HasData(
                new ConfigurationPolicy
                {
                    Id = 1,
                    ConfigKey = "jwt.expiry.minutes",
                    Category = "Security",
                    RequiresApproval = true,
                    Sensitivity = "High",
                    Description = "JWT token expiration time in minutes",
                    KubernetesNamespace = "default",
                    KubernetesConfigMap = "identity-service-config",
                    ConfigMapKey = "JwtExpiryMinutes"
                },
                new ConfigurationPolicy
                {
                    Id = 2,
                    ConfigKey = "jwt.refresh.expiry.days",
                    Category = "Security",
                    RequiresApproval = true,
                    Sensitivity = "High",
                    Description = "Refresh token expiration time in days",
                    KubernetesNamespace = "default",
                    KubernetesConfigMap = "identity-service-config",
                    ConfigMapKey = "RefreshExpiryDays"
                },
                new ConfigurationPolicy
                {
                    Id = 3,
                    ConfigKey = "loan.approval.threshold",
                    Category = "Application",
                    RequiresApproval = true,
                    Sensitivity = "Critical",
                    Description = "Loan amount requiring senior approval",
                    KubernetesNamespace = "default",
                    KubernetesConfigMap = "loan-service-config",
                    ConfigMapKey = "ApprovalThreshold"
                },
                new ConfigurationPolicy
                {
                    Id = 4,
                    ConfigKey = "audit.retention.days",
                    Category = "Security",
                    RequiresApproval = true,
                    Sensitivity = "High",
                    Description = "Audit log retention period in days",
                    KubernetesNamespace = "default",
                    KubernetesConfigMap = "admin-service-config",
                    ConfigMapKey = "AuditRetentionDays"
                },
                new ConfigurationPolicy
                {
                    Id = 5,
                    ConfigKey = "api.rate.limit.requests",
                    Category = "Infrastructure",
                    RequiresApproval = false,
                    Sensitivity = "Medium",
                    Description = "API rate limit requests per minute",
                    KubernetesNamespace = "default",
                    KubernetesConfigMap = "api-gateway-config",
                    ConfigMapKey = "RateLimitRequests"
                },
                new ConfigurationPolicy
                {
                    Id = 6,
                    ConfigKey = "logging.level",
                    Category = "Application",
                    RequiresApproval = false,
                    Sensitivity = "Low",
                    Description = "Application logging level (Debug, Info, Warning, Error)",
                    AllowedValuesList = "[\"Debug\",\"Info\",\"Warning\",\"Error\"]",
                    KubernetesNamespace = "default",
                    KubernetesConfigMap = "api-gateway-config",
                    ConfigMapKey = "LogLevel"
                },
                new ConfigurationPolicy
                {
                    Id = 7,
                    ConfigKey = "mfa.required.threshold",
                    Category = "Security",
                    RequiresApproval = true,
                    Sensitivity = "Critical",
                    Description = "Transaction amount requiring MFA",
                    KubernetesNamespace = "default",
                    KubernetesConfigMap = "identity-service-config",
                    ConfigMapKey = "MfaThreshold"
                },
                new ConfigurationPolicy
                {
                    Id = 8,
                    ConfigKey = "database.connection.timeout",
                    Category = "Infrastructure",
                    RequiresApproval = false,
                    Sensitivity = "Medium",
                    Description = "Database connection timeout in seconds",
                    KubernetesNamespace = "default",
                    KubernetesConfigMap = "loan-service-config",
                    ConfigMapKey = "DbConnectionTimeout"
                }
            );
        });

        modelBuilder.Entity<ConfigurationChange>(entity =>
        {
            entity.ToTable("ConfigurationChanges");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ChangeRequestId)
                  .HasDefaultValueSql("NEWID()");

            entity.Property(e => e.ConfigKey)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.Justification)
                  .HasMaxLength(1000)
                  .IsRequired();

            entity.Property(e => e.Category)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.Status)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.Sensitivity)
                  .HasMaxLength(20)
                  .IsRequired();

            entity.Property(e => e.RequestedBy)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.ApprovedBy)
                  .HasMaxLength(100);

            entity.Property(e => e.RejectedBy)
                  .HasMaxLength(100);

            entity.Property(e => e.RejectionReason)
                  .HasMaxLength(500);

            entity.Property(e => e.GitCommitSha)
                  .HasMaxLength(100);

            entity.Property(e => e.GitRepository)
                  .HasMaxLength(200);

            entity.Property(e => e.GitBranch)
                  .HasMaxLength(100);

            entity.Property(e => e.KubernetesNamespace)
                  .HasMaxLength(100);

            entity.Property(e => e.KubernetesConfigMap)
                  .HasMaxLength(100);

            entity.Property(e => e.ConfigMapKey)
                  .HasMaxLength(200);

            entity.Property(e => e.CamundaProcessInstanceId)
                  .HasMaxLength(100);

            entity.Property(e => e.CorrelationId)
                  .HasMaxLength(100);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.UpdatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.ConfigKey)
                  .HasDatabaseName("IX_ConfigurationChanges_ConfigKey");

            entity.HasIndex(e => e.Status)
                  .HasDatabaseName("IX_ConfigurationChanges_Status");

            entity.HasIndex(e => e.RequestedAt)
                  .HasDatabaseName("IX_ConfigurationChanges_RequestedAt");

            entity.HasIndex(e => e.ChangeRequestId)
                  .IsUnique()
                  .HasDatabaseName("IX_ConfigurationChanges_ChangeRequestId");
        });

        modelBuilder.Entity<ConfigurationRollback>(entity =>
        {
            entity.ToTable("ConfigurationRollbacks");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.RollbackId)
                  .HasDefaultValueSql("NEWID()");

            entity.Property(e => e.ConfigKey)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.RolledBackValue)
                  .HasColumnType("nvarchar(max)");

            entity.Property(e => e.Reason)
                  .HasMaxLength(500)
                  .IsRequired();

            entity.Property(e => e.RolledBackBy)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.RolledBackAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.OriginalChangeRequestId)
                  .HasDatabaseName("IX_ConfigurationRollbacks_OriginalId");

            entity.HasIndex(e => e.NewChangeRequestId)
                  .HasDatabaseName("IX_ConfigurationRollbacks_NewId");
        });

        modelBuilder.Entity<VaultLeaseRecord>(entity =>
        {
            entity.ToTable("VaultLeaseRecords");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.LeaseId)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.ServiceName)
                  .HasMaxLength(150)
                  .IsRequired();

            entity.Property(e => e.DatabaseName)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.Username)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.Status)
                  .HasMaxLength(40)
                  .HasDefaultValue(VaultLeaseStatus.Active)
                  .IsRequired();

            entity.Property(e => e.MetadataJson)
                  .HasColumnType("nvarchar(max)");

            entity.Property(e => e.CorrelationId)
                  .HasMaxLength(100);

            entity.Property(e => e.RevokedBy)
                  .HasMaxLength(100);

            entity.Property(e => e.RevocationReason)
                  .HasMaxLength(500);

            entity.Property(e => e.CreatedAtUtc)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.UpdatedAtUtc)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.LeaseId)
                  .IsUnique()
                  .HasDatabaseName("IX_VaultLeaseRecords_LeaseId");

            entity.HasIndex(e => e.ServiceName)
                  .HasDatabaseName("IX_VaultLeaseRecords_ServiceName");

            entity.HasIndex(e => e.Status)
                  .HasDatabaseName("IX_VaultLeaseRecords_Status");

            entity.HasIndex(e => e.ExpiresAtUtc)
                  .HasDatabaseName("IX_VaultLeaseRecords_ExpiresAt");
        });

        modelBuilder.Entity<RecertificationCampaign>(entity =>
        {
            entity.ToTable("RecertificationCampaigns");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.CampaignId)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.CampaignName)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.Status)
                  .HasMaxLength(50)
                  .HasDefaultValue("Active")
                  .IsRequired();

            entity.Property(e => e.CamundaProcessInstanceId)
                  .HasMaxLength(100);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.CreatedBy)
                  .HasMaxLength(100);

            entity.Property(e => e.CompletedBy)
                  .HasMaxLength(100);

            entity.HasIndex(e => e.CampaignId)
                  .IsUnique()
                  .HasDatabaseName("IX_RecertificationCampaigns_CampaignId");

            entity.HasIndex(e => e.Status)
                  .HasDatabaseName("IX_RecertificationCampaigns_Status");

            entity.HasIndex(e => new { e.Quarter, e.Year })
                  .HasDatabaseName("IX_RecertificationCampaigns_QuarterYear");
        });

        modelBuilder.Entity<RecertificationTask>(entity =>
        {
            entity.ToTable("RecertificationTasks");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.TaskId)
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.CampaignId)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.ManagerUserId)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.ManagerName)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.ManagerEmail)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.Status)
                  .HasMaxLength(50)
                  .HasDefaultValue("Pending")
                  .IsRequired();

            entity.Property(e => e.AssignedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.CamundaTaskId)
                  .HasMaxLength(100);

            entity.Property(e => e.EscalatedTo)
                  .HasMaxLength(100);

            entity.HasIndex(e => e.TaskId)
                  .IsUnique()
                  .HasDatabaseName("IX_RecertificationTasks_TaskId");

            entity.HasIndex(e => e.ManagerUserId)
                  .HasDatabaseName("IX_RecertificationTasks_Manager");

            entity.HasIndex(e => e.Status)
                  .HasDatabaseName("IX_RecertificationTasks_Status");

            entity.HasIndex(e => e.DueDate)
                  .HasDatabaseName("IX_RecertificationTasks_DueDate");
        });

        modelBuilder.Entity<RecertificationReview>(entity =>
        {
            entity.ToTable("RecertificationReviews");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ReviewId)
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.CampaignId)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.UserId)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.UserName)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.UserEmail)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.UserDepartment)
                  .HasMaxLength(100);

            entity.Property(e => e.UserJobTitle)
                  .HasMaxLength(100);

            entity.Property(e => e.RiskLevel)
                  .HasMaxLength(20);

            entity.Property(e => e.Decision)
                  .HasMaxLength(50)
                  .HasDefaultValue("Pending")
                  .IsRequired();

            entity.Property(e => e.DecisionMadeBy)
                  .HasMaxLength(100);

            entity.Property(e => e.DecisionComments)
                  .HasMaxLength(1000);

            entity.Property(e => e.AppealStatus)
                  .HasMaxLength(50);

            entity.HasIndex(e => e.ReviewId)
                  .IsUnique()
                  .HasDatabaseName("IX_RecertificationReviews_ReviewId");

            entity.HasIndex(e => e.UserId)
                  .HasDatabaseName("IX_RecertificationReviews_UserId");

            entity.HasIndex(e => e.Decision)
                  .HasDatabaseName("IX_RecertificationReviews_Decision");

            entity.HasIndex(e => e.RiskLevel)
                  .HasDatabaseName("IX_RecertificationReviews_RiskLevel");
        });

        modelBuilder.Entity<RecertificationEscalation>(entity =>
        {
            entity.ToTable("RecertificationEscalations");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.EscalationId)
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.CampaignId)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.OriginalManagerUserId)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.EscalatedToUserId)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.EscalationType)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.Resolution)
                  .HasMaxLength(50);

            entity.Property(e => e.ResolutionComments)
                  .HasMaxLength(500);

            entity.HasIndex(e => e.TaskId)
                  .HasDatabaseName("IX_RecertificationEscalations_TaskId");

            entity.HasIndex(e => e.EscalatedToUserId)
                  .HasDatabaseName("IX_RecertificationEscalations_EscalatedTo");
        });

        modelBuilder.Entity<RecertificationReport>(entity =>
        {
            entity.ToTable("RecertificationReports");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ReportId)
                  .ValueGeneratedOnAdd();

            entity.Property(e => e.CampaignId)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.ReportType)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.ReportFormat)
                  .HasMaxLength(20)
                  .IsRequired();

            entity.Property(e => e.FilePath)
                  .HasMaxLength(500);

            entity.Property(e => e.GeneratedBy)
                  .HasMaxLength(100);

            entity.Property(e => e.LastAccessedBy)
                  .HasMaxLength(100);

            entity.Property(e => e.GeneratedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.CampaignId)
                  .HasDatabaseName("IX_RecertificationReports_CampaignId");

            entity.HasIndex(e => e.ReportType)
                  .HasDatabaseName("IX_RecertificationReports_ReportType");
        });

        modelBuilder.Entity<ContainerImage>(entity =>
        {
            entity.ToTable("ContainerImages");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ServiceName)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.Version)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.ImageDigest)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.Registry)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.BuildNumber)
                  .HasMaxLength(50);

            entity.Property(e => e.GitCommitSha)
                  .HasMaxLength(100);

            entity.Property(e => e.SignedBy)
                  .HasMaxLength(100);

            entity.Property(e => e.SbomPath)
                  .HasMaxLength(500);

            entity.Property(e => e.SbomFormat)
                  .HasMaxLength(50);

            entity.Property(e => e.CreatedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.CriticalCount)
                  .HasDefaultValue(0);

            entity.Property(e => e.HighCount)
                  .HasDefaultValue(0);

            entity.Property(e => e.MediumCount)
                  .HasDefaultValue(0);

            entity.Property(e => e.LowCount)
                  .HasDefaultValue(0);

            entity.HasIndex(e => new { e.ServiceName, e.Version })
                  .IsUnique()
                  .HasDatabaseName("IX_ContainerImages_ServiceVersion");

            entity.HasIndex(e => e.IsSigned)
                  .HasDatabaseName("IX_ContainerImages_IsSigned");

            entity.HasIndex(e => e.VulnerabilityScanCompleted)
                  .HasDatabaseName("IX_ContainerImages_ScanCompleted");

            entity.HasIndex(e => e.BuildTimestamp)
                  .HasDatabaseName("IX_ContainerImages_BuildTimestamp");
        });

        modelBuilder.Entity<Vulnerability>(entity =>
        {
            entity.ToTable("Vulnerabilities");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.VulnerabilityId)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.PackageName)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.InstalledVersion)
                  .HasMaxLength(100);

            entity.Property(e => e.FixedVersion)
                  .HasMaxLength(100);

            entity.Property(e => e.Severity)
                  .HasMaxLength(20)
                  .IsRequired();

            entity.Property(e => e.Status)
                  .HasMaxLength(50)
                  .HasDefaultValue("Open")
                  .IsRequired();

            entity.Property(e => e.AcknowledgedBy)
                  .HasMaxLength(100);

            entity.Property(e => e.AcknowledgmentComments)
                  .HasMaxLength(500);

            entity.Property(e => e.MitigationPlan)
                  .HasMaxLength(1000);

            entity.HasOne(e => e.ContainerImage)
                  .WithMany(e => e.Vulnerabilities)
                  .HasForeignKey(e => e.ContainerImageId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.VulnerabilityId)
                  .HasDatabaseName("IX_Vulnerabilities_VulnerabilityId");

            entity.HasIndex(e => e.Severity)
                  .HasDatabaseName("IX_Vulnerabilities_Severity");

            entity.HasIndex(e => e.Status)
                  .HasDatabaseName("IX_Vulnerabilities_Status");
        });

        modelBuilder.Entity<SignatureVerificationAudit>(entity =>
        {
            entity.ToTable("SignatureVerificationAudit");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ImageDigest)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.ServiceName)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.Version)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.VerificationTimestamp)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.VerificationResult)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.VerificationMethod)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.VerifiedBy)
                  .HasMaxLength(100);

            entity.Property(e => e.VerificationContext)
                  .HasMaxLength(100);

            entity.Property(e => e.ErrorMessage)
                  .HasMaxLength(500);

            entity.HasIndex(e => e.ImageDigest)
                  .HasDatabaseName("IX_SignatureAudit_ImageDigest");

            entity.HasIndex(e => e.VerificationTimestamp)
                  .HasDatabaseName("IX_SignatureAudit_Timestamp");

            entity.HasIndex(e => e.VerificationResult)
                  .HasDatabaseName("IX_SignatureAudit_Result");
        });

        modelBuilder.Entity<BastionAccessRequest>(entity =>
        {
            entity.ToTable("BastionAccessRequests");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.RequestId)
                  .HasDefaultValueSql("NEWID()");

            entity.Property(e => e.UserId)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.UserName)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.UserEmail)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(e => e.Environment)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.TargetHosts)
                  .HasColumnType("nvarchar(max)");

            entity.Property(e => e.AccessDurationHours)
                  .HasDefaultValue(2);

            entity.Property(e => e.Justification)
                  .HasMaxLength(1000)
                  .IsRequired();

            entity.Property(e => e.Status)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.RequestedAt)
                  .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.ApprovedBy)
                  .HasMaxLength(100);

            entity.Property(e => e.DeniedBy)
                  .HasMaxLength(100);

            entity.Property(e => e.DenialReason)
                  .HasMaxLength(500);

            entity.Property(e => e.VaultCertificatePath)
                  .HasMaxLength(500);

            entity.Property(e => e.CertificateSerialNumber)
                  .HasMaxLength(100);

            entity.Property(e => e.CertificateContent)
                  .HasColumnType("nvarchar(max)");

            entity.Property(e => e.CamundaProcessInstanceId)
                  .HasMaxLength(100);

            entity.HasIndex(e => e.RequestId)
                  .IsUnique()
                  .HasDatabaseName("IX_BastionAccessRequests_RequestId");

            entity.HasIndex(e => e.UserId)
                  .HasDatabaseName("IX_BastionAccessRequests_UserId");

            entity.HasIndex(e => e.Status)
                  .HasDatabaseName("IX_BastionAccessRequests_Status");

            entity.HasMany(e => e.Sessions)
                  .WithOne(s => s.AccessRequest)
                  .HasForeignKey(s => s.AccessRequestId)
                  .HasPrincipalKey(e => e.RequestId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<BastionSession>(entity =>
        {
            entity.ToTable("BastionSessions");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.SessionId)
                  .HasDefaultValueSql("NEWID()");

            entity.Property(e => e.Username)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.ClientIp)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.BastionHost)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.TargetHost)
                  .HasMaxLength(100);

            entity.Property(e => e.Status)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(e => e.RecordingPath)
                  .HasMaxLength(500);

            entity.Property(e => e.TerminationReason)
                  .HasMaxLength(200);

            entity.HasIndex(e => e.SessionId)
                  .IsUnique()
                  .HasDatabaseName("IX_BastionSessions_SessionId");

            entity.HasIndex(e => e.Username)
                  .HasDatabaseName("IX_BastionSessions_Username");

            entity.HasIndex(e => e.Status)
                  .HasDatabaseName("IX_BastionSessions_Status");
        });

        modelBuilder.Entity<EmergencyAccessLog>(entity =>
        {
            entity.ToTable("EmergencyAccessLogs");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.EmergencyId)
                  .HasDefaultValueSql("NEWID()");

            entity.Property(e => e.RequestedBy)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.ApprovedBy1)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.ApprovedBy2)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.IncidentTicketId)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(e => e.Justification)
                  .HasMaxLength(1000)
                  .IsRequired();

            entity.Property(e => e.VaultOneTimeToken)
                  .HasMaxLength(200);

            entity.Property(e => e.ReviewNotes)
                  .HasColumnType("nvarchar(max)");

            entity.HasIndex(e => e.EmergencyId)
                  .IsUnique()
                  .HasDatabaseName("IX_EmergencyAccessLogs_EmergencyId");

            entity.HasIndex(e => e.IncidentTicketId)
                  .HasDatabaseName("IX_EmergencyAccessLogs_IncidentTicketId");
        });
    }
}
