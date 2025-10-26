# IntelliFin.ClientManagement Integration Tests

This project contains integration tests for the Client Management service using TestContainers and WebApplicationFactory.

## Requirements

- .NET 9.0 SDK
- Docker (for TestContainers in database tests)

## Running Tests

```bash
# Run all integration tests
dotnet test

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run specific test category
dotnet test --filter "FullyQualifiedName~Database"
dotnet test --filter "FullyQualifiedName~Middleware"
dotnet test --filter "FullyQualifiedName~Authentication"
```

## Test Categories

### Database Tests (`Database/`)
- DbContext connection and migration tests
- Schema validation tests
- Query execution tests
- **Story:** 1.1

### Health Check Tests (`HealthChecks/`)
- Database health check endpoint tests
- Service availability tests
- **Story:** 1.1

### Middleware Tests (`Middleware/`)
- Correlation ID middleware tests
- Global exception handler tests
- **Story:** 1.2

### Authentication Tests (`Authentication/`)
- JWT authentication tests
- Protected endpoint access tests
- Token validation tests
- **Story:** 1.2

### Validation Tests (`Validation/`)
- FluentValidation integration tests
- Model validation tests
- **Story:** 1.2

## TestContainers

Database tests use TestContainers to spin up real SQL Server instances in Docker containers.
The containers are automatically started and stopped for each test class.

### SQL Server Container
- Image: `mcr.microsoft.com/mssql/server:2022-latest`
- Password: `YourStrong!Passw0rd`
- Automatically applies EF Core migrations

## WebApplicationFactory

Middleware, authentication, and validation tests use `WebApplicationFactory` for in-memory API testing without requiring Docker.

## Test Coverage

### Story 1.1 - Database Foundation (7 tests)
- ✅ Database connection and migration tests
- ✅ Health check endpoint tests

### Story 1.2 - Shared Libraries & DI (12 tests)
- ✅ Correlation ID middleware tests (3 tests)
- ✅ Global exception handler tests (2 tests)
- ✅ JWT authentication tests (4 tests)
- ✅ FluentValidation tests (2 tests)
- ✅ Integration with shared libraries verified

### Story 1.3 - Client CRUD Operations (22 tests)
- ✅ ClientService unit tests (10 tests)
- ✅ ClientController API integration tests (12 tests)
- ✅ Full CRUD workflow with authentication
- ✅ Validation and error handling

**Total Tests:** 41 tests  
**All Categories:** Database, HealthChecks, Middleware, Authentication, Validation, Services, Controllers

## Future Tests

Subsequent stories will add:
- Story 1.4: Client versioning tests
- Story 1.5: AdminService audit integration tests
- Story 1.6: Document integration tests
- Story 1.7: Communications integration tests

## Test Patterns

### Correlation ID Tests
- Auto-generation when not provided
- Preservation of provided correlation IDs
- Uniqueness across requests

### Exception Handler Tests
- Consistent error response format
- Appropriate HTTP status codes
- Environment-specific error details

### Authentication Tests
- Unauthorized access without token
- Successful access with valid token
- Rejection of invalid/expired tokens

### Validation Tests
- 400 Bad Request for invalid data
- Detailed validation error messages
- Successful validation of valid data
