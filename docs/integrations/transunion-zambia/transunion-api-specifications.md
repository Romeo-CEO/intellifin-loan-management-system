# TransUnion Zambia API Specifications - Limelight Moneylink Services

## Executive Summary

This document defines the detailed API specifications for TransUnion Zambia credit bureau integration in the Limelight Moneylink Services LMS system. It covers API endpoints, authentication, data models, request/response formats, and integration protocols for comprehensive credit bureau data access.

## Business Context

### API Integration Purpose
- **Credit Data Access**: Real-time access to comprehensive credit information for first-time applicants
- **V1 Focus**: Single credit report endpoint containing all necessary data (score, history, fraud flags)
- **Compliance**: Meeting regulatory requirements for credit bureau utilization
- **Operational Efficiency**: Streamlined credit assessment processes
- **Data Quality**: Access to accurate and up-to-date credit information

### API Integration Philosophy
- **Real-Time**: Immediate access to current credit information
- **Secure**: Robust security measures for sensitive credit data
- **Reliable**: High availability and performance for business operations
- **Compliant**: Full adherence to regulatory and data protection requirements
- **Audit-Ready**: Complete audit trail for all API interactions

## API Architecture

### 1. API Structure
**API Components (V1)**:
```
API Services:
- Authentication API
- Credit Report API (Single comprehensive endpoint)

API Features:
- RESTful endpoints
- JSON data format
- OAuth 2.0 authentication
- Rate limiting
- Error handling
- Audit logging
```

**V1 Scope**:
```
V1 Implementation:
- Single endpoint: /api/v1/credit-reports/individual/{nrc_number}
- Comprehensive response containing all necessary data
- Internal parsing of score, history, and fraud flags
- Our Credit Bureau Service acts as Anti-Corruption Layer (ACL)
```

**API Environment**:
```
Environment Configuration:
- Production Environment
- Staging Environment
- Development Environment
- Testing Environment
- Sandbox Environment
```

### 2. API Integration Model
**Integration Architecture**:
```
LMS System ←→ Credit Bureau Service (ACL) ←→ TransUnion Zambia API
     ↓                    ↓                           ↓
Credit Assessment    Data Transformation        Credit Bureau Services
Risk Management      Security Management        Credit Reports
Decision Support     Audit Logging              Credit Scores
```

**Anti-Corruption Layer (ACL)**:
```
ACL Function:
The Credit Bureau Service will act as our Anti-Corruption Layer (ACL). It will receive internal requests and be responsible for translating them into the exact format required by the real TransUnion Zambia API. This specification defines the ideal internal interface that our ACL will work against.
```

## Authentication and Security

### 1. Authentication Framework
**Authentication Method**:
```
Authentication Type: OAuth 2.0 Client Credentials
Grant Type: client_credentials
Token Type: Bearer Token
Token Expiry: 3600 seconds (1 hour)
```

**Secrets Management**:
```
Vault Integration:
All sensitive credentials (client_id, client_secret) will be securely stored and managed in HashiCorp Vault. Our Credit Bureau Service will fetch these credentials at runtime and will handle the OAuth 2.0 token request and refresh lifecycle.
```

**Authentication Endpoint**:
```
POST /oauth/token
Content-Type: application/x-www-form-urlencoded

Request Body:
grant_type=client_credentials
client_id=limelight_moneylink_client_id
client_secret=encrypted_client_secret
scope=credit_reports credit_scores risk_assessment
```

**Authentication Response**:
```json
{
  "access_token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "token_type": "Bearer",
  "expires_in": 3600,
  "scope": "credit_reports credit_scores risk_assessment"
}
```

### 2. Security Headers
**Required Headers**:
```
Authorization: Bearer {access_token}
Content-Type: application/json
X-Client-ID: limelight_moneylink_client_id
X-Request-ID: {unique_request_id}
X-Timestamp: {ISO_8601_timestamp}
X-Signature: {HMAC_SHA256_signature}
```

**Security Validation**:
```
Security Checks:
- Token validation
- Request signature verification
- Timestamp validation
- Rate limiting
- IP whitelisting
- Audit logging
```

## API Endpoints

### 1. Credit Report API
**Individual Credit Report**:
```
GET /api/v1/credit-reports/individual/{nrc_number}
Authorization: Bearer {access_token}
Content-Type: application/json

Path Parameters:
- nrc_number: National Registration Card number

Query Parameters:
- include_history: boolean (default: true)
- include_scores: boolean (default: true)
- include_fraud: boolean (default: true)
```

**Credit Report Response**:
```json
{
  "report_id": "CR-2024-001234",
  "nrc_number": "123456/78/9",
  "personal_info": {
    "full_name": "John Doe",
    "date_of_birth": "1985-06-15",
    "gender": "Male",
    "address": {
      "street": "123 Main Street",
      "city": "Lusaka",
      "province": "Lusaka",
      "postal_code": "10101"
    },
    "phone": "+260955123456",
    "email": "john.doe@email.com"
  },
  "credit_summary": {
    "total_accounts": 5,
    "active_accounts": 3,
    "closed_accounts": 2,
    "total_credit_limit": 50000.00,
    "total_outstanding": 15000.00,
    "credit_utilization": 30.0
  },
  "credit_score": {
    "score": 750,
    "score_range": "Good",
    "score_date": "2024-01-15T10:30:00Z",
    "score_factors": [
      "Payment history: Excellent",
      "Credit utilization: Good",
      "Credit length: Average",
      "Recent activity: Good"
    ]
  },
  "credit_history": [
    {
      "account_id": "ACC-001",
      "creditor_name": "ABC Bank",
      "account_type": "Credit Card",
      "account_status": "Active",
      "opening_date": "2020-03-15",
      "credit_limit": 20000.00,
      "outstanding_balance": 5000.00,
      "payment_status": "Current",
      "last_payment_date": "2024-01-10",
      "payment_history": "000000000000"
    }
  ],
  "fraud_indicators": {
    "fraud_score": 25,
    "fraud_risk": "Low",
    "fraud_alerts": [],
    "identity_verification": "Verified"
  },
  "report_date": "2024-01-15T10:30:00Z",
  "report_expiry": "2024-02-15T10:30:00Z"
}
```

