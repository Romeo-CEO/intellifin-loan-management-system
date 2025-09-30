# Epic 4: Template Management System

## Overview
Complete the existing template system foundation with full rendering capabilities, dynamic content support, personalization engine, and management interface for creating professional, relevant customer communications.

## Current State Analysis
**Existing Foundation:**
- SmsTemplateService exists with basic structure
- EmailTemplateService foundation in place
- Basic rendering capabilities started
- Template storage infrastructure ready

**Enhancement Required:**
- Complete template rendering engine implementation
- Advanced personalization with customer data
- Template validation and testing framework
- Management interface for template CRUD operations

## User Stories

### Story 4.1: Dynamic Template Management
**As a** communications manager
**I want** to create and modify message templates without code changes
**So that** we can quickly adapt communications for different scenarios

**Acceptance Criteria:**
- ✅ Template CRUD operations via web interface
- ✅ Real-time template preview with sample data
- ✅ Template validation before saving
- ✅ Version control with rollback capability
- ✅ A/B testing support with template variants

### Story 4.2: Advanced Personalization
**As a** loan officer
**I want** personalized messages with customer-specific information
**So that** communications feel professional and relevant

**Acceptance Criteria:**
- ✅ Support all required personalization tokens
- ✅ Dynamic content based on customer data
- ✅ Conditional content rendering
- ✅ Multi-format support (SMS, Email, In-App)
- ✅ Fallback handling for missing data

### Story 4.3: Template Testing Framework
**As a** system administrator
**I want** to test templates before deployment
**So that** we avoid sending incorrect communications

**Acceptance Criteria:**
- ✅ Template preview with real customer data
- ✅ Send test messages to specified recipients
- ✅ Validation of personalization tokens
- ✅ Template performance testing
- ✅ Error handling validation

## Technical Implementation

### Template Engine Architecture
```csharp
// Core rendering engine
public interface ITemplateRenderingEngine
{
    Task<string> RenderAsync(string templateContent, object context);
    Task<string> RenderAsync(int templateId, object context);
    Task<TemplateValidationResult> ValidateAsync(string templateContent);
    Task<string> PreviewAsync(string templateContent, object sampleContext);
}

// Personalization service
public interface IPersonalizationService
{
    Task<object> BuildContextAsync(string recipientId, string eventType);
    Task<string> RenderTokensAsync(string content, object context);
    List<string> ExtractTokens(string content);
    object CreateSampleContext(string templateCategory);
}

// Template management service
public interface ITemplateManagementService
{
    Task<NotificationTemplate> CreateAsync(CreateTemplateRequest request);
    Task<NotificationTemplate> UpdateAsync(int id, UpdateTemplateRequest request);
    Task<List<NotificationTemplate>> GetByCategoryAsync(string category, string? channel = null);
    Task<NotificationTemplate?> GetActiveVersionAsync(string name);
    Task<TemplateTestResult> SendTestAsync(int templateId, string recipientId);
}
```

### Personalization Tokens

#### Customer Information
```csharp
public class CustomerTokens
{
    public string customer_name { get; set; }          // "John Mwanza"
    public string customer_first_name { get; set; }    // "John"
    public string customer_nrc { get; set; }           // "123456/78/9"
    public string customer_phone { get; set; }         // "+260977123456"
    public string customer_email { get; set; }         // "john@example.com"
    public string customer_branch { get; set; }        // "Lusaka Main"
}
```

#### Loan Information
```csharp
public class LoanTokens
{
    public string loan_reference { get; set; }         // "LN202501001"
    public string loan_amount { get; set; }            // "K 50,000.00"
    public string loan_balance { get; set; }           // "K 45,000.00"
    public string loan_type { get; set; }              // "Payroll Loan"
    public string due_date { get; set; }               // "15 Feb 2025"
    public string next_payment { get; set; }           // "K 5,000.00"
    public string dpd_days { get; set; }               // "5"
    public string total_arrears { get; set; }          // "K 2,500.00"
}
```

#### System Information
```csharp
public class SystemTokens
{
    public string company_name { get; set; }           // "IntelliFin Microfinance"
    public string company_phone { get; set; }          // "+260211234567"
    public string company_email { get; set; }          // "info@intellifin.com"
    public string portal_url { get; set; }             // "https://portal.intellifin.com"
    public string current_date { get; set; }           // "15 January 2025"
    public string business_hours { get; set; }         // "Monday-Friday 8AM-5PM"
}
```

### Template Examples

#### Loan Application Confirmation
```text
Subject: Application Received - {{loan_reference}}

Dear {{customer_name}},

Your loan application for {{loan_amount}} has been received successfully.

Reference Number: {{loan_reference}}
Application Date: {{current_date}}
Product Type: {{loan_type}}

We will review your application and respond within 48 hours. You can track your application status at {{portal_url}}.

Thank you for choosing {{company_name}}.

Customer Service: {{company_phone}}
```

