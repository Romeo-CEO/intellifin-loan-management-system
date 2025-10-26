-- Credit Assessment Migration Verification Queries
-- Story 1.2: Database Schema Enhancement
-- These queries help verify the migration was successful

-- ========================================
-- PRE-MIGRATION VERIFICATION
-- ========================================

-- 1. Backup current data counts
SELECT 'CreditAssessments' as TableName, COUNT(*) as RowCount FROM CreditAssessments
UNION ALL
SELECT 'CreditFactors', COUNT(*) FROM CreditFactors
UNION ALL
SELECT 'RiskIndicators', COUNT(*) FROM RiskIndicators;

-- 2. Sample existing data
SELECT TOP 5 
    Id, LoanApplicationId, RiskGrade, CreditScore, 
    DebtToIncomeRatio, AssessedBy, AssessedAt
FROM CreditAssessments
ORDER BY AssessedAt DESC;

-- ========================================
-- POST-MIGRATION VERIFICATION
-- ========================================

-- 3. Verify new tables exist (SQL Server)
SELECT table_name, table_type
FROM INFORMATION_SCHEMA.TABLES
WHERE table_name IN ('CreditAssessmentAudits', 'RuleEvaluations', 'AssessmentConfigVersions')
ORDER BY table_name;

-- 4. Verify new columns in CreditAssessments (SQL Server)
SELECT 
    column_name, 
    data_type, 
    is_nullable,
    column_default
FROM INFORMATION_SCHEMA.COLUMNS
WHERE table_name = 'CreditAssessments'
AND column_name IN (
    'AssessedByUserId', 
    'DecisionCategory', 
    'TriggeredRules',
    'ManualOverrideByUserId',
    'ManualOverrideReason',
    'ManualOverrideAt',
    'IsValid',
    'InvalidReason',
    'VaultConfigVersion'
)
ORDER BY column_name;

-- 5. Verify indexes created (SQL Server)
SELECT 
    i.name as IndexName,
    t.name as TableName,
    c.name as ColumnName,
    i.is_unique
FROM sys.indexes i
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
INNER JOIN sys.tables t ON i.object_id = t.object_id
WHERE t.name IN ('CreditAssessments', 'CreditAssessmentAudits', 'RuleEvaluations', 'AssessmentConfigVersions')
AND i.name LIKE 'IX_%'
ORDER BY t.name, i.name;

-- 6. Verify existing data intact
SELECT COUNT(*) as ExistingAssessmentsCount
FROM CreditAssessments
WHERE AssessedAt < GETUTCDATE();

-- 7. Test inserting into new tables
-- (Run after migration)

-- Insert test config version
INSERT INTO AssessmentConfigVersions (Id, Version, ConfigSnapshot, LoadedAt, LoadedBy, IsActive)
VALUES (NEWID(), 'v1.0.0-test', '{"test": true}', GETUTCDATE(), 'migration-test', 0);

-- Insert test audit record (requires existing assessment)
DECLARE @TestAssessmentId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM CreditAssessments);
INSERT INTO CreditAssessmentAudits (Id, AssessmentId, EventType, EventPayload, Timestamp)
VALUES (NEWID(), @TestAssessmentId, 'TestEvent', '{"test": true}', GETUTCDATE());

-- Insert test rule evaluation (requires existing assessment)
INSERT INTO RuleEvaluations (
    Id, AssessmentId, RuleId, RuleName, Passed, Score, Weight, 
    WeightedScore, InputValues, EvaluatedAt
)
VALUES (
    NEWID(), @TestAssessmentId, 'TEST-001', 'Test Rule', 1, 100, 0.25, 
    25, '{"test": true}', GETUTCDATE()
);

-- 8. Verify test inserts
SELECT 'ConfigVersions' as Table, COUNT(*) as TestRecords FROM AssessmentConfigVersions WHERE Version LIKE '%test%'
UNION ALL
SELECT 'Audits', COUNT(*) FROM CreditAssessmentAudits WHERE EventType = 'TestEvent'
UNION ALL
SELECT 'RuleEvals', COUNT(*) FROM RuleEvaluations WHERE RuleId = 'TEST-001';

-- 9. Clean up test data
DELETE FROM AssessmentConfigVersions WHERE Version LIKE '%test%';
DELETE FROM CreditAssessmentAudits WHERE EventType = 'TestEvent';
DELETE FROM RuleEvaluations WHERE RuleId = 'TEST-001';

-- 10. Verify backward compatibility - old style query still works
SELECT 
    Id, 
    LoanApplicationId, 
    RiskGrade, 
    CreditScore, 
    DebtToIncomeRatio,
    AssessedBy,
    AssessedAt
FROM CreditAssessments
WHERE RiskGrade = 'B'
ORDER BY AssessedAt DESC;

-- 11. Test new fields query
SELECT 
    Id,
    RiskGrade,
    DecisionCategory,
    IsValid,
    InvalidReason,
    VaultConfigVersion,
    AssessedByUserId,
    ManualOverrideByUserId,
    ManualOverrideAt
FROM CreditAssessments
ORDER BY AssessedAt DESC;

-- 12. Check migration history
SELECT * FROM __EFMigrationsHistory 
ORDER BY MigrationId DESC;

-- ========================================
-- PERFORMANCE VERIFICATION
-- ========================================

-- 13. Test index performance - should use IX_CreditAssessments_IsValid
SELECT COUNT(*) 
FROM CreditAssessments WITH (INDEX(IX_CreditAssessments_IsValid))
WHERE IsValid = 1;

