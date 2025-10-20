# IntelliFin.ClientManagement Integration Tests

This project contains integration tests for the Client Management service using TestContainers.

## Requirements

- .NET 9.0 SDK
- Docker (for TestContainers)

## Running Tests

```bash
# Run all integration tests
dotnet test

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run specific test
dotnet test --filter "FullyQualifiedName~DbContextTests"
```

## Test Categories

### Database Tests (`Database/`)
- DbContext connection and migration tests
- Schema validation tests
- Query execution tests

### Health Check Tests (`HealthChecks/`)
- Database health check endpoint tests
- Service availability tests

## TestContainers

These tests use TestContainers to spin up real SQL Server instances in Docker containers.
The containers are automatically started and stopped for each test class.

### SQL Server Container
- Image: `mcr.microsoft.com/mssql/server:2022-latest`
- Password: `YourStrong!Passw0rd`
- Automatically applies EF Core migrations

## Test Coverage

Story 1.1 Acceptance Criteria:
- ✅ SQL Server database can be created and connected
- ✅ EF Core migrations can be applied successfully
- ✅ DbContext can connect and execute queries
- ✅ Health check endpoint `/health/db` returns healthy status
- ✅ Health check returns unhealthy when database is down

## Future Tests

Subsequent stories will add:
- Story 1.3: Client CRUD operation tests
- Story 1.4: Client versioning tests
- Story 1.6: Document integration tests
- Story 1.7: Communications integration tests
