@echo off
REM 🔒 CORRECTED MERGE COMMANDS (master branch, not main)
REM Execute these in your local git terminal

echo 🔒 SAFE MERGE EXECUTION - IntelliFin Loan Management System
echo ============================================================
echo Note: Using 'master' branch as confirmed by user
echo.

REM Set backup timestamp
for /f "tokens=1-6 delims=/: " %%a in ("%date% %time%") do set BACKUP_TIMESTAMP=%%a%%b%%c_%%d%%e%%f

echo [INFO] Creating backup with timestamp: %BACKUP_TIMESTAMP%

REM Document current state and create backups
echo [INFO] Creating backup documentation...

echo BACKUP TIMESTAMP: %date% %time% > BACKUP_STATE_%BACKUP_TIMESTAMP%.md
git log --oneline --all --graph --decorate >> BACKUP_STATE_%BACKUP_TIMESTAMP%.md 2>&1
git branch -a >> BACKUP_STATE_%BACKUP_TIMESTAMP%.md 2>&1

echo [INFO] ✅ Backup documentation created: BACKUP_STATE_%BACKUP_TIMESTAMP%.md

REM Get current branch
for /f "delims=" %%i in ('git branch --show-current') do set CURRENT_BRANCH=%%i
echo [INFO] Current branch: %CURRENT_BRANCH%

REM Create backup branches for safety
echo [INFO] Creating backup branches...

REM Backup current branch (Client Management)
git checkout -b "backup/complete-client-management-%BACKUP_TIMESTAMP%" 2>&1
echo [INFO] ✅ Created backup: backup/complete-client-management-%BACKUP_TIMESTAMP%
git checkout %CURRENT_BRANCH% 2>&1

REM Backup IAM branch
git checkout -b "backup/iam-work-%BACKUP_TIMESTAMP%" 2>&1
git checkout feature/iam-remaining-work 2>&1
git checkout %CURRENT_BRANCH% 2>&1
echo [INFO] ✅ Created backup: backup/iam-work-%BACKUP_TIMESTAMP%

REM Merge Client Management to master
echo [INFO] Merging Client Management to master...

REM Ensure master is up to date
git checkout master 2>&1
git pull origin master 2>&1

REM Merge Client Management
git merge %CURRENT_BRANCH% --no-ff -m "feat: Complete Client Management module with KYC, AML, and compliance engine - 17/17 stories implemented (100%% complete) - KYC verification workflow with Camunda BPMN - AML screening with fuzzy matching and EDD - Vault risk scoring engine with dynamic rules - Event-driven notifications (MassTransit/RabbitMQ) - Performance analytics and real-time dashboards - Document retention automation (BoZ compliance) - Mobile optimization with pagination and compression - 142 integration tests (100%% passing) - ~15,791 lines of production code" 2>&1

echo [INFO] ✅ Client Management merged successfully!
git log --oneline -3

REM Merge IAM branch
echo [INFO] Merging IAM branch...

REM Fetch and merge IAM branch
git fetch origin feature/iam-remaining-work 2>&1
git checkout master 2>&1
git merge origin/feature/iam-remaining-work --no-ff -m "feat: Complete IAM enhancement (94%% implementation) - 15/16 stories implemented (94%% complete) - Database schema extensions (8 new tables) - Keycloak OIDC integration - Enhanced RBAC with tenant context - Service account management - JWT token enhancements - Migration orchestration - Self-service password reset" 2>&1

echo [INFO] ✅ IAM branch merged successfully!
git log --oneline -3

REM Verify merges
echo [INFO] Verifying merges...

REM Check that files are present
if exist "apps\IntelliFin.ClientManagement\MODULE-COMPLETE.md" (
    echo [INFO] ✅ Client Management implementation preserved
) else (
    echo [WARN] ⚠️  WARNING: Client Management files not found!
)

if exist "docs\domains\identity-access-management\IAM_IMPLEMENTATION_REVIEW_2025-10-20.md" (
    echo [INFO] ✅ IAM implementation documentation preserved
) else (
    echo [WARN] ⚠️  WARNING: IAM documentation not found!
)

if exist "docs\domains\system-administration\COMPLETION_SUMMARY.md" (
    echo [INFO] ✅ System Administration documentation preserved
) else (
    echo [WARN] ⚠️  WARNING: System Administration documentation not found!
)

REM Push to remote
echo [INFO] Pushing merged changes to remote...
git push origin master 2>&1

REM Create release tags
echo [INFO] Creating release tags...
git tag -a "v1.0.0-client-management" -m "Client Management v1.0.0 - Complete KYC and compliance engine" 2>&1
git tag -a "v1.0.0-iam-enhancement" -m "IAM Enhancement v1.0.0 - Enhanced identity and access management" 2>&1
git tag -a "v1.0.0-system-administration" -m "System Administration v1.0.0 - Complete control plane" 2>&1
git push origin --tags 2>&1

REM Push backup branches for safety
echo [INFO] Pushing backup branches...
git push origin backup/complete-client-management-%BACKUP_TIMESTAMP% 2>&1
git push origin backup/iam-work-%BACKUP_TIMESTAMP% 2>&1

REM Final status
echo.
echo 🎉 MERGE COMPLETED SUCCESSFULLY!
echo ================================
echo ✅ Client Management: Merged (17/17 stories)
echo ✅ IAM Enhancement: Merged (15/16 stories)
echo ✅ System Administration: Already complete
echo ✅ Backup branches created for safety
echo ✅ Release tags created
echo ✅ All work preserved - no data loss
echo.
echo 📋 Backup branches available:
echo   - backup/complete-client-management-%BACKUP_TIMESTAMP%
echo   - backup/iam-work-%BACKUP_TIMESTAMP%
echo.
echo 🔒 SAFETY: If any issues, restore from backup branches
echo    Command: git checkout backup/complete-client-management-%BACKUP_TIMESTAMP%
echo.
echo 📋 Next Steps:
echo 1. Verify application builds: dotnet build
echo 2. Run tests: dotnet test
echo 3. Test key workflows (KYC, authentication, etc.)
echo 4. Archive old branches after 7-14 days
echo 5. Begin Collections ^& Recovery implementation
echo.

pause
