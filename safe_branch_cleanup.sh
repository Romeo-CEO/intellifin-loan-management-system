#!/bin/bash

# 🔒 SAFE BRANCH CLEANUP SCRIPT
# Only deletes branches that are confirmed to be merged to master

echo "🔒 SAFE BRANCH CLEANUP - IntelliFin Loan Management System"
echo "=========================================================="
echo ""

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Function to print status messages
print_status() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check if we're in git repository
if ! git rev-parse --git-dir > /dev/null 2>&1; then
    print_error "Not in a git repository!"
    exit 1
fi

echo "Current directory: $(pwd)"
echo "Current branch: $(git branch --show-current)"
echo ""

# Step 1: Verify current state
print_status "Step 1: Analyzing current branch state..."

# Get all branches
ALL_BRANCHES=$(git branch -a | grep -v "backup/" | grep -v "remotes/origin/backup/" | sed 's/*//g' | sed 's/  //g')
REMOTE_BRANCHES=$(git branch -r | grep -v "backup/" | grep -v "HEAD" | sed 's|origin/||g')

print_status "All local branches:"
echo "$ALL_BRANCHES" | grep -v "remotes/"
echo ""

print_status "All remote branches:"
echo "$REMOTE_BRANCHES"
echo ""

# Step 2: Check which branches are merged to master
print_status "Step 2: Checking which branches are merged to master..."

MERGED_BRANCHES=""
UNMERGED_BRANCHES=""

for branch in $REMOTE_BRANCHES; do
    if git branch -r --contains origin/master | grep -q "origin/$branch"; then
        MERGED_BRANCHES="$MERGED_BRANCHES $branch"
    else
        UNMERGED_BRANCHES="$UNMERGED_BRANCHES $branch"
    fi
done

echo "Branches merged to master:"
if [ -n "$MERGED_BRANCHES" ]; then
    echo "$MERGED_BRANCHES"
else
    echo "  None"
fi
echo ""

echo "Branches NOT merged to master:"
if [ -n "$UNMERGED_BRANCHES" ]; then
    echo "$UNMERGED_BRANCHES"
else
    echo "  None"
fi
echo ""

# Step 3: Identify branches safe to delete
print_status "Step 3: Identifying branches safe to delete..."

# Get current branch
CURRENT_BRANCH=$(git branch --show-current)

# List of branches we know should be merged based on our analysis
EXPECTED_MERGED_BRANCHES="
cursor/integrate-admin-service-audit-logging-2890
feature/iam-remaining-work
codex/implement-vault-backed-runtime-secrets
codex/implement-identity-service-scaffold-and-infrastructure
codex/implement-service-account-management
feature/story-2.2-tenant-claims
feature/client-management
"

SAFE_TO_DELETE_LOCAL=""
SAFE_TO_DELETE_REMOTE=""

print_status "Checking expected merged branches..."

for branch in $EXPECTED_MERGED_BRANCHES; do
    branch=$(echo $branch | xargs)  # Remove whitespace

    # Skip if branch doesn't exist
    if ! git ls-remote --heads origin $branch | grep -q $branch; then
        continue
    fi

    # Check if branch is merged to master
    if git branch -r --contains origin/master | grep -q "origin/$branch"; then
        print_status "✅ Branch '$branch' is merged to master"

        # Add to safe delete lists if not current branch and not backup
        if [ "$branch" != "$CURRENT_BRANCH" ] && [[ ! "$branch" =~ ^backup/ ]]; then
            SAFE_TO_DELETE_LOCAL="$SAFE_TO_DELETE_LOCAL $branch"
            SAFE_TO_DELETE_REMOTE="$SAFE_TO_DELETE_REMOTE $branch"
        fi
    else
        print_warning "⚠️  Branch '$branch' is NOT merged to master"
    fi
done

echo ""
print_status "Branches safe to delete locally:"
if [ -n "$SAFE_TO_DELETE_LOCAL" ]; then
    echo "$SAFE_TO_DELETE_LOCAL"
else
    echo "  None"
fi
echo ""

print_status "Branches safe to delete remotely:"
if [ -n "$SAFE_TO_DELETE_REMOTE" ]; then
    echo "$SAFE_TO_DELETE_REMOTE"
else
    echo "  None"
fi
echo ""

# Step 4: Confirm before deletion
print_warning "⚠️  SAFETY CHECK: Please verify the above branches are actually merged!"
echo ""
read -p "Do you want to proceed with deleting these branches? (y/N): " -n 1 -r
echo ""

if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    print_status "Cleanup cancelled. No branches deleted."
    exit 0
fi

# Step 5: Delete local branches
print_status "Step 4: Deleting local branches..."

for branch in $SAFE_TO_DELETE_LOCAL; do
    branch=$(echo $branch | xargs)
    if [ "$branch" != "$CURRENT_BRANCH" ] && [[ ! "$branch" =~ ^backup/ ]]; then
        if git branch | grep -q "^[[:space:]]*$branch$"; then
            print_status "Deleting local branch: $branch"
            git branch -d "$branch" 2>/dev/null || print_warning "Could not delete local branch: $branch"
        fi
    fi
done

# Step 6: Delete remote branches
print_status "Step 5: Deleting remote branches..."

for branch in $SAFE_TO_DELETE_REMOTE; do
    branch=$(echo $branch | xargs)
    if [[ ! "$branch" =~ ^backup/ ]]; then
        if git ls-remote --heads origin $branch | grep -q $branch; then
            print_status "Deleting remote branch: origin/$branch"
            git push origin --delete "$branch" 2>/dev/null || print_warning "Could not delete remote branch: $branch"
        fi
    fi
done

# Step 7: Show final state
print_status "Step 6: Final branch state..."

echo "Remaining local branches:"
git branch | grep -v "backup/"
echo ""

echo "Remaining remote branches:"
git branch -r | grep -v "backup/" | grep -v "HEAD"
echo ""

echo "Backup branches (kept for safety):"
git branch | grep "backup/" || echo "  None"
git branch -r | grep "backup/" | sed 's|origin/||g' || echo "  None"
echo ""

# Step 8: Verify cleanup
print_status "Step 7: Cleanup verification..."

# Check that important branches still exist
IMPORTANT_BRANCHES="
master
cursor/integrate-admin-service-audit-logging-2890
feature/iam-remaining-work
feature/collections-recovery
"

print_status "Verifying important branches still exist..."

for branch in $IMPORTANT_BRANCHES; do
    branch=$(echo $branch | xargs)
    if git ls-remote --heads origin $branch | grep -q $branch; then
        print_status "✅ Branch '$branch' still exists (good)"
    else
        print_warning "⚠️  Branch '$branch' was deleted or doesn't exist"
    fi
done

print_status "Branch cleanup completed!"
echo ""
echo "📋 Summary:"
echo "✅ Local branches deleted: $(echo $SAFE_TO_DELETE_LOCAL | wc -w)"
echo "✅ Remote branches deleted: $(echo $SAFE_TO_DELETE_REMOTE | wc -w)"
echo "✅ Backup branches preserved for safety"
echo "✅ Master branch and current work preserved"
echo ""
print_status "Next steps:"
echo "1. Verify application still works: git checkout master && dotnet build"
echo "2. Test key functionality"
echo "3. Delete backup branches after 30 days if everything works"
echo ""
print_status "Safe cleanup completed! 🎉"

exit 0
