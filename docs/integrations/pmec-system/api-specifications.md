# PMEC API Specifications - Limelight Moneylink Services

## Document Information
- **Document Type**: Technical API Specification
- **Version**: 1.0
- **Last Updated**: [Date]
- **Owner**: Technical Integration Team
- **Compliance**: PSMD API Standards, Government Data Format
- **Reference**: PMEC Integration Overview

## Executive Summary

This document provides detailed technical specifications for the PMEC (Public Service Management Division) API integration, including data formats, authentication methods, error handling, and operational procedures. The API integration enables real-time communication with the Zambian government payroll system for automated loan deductions.

## API Overview

**Strategic Architecture Note**: This document defines the clean, modern API that our PMEC Anti-Corruption Layer (ACL) will expose to the rest of our internal microservices. The ACL's primary responsibility is to translate these clean, internal requests into whatever format the real PMEC system requires (SOAP, XML-RPC, SFTP, etc.). This specification serves as our "Ideal State" target for building the ACL itself.

### Base URL
```
Production: https://api.psmd.gov.zm/v1
Sandbox: https://api-sandbox.psmd.gov.zm/v1
```

### Authentication
- **Method**: OAuth 2.0 with Client Credentials
- **Token Expiry**: 1 hour
- **Refresh**: Automatic token refresh
- **Rate Limiting**: 100 requests per minute per client

### Supported HTTP Methods
- `GET`: Retrieve data (read-only operations)
- `POST`: Create new records
- `PUT`: Update existing records
- `DELETE`: Remove records (limited use)

## Core API Endpoints

### 1. Authentication Endpoint

#### POST /auth/token
**Purpose**: Obtain access token for API operations

**Request Headers**:
```http
Content-Type: application/x-www-form-urlencoded
```

**Request Body**:
```json
{
  "grant_type": "client_credentials",
  "client_id": "limelight_moneylink_client_id",
  "client_secret": "encrypted_client_secret",
  "scope": "payroll_deductions loan_management"
}
```

**Response**:
```json
{
  "access_token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "token_type": "Bearer",
  "expires_in": 3600,
  "scope": "payroll_deductions loan_management"
}
```

**Error Responses**:
```json
{
  "error": "invalid_client",
  "error_description": "Client authentication failed",
  "status_code": 401
}
```

### 2. Employee Verification Endpoint

#### GET /employees/{employee_id}
**Purpose**: Verify government employee status and salary information

**Request Headers**:
```http
Authorization: Bearer {access_token}
Content-Type: application/json
```

**Request Parameters**:
- `employee_id`: PMEC employee number (required)
- `include_salary`: Include salary details (optional, default: false)
- `include_deductions`: Include current deductions (optional, default: true)

**Response**:
```json
{
  "employee_id": "PSMD001234",
  "first_name": "John",
  "last_name": "Doe",
  "employment_status": "ACTIVE",
  "department": "Ministry of Finance",
  "position": "Accountant",
  "salary_grade": "G7",
  "gross_salary": 8500.00,
  "net_salary": 6800.00,
  "current_deductions": [
    {
      "deduction_code": "LML001",
      "amount": 1275.00,
      "description": "Limelight Moneylink Loan",
      "status": "ACTIVE"
    }
  ],
  "available_deduction_capacity": 1275.00,
  "last_updated": "2024-01-15T10:30:00Z"
}
```

**Error Responses**:
```json
{
  "error": "employee_not_found",
  "error_description": "Employee with ID PSMD001234 not found",
  "status_code": 404
}
```

### 3. Loan Registration Endpoint

#### POST /loans/register
**Purpose**: Register a new loan for payroll deduction

**Compliance Note**: This endpoint only sends the single, compliant interest_rate to PMEC. Internal fee structures (admin fees, management fees, etc.) are not transmitted to government systems as they are part of our internal EAR calculation and compliance with the Money Lenders Act. PMEC only needs to know the total monthly deduction amount and the interest rate for regulatory purposes.

**Request Headers**:
```http
Authorization: Bearer {access_token}
Content-Type: application/json
```

**Request Body**:
```json
{
  "loan_reference": "LML-2024-001",
  "employee_id": "PSMD001234",
  "deduction_code": "LML001",
  "loan_amount": 25000.00,
  "monthly_deduction": 1275.00,
  "loan_term": 24,
  "start_date": "2024-02-01",
  "end_date": "2026-01-31",
  "interest_rate": 48.0,
  "contact_details": {
    "phone": "+260955123456",
    "email": "john.doe@email.com"
  }
}
```

