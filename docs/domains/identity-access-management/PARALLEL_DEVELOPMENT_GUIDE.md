# IAM Parallel Development Guide

**Integration Branch:** `feature/iam-remaining-work`  
**Created:** 2025-10-17  
**Status:** Active  
**Target Completion:** 3-4 weeks

---

## Overview

This guide coordinates parallel development for the 4 remaining IAM stories across multiple AI agents. The integration branch strategy prevents merge conflicts and enables independent work.

---

## Integration Branch Structure

```
feature/iam-remaining-work (INTEGRATION BRANCH)
├── Contains: All completed work (Stories 1.1-1.6, 2.1, 3.1-3.2, 4.1-4.2)
├── Status: 75% complete (12/16 stories)
└── Purpose: Central merge point for all remaining work

Agent Feature Branches (created from integration):
├── feature/story-1.7-baseline-roles
├── feature/story-2.2-tenant-claims
├── feature/story-5.1-testing
├── feature/story-6.1-migration
└── feature/story-6.2-self-service
```

---

## Agent Assignments & Branches

### **Agent A - Primary (Story 1.7, 6.1)**

**Stories:**
- Story 1.7: Baseline Role Templates and Seed Data (3-4 days)
- Story 6.1: Migration Orchestration and Cutover (10-12 days)

**Branch Creation:**
```bash
# Start Story 1.7
git checkout feature/iam-remaining-work
git pull origin feature/iam-remaining-work
git checkout -b feature/story-1.7-baseline-roles
# Work on story 1.7
# Commit and push when done

# After 1.7 is merged, start 6.1
git checkout feature/iam-remaining-work
git pull origin feature/iam-remaining-work
git checkout -b feature/story-6.1-migration
```

**File Ownership:**
- `apps/IntelliFin.IdentityService/Data/Seeds/`
- `apps/IntelliFin.IdentityService/Services/BaselineSeedService.cs`
- `apps/IntelliFin.IdentityService/Controllers/Platform/SeedController.cs`
- `scripts/migration/` (all migration scripts)
- `apps/IntelliFin.IdentityService/Controllers/Platform/MigrationController.cs`

---

### **Agent B - Secondary (Story 2.2, 6.2)**

**Stories:**
- Story 2.2: Tenant Context in JWT Claims (1-2 days, verification/testing)
- Story 6.2: Self-Service Password Reset (5-7 days)

**Branch Creation:**
```bash
# Start Story 2.2
git checkout feature/iam-remaining-work
git pull origin feature/iam-remaining-work
git checkout -b feature/story-2.2-tenant-claims
# Work on story 2.2
# Commit and push when done

# After 2.2 is merged, start 6.2
git checkout feature/iam-remaining-work
git pull origin feature/iam-remaining-work
git checkout -b feature/story-6.2-self-service
```

**File Ownership:**
- `apps/IntelliFin.IdentityService/Services/KeycloakRoleMapper.cs` (for tenant claim mapping)
- `apps/IntelliFin.ApiGateway/Middleware/TenantContextMiddleware.cs` (if created)
- `apps/IntelliFin.IdentityService/Controllers/AccountController.cs`
- `apps/IntelliFin.IdentityService/Services/AccountManagementService.cs`
- Email templates: `templates/keycloak/email/`

---

### **Agent C - QA (Story 5.1)**

**Stories:**
- Story 5.1: Comprehensive Testing and Documentation (ongoing, 3-5 days)

**Branch Creation:**
```bash
git checkout feature/iam-remaining-work
git pull origin feature/iam-remaining-work
git checkout -b feature/story-5.1-testing
```

**File Ownership:**
- `tests/IntelliFin.IdentityService.Tests/` (all test files)
- `tests/IntelliFin.Tests.Integration/IAM/` (if created)
- API documentation updates
- Deployment runbooks

---

## Daily Workflow

### **Morning Routine (9:00 AM)**

Each agent:
1. Check integration branch for new merges
2. Update your feature branch if changes affect your work

```bash
git checkout feature/iam-remaining-work
git pull origin feature/iam-remaining-work

git checkout feature/story-X.X-your-story
git merge feature/iam-remaining-work
# Resolve conflicts if any
```

---

### **End of Day Routine (5:00 PM)**

When story is complete:
1. Ensure all tests pass
2. Push your feature branch
3. Create PR to integration branch
4. Notify other agents in Slack/Discord

```bash
# Commit your work
git add .
git commit -m "feat(story-X.X): Description of changes"

# Push to your feature branch
git push origin feature/story-X.X-your-story

# Create PR on GitHub
# Target: feature/iam-remaining-work
# Reviewers: Orchestrator + Other agents (optional)
```

---

## Merge Strategy

### **Story Completion Checklist**

Before creating PR, verify:
- [ ] All story Acceptance Criteria met
- [ ] Unit tests pass (`dotnet test`)
- [ ] No breaking changes to existing code
- [ ] Code follows existing patterns
- [ ] No TODO comments without tracking issue
- [ ] Updated IMPLEMENTATION_STATUS.md

### **PR Merge Order**

**Priority 1 (Blockers):**
- Story 1.7 (blocks 6.1 - migration needs baseline roles)

**Priority 2 (Independent):**
- Story 2.2 (independent - can merge anytime)
- Story 5.1 (ongoing - can merge as tests are added)

**Priority 3 (Final features):**
- Story 6.1 (after 1.7 complete)
- Story 6.2 (can run parallel with 6.1)

### **Merge Process**

