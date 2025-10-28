# API Design and Integration

Define new API endpoints and integration with existing APIs:

1. Plan new API endpoints required for the enhancement
2. Ensure consistency with existing API patterns
3. Define authentication and authorization integration
4. Plan versioning strategy if needed

### API Integration Strategy

**API Integration Strategy:** The CollectionsService will expose a set of RESTful APIs to allow for manual payment posting, management of reconciliation tasks, and retrieval of collections-related data. These APIs will adhere to the existing IntelliFin API Gateway routing, authentication (JWT), authorization (role-based), and observability standards (correlation IDs, branch IDs).
**Authentication:** Bearer JWT with standard IntelliFin authentication flow. Specific endpoints will require granular, role-based authorization (e.g., `collections:manage:payments`, `collections:manage:writeoffs`). Step-up authentication will be considered for high-risk operations like write-offs or restructuring.
**Versioning:** API versioning will follow the existing IntelliFin strategy, likely using URL path versioning (e.g., `/api/v1/collections/`).

### New API Endpoints

#### Post Payment

-   **Method:** `POST`
-   **Endpoint:** `/api/v1/collections/payments`
-   **Purpose:** To allow manual posting of a new payment transaction against a loan. This will trigger the internal payment processing logic.
-   **Integration:** Integrates with `PaymentProcessingService` and may trigger `RepaymentPostedEvent`. Requires `collections:manage:payments` permission.
#### Request
```json
{
  "loanId": "guid",
  "amount": 0.00,
  "paymentDate": "datetime",
  "source": "string",
  "reference": "string",
  "installmentId": "guid" (optional)
}
```
#### Response
```json
{
  "transactionId": "guid",
  "status": "string",
  "message": "string"
}
```

#### Get Repayment Schedule

-   **Method:** `GET`
-   **Endpoint:** `/api/v1/collections/loans/{loanId}/schedule`
-   **Purpose:** To retrieve the full repayment schedule for a specific loan.
-   **Integration:** Integrates with `RepaymentScheduleService`. Requires `collections:read:schedules` permission.
#### Request
```json
{}
```
#### Response
```json
{
  "scheduleId": "guid",
  "loanId": "guid",
  "generatedDate": "datetime",
  "status": "string",
  "currency": "string",
  "installments": [
    {
      "installmentId": "guid",
      "dueDate": "datetime",
      "expectedPrincipal": 0.00,
      "expectedInterest": 0.00,
      "expectedTotal": 0.00,
      "paidAmount": 0.00,
      "status": "string",
      "daysPastDue": 0
    }
  ]
}
```

#### Get Overdue Loans

-   **Method:** `GET`
-   **Endpoint:** `/api/v1/collections/loans/overdue`
-   **Purpose:** To retrieve a list of loans currently in an overdue status. Supports filtering and pagination.
-   **Integration:** Integrates with `InstallmentRepository` and `ArrearsClassificationService`. Requires `collections:read:overdue` permission.
#### Request
```json
{
  "page": 1,
  "pageSize": 10,
  "minDaysPastDue": 1,
  "maxDaysPastDue": 30,
  "bozCategory": "string"
}
```
#### Response
```json
{
  "totalCount": 0,
  "page": 1,
  "pageSize": 10,
  "items": [
    {
      "loanId": "guid",
      "clientName": "string",
      "productName": "string",
      "currentDPD": 0,
      "bozCategory": "string",
      "totalOverdueAmount": 0.00
    }
  ]
}
```

#### Reclassify Loan

-   **Method:** `POST`
-   **Endpoint:** `/api/v1/collections/loans/{loanId}/reclassify`
-   **Purpose:** To manually re-evaluate and reclassify a loan's BoZ arrears category. Requires specific permissions and potentially dual control.
-   **Integration:** Integrates with `ArrearsClassificationService`. Requires `collections:manage:classification` permission, possibly step-up auth.
#### Request
```json
{
  "reason": "string",
  "overrideCategory": "string" (optional, with justification)
}
```
#### Response
```json
{
  "loanId": "guid",
  "oldBoZCategory": "string",
  "newBoZCategory": "string",
  "reclassificationDate": "datetime",
  "message": "string"
}
```

#### Initiate Write-Off

-   **Method:** `POST`
-   **Endpoint:** `/api/v1/collections/loans/{loanId}/writeoff`
-   **Purpose:** To initiate the write-off workflow for a loan deemed uncollectible. Triggers Camunda workflow for dual control.
-   **Integration:** Integrates with `WriteOffRequestService` and `CollectionsWorkflowService`. Requires `collections:manage:writeoffs` permission and dual control.
#### Request
```json
{
  "reason": "string",
  "writeOffAmount": 0.00
}
```
#### Response
```json
{
  "requestId": "guid",
  "loanId": "guid",
  "status": "string",
  "message": "string",
  "camundaProcessInstanceId": "string"
}
```
