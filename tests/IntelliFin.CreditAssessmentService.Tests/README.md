# Credit Assessment Service Tests

## Test Structure

### Unit Tests
- `Unit/RiskCalculationEngineTests.cs` - Risk engine logic
- `Unit/ValidationTests.cs` - Request validation
- `Unit/ConfigurationTests.cs` - Vault configuration

### Integration Tests
- `Integration/CreditAssessmentApiTests.cs` - API endpoints
- `Integration/DatabaseTests.cs` - EF Core operations
- `Integration/ExternalClientTests.cs` - HTTP clients

### Performance Tests
- `Performance/LoadTests.cs` - Load testing
- `Performance/ConcurrencyTests.cs` - Concurrent assessments

## Running Tests

```bash
# All tests
dotnet test

# Unit tests only
dotnet test --filter "Category=Unit"

# Integration tests
dotnet test --filter "Category=Integration"

# With coverage
dotnet test /p:CollectCoverage=true /p:CoverageReportFormat=opencover
```

## Coverage Targets

- Services: 85%+
- Controllers: 80%+
- Domain Logic: 90%+

**Story**: 1.18 - Comprehensive Testing Suite