### 2. V1 Implementation Note
**Single Endpoint Approach**:
```
V1 Scope:
For V1, we will only implement the functionality of the single /api/v1/credit-reports/individual/{nrc_number} endpoint. This comprehensive response contains all necessary data (personal info, credit summary, score, history, and fraud flags). Our Credit Bureau Service will parse this response to extract the score and other relevant data for our internal risk assessment and decisioning workflows.
```

## Error Handling

### 1. Error Response Format
**Standard Error Response**:
```json
{
  "error": {
    "code": "INVALID_NRC",
    "message": "Invalid NRC number format",
    "details": "NRC number must be in format: XXXXX/XX/X",
    "request_id": "req_123456789",
    "timestamp": "2024-01-15T10:30:00Z"
  }
}
```

### 2. Error Codes
**Common Error Codes**:
```
Authentication Errors:
- INVALID_CREDENTIALS: Invalid client credentials
- TOKEN_EXPIRED: Access token has expired
- INSUFFICIENT_SCOPE: Insufficient permissions

Request Errors:
- INVALID_NRC: Invalid NRC number format
- NRC_NOT_FOUND: NRC number not found in database
- INVALID_REQUEST: Invalid request format
- MISSING_PARAMETERS: Required parameters missing

System Errors:
- INTERNAL_ERROR: Internal server error
- SERVICE_UNAVAILABLE: Service temporarily unavailable
- RATE_LIMIT_EXCEEDED: Rate limit exceeded
- TIMEOUT: Request timeout
```

## Rate Limiting

### 1. Rate Limit Specifications
**Rate Limit Rules**:
```
Rate Limits:
- Credit Reports: 100 requests per hour
- Credit Scores: 200 requests per hour
- Risk Assessment: 50 requests per hour
- Fraud Detection: 100 requests per hour

Rate Limit Headers:
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 95
X-RateLimit-Reset: 1642248600
```

### 2. Rate Limit Handling
**Rate Limit Response**:
```json
{
  "error": {
    "code": "RATE_LIMIT_EXCEEDED",
    "message": "Rate limit exceeded",
    "details": "Maximum 100 requests per hour allowed",
    "retry_after": 3600,
    "request_id": "req_123456789",
    "timestamp": "2024-01-15T10:30:00Z"
  }
}
```

## Data Models

### 1. Credit Report Model (V1 Primary Model)
**Credit Report Structure**:
```
Credit Report:
- report_id: string
- nrc_number: string
- personal_info: PersonalInfo
- credit_summary: CreditSummary
- credit_score: CreditScore
- credit_history: CreditHistory[]
- fraud_indicators: FraudIndicators
- report_date: datetime
- report_expiry: datetime
```

**V1 Data Extraction**:
```
Internal Processing:
Our Credit Bureau Service will extract the following data from the comprehensive credit report response:
- Credit Score: For our internal risk assessment
- Credit History: For payment behavior analysis
- Fraud Indicators: For fraud risk assessment
- Personal Information: For identity verification
- Credit Summary: For overall credit profile assessment
```

## Integration Guidelines

### 1. Best Practices
**Integration Best Practices**:
```
Best Practices:
- Implement proper error handling
- Use connection pooling
- Implement retry logic
- Cache responses when appropriate
- Monitor API usage
- Implement proper logging
- Use secure communication
- Follow rate limits
```

### 2. Performance Optimization
**Performance Guidelines**:
```
Optimization Strategies:
- Use appropriate HTTP methods
- Implement response caching
- Use compression
- Optimize request size
- Implement connection pooling
- Use async processing
- Monitor performance
- Optimize data processing
```

## Security Considerations

### 1. Data Security
**Security Requirements**:
```
Security Measures:
- Use HTTPS for all communications
- Implement proper authentication
- Encrypt sensitive data
- Implement access controls
- Use secure headers
- Implement audit logging
- Monitor security events
- Regular security reviews
```

### 2. Data Protection
**Protection Requirements**:
```
Protection Measures:
- Data encryption in transit
- Data encryption at rest
- Access control
- Data masking
- Audit logging
- Privacy compliance
- Data retention policies
- Secure data disposal
```

## Testing and Validation

### 1. API Testing
**Testing Requirements**:
```
Testing Types:
- Unit testing
- Integration testing
- Performance testing
- Security testing
- Compliance testing
- User acceptance testing
```

### 2. Validation Procedures
**Validation Process**:
```
Validation Steps:
1. API endpoint testing
2. Authentication testing
3. Data validation testing
4. Error handling testing
5. Performance testing
6. Security testing
7. Compliance testing
8. User acceptance testing
```

## Next Steps

This TransUnion Zambia API Specifications document serves as the foundation for:
1. **API Integration** - TransUnion API integration implementation
2. **Data Processing** - Credit bureau data processing and management
3. **Security Implementation** - API security and authentication setup
4. **Testing Framework** - API testing and validation procedures
5. **Performance Optimization** - API performance monitoring and optimization

---

**Document Status**: Ready for Review  
**Next Document**: `tingg-payment-gateway/disbursement-integration.md` - Tingg payment gateway disbursement integration
