# IAM Story Assignments

**Integration Branch:** `feature/iam-remaining-work`  
**Status:** Ready for parallel development  
**Updated:** 2025-10-17

---

## Quick Start for Agents

### **Setup (All Agents)**

```bash
# Clone/pull the repo
git clone https://github.com/Romeo-CEO/intellifin-loan-management-system.git
cd "Intellifin Loan Management System"

# Checkout the integration branch
git checkout feature/iam-remaining-work
git pull origin feature/iam-remaining-work

# Verify you're on the right branch
git branch --show-current
# Should output: feature/iam-remaining-work
```

---

## Agent Assignments

### 🤖 **Agent A - Primary** (Foundation & Migration)

**Assigned Stories:**
- **Story 1.7**: Baseline Role Templates and Seed Data (3-4 days)
- **Story 6.1**: Migration Orchestration and Cutover (10-12 days)

**Story File Locations:**
- `docs/domains/identity-access-management/stories/story-1.7-baseline-role-templates.md`
- `docs/domains/identity-access-management/stories/story-6.1-migration-orchestration.md`

**Create Your Branch:**
```bash
git checkout feature/iam-remaining-work
git pull origin feature/iam-remaining-work
git checkout -b feature/story-1.7-baseline-roles

# Read your story
cat "docs/domains/identity-access-management/stories/story-1.7-baseline-role-templates.md"
```

**Your Mission:**
Create baseline role templates (System Admin, Loan Officer, Underwriter, etc.) with seed data service. After completion, build the migration orchestration system.

**Key Files to Create:**
- `apps/IntelliFin.IdentityService/Data/Seeds/BaselineRolesSeed.json`
- `apps/IntelliFin.IdentityService/Services/IBaselineSeedService.cs`
- `apps/IntelliFin.IdentityService/Services/BaselineSeedService.cs`
- `apps/IntelliFin.IdentityService/Controllers/Platform/SeedController.cs`

---

### 🤖 **Agent B - Secondary** (Tenancy & Self-Service)

**Assigned Stories:**
- **Story 2.2**: Tenant Context in JWT Claims (1-2 days, verification/testing)
- **Story 6.2**: Self-Service Password Reset (5-7 days)

**Story File Locations:**
- `docs/domains/identity-access-management/stories/story-2.2-tenant-api-endpoints.md`
- `docs/domains/identity-access-management/stories/story-6.2-self-service-password-reset.md`

**Create Your Branch:**
```bash
git checkout feature/iam-remaining-work
git pull origin feature/iam-remaining-work
git checkout -b feature/story-2.2-tenant-claims

# Read your story
cat "docs/domains/identity-access-management/stories/story-2.2-tenant-api-endpoints.md"
```

**Your Mission:**
Verify/implement tenant context claims in JWT tokens and API Gateway header extraction. Then build self-service password reset with email templates.

**Key Files to Create:**
- Keycloak protocol mapper configuration
- API Gateway tenant header middleware (if needed)
- `apps/IntelliFin.IdentityService/Controllers/AccountController.cs`
- `apps/IntelliFin.IdentityService/Services/AccountManagementService.cs`
- Email templates for Keycloak

---

### 🤖 **Agent C - QA** (Testing & Documentation)

**Assigned Stories:**
- **Story 5.1**: Comprehensive Testing and Documentation (3-5 days, ongoing)

**Story File Location:**
- `docs/domains/identity-access-management/stories/story-5.1-testing-and-documentation.md`

**Create Your Branch:**
```bash
git checkout feature/iam-remaining-work
git pull origin feature/iam-remaining-work
git checkout -b feature/story-5.1-testing

# Read your story
cat "docs/domains/identity-access-management/stories/story-5.1-testing-and-documentation.md"
```

**Your Mission:**
Create comprehensive test coverage for all IAM features, integration tests, and API documentation. Ensure >80% test coverage.

**Key Areas:**
- Unit tests for all IAM services
- Integration tests for authentication flows
- API documentation (OpenAPI/Swagger)
- Load testing scenarios
- Deployment runbooks

---

## Story Dependencies

```
Phase 1 (Parallel - Start NOW):
├── Story 1.7 (Agent A)
├── Story 2.2 (Agent B)
└── Story 5.1 (Agent C) - ongoing

Phase 2 (After Phase 1):
├── Story 6.1 (Agent A) - requires 1.7 complete
└── Story 6.2 (Agent B) - requires 2.2 complete
```

**Critical Path:** Story 1.7 → Story 6.1 (blocks migration)

---

## Branch Workflow

