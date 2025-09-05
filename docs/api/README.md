# IntelliFin Loan Management System - API Documentation

## Overview

The IntelliFin Loan Management System provides a comprehensive set of APIs for managing loan applications, client data, and financial operations. The system is built using a microservices architecture with an API Gateway for unified access.

## Base URLs

- **API Gateway**: `http://localhost:5033` (Development)
- **Identity Service**: `http://localhost:5235` (Development)

## Authentication

All API endpoints (except authentication) require a valid JWT token in the Authorization header:

```
Authorization: Bearer <jwt_token>
```

### Getting a Token (Development)

```bash
POST http://localhost:5235/auth/dev-token
Content-Type: application/json

{
  "username": "dev-user",
  "roles": ["Admin"]
}
```

**Response:**
```json
{
  "access_token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "token_type": "Bearer",
  "expires_in": 3600
}
```

## API Endpoints

### Client Management

#### Get All Clients
```
GET /api/clients/
Authorization: Bearer <token>
```

#### Create Client
```
POST /api/clients/
Authorization: Bearer <token>
Content-Type: application/json

{
  "firstName": "John",
  "lastName": "Doe",
  "nationalId": "123456789"
}
```

### Loan Origination

#### Create Loan Application
```
POST /api/origination/loan-applications
Authorization: Bearer <token>
Content-Type: application/json

{
  "clientId": "11111111-1111-1111-1111-111111111111",
  "amount": 50000,
  "termMonths": 12,
  "productCode": "PAYROLL"
}
```

**Response:**
```json
{
  "applicationId": "f7d3e5af-75c3-4553-bc9e-92603907c86e",
  "message": "Loan application created and published"
}
```

### Health Checks

#### API Gateway Health
```
GET /health
```

#### Service Health
```
GET /api/clients/health
GET /api/origination/health
GET /api/communications/health
```

## Error Responses

All APIs return standard HTTP status codes:

- `200 OK` - Success
- `400 Bad Request` - Invalid request data
- `401 Unauthorized` - Missing or invalid token
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Resource not found
- `500 Internal Server Error` - Server error

Error response format:
```json
{
  "error": "Error message",
  "details": "Additional error details"
}
```

## Rate Limiting

- Development: No rate limiting
- Production: 1000 requests per minute per client

## Versioning

Current API version: v1
Version is included in the URL path: `/api/v1/...`

## OpenAPI Documentation

Interactive API documentation is available at:
- Development: `http://localhost:5033/swagger`
