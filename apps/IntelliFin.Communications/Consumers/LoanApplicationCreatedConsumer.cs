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

public class LoanApplicationCreatedConsumer : BaseNotificationConsumer<LoanApplicationCreated>
{
private readonly LmsDbContext _dbContext;
   private readonly IInAppNotificationService _inAppNotificationService;

public LoanApplicationCreatedConsumer(
INotificationRepository notificationRepository,
LmsDbContext dbContext,
IInAppNotificationService inAppNotificationService,
    ILogger<LoanApplicationCreatedConsumer> logger)
: base(notificationRepository, logger)
{
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _inAppNotificationService = inAppNotificationService ?? throw new ArgumentNullException(nameof(inAppNotificationService));
    }

    protected override async Task ProcessEventAsync(
        LoanApplicationCreated eventData,
        ConsumeContext<LoanApplicationCreated> context)
    {
        _logger.LogInformation(
            "Processing loan application created event {EventId} for application {ApplicationId}, client {ClientId}",
            eventData.EventId, eventData.ApplicationId, eventData.ClientId);

        // Build notification targets for different recipients
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
            "Successfully processed {Count} notifications for loan application {ApplicationId}",
            notificationRequests.Count, eventData.ApplicationId);
    }

    private async Task<List<NotificationTarget>> BuildNotificationTargetsAsync(
        LoanApplicationCreated eventData)
    {
        var targets = new List<NotificationTarget>();

        // 1. SMS notification to customer
        var customerTarget = await CreateCustomerNotificationTargetAsync(eventData);
        targets.Add(customerTarget);

        // 2. In-app notification to assigned loan officer
        var officerTarget = await CreateLoanOfficerNotificationTargetAsync(eventData);
        if (officerTarget != null)
        {
            targets.Add(officerTarget);
        }

        // 3. High-value loan notification to branch manager (if applicable)
        var managerTarget = await CreateBranchManagerNotificationTargetAsync(eventData);
        if (managerTarget != null)
        {
            targets.Add(managerTarget);
        }

        return targets;
    }

    private async Task<NotificationTarget> CreateCustomerNotificationTargetAsync(
        LoanApplicationCreated eventData)
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

        // Build personalization data for customer SMS
        var personalizationData = new
        {
            CustomerName = $"{client.FirstName} {client.LastName}",
            ApplicationRef = eventData.ApplicationId.ToString(),
            Amount = $"K {eventData.Amount:N2}",
            NextSteps = "Please visit your branch to complete documentation",
            ProcessingTime = "48 hours",
            ProductName = application?.Product?.Name ?? "Loan",
            Branch = "Lusaka Main" // TODO: Get from application branch
        };

        return new NotificationTarget
        {
            RecipientId = eventData.ClientId.ToString(),
            RecipientType = "Customer",
            PreferredChannel = "SMS",
            Priority = NotificationPriority.Normal,
            PersonalizationData = personalizationData,
            TemplateName = "loan-application-confirmation-customer",
            BranchId = 1 // TODO: Get from application
        };
    }

    private async Task<NotificationTarget?> CreateLoanOfficerNotificationTargetAsync(
        LoanApplicationCreated eventData)
    {
        try
        {
            // Get loan application with product details
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
            // For now, assign to default officer
            var assignedOfficerId = "user-officer-default"; // TODO: Real assignment logic

            // Build personalization data for loan officer
            var personalizationData = new
            {
                CustomerName = $"{client.FirstName} {client.LastName}",
                ApplicationRef = eventData.ApplicationId.ToString(),
                Product = application.Product?.Name ?? "Unknown Product",
                Amount = $"K {eventData.Amount:N2}",
                UrgentFlag = eventData.Amount > 100000, // Highlight high-value applications
                NextAction = "Review application and contact customer",
                DueDate = DateTime.Today.AddDays(2).ToString("dd MMM yyyy")
            };

            var priority = eventData.Amount > 100000
                ? NotificationPriority.High
                : NotificationPriority.Normal;

            return new NotificationTarget
            {
                RecipientId = assignedOfficerId,
                RecipientType = "LoanOfficer",
                PreferredChannel = "InApp",
                Priority = priority,
                PersonalizationData = personalizationData,
                TemplateName = "loan-application-assigned-officer",
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
        LoanApplicationCreated eventData)
    {
        // Only create notification for high-value loans (K100,000+)
        if (eventData.Amount < 100000)
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
                _logger.LogWarning("Loan application {ApplicationId} not found for high-value notification",
                    eventData.ApplicationId);
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
                HighValueAmount = $"K {eventData.Amount:N2}",
                Product = application.Product?.Name ?? "Unknown Product",
                RiskLevel = eventData.Amount > 500000 ? "High Risk" : "Medium Risk",
                RequiresApproval = eventData.Amount > 500000,
                ApprovalThreshold = "K 500,000.00",
                AssignedOfficer = "Pending Assignment" // TODO: Add officer assignment
            };

            return new NotificationTarget
            {
                RecipientId = branchManagerId,
                RecipientType = "BranchManager",
                PreferredChannel = "InApp",
                Priority = NotificationPriority.High,
                PersonalizationData = personalizationData,
                TemplateName = "high-value-loan-application-manager",
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
            // Customer SMS template
            var data when request.TemplateName == "loan-application-confirmation-customer" =>
                $"Dear {GetProperty(data, "CustomerName")}, your loan application {GetProperty(data, "ApplicationRef")} for {GetProperty(data, "Amount")} has been received. Processing time: {GetProperty(data, "ProcessingTime")}. {GetProperty(data, "NextSteps")}. IntelliFin MicroFinance.",

            // Officer InApp template
            var data when request.TemplateName == "loan-application-assigned-officer" =>
                $"New loan application assigned: {GetProperty(data, "ApplicationRef")} for {GetProperty(data, "CustomerName")}. Amount: {GetProperty(data, "Amount")}. Product: {GetProperty(data, "Product")}. {GetProperty(data, "NextAction")}.",

            // Branch Manager InApp template
            var data when request.TemplateName == "high-value-loan-application-manager" =>
                $"HIGH VALUE: Loan application {GetProperty(data, "ApplicationRef")} for {GetProperty(data, "HighValueAmount")}. Requires review. {(GetProperty(data, "RequiresApproval") == "True" ? "Approval required for amounts above threshold." : "")}",

            // Default fallback
            _ => $"Loan application notification created for {request.RecipientId}."
        };
    }

    private string GetProperty(object data, string propertyName)
    {
        if (data == null) return string.Empty;

        var property = data.GetType().GetProperty(propertyName);
        return property?.GetValue(data)?.ToString() ?? string.Empty;
    }

    protected override int GetBranchId(LoanApplicationCreated eventData)
    {
        // TODO: Implement proper branch determination from event data
        // For now, return default branch
        return 1;
    }
}
