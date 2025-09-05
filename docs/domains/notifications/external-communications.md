# External Communications

## Overview

External communications handle all customer-facing notifications and messages sent through external channels (SMS, Email, and future channels like WhatsApp). This system ensures timely, personalized, and compliant communication with customers throughout their loan lifecycle.

## Core Features

### 1. Multi-Channel Delivery

#### SMS (Primary Channel - V1)
**Purpose**: Primary communication channel for all customer notifications

**Key Features**:
- **High Delivery Rate**: SMS has the highest open and read rates in Zambia
- **Universal Access**: Works on all mobile phones without internet
- **Immediate Delivery**: Real-time message delivery
- **Cost Effective**: Lower cost per message compared to other channels
- **Regulatory Compliance**: Meets BoZ requirements for customer communication

**Technical Implementation**:
- **Primary Provider**: Africa's Talking. The system will be built to integrate directly with their comprehensive API.
- **API Integration**: RESTful API with webhook callbacks for real-time delivery status updates.
- **Resilience**: All API calls to Africa's Talking will be wrapped in resilience policies (Polly) to handle transient network errors.
- **Rate Limiting**: The system will respect the provider's rate limits and quotas to ensure reliable delivery.
- **Cost Tracking**: The Communications Service will log the cost of each sent message to monitor and manage usage.

#### Email (Future Channel)
**Purpose**: Detailed communications and document delivery

**Planned Features**:
- **Rich Content**: HTML emails with branding and formatting
- **Document Attachments**: Loan agreements, statements, receipts
- **Bulk Communications**: Marketing and promotional content
- **Delivery Tracking**: Open rates, click tracking, bounce handling
- **Template Management**: Professional email templates

#### WhatsApp Business API (Future Channel)
**Purpose**: Interactive customer service and notifications

**Planned Features**:
- **Interactive Messages**: Buttons, quick replies, list messages
- **Media Support**: Images, documents, voice messages
- **Customer Service**: Automated responses and human handoff
- **Rich Notifications**: Enhanced formatting and media content
- **Two-Way Communication**: Customer responses and queries

### 2. Template Management System

#### Template Categories

**Loan Origination Templates**:
- **Application Confirmation**: Acknowledgment of loan application submission
- **Documentation Request**: Request for additional documents
- **Approval Notification**: Loan approval with terms and conditions
- **Rejection Notification**: Loan rejection with reasons and alternatives
- **Disbursement Confirmation**: Confirmation of loan disbursement

**Payment & Collections Templates**:
- **Payment Reminder**: Reminder before due date (1, 3, 7 days)
- **Payment Confirmation**: Confirmation of successful payment
- **Overdue Notification**: Notification of overdue payment
- **Payment Arrangement**: Confirmation of payment arrangement
- **Final Notice**: Legal notice before collection action

**Customer Service Templates**:
- **Welcome Message**: Welcome message for new customers
- **Account Statement**: Monthly account statement summary
- **Interest Rate Change**: Notification of interest rate changes
- **Product Promotion**: New product and service offerings
- **System Maintenance**: Notification of scheduled maintenance

#### Personalization Tokens

**Customer Information**:
- `{{customer_name}}` - Customer's full name
- `{{first_name}}` - Customer's first name
- `{{last_name}}` - Customer's last name
- `{{customer_id}}` - Unique customer identifier
- `{{phone_number}}` - Customer's phone number
- `{{email_address}}` - Customer's email address

**Loan Information**:
- `{{loan_reference}}` - Unique loan reference number
- `{{loan_amount}}` - Loan amount with currency formatting
- `{{outstanding_balance}}` - Current outstanding balance
- `{{next_payment_amount}}` - Next payment amount
- `{{due_date}}` - Next payment due date
- `{{interest_rate}}` - Current interest rate
- `{{loan_term}}` - Loan term in months
- `{{disbursement_date}}` - Loan disbursement date

**Payment Information**:
- `{{payment_amount}}` - Payment amount
- `{{payment_date}}` - Payment date
- `{{payment_method}}` - Payment method used
- `{{transaction_reference}}` - Payment transaction reference
- `{{remaining_balance}}` - Remaining loan balance after payment

**Institution Information**:
- `{{institution_name}}` - Financial institution name
- `{{branch_name}}` - Branch location
- `{{contact_phone}}` - Customer service phone number
- `{{contact_email}}` - Customer service email
- `{{website_url}}` - Institution website
- `{{physical_address}}` - Branch physical address

#### Template Examples

