IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250905144624_InitialCreate'
)
BEGIN
    CREATE TABLE [AuditEvents] (
        [Id] uniqueidentifier NOT NULL,
        [Actor] nvarchar(200) NOT NULL,
        [Action] nvarchar(200) NOT NULL,
        [EntityType] nvarchar(200) NOT NULL,
        [EntityId] nvarchar(200) NOT NULL,
        [OccurredAtUtc] datetime2 NOT NULL,
        [Data] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_AuditEvents] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250905144624_InitialCreate'
)
BEGIN
    CREATE TABLE [Clients] (
        [Id] uniqueidentifier NOT NULL,
        [FirstName] nvarchar(100) NOT NULL,
        [LastName] nvarchar(100) NOT NULL,
        [NationalId] nvarchar(32) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_Clients] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250905144624_InitialCreate'
)
BEGIN
    CREATE TABLE [GLAccounts] (
        [Id] uniqueidentifier NOT NULL,
        [AccountCode] nvarchar(50) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Category] nvarchar(50) NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        CONSTRAINT [PK_GLAccounts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250905144624_InitialCreate'
)
BEGIN
    CREATE TABLE [LoanApplications] (
        [Id] uniqueidentifier NOT NULL,
        [ClientId] uniqueidentifier NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [TermMonths] int NOT NULL,
        [ProductCode] nvarchar(64) NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_LoanApplications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LoanApplications_Clients_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [Clients] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250905144624_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditEvents_EntityType_EntityId_OccurredAtUtc] ON [AuditEvents] ([EntityType], [EntityId], [OccurredAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250905144624_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Clients_NationalId] ON [Clients] ([NationalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250905144624_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_GLAccounts_AccountCode] ON [GLAccounts] ([AccountCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250905144624_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_LoanApplications_ClientId_CreatedAtUtc] ON [LoanApplications] ([ClientId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250905144624_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250905144624_InitialCreate', N'9.0.8');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250905151943_SeedReferenceData'
)
BEGIN
    CREATE TABLE [LoanProducts] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [InterestRateAnnualPercent] decimal(5,2) NOT NULL,
        [TermMonthsDefault] int NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_LoanProducts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250905151943_SeedReferenceData'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountCode', N'Category', N'IsActive', N'Name') AND [object_id] = OBJECT_ID(N'[GLAccounts]'))
        SET IDENTITY_INSERT [GLAccounts] ON;
    EXEC(N'INSERT INTO [GLAccounts] ([Id], [AccountCode], [Category], [IsActive], [Name])
    VALUES (''aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1'', N''1000'', N''Asset'', CAST(1 AS bit), N''Cash and Bank''),
    (''aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2'', N''1100'', N''Asset'', CAST(1 AS bit), N''Loans Receivable''),
    (''aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3'', N''2000'', N''Liability'', CAST(1 AS bit), N''Customer Deposits''),
    (''aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa4'', N''3000'', N''Equity'', CAST(1 AS bit), N''Share Capital''),
    (''aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa5'', N''4000'', N''Income'', CAST(1 AS bit), N''Interest Income''),
    (''aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa6'', N''5000'', N''Expense'', CAST(1 AS bit), N''Operational Expenses'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccountCode', N'Category', N'IsActive', N'Name') AND [object_id] = OBJECT_ID(N'[GLAccounts]'))
        SET IDENTITY_INSERT [GLAccounts] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250905151943_SeedReferenceData'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'CreatedAtUtc', N'InterestRateAnnualPercent', N'IsActive', N'Name', N'TermMonthsDefault') AND [object_id] = OBJECT_ID(N'[LoanProducts]'))
        SET IDENTITY_INSERT [LoanProducts] ON;
    EXEC(N'INSERT INTO [LoanProducts] ([Id], [Code], [CreatedAtUtc], [InterestRateAnnualPercent], [IsActive], [Name], [TermMonthsDefault])
    VALUES (''11111111-1111-1111-1111-111111111111'', N''SALARY'', ''2025-09-05T15:19:41.9901704Z'', 24.0, CAST(1 AS bit), N''Salary Advance'', 6),
    (''22222222-2222-2222-2222-222222222222'', N''PAYROLL'', ''2025-09-05T15:19:41.9901704Z'', 28.0, CAST(1 AS bit), N''Payroll Loan'', 12),
    (''33333333-3333-3333-3333-333333333333'', N''SME'', ''2025-09-05T15:19:41.9901704Z'', 32.0, CAST(1 AS bit), N''SME Working Capital'', 18)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'CreatedAtUtc', N'InterestRateAnnualPercent', N'IsActive', N'Name', N'TermMonthsDefault') AND [object_id] = OBJECT_ID(N'[LoanProducts]'))
        SET IDENTITY_INSERT [LoanProducts] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250905151943_SeedReferenceData'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LoanProducts_Code] ON [LoanProducts] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250905151943_SeedReferenceData'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250905151943_SeedReferenceData', N'9.0.8');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    ALTER TABLE [LoanProducts] ADD [BaseInterestRate] decimal(5,4) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    ALTER TABLE [LoanProducts] ADD [Category] nvarchar(100) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    ALTER TABLE [LoanProducts] ADD [Description] nvarchar(1000) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    ALTER TABLE [LoanProducts] ADD [MaxAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    ALTER TABLE [LoanProducts] ADD [MaxTermMonths] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    ALTER TABLE [LoanProducts] ADD [MinAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    ALTER TABLE [LoanProducts] ADD [MinTermMonths] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    DECLARE @var sysname;
    SELECT @var = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[LoanApplications]') AND [c].[name] = N'ProductCode');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [LoanApplications] DROP CONSTRAINT [' + @var + '];');
    ALTER TABLE [LoanApplications] ALTER COLUMN [ProductCode] nvarchar(50) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    ALTER TABLE [LoanApplications] ADD [ApplicationDataJson] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    ALTER TABLE [LoanApplications] ADD [ApprovedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    ALTER TABLE [LoanApplications] ADD [ApprovedBy] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    ALTER TABLE [LoanApplications] ADD [DeclineReason] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    ALTER TABLE [LoanApplications] ADD [ProductName] nvarchar(200) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    ALTER TABLE [LoanApplications] ADD [RequestedAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    ALTER TABLE [LoanApplications] ADD [SubmittedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    ALTER TABLE [LoanApplications] ADD [WorkflowInstanceId] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    ALTER TABLE [GLAccounts] ADD [AccountType] nvarchar(50) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    ALTER TABLE [GLAccounts] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    ALTER TABLE [GLAccounts] ADD [CurrentBalance] decimal(18,2) NOT NULL DEFAULT 0.0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    ALTER TABLE [GLAccounts] ADD [IsContraAccount] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    ALTER TABLE [GLAccounts] ADD [LastModified] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    ALTER TABLE [GLAccounts] ADD [Level] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    ALTER TABLE [GLAccounts] ADD [ParentAccountId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    ALTER TABLE [LoanProducts] ADD CONSTRAINT [AK_LoanProducts_Code] UNIQUE ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE TABLE [ApplicationFields] (
        [Id] uniqueidentifier NOT NULL,
        [LoanProductId] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Label] nvarchar(200) NOT NULL,
        [Type] nvarchar(50) NOT NULL,
        [IsRequired] bit NOT NULL,
        [Order] int NOT NULL,
        [DefaultValue] nvarchar(500) NOT NULL,
        [ValidationPattern] nvarchar(500) NOT NULL,
        [HelpText] nvarchar(1000) NOT NULL,
        [OptionsJson] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_ApplicationFields] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ApplicationFields_LoanProducts_LoanProductId] FOREIGN KEY ([LoanProductId]) REFERENCES [LoanProducts] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE TABLE [CreditAssessments] (
        [Id] uniqueidentifier NOT NULL,
        [LoanApplicationId] uniqueidentifier NOT NULL,
        [RiskGrade] nvarchar(10) NOT NULL,
        [CreditScore] decimal(8,2) NOT NULL,
        [DebtToIncomeRatio] decimal(5,4) NOT NULL,
        [PaymentCapacity] decimal(18,2) NOT NULL,
        [HasCreditBureauData] bit NOT NULL,
        [ScoreExplanation] nvarchar(max) NOT NULL,
        [AssessedAt] datetime2 NOT NULL,
        [AssessedBy] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_CreditAssessments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CreditAssessments_LoanApplications_LoanApplicationId] FOREIGN KEY ([LoanApplicationId]) REFERENCES [LoanApplications] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE TABLE [DocumentVerifications] (
        [Id] uniqueidentifier NOT NULL,
        [ClientId] uniqueidentifier NOT NULL,
        [DocumentType] nvarchar(50) NOT NULL,
        [DocumentNumber] nvarchar(100) NOT NULL,
        [DocumentImagePath] nvarchar(1000) NOT NULL,
        [ManuallyEnteredData] nvarchar(max) NOT NULL,
        [OcrExtractedData] nvarchar(max) NOT NULL,
        [OcrConfidenceScore] decimal(5,4) NOT NULL,
        [OcrProvider] nvarchar(100) NOT NULL,
        [IsVerified] bit NOT NULL,
        [VerifiedBy] nvarchar(200) NULL,
        [VerificationDate] datetime2 NULL,
        [VerificationNotes] nvarchar(2000) NOT NULL,
        [VerificationDecisionReason] nvarchar(1000) NOT NULL,
        [DataMismatches] nvarchar(max) NOT NULL,
        [HasDataMismatches] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [LastModified] datetime2 NOT NULL,
        CONSTRAINT [PK_DocumentVerifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DocumentVerifications_Clients_ClientId] FOREIGN KEY ([ClientId]) REFERENCES [Clients] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE TABLE [GLBalances] (
        [Id] uniqueidentifier NOT NULL,
        [GLAccountId] uniqueidentifier NOT NULL,
        [PeriodYear] int NOT NULL,
        [PeriodMonth] int NOT NULL,
        [OpeningBalance] decimal(18,2) NOT NULL,
        [DebitTotal] decimal(18,2) NOT NULL,
        [CreditTotal] decimal(18,2) NOT NULL,
        [ClosingBalance] decimal(18,2) NOT NULL,
        [LastUpdated] datetime2 NOT NULL,
        [IsLocked] bit NOT NULL,
        CONSTRAINT [PK_GLBalances] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GLBalances_GLAccounts_GLAccountId] FOREIGN KEY ([GLAccountId]) REFERENCES [GLAccounts] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE TABLE [GLEntries] (
        [Id] uniqueidentifier NOT NULL,
        [EntryNumber] nvarchar(50) NOT NULL,
        [TransactionDate] datetime2 NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        [Reference] nvarchar(200) NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [Status] nvarchar(50) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(200) NOT NULL,
        [BatchId] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_GLEntries] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE TABLE [Permissions] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        [Resource] nvarchar(100) NOT NULL,
        [Action] nvarchar(50) NOT NULL,
        [Type] int NOT NULL,
        [IsActive] bit NOT NULL,
        [IsSystemPermission] bit NOT NULL,
        [ParentPermissionId] nvarchar(450) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedBy] nvarchar(100) NULL,
        [Metadata] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_Permissions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Permissions_Permissions_ParentPermissionId] FOREIGN KEY ([ParentPermissionId]) REFERENCES [Permissions] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE TABLE [Roles] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [Type] int NOT NULL,
        [IsActive] bit NOT NULL,
        [IsSystemRole] bit NOT NULL,
        [ParentRoleId] nvarchar(450) NULL,
        [Level] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Roles_Roles_ParentRoleId] FOREIGN KEY ([ParentRoleId]) REFERENCES [Roles] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] nvarchar(450) NOT NULL,
        [Username] nvarchar(100) NOT NULL,
        [Email] nvarchar(255) NOT NULL,
        [FirstName] nvarchar(100) NOT NULL,
        [LastName] nvarchar(100) NOT NULL,
        [PhoneNumber] nvarchar(20) NULL,
        [PasswordHash] nvarchar(255) NOT NULL,
        [BranchId] nvarchar(50) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [AccessFailedCount] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [LastLoginAt] datetime2 NULL,
        [CreatedBy] nvarchar(100) NOT NULL,
        [UpdatedBy] nvarchar(100) NULL,
        [Metadata] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE TABLE [ValidationRules] (
        [Id] uniqueidentifier NOT NULL,
        [LoanProductId] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Expression] nvarchar(max) NOT NULL,
        [ErrorMessage] nvarchar(1000) NOT NULL,
        [Category] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_ValidationRules] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ValidationRules_LoanProducts_LoanProductId] FOREIGN KEY ([LoanProductId]) REFERENCES [LoanProducts] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE TABLE [CreditFactors] (
        [Id] uniqueidentifier NOT NULL,
        [CreditAssessmentId] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Value] nvarchar(500) NOT NULL,
        [Weight] decimal(5,4) NOT NULL,
        [Score] decimal(8,2) NOT NULL,
        [Impact] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_CreditFactors] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CreditFactors_CreditAssessments_CreditAssessmentId] FOREIGN KEY ([CreditAssessmentId]) REFERENCES [CreditAssessments] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE TABLE [RiskIndicators] (
        [Id] uniqueidentifier NOT NULL,
        [CreditAssessmentId] uniqueidentifier NOT NULL,
        [Category] nvarchar(100) NOT NULL,
        [Description] nvarchar(1000) NOT NULL,
        [Level] nvarchar(50) NOT NULL,
        [Impact] decimal(8,2) NOT NULL,
        CONSTRAINT [PK_RiskIndicators] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RiskIndicators_CreditAssessments_CreditAssessmentId] FOREIGN KEY ([CreditAssessmentId]) REFERENCES [CreditAssessments] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE TABLE [GLEntryLines] (
        [Id] uniqueidentifier NOT NULL,
        [GLEntryId] uniqueidentifier NOT NULL,
        [GLAccountId] uniqueidentifier NOT NULL,
        [DebitAmount] decimal(18,2) NOT NULL,
        [CreditAmount] decimal(18,2) NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        [Reference] nvarchar(200) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_GLEntryLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GLEntryLines_GLAccounts_GLAccountId] FOREIGN KEY ([GLAccountId]) REFERENCES [GLAccounts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_GLEntryLines_GLEntries_GLEntryId] FOREIGN KEY ([GLEntryId]) REFERENCES [GLEntries] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE TABLE [RolePermissions] (
        [RoleId] nvarchar(450) NOT NULL,
        [PermissionId] nvarchar(450) NOT NULL,
        [GrantedAt] datetime2 NOT NULL,
        [GrantedBy] nvarchar(100) NOT NULL,
        [IsActive] bit NOT NULL,
        [ExpiresAt] datetime2 NULL,
        [Conditions] nvarchar(1000) NULL,
        [Metadata] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([RoleId], [PermissionId]),
        CONSTRAINT [FK_RolePermissions_Permissions_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [Permissions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_RolePermissions_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE TABLE [UserRoles] (
        [UserId] nvarchar(450) NOT NULL,
        [RoleId] nvarchar(450) NOT NULL,
        [AssignedAt] datetime2 NOT NULL,
        [AssignedBy] nvarchar(100) NOT NULL,
        [BranchId] nvarchar(50) NULL,
        [IsActive] bit NOT NULL,
        [ExpiresAt] datetime2 NULL,
        [Reason] nvarchar(500) NULL,
        [Metadata] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_UserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    EXEC(N'UPDATE [GLAccounts] SET [AccountType] = N'''', [CreatedAt] = ''2025-09-06T10:23:27.7008734Z'', [CurrentBalance] = 0.0, [IsContraAccount] = CAST(0 AS bit), [LastModified] = ''2025-09-06T10:23:27.7008740Z'', [Level] = 0, [ParentAccountId] = NULL
    WHERE [Id] = ''aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    EXEC(N'UPDATE [GLAccounts] SET [AccountType] = N'''', [CreatedAt] = ''2025-09-06T10:23:27.7011795Z'', [CurrentBalance] = 0.0, [IsContraAccount] = CAST(0 AS bit), [LastModified] = ''2025-09-06T10:23:27.7011799Z'', [Level] = 0, [ParentAccountId] = NULL
    WHERE [Id] = ''aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    EXEC(N'UPDATE [GLAccounts] SET [AccountType] = N'''', [CreatedAt] = ''2025-09-06T10:23:27.7012207Z'', [CurrentBalance] = 0.0, [IsContraAccount] = CAST(0 AS bit), [LastModified] = ''2025-09-06T10:23:27.7012211Z'', [Level] = 0, [ParentAccountId] = NULL
    WHERE [Id] = ''aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    EXEC(N'UPDATE [GLAccounts] SET [AccountType] = N'''', [CreatedAt] = ''2025-09-06T10:23:27.7012220Z'', [CurrentBalance] = 0.0, [IsContraAccount] = CAST(0 AS bit), [LastModified] = ''2025-09-06T10:23:27.7012220Z'', [Level] = 0, [ParentAccountId] = NULL
    WHERE [Id] = ''aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa4'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    EXEC(N'UPDATE [GLAccounts] SET [AccountType] = N'''', [CreatedAt] = ''2025-09-06T10:23:27.7012226Z'', [CurrentBalance] = 0.0, [IsContraAccount] = CAST(0 AS bit), [LastModified] = ''2025-09-06T10:23:27.7012226Z'', [Level] = 0, [ParentAccountId] = NULL
    WHERE [Id] = ''aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa5'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    EXEC(N'UPDATE [GLAccounts] SET [AccountType] = N'''', [CreatedAt] = ''2025-09-06T10:23:27.7012231Z'', [CurrentBalance] = 0.0, [IsContraAccount] = CAST(0 AS bit), [LastModified] = ''2025-09-06T10:23:27.7012231Z'', [Level] = 0, [ParentAccountId] = NULL
    WHERE [Id] = ''aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa6'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    EXEC(N'UPDATE [LoanProducts] SET [BaseInterestRate] = 0.0, [Category] = N'''', [CreatedAtUtc] = ''2025-09-06T10:23:27.6948436Z'', [Description] = N'''', [MaxAmount] = 0.0, [MaxTermMonths] = 0, [MinAmount] = 0.0, [MinTermMonths] = 0
    WHERE [Id] = ''11111111-1111-1111-1111-111111111111'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    EXEC(N'UPDATE [LoanProducts] SET [BaseInterestRate] = 0.0, [Category] = N'''', [CreatedAtUtc] = ''2025-09-06T10:23:27.6948436Z'', [Description] = N'''', [MaxAmount] = 0.0, [MaxTermMonths] = 0, [MinAmount] = 0.0, [MinTermMonths] = 0
    WHERE [Id] = ''22222222-2222-2222-2222-222222222222'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    EXEC(N'UPDATE [LoanProducts] SET [BaseInterestRate] = 0.0, [Category] = N'''', [CreatedAtUtc] = ''2025-09-06T10:23:27.6948436Z'', [Description] = N'''', [MaxAmount] = 0.0, [MaxTermMonths] = 0, [MinAmount] = 0.0, [MinTermMonths] = 0
    WHERE [Id] = ''33333333-3333-3333-3333-333333333333'';
    SELECT @@ROWCOUNT');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'CreatedBy', N'Description', N'IsActive', N'IsSystemRole', N'Level', N'Name', N'ParentRoleId', N'Type', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Roles]'))
        SET IDENTITY_INSERT [Roles] ON;
    EXEC(N'INSERT INTO [Roles] ([Id], [CreatedAt], [CreatedBy], [Description], [IsActive], [IsSystemRole], [Level], [Name], [ParentRoleId], [Type], [UpdatedAt], [UpdatedBy])
    VALUES (N''role-analyst'', ''2025-09-06T10:23:27.6948436Z'', N''system'', N''Credit Analyst'', CAST(1 AS bit), CAST(1 AS bit), 3, N''Analyst'', NULL, 1, NULL, NULL),
    (N''role-ceo'', ''2025-09-06T10:23:27.6948436Z'', N''system'', N''Chief Executive Officer'', CAST(1 AS bit), CAST(1 AS bit), 1, N''CEO'', NULL, 3, NULL, NULL),
    (N''role-manager'', ''2025-09-06T10:23:27.6948436Z'', N''system'', N''Branch Manager'', CAST(1 AS bit), CAST(1 AS bit), 2, N''Manager'', NULL, 3, NULL, NULL),
    (N''role-officer'', ''2025-09-06T10:23:27.6948436Z'', N''system'', N''Loan Officer'', CAST(1 AS bit), CAST(1 AS bit), 3, N''LoanOfficer'', NULL, 1, NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'CreatedBy', N'Description', N'IsActive', N'IsSystemRole', N'Level', N'Name', N'ParentRoleId', N'Type', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[Roles]'))
        SET IDENTITY_INSERT [Roles] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccessFailedCount', N'BranchId', N'CreatedAt', N'CreatedBy', N'Email', N'EmailConfirmed', N'FirstName', N'IsActive', N'LastLoginAt', N'LastName', N'LockoutEnabled', N'LockoutEnd', N'Metadata', N'PasswordHash', N'PhoneNumber', N'PhoneNumberConfirmed', N'TwoFactorEnabled', N'UpdatedAt', N'UpdatedBy', N'Username') AND [object_id] = OBJECT_ID(N'[Users]'))
        SET IDENTITY_INSERT [Users] ON;
    EXEC(N'INSERT INTO [Users] ([Id], [AccessFailedCount], [BranchId], [CreatedAt], [CreatedBy], [Email], [EmailConfirmed], [FirstName], [IsActive], [LastLoginAt], [LastName], [LockoutEnabled], [LockoutEnd], [Metadata], [PasswordHash], [PhoneNumber], [PhoneNumberConfirmed], [TwoFactorEnabled], [UpdatedAt], [UpdatedBy], [Username])
    VALUES (N''user-admin'', 0, NULL, ''2025-09-06T10:23:27.6948436Z'', N''system'', N''admin@intellifin.com'', CAST(1 AS bit), N''System'', CAST(1 AS bit), NULL, N''Administrator'', CAST(1 AS bit), NULL, N''{}'', N''$2a$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/LewdBPj/RK.PJ/...'', NULL, CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, N''admin'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AccessFailedCount', N'BranchId', N'CreatedAt', N'CreatedBy', N'Email', N'EmailConfirmed', N'FirstName', N'IsActive', N'LastLoginAt', N'LastName', N'LockoutEnabled', N'LockoutEnd', N'Metadata', N'PasswordHash', N'PhoneNumber', N'PhoneNumberConfirmed', N'TwoFactorEnabled', N'UpdatedAt', N'UpdatedBy', N'Username') AND [object_id] = OBJECT_ID(N'[Users]'))
        SET IDENTITY_INSERT [Users] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'RoleId', N'UserId', N'AssignedAt', N'AssignedBy', N'BranchId', N'ExpiresAt', N'IsActive', N'Metadata', N'Reason') AND [object_id] = OBJECT_ID(N'[UserRoles]'))
        SET IDENTITY_INSERT [UserRoles] ON;
    EXEC(N'INSERT INTO [UserRoles] ([RoleId], [UserId], [AssignedAt], [AssignedBy], [BranchId], [ExpiresAt], [IsActive], [Metadata], [Reason])
    VALUES (N''role-ceo'', N''user-admin'', ''2025-09-06T10:23:27.6948436Z'', N''system'', NULL, NULL, CAST(1 AS bit), N''{}'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'RoleId', N'UserId', N'AssignedAt', N'AssignedBy', N'BranchId', N'ExpiresAt', N'IsActive', N'Metadata', N'Reason') AND [object_id] = OBJECT_ID(N'[UserRoles]'))
        SET IDENTITY_INSERT [UserRoles] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE INDEX [IX_LoanProducts_Category] ON [LoanProducts] ([Category]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE INDEX [IX_LoanApplications_ProductCode] ON [LoanApplications] ([ProductCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE INDEX [IX_LoanApplications_Status] ON [LoanApplications] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE INDEX [IX_GLAccounts_Category] ON [GLAccounts] ([Category]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE INDEX [IX_GLAccounts_ParentAccountId] ON [GLAccounts] ([ParentAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE INDEX [IX_ApplicationFields_LoanProductId] ON [ApplicationFields] ([LoanProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE INDEX [IX_CreditAssessments_AssessedAt] ON [CreditAssessments] ([AssessedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE INDEX [IX_CreditAssessments_LoanApplicationId] ON [CreditAssessments] ([LoanApplicationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE INDEX [IX_CreditAssessments_RiskGrade] ON [CreditAssessments] ([RiskGrade]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE INDEX [IX_CreditFactors_CreditAssessmentId] ON [CreditFactors] ([CreditAssessmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE INDEX [IX_DocumentVerifications_ClientId] ON [DocumentVerifications] ([ClientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE INDEX [IX_DocumentVerifications_DocumentType_DocumentNumber] ON [DocumentVerifications] ([DocumentType], [DocumentNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE INDEX [IX_DocumentVerifications_IsVerified] ON [DocumentVerifications] ([IsVerified]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE INDEX [IX_DocumentVerifications_VerificationDate] ON [DocumentVerifications] ([VerificationDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE UNIQUE INDEX [IX_GLBalances_GLAccountId_PeriodYear_PeriodMonth] ON [GLBalances] ([GLAccountId], [PeriodYear], [PeriodMonth]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE INDEX [IX_GLEntries_BatchId] ON [GLEntries] ([BatchId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE UNIQUE INDEX [IX_GLEntries_EntryNumber] ON [GLEntries] ([EntryNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE INDEX [IX_GLEntries_TransactionDate] ON [GLEntries] ([TransactionDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE INDEX [IX_GLEntryLines_GLAccountId] ON [GLEntryLines] ([GLAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE INDEX [IX_GLEntryLines_GLEntryId] ON [GLEntryLines] ([GLEntryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE INDEX [IX_Permissions_IsActive] ON [Permissions] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Permissions_Name] ON [Permissions] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE INDEX [IX_Permissions_ParentPermissionId] ON [Permissions] ([ParentPermissionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Permissions_Resource_Action] ON [Permissions] ([Resource], [Action]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE INDEX [IX_RiskIndicators_CreditAssessmentId] ON [RiskIndicators] ([CreditAssessmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE INDEX [IX_RolePermissions_IsActive] ON [RolePermissions] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE INDEX [IX_RolePermissions_PermissionId] ON [RolePermissions] ([PermissionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE INDEX [IX_Roles_ParentRoleId] ON [Roles] ([ParentRoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE INDEX [IX_UserRoles_IsActive] ON [UserRoles] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE INDEX [IX_UserRoles_RoleId] ON [UserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE INDEX [IX_Users_IsActive] ON [Users] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Username] ON [Users] ([Username]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    CREATE INDEX [IX_ValidationRules_LoanProductId] ON [ValidationRules] ([LoanProductId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    ALTER TABLE [GLAccounts] ADD CONSTRAINT [FK_GLAccounts_GLAccounts_ParentAccountId] FOREIGN KEY ([ParentAccountId]) REFERENCES [GLAccounts] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    ALTER TABLE [LoanApplications] ADD CONSTRAINT [FK_LoanApplications_LoanProducts_ProductCode] FOREIGN KEY ([ProductCode]) REFERENCES [LoanProducts] ([Code]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20250906102331_AddUserEntitiesFixed'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250906102331_AddUserEntitiesFixed', N'9.0.8');
END;

COMMIT;
GO

