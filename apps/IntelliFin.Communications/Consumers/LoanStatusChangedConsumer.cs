using IntelliFin.Shared.DomainModels.Entities;
using IntelliFin.Shared.DomainModels.Repositories;
using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.Shared.Infrastructure.Messaging.Contracts;
using IntelliFin.Communications.Services;
using IntelliFin.Communications.Models;
using Microsoft.Extensions.Logging;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace IntelliFin.Communications.Consumers;

public class LoanStatusChangedConsumer : BaseNotificationConsumer<LoanStatusChanged>
{
    private readonly LmsDbContext _dbContext;
       private readonly IInAppNotificationService _inAppNotificationService;
    
    public LoanStatusChangedConsumer(
    INotificationRepository notificationRepository,
    LmsDbContext dbContext,
        IInAppNotificationService inAppNotificationService,
        ILogger<LoanStatusChangedConsumer> logger)
    : base(notificationRepository, logger)
    {
         _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _inAppNotificationService = inAppNotificationService ?? throw new ArgumentNullException(nameof(inAppNotificationService));
     }

    protected override async Task ProcessEventAsync(
        LoanStatusChanged eventData,
        ConsumeContext<LoanStatusChanged> context)
    {
        _logger.LogInformation(
            "Processing loan status change event {EventId} for application {ApplicationId}, client {ClientId}: {PreviousStatus} -> {NewStatus}",
            eventData.EventId, eventData.ApplicationId, eventData.ClientId, eventData.PreviousStatus, eventData.NewStatus);

        // Build notification targets for different recipients based on status transition
        var targets = await BuildNotificationTargetsAsync(eventData);

        // Convert targets to notification requests
        var notificationRequests = BuildNotificationRequests(
            eventData.EventId,
            targets,
            "LoanOrigination");

        // Process each notification request
        foreach (var request in notificationRequests)
        {
            await ProcessNotificationRequestAsync(request);
        }

        _logger.LogInformation(
            "Successfully processed {Count} notifications for status change {ApplicationId}: {PreviousStatus} -> {NewStatus}",
            notificationRequests.Count, eventData.ApplicationId, eventData.PreviousStatus, eventData.NewStatus);
    }

    private async Task<List<NotificationTarget>> BuildNotificationTargetsAsync(
        LoanStatusChanged eventData)
    {
        var targets = new List<NotificationTarget>();

        // Always notify the customer for significant status changes
        var customerTarget = await CreateCustomerNotificationTargetAsync(eventData);
        if (customerTarget != null)
        {
            targets.Add(customerTarget);
        }

        // Notify loan officer/manager for workflow status changes
        var officerTarget = await CreateLoanOfficerNotificationTargetAsync(eventData);
        if (officerTarget != null)
        {
            targets.Add(officerTarget);
        }

        // For approvals, branch manager notification
        var managerTarget = await CreateBranchManagerNotificationTargetAsync(eventData);
        if (managerTarget != null)
        {
            targets.Add(managerTarget);
        }

        return targets;
    }

    private async Task<NotificationTarget?> CreateCustomerNotificationTargetAsync(
        LoanStatusChanged eventData)
    {
        // Get customer details from database
        var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == eventData.ClientId);
        if (client == null)
        {
            throw new InvalidOperationException($"Client {eventData.ClientId} not found");
        }

        // Get application details
        var application = await _dbContext.LoanApplications
            .Include(a => a.Product)
            .FirstOrDefaultAsync(a => a.Id == eventData.ApplicationId);

        // Only notify customer for significant status changes
        if (!IsCustomerNotificationRequired(eventData.PreviousStatus, eventData.NewStatus))
        {
            return null;
        }

        // Build personalization data for customer SMS
        var personalizationData = new
        {
            CustomerName = $"{client.FirstName} {client.LastName}",
            ApplicationRef = eventData.ApplicationId.ToString(),
            PreviousStatus = FormatStatusForCustomer(eventData.PreviousStatus),
            NewStatus = FormatStatusForCustomer(eventData.NewStatus),
            NextSteps = GetNextStepsForCustomer(eventData.NewStatus),
            ProcessingTime = GetProcessingTimeForStatus(eventData.NewStatus),
            Reason = eventData.Reason,
            Branch = "Lusaka Main" // TODO: Get from application branch
        };

        var templateName = GetCustomerTemplateForStatus(eventData.NewStatus);