**Response**:
```json
{
  "registration_id": "REG-2024-001",
  "status": "PENDING_APPROVAL",
  "message": "Loan registration submitted for approval",
  "estimated_approval_time": "2-3 business days",
  "tracking_number": "TRK-2024-001"
}
```

**Error Responses**:
```json
{
  "error": "deduction_limit_exceeded",
  "error_description": "Total deductions exceed 15% of gross salary",
  "status_code": 400,
  "details": {
    "current_deductions": 1275.00,
    "new_deduction": 1275.00,
    "gross_salary": 8500.00,
    "maximum_allowed": 1275.00
  }
}
```

### 4. Deduction Processing Endpoint

#### POST /deductions/process
**Purpose**: Process monthly loan deductions

**Processing Note**: This endpoint sends only the total deduction_amount to PMEC. The government payroll system does not need our internal accounting breakdown (principal, interest, fees). PMEC confirms the amount successfully deducted, and our internal Collections Service then appropriates that confirmed amount according to our Reducing Balance rules and internal fee structures.

**Request Headers**:
```http
Authorization: Bearer {access_token}
Content-Type: application/json
```

**Request Body**:
```json
{
  "deduction_code": "LML001",
  "processing_month": "2024-02",
  "deductions": [
    {
      "employee_id": "PSMD001234",
      "loan_reference": "LML-2024-001",
      "deduction_amount": 1275.00
    }
  ]
}
```

**Response**:
```json
{
  "processing_id": "PROC-2024-002",
  "status": "PROCESSED",
  "processed_count": 1,
  "total_amount": 1275.00,
  "processing_date": "2024-02-25T09:00:00Z",
  "settlement_date": "2024-02-26T14:00:00Z"
}
```

**Error Responses**:
```json
{
  "error": "insufficient_salary",
  "error_description": "Employee salary insufficient for deduction",
  "status_code": 400,
  "details": {
    "employee_id": "PSMD001234",
    "available_salary": 500.00,
    "required_deduction": 1275.00
  }
}
```

### 5. Deduction Status Endpoint

#### GET /deductions/status/{deduction_code}
**Purpose**: Check status of loan deductions

**Request Headers**:
```http
Authorization: Bearer {access_token}
Content-Type: application/json
```

**Request Parameters**:
- `deduction_code`: LMS deduction code (required)
- `month`: Processing month (optional, format: YYYY-MM)
- `status`: Filter by status (optional: PENDING, PROCESSED, FAILED)

**Response**:
```json
{
  "deduction_code": "LML001",
  "status": "ACTIVE",
  "total_loans": 150,
  "monthly_deduction": 191250.00,
  "last_processing": "2024-02-25T09:00:00Z",
  "next_processing": "2024-03-25T09:00:00Z",
  "monthly_summary": [
    {
      "month": "2024-02",
      "status": "PROCESSED",
      "total_amount": 191250.00,
      "successful_count": 150,
      "failed_count": 0
    }
  ]
}
```

## Data Format Specifications

### Employee Data Format
```json
{
  "employee_id": "string(10)",
  "first_name": "string(50)",
  "last_name": "string(50)",
  "employment_status": "enum(ACTIVE, INACTIVE, SUSPENDED, TERMINATED)",
  "department": "string(100)",
  "position": "string(100)",
  "salary_grade": "string(5)",
  "gross_salary": "decimal(10,2)",
  "net_salary": "decimal(10,2)",
  "hire_date": "date",
  "last_salary_update": "datetime"
}
```

### Loan Registration Format
```json
{
  "loan_reference": "string(20)",
  "employee_id": "string(10)",
  "deduction_code": "string(10)",
  "loan_amount": "decimal(12,2)",
  "monthly_deduction": "decimal(10,2)",
  "loan_term": "integer(1-60)",
  "start_date": "date",
  "end_date": "date",
  "interest_rate": "decimal(5,2)",
  "contact_details": "object"
}
```

### Deduction Processing Format
```json
{
  "deduction_code": "string(10)",
  "processing_month": "string(7)",
  "deductions": "array",
  "total_amount": "decimal(12,2)",
  "processing_date": "datetime"
}
```

## Error Handling

