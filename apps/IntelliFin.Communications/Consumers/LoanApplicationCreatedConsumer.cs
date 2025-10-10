using System.Globalization;
using IntelliFin.Communications.Models;
using IntelliFin.Communications.Services;
using IntelliFin.Shared.DomainModels.Data;
using IntelliFin.Shared.DomainModels.Entities;
using IntelliFin.Shared.DomainModels.Repositories;
using IntelliFin.Shared.Infrastructure.Messaging.Contracts;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IntelliFin.Communications.Consumers;

public class LoanApplicationCreatedConsumer : BaseNotificationConsumer<LoanApplicationCreated>
{
    private const decimal HighValueThreshold = 100_000m;

    private readonly LmsDbContext _dbContext;
    private readonly ISmsService _smsService;
    private readonly IInAppNotificationService _inAppNotificationService;

    public LoanApplicationCreatedConsumer(
        INotificationRepository notificationRepository,
        LmsDbContext dbContext,
        ISmsService smsService,
        IInAppNotificationService inAppNotificationService,
        ILogger<LoanApplicationCreatedConsumer> logger)
        : base(notificationRepository, logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _smsService = smsService ?? throw new ArgumentNullException(nameof(smsService));
        _inAppNotificationService = inAppNotificationService ?? throw new ArgumentNullException(nameof(inAppNotificationService));
    }

    protected override async Task ProcessEventAsync(
        LoanApplicationCreated eventData,
        ConsumeContext<LoanApplicationCreated> context)
    {
        var cancellationToken = context.CancellationToken;
        var branchId = GetBranchId(eventData);

        _logger.LogInformation(
            "Processing LoanApplicationCreated event {EventId} for application {ApplicationId} (branch {BranchId})",
            eventData.EventId,
            eventData.ApplicationId,
            branchId);

        await ProcessCustomerNotificationAsync(eventData, branchId, cancellationToken);
        await ProcessLoanOfficerNotificationAsync(eventData, branchId, cancellationToken);

        if (eventData.RequestedAmount >= HighValueThreshold)
        {
            await ProcessBranchManagerNotificationAsync(eventData, branchId, cancellationToken);
        }

        _logger.LogInformation(
            "LoanApplicationCreated event {EventId} processed successfully",
            eventData.EventId);
    }

    private async Task ProcessCustomerNotificationAsync(
        LoanApplicationCreated eventData,
        int branchId,
        CancellationToken cancellationToken)
    {
        var message = BuildCustomerMessage(eventData);
        var request = new NotificationRequest
        {
            EventId = eventData.EventId,
            RecipientId = eventData.ClientId.ToString(),
            RecipientType = "Customer",
            Channel = "SMS",
            TemplateCategory = "LoanOrigination",
            TemplateName = "loan-application-created-customer",
            Subject = "Loan application received",
            Priority = NotificationPriority.Normal,
            PersonalizationContext = new Dictionary<string, object>
            {
                ["CustomerName"] = eventData.CustomerName,
                ["ApplicationId"] = eventData.ApplicationId,
                ["RequestedAmount"] = eventData.RequestedAmount,
                ["ProductType"] = eventData.ProductType
            },
            BranchId = branchId
        };

        var log = await CreateNotificationLogAsync(request, message);

        if (string.IsNullOrWhiteSpace(eventData.CustomerPhone))
        {
            log.Status = NotificationStatus.Failed;
            log.FailureReason = "Customer phone number not supplied";
            await _notificationRepository.CreateAsync(log, cancellationToken);
            _logger.LogWarning(
                "Loan application {ApplicationId} could not send SMS notification because the phone number is missing",
                eventData.ApplicationId);
            return;
        }

        log = await _notificationRepository.CreateAsync(log, cancellationToken);

        var smsResponse = await _smsService.SendSmsAsync(new SmsNotificationRequest
        {
            PhoneNumber = eventData.CustomerPhone,
            Message = message,
            NotificationType = SmsNotificationType.LoanApplicationStatus,
            ClientId = eventData.ClientId.ToString(),
            LoanId = eventData.ApplicationId.ToString(),
            TemplateData = new Dictionary<string, object>
            {
                ["CustomerName"] = eventData.CustomerName,
                ["ApplicationRef"] = eventData.ApplicationId.ToString(),
                ["RequestedAmount"] = eventData.RequestedAmount
            }
        }, cancellationToken);

        var status = MapSmsStatus(smsResponse.Status);
        var failureReason = status == NotificationStatus.Failed ? smsResponse.ErrorMessage : null;
        var gatewayResponse = smsResponse.ProviderMessageId ?? smsResponse.NotificationId;

        await _notificationRepository.UpdateStatusAsync(
            log.Id,
            status,
            gatewayResponse,
            failureReason,
            cancellationToken);

        _logger.LogInformation(
            "Customer SMS notification processed for application {ApplicationId} with status {Status}",
            eventData.ApplicationId,
            status);
    }

