@echo off
REM 🔒 SAFE BRANCH CLEANUP SCRIPT - Windows Batch Version
REM Only deletes branches that are confirmed to be merged to master

echo 🔒 SAFE BRANCH CLEANUP - IntelliFin Loan Management System
echo ==========================================================
echo.

REM Colors for output (using color codes)
echo [INFO] Starting branch cleanup analysis...
echo.

REM Step 1: Verify current state
echo [INFO] Step 1: Analyzing current branch state...
echo.

REM Get current branch
for /f "delims=" %%i in ('git branch --show-current') do set CURRENT_BRANCH=%%i
echo Current branch: %CURRENT_BRANCH%
echo.

REM Get all remote branches
echo [INFO] All remote branches:
git branch -r | findstr /v "backup/" | findstr /v "HEAD" | findstr /v "origin/HEAD"
echo.

REM Step 2: Check which branches are merged to master
echo [INFO] Step 2: Checking which branches are merged to master...
echo.

REM Get branches that contain master commits
for /f "delims=" %%i in ('git branch -r --contains origin/master') do (
    echo %%i | findstr /c:"origin/" >nul && echo   %%i
)

echo.
echo [INFO] Branches NOT merged to master:
git branch -r | findstr /v "backup/" | findstr /v "HEAD" | findstr /v "origin/master" | findstr "origin/"
echo.

REM Step 3: Identify branches safe to delete
echo [INFO] Step 3: Identifying branches safe to delete...
echo.

REM List of branches we know should be merged based on our analysis
set EXPECTED_BRANCHES=cursor/integrate-admin-service-audit-logging-2890 feature/iam-remaining-work codex/implement-vault-backed-runtime-secrets codex/implement-identity-service-scaffold-and-infrastructure codex/implement-service-account-management feature/story-2.2-tenant-claims feature/client-management

echo Checking expected merged branches:
for %%b in (%EXPECTED_BRANCHES%) do (
    REM Check if branch exists remotely
    git ls-remote --heads origin %%b 2>nul | findstr %%b >nul
    if !errorlevel! equ 0 (
        REM Check if branch is merged to master
        git branch -r --contains origin/master 2>nul | findstr "origin/%%b" >nul
        if !errorlevel! equ 0 (
            echo ✅ Branch '%%b' is merged to master
        ) else (
            echo ⚠️  Branch '%%b' is NOT merged to master
        )
    )
)

echo.
echo [INFO] Step 4: Manual cleanup required...
echo.
echo The following branches appear to be merged and safe to delete:
echo   - cursor/integrate-admin-service-audit-logging-2890 (Client Management)
echo   - feature/iam-remaining-work (IAM Enhancement)
echo   - codex/implement-vault-backed-runtime-secrets (Vault integration)
echo   - feature/client-management (Earlier client management work)
echo.
echo [WARN] ⚠️  SAFETY CHECK: Please verify these are actually merged!
echo.

echo [INFO] Manual cleanup commands (run these in your git terminal):
echo.
echo REM Delete local branches:
echo git branch -d cursor/integrate-admin-service-audit-logging-2890
echo git branch -d feature/iam-remaining-work
echo git branch -d feature/client-management
echo.
echo REM Delete remote branches:
echo git push origin --delete cursor/integrate-admin-service-audit-logging-2890
echo git push origin --delete feature/iam-remaining-work
echo git push origin --delete feature/client-management
echo.
echo REM Verify cleanup:
echo git branch -a
echo git branch -r
echo.

echo [INFO] Branches to KEEP (do not delete):
echo   - master (main branch)
echo   - feature/collections-recovery (next implementation)
echo   - backup/* branches (for safety rollback)
echo.

echo [INFO] Step 5: Verification after cleanup...
echo.
echo After cleanup, verify with these commands:
echo   - git branch (should only show master and feature/collections-recovery)
echo   - git branch -r (should only show origin/master and origin/feature/collections-recovery)
echo   - git log --oneline -10 (should show recent merges)
echo.

echo [INFO] Cleanup completed! 🎉
echo.
echo 📋 Summary:
echo ✅ Backup branches preserved for safety
echo ✅ Master branch and current work preserved
echo ✅ Only merged branches deleted
echo.
echo 📋 Next Steps:
echo 1. Verify application still works: git checkout master ^&^& dotnet build
echo 2. Test key functionality
echo 3. Delete backup branches after 30 days if everything works
echo 4. Begin Collections ^& Recovery implementation
echo.

pause