### Standard Error Response Format
```json
{
  "error": "error_code",
  "error_description": "Human-readable error description",
  "status_code": 400,
  "timestamp": "2024-01-15T10:30:00Z",
  "request_id": "req-123456789",
  "details": {
    "field": "additional_error_details"
  }
}
```

### Common Error Codes
| Error Code | HTTP Status | Description |
|------------|-------------|-------------|
| `invalid_token` | 401 | Access token expired or invalid |
| `insufficient_permissions` | 403 | Client lacks required permissions |
| `validation_error` | 400 | Request data validation failed |
| `employee_not_found` | 404 | Specified employee not found |
| `deduction_limit_exceeded` | 400 | Deduction exceeds salary limits |
| `duplicate_registration` | 409 | Loan already registered |
| `system_unavailable` | 503 | PMEC system temporarily unavailable |
| `rate_limit_exceeded` | 429 | API rate limit exceeded |

### Error Recovery Procedures
1. **Token Expiry**: Automatically refresh access token
2. **Rate Limiting**: Implement exponential backoff
3. **System Unavailable**: Queue requests for retry
4. **Validation Errors**: Log and report data issues
5. **Network Errors**: Retry with exponential backoff

## Rate Limiting & Throttling

### Rate Limits
- **Authentication**: 10 requests per minute
- **Employee Verification**: 100 requests per minute
- **Loan Registration**: 50 requests per minute
- **Deduction Processing**: 20 requests per minute
- **Status Queries**: 200 requests per minute

### Throttling Headers
```http
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 95
X-RateLimit-Reset: 1642233600
X-RateLimit-ResetTime: Mon, 17 Jan 2022 00:00:00 GMT
```

### Rate Limit Exceeded Response
```json
{
  "error": "rate_limit_exceeded",
  "error_description": "Rate limit exceeded. Try again in 60 seconds.",
  "status_code": 429,
  "retry_after": 60
}
```

## Security Requirements

### Data Encryption
- **In Transit**: TLS 1.3 encryption
- **At Rest**: AES-256 encryption for sensitive data
- **API Keys**: Encrypted storage with rotation

### Access Control
- **IP Whitelisting**: Restrict API access to authorized IPs
- **Role-Based Access**: Different permissions for different operations
- **Audit Logging**: Complete access and operation logging

### Data Validation
- **Input Sanitization**: Prevent injection attacks
- **Schema Validation**: Strict JSON schema validation
- **Size Limits**: Prevent oversized payload attacks

## Monitoring & Logging

### API Metrics
- **Response Times**: Track API performance
- **Success Rates**: Monitor API reliability
- **Error Rates**: Track error frequency
- **Usage Patterns**: Monitor API usage

### Logging Requirements
- **Request Logging**: Log all API requests
- **Response Logging**: Log all API responses
- **Error Logging**: Detailed error logging
- **Audit Logging**: Security and compliance logging

### Alerting
- **High Error Rates**: Alert on error rate spikes
- **Slow Response Times**: Alert on performance degradation
- **Authentication Failures**: Alert on security issues
- **Rate Limit Exceeded**: Alert on usage spikes

## Testing & Validation

### API Testing
- **Unit Testing**: Individual endpoint testing
- **Integration Testing**: End-to-end workflow testing
- **Performance Testing**: Load and stress testing
- **Security Testing**: Vulnerability and penetration testing

### Test Data
- **Sandbox Environment**: Separate test environment
- **Test Credentials**: Dedicated test client credentials
- **Mock Data**: Realistic test data sets
- **Error Scenarios**: Comprehensive error testing

### Validation Checklist
- [ ] All endpoints respond correctly
- [ ] Error handling works as expected
- [ ] Rate limiting functions properly
- [ ] Authentication and authorization work
- [ ] Data validation is effective
- [ ] Performance meets requirements

## Next Steps

This API specification serves as the foundation for:
1. **Data Mapping Documentation** - Field mappings and transformations
2. **Error Handling Procedures** - Comprehensive error management
3. **Operational Procedures** - Day-to-day operational guidelines
4. **Testing Procedures** - Comprehensive testing protocols

## Document Approval

- **Technical Lead**: [Name] - [Date]
- **API Developer**: [Name] - [Date]
- **Security Officer**: [Name] - [Date]
- **Integration Manager**: [Name] - [Date]

---

**Document Control**: This document must be reviewed and updated when API changes occur.