-- 14. Test composite index - should use IX_RuleEvaluations_AssessmentId_RuleId
SELECT re.*
FROM RuleEvaluations re WITH (INDEX(IX_RuleEvaluations_AssessmentId_RuleId))
WHERE re.AssessmentId = (SELECT TOP 1 Id FROM CreditAssessments)
AND re.RuleId = 'PR-001';

-- 15. Check index usage statistics (run after some operations)
SELECT 
    OBJECT_NAME(s.object_id) as TableName,
    i.name as IndexName,
    s.user_seeks,
    s.user_scans,
    s.user_lookups,
    s.user_updates
FROM sys.dm_db_index_usage_stats s
INNER JOIN sys.indexes i ON s.object_id = i.object_id AND s.index_id = i.index_id
WHERE OBJECT_NAME(s.object_id) IN ('CreditAssessments', 'CreditAssessmentAudits', 'RuleEvaluations')
ORDER BY TableName, IndexName;

-- ========================================
-- FOREIGN KEY VERIFICATION
-- ========================================

-- 16. Verify foreign key relationships
SELECT 
    fk.name as ForeignKeyName,
    OBJECT_NAME(fk.parent_object_id) as TableName,
    COL_NAME(fkc.parent_object_id, fkc.parent_column_id) as ColumnName,
    OBJECT_NAME(fk.referenced_object_id) as ReferencedTable,
    COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) as ReferencedColumn,
    fk.delete_referential_action_desc as DeleteAction
FROM sys.foreign_keys fk
INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
WHERE OBJECT_NAME(fk.parent_object_id) IN ('CreditAssessmentAudits', 'RuleEvaluations')
ORDER BY TableName, ForeignKeyName;

-- ========================================
-- DATA INTEGRITY CHECKS
-- ========================================

-- 17. Check for orphaned audit records (should be 0)
SELECT COUNT(*) as OrphanedAudits
FROM CreditAssessmentAudits ca
WHERE NOT EXISTS (
    SELECT 1 FROM CreditAssessments c WHERE c.Id = ca.AssessmentId
);

-- 18. Check for orphaned rule evaluations (should be 0)
SELECT COUNT(*) as OrphanedRuleEvals
FROM RuleEvaluations re
WHERE NOT EXISTS (
    SELECT 1 FROM CreditAssessments c WHERE c.Id = re.AssessmentId
);

-- 19. Verify default value for IsValid
SELECT 
    CASE WHEN IsValid IS NULL THEN 'NULL' 
         WHEN IsValid = 1 THEN 'TRUE' 
         ELSE 'FALSE' 
    END as IsValidValue,
    COUNT(*) as Count
FROM CreditAssessments
GROUP BY IsValid;

-- ========================================
-- SUMMARY REPORT
-- ========================================

-- 20. Migration summary
SELECT 
    'Total Assessments' as Metric, COUNT(*) as Value FROM CreditAssessments
UNION ALL
SELECT 'Assessments with New Fields', COUNT(*) FROM CreditAssessments 
    WHERE AssessedByUserId IS NOT NULL OR DecisionCategory IS NOT NULL
UNION ALL
SELECT 'Audit Records', COUNT(*) FROM CreditAssessmentAudits
UNION ALL
SELECT 'Rule Evaluations', COUNT(*) FROM RuleEvaluations
UNION ALL
SELECT 'Config Versions', COUNT(*) FROM AssessmentConfigVersions
UNION ALL
SELECT 'Active Config Versions', COUNT(*) FROM AssessmentConfigVersions WHERE IsActive = 1
UNION ALL
SELECT 'Invalid Assessments', COUNT(*) FROM CreditAssessments WHERE IsValid = 0;

-- ========================================
-- POSTGRESQL VERSIONS (if using PostgreSQL)
-- ========================================

/*
-- For PostgreSQL, replace the above queries with:

-- Verify tables exist
SELECT table_name, table_type
FROM information_schema.tables
WHERE table_schema = 'public'
AND table_name IN ('CreditAssessments', 'CreditAssessmentAudits', 'RuleEvaluations', 'AssessmentConfigVersions');

-- Verify columns
SELECT column_name, data_type, is_nullable, column_default
FROM information_schema.columns
WHERE table_schema = 'public'
AND table_name = 'CreditAssessments'
AND column_name IN ('AssessedByUserId', 'DecisionCategory', 'IsValid', 'VaultConfigVersion');

-- Verify indexes
SELECT 
    tablename,
    indexname,
    indexdef
FROM pg_indexes
WHERE schemaname = 'public'
AND tablename IN ('CreditAssessments', 'CreditAssessmentAudits', 'RuleEvaluations', 'AssessmentConfigVersions')
ORDER BY tablename, indexname;

-- Verify foreign keys
SELECT
    tc.constraint_name,
    tc.table_name,
    kcu.column_name,
    ccu.table_name AS foreign_table_name,
    ccu.column_name AS foreign_column_name,
    rc.delete_rule
FROM information_schema.table_constraints AS tc
JOIN information_schema.key_column_usage AS kcu
    ON tc.constraint_name = kcu.constraint_name
JOIN information_schema.constraint_column_usage AS ccu
    ON ccu.constraint_name = tc.constraint_name
JOIN information_schema.referential_constraints AS rc
    ON rc.constraint_name = tc.constraint_name
WHERE tc.constraint_type = 'FOREIGN KEY'
AND tc.table_name IN ('CreditAssessmentAudits', 'RuleEvaluations');
*/