### **Daily Sync (Morning)**
```bash
# Update your branch with latest integration changes
git checkout feature/iam-remaining-work
git pull origin feature/iam-remaining-work

git checkout feature/story-X.X-your-story
git merge feature/iam-remaining-work
```

### **When Story Complete**
```bash
# Commit your work
git add .
git commit -m "feat(story-X.X): [Description]"

# Push your feature branch
git push origin feature/story-X.X-your-story

# Create PR on GitHub
# Target: feature/iam-remaining-work
# Title: "Story X.X: [Story Name]"
```

### **After PR Merged**
```bash
# Start next story
git checkout feature/iam-remaining-work
git pull origin feature/iam-remaining-work
git checkout -b feature/story-X.X-next-story
```

---

## Essential Reading

**Before you start, read these 3 files:**

1. **Implementation Status** (5 min read)
   ```bash
   cat "docs/domains/identity-access-management/IMPLEMENTATION_STATUS.md"
   ```
   Understand what's already complete (12/16 stories).

2. **Parallel Development Guide** (10 min read)
   ```bash
   cat "docs/domains/identity-access-management/PARALLEL_DEVELOPMENT_GUIDE.md"
   ```
   Learn the coordination protocol, merge strategy, conflict resolution.

3. **Your Story File** (15 min read)
   ```bash
   cat "docs/domains/identity-access-management/stories/story-X.X-your-story.md"
   ```
   Detailed acceptance criteria and implementation steps.

---

## Testing Requirements

**Before submitting PR, verify:**

```bash
# 1. Build succeeds
dotnet build

# 2. All tests pass
dotnet test

# 3. Your new tests pass
dotnet test tests/IntelliFin.IdentityService.Tests/ --filter "YourNewTests"

# 4. No breaking changes
dotnet test --no-build
```

---

## Communication

### **Report Progress (Daily)**

Post in your coordination channel:
```
Agent: [Your ID]
Story: [X.X - Story Name]
Status: [In Progress/Blocked/Complete]
Today's Plan: [Brief description]
Blockers: [Any issues]
```

### **When PR Ready**

Notify orchestrator:
```
✅ Story X.X Ready for Review
Branch: feature/story-X.X-name
Changes: [Brief summary]
Tests: [Pass/Fail - details]
PR Link: [GitHub URL]
```

---

## Common Issues

### **"I don't see the integration branch"**
```bash
git fetch --all
git checkout feature/iam-remaining-work
```

### **"My story file is missing"**
Check you're in the stories subdirectory:
```bash
ls docs/domains/identity-access-management/stories/
```

### **"Build fails with missing dependencies"**
```bash
dotnet restore
dotnet build
```

### **"Tests fail in existing code"**
This is normal - there are pre-existing issues. Focus on making sure YOUR new tests pass and you don't introduce NEW failures.

---

## Success Criteria

**Each story MUST:**
- ✅ Meet all Acceptance Criteria in story file
- ✅ Include unit tests (>80% coverage for new code)
- ✅ Pass all existing tests
- ✅ Follow existing code patterns
- ✅ Update IMPLEMENTATION_STATUS.md when complete

---

## Estimated Timeline

| Week | Agent A | Agent B | Agent C |
|------|---------|---------|---------|
| 1 | Story 1.7 (days 1-4) | Story 2.2 (days 1-2) | Story 5.1 (ongoing) |
| 2-3 | Story 6.1 (days 5-16) | Story 6.2 (days 3-9) | Story 5.1 (ongoing) |
| 4 | Integration testing | Integration testing | Final docs |

**Target:** All stories complete in 3-4 weeks

---

## Quick Reference

**Integration Branch:** `feature/iam-remaining-work`

**Story Files:**
- Story 1.7: `docs/.../stories/story-1.7-baseline-role-templates.md`
- Story 2.2: `docs/.../stories/story-2.2-tenant-api-endpoints.md`
- Story 5.1: `docs/.../stories/story-5.1-testing-and-documentation.md`
- Story 6.1: `docs/.../stories/story-6.1-migration-orchestration.md`
- Story 6.2: `docs/.../stories/story-6.2-self-service-password-reset.md`

**Help Docs:**
- Implementation Status: `docs/.../IMPLEMENTATION_STATUS.md`
- Coordination Guide: `docs/.../PARALLEL_DEVELOPMENT_GUIDE.md`
- PRD: `docs/.../iam-enhancement-prd.md`

---

## Contact

**Orchestrator:** [Your contact method]  
**Questions:** Post in coordination channel  
**Blockers:** Escalate immediately

---

**Last Updated:** 2025-10-17  
**Status:** ✅ Ready for agent assignment