#### Payment Overdue Reminder
```text
Subject: Payment Reminder - {{loan_reference}}

Dear {{customer_name}},

This is a friendly reminder that your loan payment of {{next_payment}} was due on {{due_date}}.

Loan Reference: {{loan_reference}}
Outstanding Balance: {{loan_balance}}
Days Past Due: {{dpd_days}}
Amount in Arrears: {{total_arrears}}

Please make your payment as soon as possible to avoid additional charges. Visit {{portal_url}} or call {{company_phone}}.

{{company_name}} Customer Service
```

#### Loan Approval Notification
```text
Subject: Loan Approved - {{loan_reference}}

Congratulations {{customer_name}}!

Your loan application has been approved:
- Loan Amount: {{loan_amount}}
- Reference: {{loan_reference}}
- Product: {{loan_type}}

Next Steps:
1. Visit {{customer_branch}} branch for documentation
2. Bring your NRC ({{customer_nrc}}) and required documents
3. Funds will be disbursed within 24 hours of completion

Questions? Call {{company_phone}} during {{business_hours}}.

{{company_name}}
```

### Template Validation Rules
```csharp
public class TemplateValidator
{
    public TemplateValidationResult Validate(string content, string channel)
    {
        var result = new TemplateValidationResult();

        // SMS-specific validations
        if (channel == "SMS")
        {
            if (content.Length > 160)
                result.Warnings.Add("SMS content exceeds 160 characters");

            if (content.Contains("{{") && !content.Contains("}}"))
                result.Errors.Add("Unclosed personalization token");
        }

        // Email-specific validations
        if (channel == "Email")
        {
            if (!content.Contains("{{customer_name}}"))
                result.Warnings.Add("Email should include customer name");

            if (content.Contains("<script"))
                result.Errors.Add("Script tags not allowed in templates");
        }

        // Universal validations
        ValidateTokens(content, result);
        ValidateConditionalContent(content, result);

        return result;
    }
}
```

### Template Management Interface

#### Template Creation API
```http
POST /api/communications/templates
Content-Type: application/json

{
  "name": "loan-approval-sms",
  "category": "LoanOrigination",
  "channel": "SMS",
  "language": "en",
  "subject": null,
  "content": "Congratulations {{customer_name}}! Your loan {{loan_reference}} for {{loan_amount}} has been approved...",
  "personalizationTokens": ["customer_name", "loan_reference", "loan_amount"],
  "isActive": true
}
```

#### Template Testing API
```http
POST /api/communications/templates/{id}/test
Content-Type: application/json

{
  "recipientId": "customer123",
  "testMode": true,
  "channel": "SMS"
}
```

### Conditional Content Support
```text
Dear {{customer_name}},

{{#if loan_type == "Payroll"}}
Your payroll loan application has been processed.
{{else}}
Your business loan application has been processed.
{{/if}}

{{#if dpd_days > 30}}
⚠️ URGENT: Your account is significantly overdue.
{{else if dpd_days > 0}}
Please note your payment is {{dpd_days}} days overdue.
{{else}}
Thank you for keeping your account current.
{{/if}}

Best regards,
{{company_name}}
```

### A/B Testing Framework
```csharp
public class TemplateVariant
{
    public int Id { get; set; }
    public int TemplateId { get; set; }
    public string Name { get; set; }
    public string Content { get; set; }
    public decimal TrafficPercentage { get; set; }
    public bool IsActive { get; set; }
    public TemplateMetrics Metrics { get; set; }
}

public class TemplateMetrics
{
    public int SentCount { get; set; }
    public int DeliveredCount { get; set; }
    public int ClickCount { get; set; }
    public int ResponseCount { get; set; }
    public decimal DeliveryRate => SentCount > 0 ? (decimal)DeliveredCount / SentCount : 0;
}
```

## Success Metrics
- **Template Rendering Speed**: ≤1 second for complex personalized templates
- **Template Creation Time**: 80% reduction in time to create new templates
- **Personalization Accuracy**: 100% successful token replacement
- **Template Validation**: 100% error prevention before deployment
- **A/B Testing Adoption**: 50% of templates using variant testing

## Dependencies
- Epic 3: Database persistence for template storage
- Customer data access from existing LmsDbContext
- Template management UI (basic implementation)

## Risk Mitigation
- **Template Rendering Failures**: Fallback to basic templates without personalization
- **Missing Data**: Graceful handling with default values
- **Performance Issues**: Template caching and pre-compilation
- **Validation Errors**: Comprehensive testing before template activation
- **A/B Testing Conflicts**: Clear variant selection logic and traffic distribution

## Implementation Phases

### Phase 1: Core Engine (Week 1)
- Template rendering engine implementation
- Basic personalization service
- Token extraction and validation

### Phase 2: Advanced Features (Week 2)
- Conditional content support
- Multi-format rendering
- Template validation framework

### Phase 3: Management Interface (Week 3)
- Template CRUD operations
- Testing framework
- Preview functionality

### Phase 4: A/B Testing (Week 4)
- Variant management
- Traffic distribution
- Metrics collection and analysis