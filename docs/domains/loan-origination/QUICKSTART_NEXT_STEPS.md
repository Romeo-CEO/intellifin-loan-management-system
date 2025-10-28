# Loan Origination Module - Quick Start Guide
## For Continuing Implementation

---

## Current Status (2025-10-27)

✅ **Story 1.1: 88% Complete**
- Domain entities enhanced
- Database migration ready
- Event consumers implemented
- Service builds successfully (0 errors)

📋 **Next Priority: Story 1.2 - Loan Application Processing and Validation**

---

## Quick Setup

### 1. Apply Database Migration

```bash
cd "D:\Projects\Intellifin Loan Management System\libs\IntelliFin.Shared.DomainModels"
dotnet ef database update --context LmsDbContext
```

**Verify**:
```sql
-- Check new columns exist
SELECT COLUMN_NAME 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'LoanApplications' 
  AND COLUMN_NAME IN ('LoanNumber', 'Version', 'IsCurrentVersion', 'RiskGrade')
```

### 2. Start Required Services

```bash
# RabbitMQ (for event consumers)
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management

# SQL Server (if using Docker)
# Or ensure your local SQL Server is running
```

### 3. Build and Run

```bash
cd "D:\Projects\Intellifin Loan Management System"
dotnet build apps/IntelliFin.LoanOriginationService/IntelliFin.LoanOriginationService.csproj
dotnet run --project apps/IntelliFin.LoanOriginationService/IntelliFin.LoanOriginationService.csproj
```

**Health Check**: http://localhost:5000/health

---

## Files You Need to Know

### Core Files (Already Implemented)
```
libs/IntelliFin.Shared.DomainModels/
  ├── Entities/LoanApplication.cs ✅ Enhanced with versioning
  ├── Data/LmsDbContext.cs ✅ Configured with indexes
  └── Migrations/20251027000000_AddLoanVersioningFields.cs ✅ Ready

apps/IntelliFin.LoanOriginationService/
  ├── Program.cs ✅ Service bootstrap complete
  ├── Events/ClientManagementEvents.cs ✅ Event contracts
  ├── Consumers/ClientKycEventConsumers.cs ✅ Event consumers
  ├── Services/LoanApplicationService.cs ⚠️ Needs enhancement
  └── Controllers/LoanApplicationController.cs ⚠️ Needs validation
```

### Documentation
```
docs/domains/loan-origination/
  ├── LOAN_ORIGINATION_KICKOFF.md - Overall plan
  ├── stories/1.1.story.md - Story 1.1 (88% complete)
  ├── stories/1.2.story.md - Next story to implement
  ├── IMPLEMENTATION_SUMMARY_20251027.md - Progress summary
  └── QUICKSTART_NEXT_STEPS.md - This file
```

---

## What's Working

✅ **Database Schema**: New fields configured in LoanApplication entity  
✅ **Event Consumers**: Reacting to ClientManagement KYC events  
✅ **Logging**: Serilog with correlation IDs  
✅ **Health Checks**: `/health` and `/health/db` endpoints  
✅ **Build**: Compiles with 0 errors  

---

## What Needs Work

### Priority 1: Story 1.2 Tasks

1. **Loan Number Generation Service**
   - File to create: `Services/LoanNumberGenerationService.cs`
   - Create LoanNumberSequence table
   - Implement thread-safe sequence generation
   - Format: `{BranchCode}-{Year}-{Sequence}` (e.g., "CHD-2025-00001")

2. **Enhance LoanApplicationService**
   - File: `Services/LoanApplicationService.cs`
   - Add KYC compliance validation
   - Implement loan number assignment
   - Add application state transitions
   - Add audit logging with correlation IDs

3. **Multi-Product Loan Application Support**
   - File: `Services/LoanApplicationService.cs`
   - Validate product-specific rules
   - Support payroll, business, and asset-backed loans

### Priority 2: Story 1.3 - Credit Assessment

1. **Credit Bureau Integration**
   - Create `Services/CreditBureauIntegrationService.cs`
   - Implement API client for external bureaus
   - Add credit report processing

2. **Risk Scoring Enhancement**
   - File: `Services/RiskCalculationEngine.cs` (already exists)
   - Enhance with real credit data
   - Implement BoZ-aligned risk grading

### Priority 3: Story 1.7 - Workflow Integration

1. **Camunda/Zeebe Client**
   - File: `Program.cs` (commented out)
   - Configure Zeebe client properly
   - Create BPMN workflows
   - Implement workflow workers

---

## Testing Guide

### Manual Testing

#### 1. Test Event Consumers

Publish a test event to RabbitMQ:

```csharp
// ClientKycApprovedEvent
{
  "ClientId": "guid-here",
  "ClientName": "Test Client",
  "NationalId": "123456/78/9",
  "ApprovedAt": "2025-10-27T12:00:00Z",
  "ApprovedBy": "Test User",
  "KycLevel": "Standard",
  "CorrelationId": "test-correlation-id"
}
```

**Expected**: Pending loan applications for that client update to "Submitted"

#### 2. Test Health Checks

