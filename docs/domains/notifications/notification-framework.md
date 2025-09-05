# Notification Framework

## Overview

The LMS Notification Framework is a centralized, event-driven communication system designed to manage all automated and manual communications originating from the LMS. This framework ensures that the right message gets to the right person at the right time through the most appropriate channel.

## Core Architecture Principles

### 1. Centralized Logic
- **Single Communications Service**: All notification logic is handled by a dedicated Communications Service
- **Event-Driven Architecture**: Other services publish events (e.g., `LoanApprovedEvent`) rather than sending messages directly
- **API-First Design**: Simple API calls to the Communications Service for manual notifications
- **Decoupled Design**: Business logic is completely separated from communication logic

### 2. Multi-Channel Delivery Support

#### V1 Priority Channels
- **SMS**: Primary channel for all external customer communications
- **In-App Notifications**: Internal staff communications (loan officers, approvers, managers)

#### Future-Ready Architecture
- **Email**: Ready for implementation when required
- **Push Notifications**: Architecture supports mobile app notifications
- **WhatsApp Business API**: Framework ready for future integration

### 3. Resilience & Reliability
- **Polly Resilience Policies**: All external gateway calls wrapped with retry logic
- **Circuit Breaker Pattern**: Automatic failover and recovery mechanisms
- **Dead Letter Queues**: Failed messages queued for retry or manual intervention
- **Delivery Status Tracking**: Complete audit trail of all communication attempts

## Core Components

### 1. Communications Service
**Purpose**: Central orchestrator for all notification activities

**Key Responsibilities**:
- Event consumption and processing
- Template management and personalization
- Channel routing and delivery
- Delivery status tracking and logging
- Retry logic and error handling

**API Endpoints**:
```
POST /api/communications/send
GET  /api/communications/templates
POST /api/communications/templates
PUT  /api/communications/templates/{id}
GET  /api/communications/logs
```

### 2. Template Management System
**Purpose**: User-friendly interface for creating and managing message templates

**Features**:
- **Template Categories**: Organized by business function (loan origination, collections, approvals)
- **Personalization Tokens**: Support for dynamic content insertion
  - `{{customer_name}}` - Customer's full name
  - `{{loan_amount}}` - Loan amount with currency formatting
  - `{{due_date}}` - Payment due date
  - `{{outstanding_balance}}` - Current outstanding balance
  - `{{next_payment_amount}}` - Next payment amount
  - `{{loan_reference}}` - Unique loan reference number
  - `{{branch_name}}` - Branch location
  - `{{contact_phone}}` - Customer service phone number
- **Multi-Language Support**: Templates in English and local languages
- **Version Control**: Template versioning and approval workflows
- **Preview Functionality**: Test templates with sample data

### 3. Event-Driven Triggers
**Purpose**: Automatic notification triggering based on business events

**Event Types**:
- **Loan Origination Events**:
  - `LoanApplicationSubmitted`
  - `LoanApproved`
  - `LoanRejected`
  - `LoanDisbursed`
  - `LoanDocumentationComplete`
- **Payment Events**:
  - `PaymentReceived`
  - `PaymentOverdue`
  - `PaymentFailed`
  - `PaymentScheduled`
- **Collections Events**:
  - `CollectionCallScheduled`
  - `CollectionCallCompleted`
  - `PaymentArrangementAgreed`
  - `LegalActionInitiated`
- **System Events**:
  - `UserLogin`
  - `SystemMaintenance`
  - `SecurityAlert`

### 4. Scheduled Triggers
**Purpose**: Time-based notification delivery

**Scheduling Capabilities**:
- **Payment Reminders**: Configurable days before due date (1, 3, 7 days)
- **Overdue Notifications**: Daily escalation for overdue payments
- **Collection Follow-ups**: Scheduled follow-up communications
- **Report Generation**: Automated report delivery to management
- **System Health Checks**: Regular system status notifications

## Database Schema

### Core Tables

#### CommunicationsLog
```sql
CREATE TABLE CommunicationsLog (
    id BIGINT PRIMARY KEY IDENTITY(1,1),
    event_id VARCHAR(100) NOT NULL,
    recipient_id VARCHAR(100) NOT NULL,
    recipient_type VARCHAR(50) NOT NULL, -- 'customer', 'staff', 'system'
    channel VARCHAR(50) NOT NULL, -- 'sms', 'email', 'in_app', 'push'
    template_id BIGINT NOT NULL,
    content TEXT NOT NULL,
    personalization_data JSON,
    status VARCHAR(50) NOT NULL, -- 'pending', 'sent', 'delivered', 'failed'
    gateway_response JSON,
    created_at DATETIME2 DEFAULT GETUTCDATE(),
    sent_at DATETIME2,
    delivered_at DATETIME2,
    failure_reason VARCHAR(500),
    retry_count INT DEFAULT 0,
    max_retries INT DEFAULT 3
);
```