    private async Task ProcessLoanOfficerNotificationAsync(
        LoanApplicationCreated eventData,
        int branchId,
        CancellationToken cancellationToken)
    {
        var officerId = await ResolveLoanOfficerAsync(branchId, cancellationToken);
        if (string.IsNullOrWhiteSpace(officerId))
        {
            _logger.LogWarning(
                "No active loan officer found for branch {BranchId}. Officer notification skipped for application {ApplicationId}",
                branchId,
                eventData.ApplicationId);
            return;
        }

        var message = BuildOfficerMessage(eventData);
        var request = new NotificationRequest
        {
            EventId = eventData.EventId,
            RecipientId = officerId,
            RecipientType = "LoanOfficer",
            Channel = "InApp",
            TemplateCategory = "LoanOrigination",
            TemplateName = "loan-application-created-officer",
            Subject = "New loan application submitted",
            Priority = eventData.RequestedAmount >= HighValueThreshold
                ? NotificationPriority.High
                : NotificationPriority.Normal,
            PersonalizationContext = new Dictionary<string, object>
            {
                ["CustomerName"] = eventData.CustomerName,
                ["ApplicationId"] = eventData.ApplicationId,
                ["RequestedAmount"] = eventData.RequestedAmount,
                ["ProductType"] = eventData.ProductType,
                ["BranchId"] = branchId
            },
            BranchId = branchId
        };

        var log = await _notificationRepository.CreateAsync(
            await CreateNotificationLogAsync(request, message),
            cancellationToken);

        var response = await _inAppNotificationService.SendNotificationAsync(new CreateInAppNotificationRequest
        {
            UserId = officerId,
            Title = "New loan application",
            Message = message,
            Category = InAppNotificationCategory.LoanApplication,
            Priority = request.Priority switch
            {
                NotificationPriority.High => InAppNotificationPriority.High,
                NotificationPriority.Critical => InAppNotificationPriority.Critical,
                NotificationPriority.Low => InAppNotificationPriority.Low,
                _ => InAppNotificationPriority.Normal
            },
            SourceId = eventData.ApplicationId.ToString(),
            SourceType = "LoanApplication",
            Metadata = new Dictionary<string, string>
            {
                ["eventId"] = eventData.EventId.ToString(),
                ["branchId"] = branchId.ToString(CultureInfo.InvariantCulture)
            }
        }, cancellationToken);

        await _notificationRepository.UpdateStatusAsync(
            log.Id,
            response.Success ? NotificationStatus.Sent : NotificationStatus.Failed,
            response.NotificationId,
            response.ErrorMessage,
            cancellationToken);
    }

    private async Task ProcessBranchManagerNotificationAsync(
        LoanApplicationCreated eventData,
        int branchId,
        CancellationToken cancellationToken)
    {
        var managerId = await ResolveBranchManagerAsync(branchId, cancellationToken);
        if (string.IsNullOrWhiteSpace(managerId))
        {
            _logger.LogWarning(
                "No branch manager found for branch {BranchId}. High value notification queued for audit only",
                branchId);
            return;
        }

        var message = BuildBranchManagerMessage(eventData, branchId);
        var request = new NotificationRequest
        {
            EventId = eventData.EventId,
            RecipientId = managerId,
            RecipientType = "BranchManager",
            Channel = "InApp",
            TemplateCategory = "LoanOrigination",
            TemplateName = "loan-application-created-branch-manager",
            Subject = "High value loan application",
            Priority = NotificationPriority.High,
            PersonalizationContext = new Dictionary<string, object>
            {
                ["CustomerName"] = eventData.CustomerName,
                ["ApplicationId"] = eventData.ApplicationId,
                ["RequestedAmount"] = eventData.RequestedAmount,
                ["BranchId"] = branchId
            },
            BranchId = branchId
        };

        var log = await _notificationRepository.CreateAsync(
            await CreateNotificationLogAsync(request, message),
            cancellationToken);

        var response = await _inAppNotificationService.SendNotificationAsync(new CreateInAppNotificationRequest
        {
            UserId = managerId,
            Title = "High value loan application",
            Message = message,
            Category = InAppNotificationCategory.LoanApplication,
            Priority = InAppNotificationPriority.Critical,
            SourceId = eventData.ApplicationId.ToString(),
            SourceType = "LoanApplication",
            Metadata = new Dictionary<string, string>
            {
                ["eventId"] = eventData.EventId.ToString(),
                ["branchId"] = branchId.ToString(CultureInfo.InvariantCulture)
            }
        }, cancellationToken);

        await _notificationRepository.UpdateStatusAsync(
            log.Id,
            response.Success ? NotificationStatus.Sent : NotificationStatus.Failed,
            response.NotificationId,
            response.ErrorMessage,
            cancellationToken);
    }