**Payment Reminder (3 Days Before Due Date)**:
```
Hi {{first_name}},

Your loan payment of K{{next_payment_amount}} is due on {{due_date}}.

Loan Reference: {{loan_reference}}
Outstanding Balance: K{{outstanding_balance}}

Please ensure payment is made on time to avoid late fees.

For assistance, call {{contact_phone}}.

{{institution_name}}
```

**Loan Approval Notification**:
```
Congratulations {{first_name}}!

Your loan application has been approved.

Loan Amount: K{{loan_amount}}
Interest Rate: {{interest_rate}}% per annum
Term: {{loan_term}} months
Monthly Payment: K{{next_payment_amount}}

Your loan will be disbursed within 24 hours.

{{institution_name}}
{{contact_phone}}
```

**Payment Confirmation**:
```
Dear {{first_name}},

Payment of K{{payment_amount}} received successfully on {{payment_date}}.

Loan Reference: {{loan_reference}}
Transaction Ref: {{transaction_reference}}
Remaining Balance: K{{remaining_balance}}

Thank you for your payment.

{{institution_name}}
```

### 3. Event-Driven Communication Triggers

#### Loan Origination Events

**Application Submitted**:
- **Trigger**: New loan application created
- **Recipient**: Customer
- **Template**: Application Confirmation
- **Timing**: Immediate
- **Content**: Acknowledgment with reference number and next steps

**Documentation Required**:
- **Trigger**: Missing documents identified
- **Recipient**: Customer
- **Template**: Documentation Request
- **Timing**: Within 2 hours
- **Content**: List of required documents and submission instructions

**Loan Approved**:
- **Trigger**: Loan approval decision made
- **Recipient**: Customer
- **Template**: Approval Notification
- **Timing**: Immediate
- **Content**: Approval details, terms, and disbursement timeline

**Loan Rejected**:
- **Trigger**: Loan rejection decision made
- **Recipient**: Customer
- **Template**: Rejection Notification
- **Timing**: Immediate
- **Content**: Rejection reasons and alternative options

**Loan Disbursed**:
- **Trigger**: Loan disbursement completed
- **Recipient**: Customer
- **Template**: Disbursement Confirmation
- **Timing**: Within 1 hour
- **Content**: Disbursement confirmation and payment schedule

#### Payment Events

**Payment Received**:
- **Trigger**: Successful payment processing
- **Recipient**: Customer
- **Template**: Payment Confirmation
- **Timing**: Immediate
- **Content**: Payment confirmation and updated balance

**Payment Reminder (7 Days)**:
- **Trigger**: 7 days before due date
- **Recipient**: Customer
- **Template**: Payment Reminder
- **Timing**: 9:00 AM
- **Content**: Upcoming payment reminder with amount and due date

**Payment Reminder (3 Days)**:
- **Trigger**: 3 days before due date
- **Recipient**: Customer
- **Template**: Payment Reminder
- **Timing**: 9:00 AM
- **Content**: Urgent payment reminder with payment options

**Payment Reminder (1 Day)**:
- **Trigger**: 1 day before due date
- **Recipient**: Customer
- **Template**: Payment Reminder
- **Timing**: 9:00 AM
- **Content**: Final payment reminder with late fee warning

**Payment Overdue**:
- **Trigger**: Payment not received by due date
- **Recipient**: Customer
- **Template**: Overdue Notification
- **Timing**: 9:00 AM next business day
- **Content**: Overdue notice with late fees and payment options

#### Collections Events

**Collection Call Scheduled**:
- **Trigger**: Collection call scheduled
- **Recipient**: Customer
- **Template**: Collection Call Notification
- **Timing**: 1 hour before scheduled call
- **Content**: Call appointment confirmation and purpose

**Payment Arrangement Agreed**:
- **Trigger**: Payment arrangement approved
- **Recipient**: Customer
- **Template**: Payment Arrangement Confirmation
- **Timing**: Immediate
- **Content**: Arrangement details and payment schedule

**Final Notice**:
- **Trigger**: 30 days overdue
- **Recipient**: Customer
- **Template**: Final Notice
- **Timing**: 9:00 AM
- **Content**: Legal notice before collection action

### 4. Scheduled Communication Triggers

#### Daily Scheduled Jobs

**Payment Reminders**:
- **Schedule**: Daily at 9:00 AM
- **Purpose**: Send payment reminders based on due date
- **Logic**: Query loans with due dates in 1, 3, or 7 days
- **Batch Processing**: Process all reminders in batches
- **Error Handling**: Retry failed deliveries