        return new NotificationTarget
        {
            RecipientId = eventData.ClientId.ToString(),
            RecipientType = "Customer",
            PreferredChannel = "SMS",
            Priority = GetPriorityForStatus(eventData.NewStatus),
            PersonalizationData = personalizationData,
            TemplateName = templateName,
            BranchId = 1 // TODO: Get from application
        };
    }

    private async Task<NotificationTarget?> CreateLoanOfficerNotificationTargetAsync(
        LoanStatusChanged eventData)
    {
        try
        {
            // Get loan application details
            var application = await _dbContext.LoanApplications
                .Include(a => a.Product)
                .FirstOrDefaultAsync(a => a.Id == eventData.ApplicationId);

            if (application == null)
            {
                _logger.LogWarning("Loan application {ApplicationId} not found", eventData.ApplicationId);
                return null;
            }

            // Get client details
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == eventData.ClientId);
            if (client == null)
            {
                _logger.LogWarning("Client {ClientId} not found for application {ApplicationId}",
                    eventData.ClientId, eventData.ApplicationId);
                return null;
            }

            // TODO: Implement logic to determine assigned loan officer
            var assignedOfficerId = "user-officer-default"; // TODO: Real assignment logic

            // Build personalization data for loan officer
            var personalizationData = new
            {
                CustomerName = $"{client.FirstName} {client.LastName}",
                ApplicationRef = eventData.ApplicationId.ToString(),
                Product = application.Product?.Name ?? "Unknown Product",
                PreviousStatus = eventData.PreviousStatus,
                NewStatus = eventData.NewStatus,
                ChangedBy = eventData.ChangedBy ?? "System",
                Reason = eventData.Reason,
                Comments = eventData.Comments,
                UrgentFlag = IsUrgentStatusChange(eventData.NewStatus),
                NextAction = GetNextActionForOfficer(eventData.NewStatus)
            };

            var templateName = GetOfficerTemplateForStatus(eventData.NewStatus);

            return new NotificationTarget
            {
                RecipientId = assignedOfficerId,
                RecipientType = "LoanOfficer",
                PreferredChannel = "InApp",
                Priority = GetPriorityForStatus(eventData.NewStatus),
                PersonalizationData = personalizationData,
                TemplateName = templateName,
                BranchId = 1 // TODO: Determine from application/branch logic
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create loan officer notification target for application {ApplicationId}",
                eventData.ApplicationId);
            return null;
        }
    }

    private async Task<NotificationTarget?> CreateBranchManagerNotificationTargetAsync(
        LoanStatusChanged eventData)
    {
        // Only create notification for high-value approvals
        if (eventData.NewStatus != "Approved")
        {
            return null;
        }

        try
        {
            // Get loan application details
            var application = await _dbContext.LoanApplications
                .Include(a => a.Product)
                .FirstOrDefaultAsync(a => a.Id == eventData.ApplicationId);

            if (application == null)
            {
                _logger.LogWarning("Loan application {ApplicationId} not found for approval notification",
                    eventData.ApplicationId);
                return null;
            }

            // High-value threshold for branch manager notification
            if ((application?.Amount ?? 0) < 200000) // K200,000 threshold
            {
                return null;
            }

            // Get client details
            var client = await _dbContext.Clients.FirstOrDefaultAsync(c => c.Id == eventData.ClientId);

            // TODO: Implement branch manager lookup based on branch
            var branchManagerId = "user-manager-default"; // TODO: Real lookup logic

            // Build personalization data for branch manager
            var personalizationData = new
            {
                CustomerName = client != null ? $"{client.FirstName} {client.LastName}" : "Unknown Customer",
                ApplicationRef = eventData.ApplicationId.ToString(),
                HighValueAmount = $"K {application?.Amount:N2}" ?? "Unknown Amount",
                Product = application.Product?.Name ?? "Unknown Product",
                ApprovedBy = eventData.ChangedBy ?? "System",
                Comments = eventData.Comments,
                RiskAssessment = CalculateRiskLevel(application?.Amount ?? 0),
                NextAction = "Review disbursement workflow and sales team assignment"
            };

            return new NotificationTarget
            {
                RecipientId = branchManagerId,
                RecipientType = "BranchManager",
                PreferredChannel = "InApp",
                Priority = NotificationPriority.High,
                PersonalizationData = personalizationData,
                TemplateName = "high-value-loan-approved-manager",
                BranchId = 1 // TODO: Determine from application/branch logic
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create branch manager notification target for application {ApplicationId}",
                eventData.ApplicationId);
            return null;
        }
    }

    private async Task ProcessNotificationRequestAsync(NotificationRequest request)
    {
        try
        {
            // Generate notification content based on template
            var content = await GenerateNotificationContentAsync(request);

            // Create notification log entry
            var notificationLog = await CreateNotificationLogAsync(request, content);

            // Save to database
            await _notificationRepository.CreateAsync(notificationLog);

            // Attempt channel delivery where possible
            if (string.Equals(request.Channel, "InApp", StringComparison.OrdinalIgnoreCase))
            {
                var inAppReq = new CreateInAppNotificationRequest
                {
                    UserId = request.RecipientId,
                    Title = request.Subject ?? $"{request.TemplateCategory} Update",
                    Message = content,
                    Type = InAppNotificationType.Info,
                    Priority = request.Priority switch
                    {
                        NotificationPriority.Critical => InAppNotificationPriority.Critical,
                        NotificationPriority.High => InAppNotificationPriority.High,
                        NotificationPriority.Low => InAppNotificationPriority.Low,
                        _ => InAppNotificationPriority.Normal
                    },
                    Category = InAppNotificationCategory.LoanApplication,
                    SourceId = request.EventId.ToString(),
                    SourceType = request.TemplateCategory
                };

                var response = await _inAppNotificationService.SendNotificationAsync(inAppReq);
                if (response.Success)
                {
                    await _notificationRepository.UpdateStatusAsync(notificationLog.Id, NotificationStatus.Sent, gatewayResponse: "InApp:OK");
                }
                else
                {
                    await _notificationRepository.UpdateStatusAsync(notificationLog.Id, NotificationStatus.Failed, failureReason: "InApp delivery failed");
                }
            }
            else if (string.Equals(request.Channel, "SMS", StringComparison.OrdinalIgnoreCase))
            {
                // Phone resolution not available in current domain model; mark as queued for external sender
                await _notificationRepository.UpdateStatusAsync(notificationLog.Id, NotificationStatus.Queued, gatewayResponse: "Queued:AwaitingPhoneResolution");
            }

            _logger.LogInformation(
                "Created notification log for event {EventId}, recipient {RecipientId}, channel {Channel}",
                request.EventId, request.RecipientId, request.Channel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to process notification request for event {EventId}, recipient {RecipientId}: {Error}",
                request.EventId, request.RecipientId, ex.Message);
            throw;
        }
    }

    private async Task<string> GenerateNotificationContentAsync(NotificationRequest request)
    {
        // For now, use basic template rendering
        // TODO: Integrate with full template engine when available

        return request.PersonalizationContext switch
        {
            // Customer status change templates
            var data when request.TemplateName == "loan-status-approved-customer" =>
                $"Dear {GetProperty(data, "CustomerName")}, your loan application {GetProperty(data, "ApplicationRef")} has been APPROVED. Please visit our branch within 24 hours for disbursement. IntelliFin MicroFinance.",

            var data when request.TemplateName == "loan-status-rejected-customer" =>
                $"Dear {GetProperty(data, "CustomerName")}, we regret that your loan application {GetProperty(data, "ApplicationRef")} has been declined. For details, please contact your relationship officer. IntelliFin MicroFinance.",

            var data when request.TemplateName == "loan-status-disbursed-customer" =>
                $"Dear {GetProperty(data, "CustomerName")}, your loan of {GetProperty(data, "HighValueAmount")} has been DISBURSED. Funds are available in your account. IntelliFin MicroFinance.",

            var data when request.TemplateName == "loan-status-under-review-customer" =>
                $"Dear {GetProperty(data, "CustomerName")}, your loan application {GetProperty(data, "ApplicationRef")} is UNDER REVIEW. We will contact you within 48 hours with an update. IntelliFin MicroFinance.",

            // Officer status change templates
            var data when request.TemplateName == "loan-status-approved-officer" =>
                $"APPROVAL: Loan application {GetProperty(data, "ApplicationRef")} for {GetProperty(data, "CustomerName")} has been approved. Proceed with disbursement arrangement.",

            var data when request.TemplateName == "loan-status-rejected-officer" =>
                $"REJECTION: Loan application {GetProperty(data, "ApplicationRef")} for {GetProperty(data, "CustomerName")} has been rejected. Reason: {GetProperty(data, "Reason")}.",

            var data when request.TemplateName == "loan-status-disbursed-officer" =>
                $"DISBURSEMENT: Loan application {GetProperty(data, "ApplicationRef")} for {GetProperty(data, "CustomerName")} has been disbursed successfully.",

            var data when request.TemplateName == "loan-status-under-review-officer" =>
                $"REVIEW: Loan application {GetProperty(data, "ApplicationRef")} for {GetProperty(data, "CustomerName")} is now under review. Assigned to you for processing.",

            // Branch manager templates
            var data when request.TemplateName == "high-value-loan-approved-manager" =>
                $"HIGH VALUE APPROVAL: {GetProperty(data, "ApplicationRef")} - {GetProperty(data, "HighValueAmount")} for {GetProperty(data, "CustomerName")}. Action required: Review disbursement and sales team coordination.",

            // Default fallback
            _ => $"Loan status changed for {request.RecipientId}: {GetProperty(request.PersonalizationContext, "PreviousStatus")} -> {GetProperty(request.PersonalizationContext, "NewStatus")}."
        };
    }

    private string GetProperty(object data, string propertyName)
    {
        if (data == null) return string.Empty;

        var property = data.GetType().GetProperty(propertyName);
        return property?.GetValue(data)?.ToString() ?? string.Empty;
    }

    // Status change logic methods
    private bool IsCustomerNotificationRequired(string previousStatus, string newStatus)
    {
        // Define which status changes require customer notifications
        var notificationStatuses = new[] { "Approved", "Rejected", "Disbursed", "Under Review", "Pending" };

        return notificationStatuses.Contains(newStatus) && newStatus != previousStatus;
    }

    private string FormatStatusForCustomer(string status)
    {
        return status switch
        {
            "Approved" => "approved",
            "Rejected" => "declined",
            "Disbursed" => "disbursed",
            "Under Review" => "under review",
            "Pending" => "received and pending",
            _ => status.ToLower().Replace('_', ' ')
        };
    }

    private string GetNextStepsForCustomer(string status)
    {
        return status switch
        {
            "Approved" => "Please visit our branch to complete disbursement within 24 hours",
            "Rejected" => "Contact your relationship officer for feedback and next steps",
            "Disbursed" => "Your loan funds are now available in your linked account",
            "Under Review" => "We will contact you within 48 hours with an update",
            _ => "Contact our branch for more information"
        };
    }

    private string GetProcessingTimeForStatus(string status)
    {
        return status switch
        {
            "Approved" => "24 hours",
            "Rejected" => "Immediate",
            "Disbursed" => "Now available",
            "Under Review" => "48 hours",
            _ => "48 hours"
        };
    }

    private string GetCustomerTemplateForStatus(string status)
    {
        return status switch
        {
            "Approved" => "loan-status-approved-customer",
            "Rejected" => "loan-status-rejected-customer",
            "Disbursed" => "loan-status-disbursed-customer",
            "Under Review" => "loan-status-under-review-customer",
            _ => "loan-status-changed-customer"
        };
    }

    private string GetOfficerTemplateForStatus(string status)
    {
        return status switch
        {
            "Approved" => "loan-status-approved-officer",
            "Rejected" => "loan-status-rejected-officer",
            "Disbursed" => "loan-status-disbursed-officer",
            "Under Review" => "loan-status-under-review-officer",
            _ => "loan-status-changed-officer"
        };
    }

    private string GetNextActionForOfficer(string status)
    {
        return status switch
        {
            "Approved" => "Coordinate with client for disbursement arrangement",
            "Rejected" => "Contact client with feedback and document requirements",
            "Disbursed" => "Update records and prepare client education materials",
            "Under Review" => "Review all documentation and prepare approval/rejection decision",
            _ => "Review application and take appropriate action"
        };
    }

    private bool IsUrgentStatusChange(string status)
    {
        return status switch
        {
            "Approved" => true,
            "Rejected" => true,
            "Disbursed" => true,
            _ => false
        };
    }

    private NotificationPriority GetPriorityForStatus(string status)
    {
        return status switch
        {
            "Approved" => NotificationPriority.High,
            "Rejected" => NotificationPriority.High,
            "Disbursed" => NotificationPriority.Normal,
            "Under Review" => NotificationPriority.Normal,
            _ => NotificationPriority.Normal
        };
    }

    private string CalculateRiskLevel(decimal amount)
    {
        return amount switch
        {
            >= 500000 => "High Risk - Additional approval required",
            >= 200000 and < 500000 => "Medium Risk - Standard procedures",
            _ => "Low Risk - Standard procedures"
        };
    }

    protected override int GetBranchId(LoanStatusChanged eventData)
    {
        // TODO: Implement proper branch determination from event data
        // For now, return default branch
        return 1;
    }
}