#### NotificationTemplates
```sql
CREATE TABLE NotificationTemplates (
    id BIGINT PRIMARY KEY IDENTITY(1,1),
    name VARCHAR(200) NOT NULL,
    category VARCHAR(100) NOT NULL,
    channel VARCHAR(50) NOT NULL,
    language VARCHAR(10) DEFAULT 'en',
    subject VARCHAR(500), -- For email templates
    content TEXT NOT NULL,
    personalization_tokens JSON, -- List of required tokens
    is_active BIT DEFAULT 1,
    created_by VARCHAR(100) NOT NULL,
    created_at DATETIME2 DEFAULT GETUTCDATE(),
    updated_by VARCHAR(100),
    updated_at DATETIME2,
    version INT DEFAULT 1
);
```

#### InAppNotifications
```sql
CREATE TABLE InAppNotifications (
    id BIGINT PRIMARY KEY IDENTITY(1,1),
    user_id VARCHAR(100) NOT NULL,
    title VARCHAR(200) NOT NULL,
    message TEXT NOT NULL,
    notification_type VARCHAR(100) NOT NULL, -- 'info', 'warning', 'success', 'error'
    action_url VARCHAR(500), -- Optional link for user action
    is_read BIT DEFAULT 0,
    created_at DATETIME2 DEFAULT GETUTCDATE(),
    read_at DATETIME2,
    expires_at DATETIME2 -- Optional expiration
);
```

#### CommunicationRouting
```sql
CREATE TABLE CommunicationRouting (
    id BIGINT PRIMARY KEY IDENTITY(1,1),
    event_type VARCHAR(100) NOT NULL,
    recipient_type VARCHAR(50) NOT NULL,
    channel VARCHAR(50) NOT NULL,
    template_id BIGINT NOT NULL,
    priority INT DEFAULT 1, -- 1=high, 2=medium, 3=low
    is_active BIT DEFAULT 1,
    created_at DATETIME2 DEFAULT GETUTCDATE()
);
```

## Integration Points

### 1. External SMS Gateway Integration
**Primary Provider**: Twilio (with Clickatell as backup)
- **API Integration**: RESTful API calls with authentication
- **Delivery Status**: Webhook callbacks for delivery confirmations
- **Rate Limiting**: Respect provider rate limits and quotas
- **Cost Tracking**: Monitor SMS costs and usage patterns

### 2. Internal Service Integration
**Event Bus**: Azure Service Bus or RabbitMQ
- **Event Publishing**: Services publish events to the bus
- **Event Consumption**: Communications Service subscribes to relevant events
- **Message Serialization**: JSON format with schema validation
- **Dead Letter Handling**: Failed message processing and retry logic

### 3. Frontend Integration
**Real-time Updates**: SignalR for in-app notifications
- **Connection Management**: User session-based connections
- **Notification Broadcasting**: Real-time delivery to connected users
- **Offline Handling**: Queue notifications for offline users
- **Badge Management**: Unread notification counts

## Security & Compliance

### 1. Data Protection
- **PII Encryption**: All personal data encrypted at rest and in transit
- **Access Controls**: Role-based access to communication logs
- **Audit Trail**: Complete logging of all communication activities
- **Data Retention**: Configurable retention policies for communication logs

### 2. Regulatory Compliance
- **Opt-out Management**: Customer preference management
- **Consent Tracking**: Record customer communication preferences
- **Spam Prevention**: Rate limiting and content filtering
- **GDPR Compliance**: Right to be forgotten and data portability

## Performance & Scalability

### 1. High Availability
- **Service Redundancy**: Multiple instances of Communications Service
- **Database Clustering**: High availability database setup
- **Load Balancing**: Distributed processing of notification queues
- **Health Monitoring**: Comprehensive health checks and alerting

### 2. Scalability
- **Horizontal Scaling**: Auto-scaling based on queue depth
- **Queue Management**: Distributed message queues for high throughput
- **Caching**: Template and routing rule caching
- **Batch Processing**: Efficient batch sending for bulk notifications

## Monitoring & Analytics

### 1. Delivery Metrics
- **Success Rates**: Channel-specific delivery success rates
- **Response Times**: Gateway response time monitoring
- **Error Rates**: Failed delivery tracking and analysis
- **Cost Analysis**: Communication cost tracking and optimization

### 2. Business Metrics
- **Engagement Rates**: Customer response to notifications
- **Conversion Tracking**: Business outcome measurement
- **A/B Testing**: Template and timing optimization
- **Reporting Dashboard**: Real-time communication analytics

## Implementation Roadmap

### Phase 1: Core Framework (V1)
- [ ] Communications Service development
- [ ] SMS gateway integration (Twilio)
- [ ] In-app notification system
- [ ] Basic template management
- [ ] Event-driven triggers for loan origination

### Phase 2: Enhanced Features
- [ ] Advanced template personalization
- [ ] Scheduled notification system
- [ ] Email channel integration
- [ ] Comprehensive audit logging
- [ ] Admin interface for template management

### Phase 3: Advanced Capabilities
- [ ] Push notification support
- [ ] A/B testing framework
- [ ] Advanced analytics and reporting
- [ ] Multi-language template support
- [ ] WhatsApp Business API integration

This framework provides a robust, scalable foundation for all LMS communication needs while maintaining the flexibility to adapt to future requirements and regulatory changes.