**Overdue Notifications**:
- **Schedule**: Daily at 9:00 AM
- **Purpose**: Send overdue notifications
- **Logic**: Query loans with overdue payments
- **Escalation**: Different templates based on days overdue
- **Collection Assignment**: Assign to collection officers

**Account Statements**:
- **Schedule**: Monthly on 1st at 10:00 AM
- **Purpose**: Send monthly account statements
- **Logic**: Generate statements for all active loans
- **Content**: Payment history, current balance, next payment
- **Delivery**: SMS summary with email attachment option

#### Weekly Scheduled Jobs

**Collection Follow-ups**:
- **Schedule**: Weekly on Mondays at 10:00 AM
- **Purpose**: Follow up on collection activities
- **Logic**: Review collection call outcomes
- **Content**: Status updates and next steps
- **Assignment**: Update collection officer assignments

**Performance Reports**:
- **Schedule**: Weekly on Fridays at 5:00 PM
- **Purpose**: Send performance summaries to management
- **Logic**: Aggregate weekly performance metrics
- **Content**: Loan volume, collection rates, system health
- **Recipients**: Branch managers, regional managers

#### Monthly Scheduled Jobs

**Regulatory Reports**:
- **Schedule**: Monthly on last business day
- **Purpose**: Send regulatory compliance reports
- **Logic**: Generate BoZ and other regulatory reports
- **Content**: Compliance status, audit trails, risk metrics
- **Recipients**: Compliance officers, senior management

**Customer Satisfaction Surveys**:
- **Schedule**: Monthly on 15th at 2:00 PM
- **Purpose**: Send customer satisfaction surveys
- **Logic**: Select random sample of active customers
- **Content**: Short survey with rating and feedback options
- **Follow-up**: Thank you message and incentive offers

### 5. Customer Communication Preferences

#### Opt-in/Opt-out Management
**Purpose**: Ensure compliance with communication preferences and regulations

**Preference Types**:
- **Marketing Communications**: Promotional messages and product offers
- **Service Communications**: Essential loan-related notifications
- **Collection Communications**: Overdue and collection-related messages
- **System Notifications**: Maintenance and system-related messages

**Preference Management**:
- **Default Settings**: All communications enabled by default
- **Easy Opt-out**: Simple opt-out mechanism in all messages
- **Preference Center**: Customer portal for managing preferences
- **Compliance Tracking**: Audit trail of preference changes

**Implementation**:
```json
{
  "customer_id": "CUST123",
  "preferences": {
    "marketing_communications": {
      "enabled": true,
      "channels": ["sms", "email"],
      "frequency": "weekly"
    },
    "service_communications": {
      "enabled": true,
      "channels": ["sms"],
      "frequency": "immediate"
    },
    "collection_communications": {
      "enabled": true,
      "channels": ["sms"],
      "frequency": "immediate"
    },
    "system_notifications": {
      "enabled": true,
      "channels": ["sms"],
      "frequency": "immediate"
    }
  },
  "opt_out_date": null,
  "last_updated": "2024-01-15T10:30:00Z"
}
```

#### Communication Frequency Limits
**Purpose**: Prevent spam and ensure positive customer experience

**Frequency Rules**:
- **Payment Reminders**: Maximum 3 reminders per payment
- **Overdue Notifications**: Maximum 1 per day
- **Marketing Messages**: Maximum 2 per week
- **Collection Messages**: Maximum 1 per day
- **System Notifications**: No limit (essential communications)

**Implementation**:
```json
{
  "frequency_limits": {
    "payment_reminders": {
      "max_per_payment": 3,
      "min_interval_hours": 24
    },
    "overdue_notifications": {
      "max_per_day": 1,
      "min_interval_hours": 24
    },
    "marketing_messages": {
      "max_per_week": 2,
      "min_interval_hours": 72
    },
    "collection_messages": {
      "max_per_day": 1,
      "min_interval_hours": 24
    }
  }
}
```

### 6. Delivery Status Tracking

#### Status Types
- **Pending**: Message queued for delivery
- **Sent**: Message sent to gateway
- **Delivered**: Message delivered to recipient
- **Failed**: Message delivery failed
- **Bounced**: Message bounced back
- **Opted Out**: Recipient has opted out

#### Delivery Confirmation
**SMS Delivery**:
- **Gateway Callbacks**: Webhook callbacks from SMS provider
- **Status Updates**: Real-time status updates in database
- **Retry Logic**: Automatic retry for failed deliveries
- **Error Handling**: Detailed error logging and analysis

**Email Delivery**:
- **Bounce Handling**: Automatic bounce detection and processing
- **Open Tracking**: Track email open rates
- **Click Tracking**: Track link clicks in emails
- **Unsubscribe Handling**: Automatic unsubscribe processing