```bash
curl http://localhost:5000/health
curl http://localhost:5000/health/db
```

**Expected**: JSON response with "Healthy" status

#### 3. Test Loan Application API

```bash
# Create application
curl -X POST http://localhost:5000/api/loanapplication \
  -H "Content-Type: application/json" \
  -d '{
    "ClientId": "guid-here",
    "ProductCode": "PAYROLL",
    "RequestedAmount": 10000,
    "TermMonths": 12,
    "ApplicationData": {}
  }'
```

### Integration Testing (Story 1.9)

Will use TestContainers for:
- SQL Server database
- RabbitMQ message broker
- Event consumer verification

---

## Common Issues & Solutions

### Issue: Build Errors
```bash
# Solution: Restore packages
dotnet restore
dotnet build
```

### Issue: Migration Not Applied
```bash
# Check pending migrations
dotnet ef migrations list --context LmsDbContext

# Apply migration
dotnet ef database update --context LmsDbContext
```

### Issue: RabbitMQ Not Running
```bash
# Start RabbitMQ with Docker
docker start rabbitmq

# Or install locally:
# Windows: choco install rabbitmq
# Mac: brew install rabbitmq
```

### Issue: Database Connection Failed
```bash
# Check connection string in appsettings.json
# Default: Server=(localdb)\\mssqllocaldb;Database=IntelliFin_LoanManagement

# Test connection
dotnet ef dbcontext info --context LmsDbContext
```

---

## Architecture Patterns to Follow

### 1. Event-Driven Communication
```csharp
// Always publish events for state changes
await _bus.Publish(new LoanApplicationSubmittedEvent
{
    ApplicationId = application.Id,
    ClientId = application.ClientId,
    CorrelationId = context.CorrelationId
});
```

### 2. Correlation ID Tracking
```csharp
// Always propagate correlation IDs
_logger.LogInformation(
    "Processing loan application {ApplicationId}, CorrelationId: {CorrelationId}",
    applicationId, correlationId);
```

### 3. Audit Trail
```csharp
// Always set audit fields
application.LastModifiedBy = $"System:{username}";
application.LastModifiedAtUtc = DateTime.UtcNow;
```

### 4. Error Handling
```csharp
try
{
    // Business logic
}
catch (Exception ex)
{
    _logger.LogError(ex, "Error message with context");
    throw; // Re-throw for MassTransit retry
}
```

---

## Next Story: 1.2 Implementation Checklist

### Before Starting
- [ ] Read Story 1.2 document: `stories/1.2.story.md`
- [ ] Apply database migration (Story 1.1)
- [ ] Verify build succeeds (0 errors)
- [ ] Start RabbitMQ and SQL Server

### Core Tasks
- [ ] Create LoanNumberSequence table
- [ ] Implement LoanNumberGenerationService
- [ ] Enhance LoanApplicationService with KYC validation
- [ ] Add loan number assignment on approval
- [ ] Implement backfill script for existing applications

### Testing
- [ ] Unit tests for loan number generation
- [ ] Unit tests for KYC validation
- [ ] Integration test with TestContainers
- [ ] Manual test with real database

### Documentation
- [ ] Update Story 1.2 document with progress
- [ ] Update implementation summary
- [ ] Update this quick start guide

---

## Useful Commands

### Build & Run
```bash
# Build only
dotnet build apps/IntelliFin.LoanOriginationService/IntelliFin.LoanOriginationService.csproj

# Run
dotnet run --project apps/IntelliFin.LoanOriginationService/IntelliFin.LoanOriginationService.csproj

# Watch mode (auto-reload)
dotnet watch --project apps/IntelliFin.LoanOriginationService/IntelliFin.LoanOriginationService.csproj
```

### Database
```bash
# Create migration
dotnet ef migrations add MigrationName --context LmsDbContext

# Apply migration
dotnet ef database update --context LmsDbContext

# Rollback migration
dotnet ef database update PreviousMigrationName --context LmsDbContext

# Drop database
dotnet ef database drop --context LmsDbContext
```

### Testing
```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/IntelliFin.LoanOriginationService.Tests/

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

---

## Resources

### Documentation
- **PRD**: `docs/domains/loan-origination/prd.md`
- **Architecture**: `docs/domains/loan-origination/brownfield-architecture.md`
- **Stories**: `docs/domains/loan-origination/stories/*.md`

### Code Examples
- **ClientManagement**: Similar architecture patterns
- **Collections**: Event-driven examples
- **TreasuryService**: Service integration patterns

### External Documentation
- **MassTransit**: https://masstransit.io/
- **Serilog**: https://serilog.net/
- **EF Core**: https://docs.microsoft.com/en-us/ef/core/

---

## Contact & Support

- Check existing implementation patterns in ClientManagement
- Review similar stories in other modules
- Refer to architectural decisions in brownfield-architecture.md

---

**Last Updated**: 2025-10-27  
**Current Story**: 1.1 (88% complete)  
**Next Story**: 1.2 (Loan Application Processing and Validation)  
**Build Status**: ✅ Successful (0 errors)