1. **Orchestrator reviews PR**
2. **Automated checks pass** (if CI/CD configured)
3. **Merge to integration branch** using "Squash and Merge" or "Merge Commit"
4. **Update IMPLEMENTATION_STATUS.md** - mark story as ✅ COMPLETE
5. **Notify all agents** that integration branch has been updated

---

## Conflict Resolution Protocol

### **If you encounter merge conflicts:**

1. **DO NOT force push** - You'll lose work
2. **Communicate with other agent** who modified the same file
3. **Resolve conflicts manually**:

```bash
git checkout feature/iam-remaining-work
git pull origin feature/iam-remaining-work
git checkout feature/story-X.X-your-story
git merge feature/iam-remaining-work

# Git will show conflicts
# Edit conflicted files manually
# Choose the correct code

git add <resolved-files>
git commit -m "fix: Resolve merge conflicts with integration branch"
git push origin feature/story-X.X-your-story
```

4. **If uncertain**, pause and ask orchestrator

### **Shared File Coordination**

**These files may be touched by multiple agents:**

| File | Primary Owner | Rule |
|------|--------------|------|
| `ServiceCollectionExtensions.cs` | Agent A | Add TODO comments for other agents |
| `appsettings.json` | Agent A | Document config keys in PR |
| `Program.cs` | Agent A | Coordinate startup logic changes |
| `LmsDbContext.cs` | Agent A | No changes expected (schema complete) |

**Protocol for shared files:**
1. Add comment: `// TODO: [Agent-B] Add your service registration here after Story X.X`
2. Other agent fills in TODO when their story is ready
3. Remove TODO comment after completion

---

## Testing Gates

### **Before Merging to Integration Branch**

Each story MUST pass:

1. ✅ **Unit Tests**: All new code has unit tests
   ```bash
   dotnet test tests/IntelliFin.IdentityService.Tests/
   ```

2. ✅ **Integration Tests**: If story adds API endpoints
   ```bash
   dotnet test tests/IntelliFin.Tests.Integration/
   ```

3. ✅ **Manual Testing**: Test the feature end-to-end
   - Use Postman/curl to test API endpoints
   - Verify database changes
   - Check logs for errors

4. ✅ **No Regressions**: Existing tests still pass
   ```bash
   dotnet test --no-build
   ```

---

## Communication Protocol

### **Slack/Discord Channels**

- **#iam-coordination**: Daily updates, merge notifications
- **#iam-blockers**: Report any blockers immediately
- **#iam-questions**: Technical questions, clarifications

### **Daily Standup (Async - Posted by 10 AM)**

Each agent posts:
```
Agent: [Your Name]
Story: [X.X - Story Name]
Status: [In Progress/Blocked/Complete]
Today's Plan: [What you'll work on]
Blockers: [Any issues preventing progress]
```

### **Merge Notification (Required)**

When you merge to integration:
```
🎉 Story X.X Merged!
Branch: feature/story-X.X-name -> feature/iam-remaining-work
Changes: [Brief description]
Impact: [Which files changed that others might depend on]
Next: [What you're working on next]
```

---

## Timeline & Milestones

### **Week 1: Foundation Completion**
- ✅ Story 1.7: Baseline Roles (Agent A) - Days 1-4
- ✅ Story 2.2: Tenant Claims (Agent B) - Days 1-2

### **Week 2-3: Final Features**
- 🔄 Story 6.1: Migration (Agent A) - Days 5-16
- 🔄 Story 6.2: Self-Service (Agent B) - Days 3-9
- 🔄 Story 5.1: Testing (Agent C) - Days 1-14 (ongoing)

### **Week 4: Integration & Final Testing**
- Integration testing
- Load testing
- Documentation finalization
- Prepare for merge to `main`

---

## Success Metrics

**Target:**
- ✅ All 16 stories complete (100%)
- ✅ Test coverage >80%
- ✅ Zero merge conflicts requiring manual resolution
- ✅ All agents complete on time

**Current Progress:**
- ✅ 12/16 stories complete (75%)
- ⏳ 4 stories in progress
- 📅 Target: 3-4 weeks

---

## Troubleshooting

### **"My branch is behind integration branch"**
```bash
git checkout feature/story-X.X-your-story
git merge feature/iam-remaining-work
git push origin feature/story-X.X-your-story
```

### **"I accidentally committed to integration branch"**
```bash
# DON'T PANIC! Reset your changes
git checkout feature/iam-remaining-work
git reset --hard origin/feature/iam-remaining-work

# Recreate your feature branch
git checkout -b feature/story-X.X-your-story
# Cherry-pick your commits if needed
```

### **"Another agent's changes broke my code"**
1. Identify the breaking change
2. Message the agent in #iam-coordination
3. Discuss solution (they fix or you adapt)
4. Update your code accordingly

---

## Orchestrator Responsibilities

**Daily:**
- [ ] Review PRs within 4 hours
- [ ] Monitor #iam-coordination for blockers
- [ ] Update IMPLEMENTATION_STATUS.md

**Weekly:**
- [ ] Review overall progress
- [ ] Adjust agent assignments if needed
- [ ] Merge completed stories to integration branch
- [ ] Prepare weekly progress report

---

## References

- **Implementation Status**: `docs/domains/identity-access-management/IMPLEMENTATION_STATUS.md`
- **PRD**: `docs/domains/identity-access-management/iam-enhancement-prd.md`
- **Story Files**: `docs/domains/identity-access-management/stories/`

---

**Last Updated:** 2025-10-17  
**Orchestrator:** AI Agent (Codex)  
**Status:** ✅ Integration branch created and ready for parallel development