    private async Task<string?> ResolveLoanOfficerAsync(int branchId, CancellationToken cancellationToken)
    {
        var branchFilter = branchId > 0 ? branchId.ToString(CultureInfo.InvariantCulture) : null;

        var officer = await _dbContext.UserRoles
            .Include(r => r.Role)
            .AsNoTracking()
            .Where(r => r.IsActive && (branchFilter == null || r.BranchId == null || r.BranchId == branchFilter))
            .Where(r => r.Role.Name == "LoanOfficer")
            .OrderBy(r => r.AssignedAt)
            .Select(r => r.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        return officer ?? (branchId > 0 ? $"loan-officer-{branchId}" : null);
    }

    private async Task<string?> ResolveBranchManagerAsync(int branchId, CancellationToken cancellationToken)
    {
        if (branchId <= 0)
        {
            return "branch-manager-default";
        }

        var branchKey = branchId.ToString(CultureInfo.InvariantCulture);

        var manager = await _dbContext.UserRoles
            .Include(r => r.Role)
            .AsNoTracking()
            .Where(r => r.IsActive && r.BranchId == branchKey && r.Role.Name == "BranchManager")
            .OrderBy(r => r.AssignedAt)
            .Select(r => r.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        return manager ?? $"branch-manager-{branchId}";
    }

    private static string BuildCustomerMessage(LoanApplicationCreated eventData)
    {
        var amount = eventData.RequestedAmount.ToString("N2", CultureInfo.InvariantCulture);
        return string.Format(
            CultureInfo.InvariantCulture,
            "Dear {0}, we received your loan application {1} for K{2}. Our team will contact you within 48 hours.",
            string.IsNullOrWhiteSpace(eventData.CustomerName) ? "Customer" : eventData.CustomerName,
            eventData.ApplicationId,
            amount);
    }

    private static string BuildOfficerMessage(LoanApplicationCreated eventData)
    {
        var amount = eventData.RequestedAmount.ToString("N2", CultureInfo.InvariantCulture);
        return string.Format(
            CultureInfo.InvariantCulture,
            "New loan application {0} from {1} for K{2}. Review details and contact the customer.",
            eventData.ApplicationId,
            string.IsNullOrWhiteSpace(eventData.CustomerName) ? "customer" : eventData.CustomerName,
            amount);
    }

    private static string BuildBranchManagerMessage(LoanApplicationCreated eventData, int branchId)
    {
        var amount = eventData.RequestedAmount.ToString("N2", CultureInfo.InvariantCulture);
        return string.Format(
            CultureInfo.InvariantCulture,
            "Branch {0}: High value loan application {1} from {2} for K{3}. Please review for approval.",
            branchId,
            eventData.ApplicationId,
            string.IsNullOrWhiteSpace(eventData.CustomerName) ? "customer" : eventData.CustomerName,
            amount);
    }

    private static NotificationStatus MapSmsStatus(SmsDeliveryStatus status)
    {
        return status switch
        {
            SmsDeliveryStatus.Sent or SmsDeliveryStatus.Pending => NotificationStatus.Sent,
            SmsDeliveryStatus.Delivered => NotificationStatus.Delivered,
            SmsDeliveryStatus.Retry => NotificationStatus.Queued,
            SmsDeliveryStatus.Failed or SmsDeliveryStatus.OptedOut => NotificationStatus.Failed,
            _ => NotificationStatus.Pending
        };
    }

    protected override int GetBranchId(LoanApplicationCreated eventData)
    {
        return eventData.BranchId > 0 ? eventData.BranchId : base.GetBranchId(eventData);
    }
}
