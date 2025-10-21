# Client Management DTOs

This directory contains Data Transfer Objects (DTOs) and their FluentValidation validators for the Client Management API.

## DTOs

### CreateClientRequest
**Purpose:** Create a new client  
**Endpoint:** `POST /api/clients`  
**Validator:** `CreateClientRequestValidator`

**Required Fields:**
- Nrc (11 chars, format: XXXXXX/XX/X)
- FirstName, LastName
- DateOfBirth (must be 18+)
- Gender (M, F, Other)
- MaritalStatus (Single, Married, Divorced, Widowed)
- PrimaryPhone (+260XXXXXXXXX)
- PhysicalAddress
- City, Province
- BranchId

**Optional Fields:**
- PayrollNumber (PMEC integration)
- OtherNames
- Nationality (defaults to "Zambian")
- Ministry, EmployerType, EmploymentStatus
- SecondaryPhone, Email

### UpdateClientRequest
**Purpose:** Update existing client  
**Endpoint:** `PUT /api/clients/{id}`  
**Validator:** `UpdateClientRequestValidator`

**Mutable Fields Only:**
- FirstName, LastName, OtherNames
- MaritalStatus
- PrimaryPhone, SecondaryPhone, Email
- PhysicalAddress, City, Province
- Ministry, EmployerType, EmploymentStatus

**Immutable Fields (cannot be updated):**
- Nrc, PayrollNumber
- DateOfBirth, Gender, Nationality
- KycStatus, AmlRiskLevel, RiskRating
- Status, BranchId
- CreatedAt, CreatedBy, UpdatedAt, UpdatedBy

### ClientResponse
**Purpose:** Client data in API responses  
**Endpoints:** All GET, POST, PUT responses  
**Contains:** All client properties including audit fields

---

## Validators

### CreateClientRequestValidator

**Validation Rules:**

**NRC:**
- Required
- Exactly 11 characters
- Format: `^\d{6}/\d{2}/\d$` (e.g., "123456/78/9")

**Names:**
- FirstName, LastName: Required, max 100 chars
- OtherNames: Optional, max 100 chars

**Date of Birth:**
- Required
- Cannot be in future
- Must be at least 18 years old (custom validation method)

**Gender:**
- Required
- Must be: M, F, or Other

**Marital Status:**
- Required
- Must be: Single, Married, Divorced, or Widowed

**Phone:**
- PrimaryPhone: Required, format `^\+260\d{9}$`
- SecondaryPhone: Optional, same format

**Email:**
- Optional
- Must be valid email format

**Address:**
- PhysicalAddress: Required, max 500 chars
- City, Province: Required, max 100 chars

**Branch:**
- BranchId: Required

**Employment (optional):**
- EmployerType: Government, Private, or Self
- EmploymentStatus: Active, Suspended, or Terminated

### UpdateClientRequestValidator

**Same validation rules as CreateClientRequestValidator** except:
- No NRC validation (immutable)
- No DateOfBirth validation (immutable)
- No Gender validation (immutable)
- No BranchId validation (immutable)

---

## Usage Examples

### Valid Create Request

```json
{
  "nrc": "123456/78/9",
  "firstName": "John",
  "lastName": "Doe",
  "dateOfBirth": "1990-01-01",
  "gender": "M",
  "maritalStatus": "Single",
  "nationality": "Zambian",
  "primaryPhone": "+260977123456",
  "email": "john.doe@example.com",
  "physicalAddress": "123 Main Street, Woodlands",
  "city": "Lusaka",
  "province": "Lusaka",
  "branchId": "00000000-0000-0000-0000-000000000001"
}
```

### Valid Update Request

```json
{
  "firstName": "Jane",
  "lastName": "Smith",
  "maritalStatus": "Married",
  "primaryPhone": "+260971234567",
  "email": "jane.smith@example.com",
  "physicalAddress": "456 New Street, Roma",
  "city": "Ndola",
  "province": "Copperbelt"
}
```

### Validation Error Response (400 Bad Request)

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Nrc": ["NRC must be in format XXXXXX/XX/X"],
    "DateOfBirth": ["Client must be at least 18 years old"],
    "PrimaryPhone": ["Primary phone must be in Zambian format (+260XXXXXXXXX)"]
  }
}
```

---

## Testing

All DTOs and validators are covered by integration tests in:
- `tests/IntelliFin.ClientManagement.IntegrationTests/Controllers/ClientControllerTests.cs`
- `tests/IntelliFin.ClientManagement.IntegrationTests/Validation/FluentValidationTests.cs`

---

## Future Enhancements

### Story 1.4: Client Versioning
- Add version history to ClientResponse
- Add point-in-time query parameters

### Story 1.6: Documents
- Add document upload DTOs
- Add document list to ClientResponse

### Story 1.7: Communications
- Add communication consent DTOs
- Add consent management endpoints