#### Analytics & Reporting
**Delivery Metrics**:
- **Success Rates**: Channel-specific delivery success rates
- **Response Times**: Time from send to delivery confirmation
- **Error Analysis**: Detailed analysis of delivery failures
- **Cost Tracking**: Communication cost analysis and optimization

**Customer Engagement**:
- **Open Rates**: Percentage of messages opened/read
- **Response Rates**: Customer response to communications
- **Opt-out Rates**: Rate of customers opting out
- **Engagement Trends**: Long-term engagement pattern analysis

### 7. Compliance & Security

#### Regulatory Compliance
**Data Protection**:
- **PII Encryption**: All personal data encrypted at rest and in transit
- **Consent Management**: Proper consent tracking and management
- **Data Retention**: Configurable retention policies for communication logs
- **Right to be Forgotten**: Customer data deletion capabilities

**Communication Regulations**:
- **Opt-out Compliance**: Easy and immediate opt-out mechanisms
- **Content Restrictions**: Compliance with local communication laws
- **Timing Restrictions**: Respect for local time zones and business hours
- **Language Requirements**: Support for local languages

#### Security Measures
**API Security**:
- **Authentication**: Secure API authentication and authorization
- **Rate Limiting**: Protection against abuse and spam
- **Input Validation**: Comprehensive input validation and sanitization
- **Audit Logging**: Complete audit trail of all API calls

**Data Security**:
- **Encryption**: End-to-end encryption for all communications
- **Access Controls**: Role-based access to communication data
- **Secure Storage**: Encrypted storage of communication logs
- **Backup Security**: Encrypted backups with secure key management

### 8. Integration Architecture

#### External Gateway Integration
**SMS Gateway (Africa's Talking)**:
```json
{
  "provider": "africastalking",
  "endpoint": "https://api.africastalking.com/version1/messaging",
  "authentication": {
    "type": "api_key",
    "username": "sandbox",
    "api_key": "env_var_from_vault"
  },
  "webhook_url": "https://lms-api.limelight.co.zm/webhooks/africastalking/status",
  "shortcode_or_sender_id": "LIMELIGHT",
  "rate_limits": {
    "requests_per_second": 200
  }
}
```

**Email Gateway (Future)**:
```json
{
  "provider": "sendgrid",
  "endpoint": "https://api.sendgrid.com/v3/mail/send",
  "authentication": {
    "type": "api_key",
    "api_key": "env_var"
  },
  "webhook_url": "https://lms-api.com/webhooks/sendgrid/events",
  "rate_limits": {
    "requests_per_second": 50,
    "emails_per_month": 100000
  }
}
```

#### Internal Service Integration
**Event Bus Integration**:
```json
{
  "event_bus": "azure_service_bus",
  "connection_string": "env_var",
  "topics": {
    "loan_events": "loan-events-topic",
    "payment_events": "payment-events-topic",
    "collection_events": "collection-events-topic"
  },
  "subscriptions": {
    "communications_service": "communications-subscription"
  }
}
```

**Database Integration**:
```sql
-- Communication Log Table
CREATE TABLE CommunicationLog (
    id BIGINT PRIMARY KEY IDENTITY(1,1),
    event_id VARCHAR(100) NOT NULL,
    customer_id VARCHAR(100) NOT NULL,
    channel VARCHAR(50) NOT NULL,
    template_id BIGINT NOT NULL,
    content TEXT NOT NULL,
    personalization_data JSON,
    status VARCHAR(50) NOT NULL,
    gateway_response JSON,
    created_at DATETIME2 DEFAULT GETUTCDATE(),
    sent_at DATETIME2,
    delivered_at DATETIME2,
    failure_reason VARCHAR(500),
    retry_count INT DEFAULT 0,
    max_retries INT DEFAULT 3
);

-- Customer Communication Preferences
CREATE TABLE CustomerCommunicationPreferences (
    id BIGINT PRIMARY KEY IDENTITY(1,1),
    customer_id VARCHAR(100) NOT NULL,
    preference_type VARCHAR(100) NOT NULL,
    enabled BIT DEFAULT 1,
    channels JSON,
    frequency VARCHAR(50),
    opt_out_date DATETIME2,
    created_at DATETIME2 DEFAULT GETUTCDATE(),
    updated_at DATETIME2 DEFAULT GETUTCDATE()
);
```

This external communications system ensures that customers receive timely, relevant, and compliant communications throughout their loan lifecycle while maintaining the highest standards of security and regulatory compliance.
