using IntelliFin.Communications.Models;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace IntelliFin.Communications.Services;

public class EmailService : IEmailService
{
    private readonly IEmailTemplateService _templateService;
    private readonly IEmailSuppressionService _suppressionService;
    private readonly IEmailQueue _emailQueue;
    private readonly IEmailScheduler _scheduler;
    private readonly IEnumerable<IEmailGatewayService> _gateways;
    private readonly ILogger<EmailService> _logger;
    private readonly IConfiguration _configuration;

    public EmailService(
        IEmailTemplateService templateService,
        IEmailSuppressionService suppressionService,
        IEmailQueue emailQueue,
        IEmailScheduler scheduler,
        IEnumerable<IEmailGatewayService> gateways,
        ILogger<EmailService> logger,
        IConfiguration configuration)
    {
        _templateService = templateService;
        _suppressionService = suppressionService;
        _emailQueue = emailQueue;
        _scheduler = scheduler;
        _gateways = gateways;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<SendEmailResponse> SendEmailAsync(SendEmailRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing email send request to {To} with subject {Subject}", 
                request.To, request.Subject);

            // Validate email request
            var validationErrors = await ValidateEmailRequestAsync(request, cancellationToken);
            if (validationErrors.Any())
            {
                return new SendEmailResponse
                {
                    Success = false,
                    Message = "Email validation failed",
                    Errors = validationErrors
                };
            }

            // Check suppression list
            if (await _suppressionService.IsSupressedAsync(request.To, cancellationToken))
            {
                _logger.LogWarning("Email to {To} is suppressed, skipping send", request.To);
                return new SendEmailResponse
                {
                    Success = false,
                    Message = "Email address is suppressed",
                    Errors = new List<string> { "Recipient is on suppression list" }
                };
            }

            // Create email message
            var message = await CreateEmailMessageAsync(request, cancellationToken);

            // Handle scheduling
            if (request.ScheduledAt.HasValue && request.ScheduledAt > DateTime.UtcNow)
            {
                await _scheduler.ScheduleEmailAsync(message.Id, request.ScheduledAt.Value, cancellationToken);
                
                return new SendEmailResponse
                {
                    Success = true,
                    MessageId = message.Id,
                    Message = "Email scheduled successfully"
                };
            }

            // Send immediately or queue based on priority
            if (request.Priority == EmailPriority.Critical)
            {
                var result = await SendImmediatelyAsync(message, cancellationToken);
                return new SendEmailResponse
                {
                    Success = result.Success,
                    MessageId = message.Id,
                    ExternalId = result.ExternalId,
                    Message = result.Message,
                    Errors = result.Errors
                };
            }
            else
            {
                await _emailQueue.EnqueueAsync(message, cancellationToken);
                
                return new SendEmailResponse
                {
                    Success = true,
                    MessageId = message.Id,
                    Message = "Email queued for sending"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing email send request");
            return new SendEmailResponse
            {
                Success = false,
                Message = "Internal server error",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<BulkEmailResponse> SendBulkEmailAsync(BulkEmailRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing bulk email send request to {RecipientCount} recipients with template {TemplateId}",
                request.Recipients.Count, request.TemplateId);

            var response = new BulkEmailResponse
            {
                BatchId = Guid.NewGuid().ToString(),
                TotalRecipients = request.Recipients.Count
            };

            // Get template
            var template = await _templateService.GetTemplateAsync(request.TemplateId, cancellationToken);
            if (template == null)
            {
                response.Success = false;
                response.Errors.Add(new BulkEmailError
                {
                    Recipient = "ALL",
                    Error = $"Template {request.TemplateId} not found",
                    Code = "TEMPLATE_NOT_FOUND"
                });
                return response;
            }

            var messages = new List<EmailMessage>();
            var errors = new List<BulkEmailError>();

            // Process recipients in batches
            var batches = request.Recipients.Chunk(request.BatchSize);
            
            foreach (var batch in batches)
            {
                var batchMessages = new List<EmailMessage>();
                
                foreach (var recipient in batch)
                {
                    try
                    {
                        // Check suppression
                        if (await _suppressionService.IsSupressedAsync(recipient.To, cancellationToken))
                        {
                            errors.Add(new BulkEmailError
                            {
                                Recipient = recipient.To,
                                Error = "Email address is suppressed",
                                Code = "SUPPRESSED"
                            });
                            continue;
                        }

                        // Validate email
                        if (!IsValidEmail(recipient.To))
                        {
                            errors.Add(new BulkEmailError
                            {
                                Recipient = recipient.To,
                                Error = "Invalid email address format",
                                Code = "INVALID_EMAIL"
                            });
                            continue;
                        }

                        // Create message
                        var (subject, textContent, htmlContent) = await _templateService.RenderFullTemplateAsync(
                            request.TemplateId, recipient.TemplateParameters, cancellationToken);

                        var message = new EmailMessage
                        {
                            Id = Guid.NewGuid().ToString(),
                            To = recipient.To,
                            Cc = recipient.Cc,
                            Bcc = recipient.Bcc,
                            From = request.From ?? _configuration.GetValue<string>("Email:DefaultFromAddress", "noreply@intellifin.zm"),
                            ReplyTo = request.ReplyTo,
                            Subject = subject,
                            TextContent = textContent,
                            HtmlContent = htmlContent,
                            TemplateId = request.TemplateId,
                            TemplateParameters = recipient.TemplateParameters,
                            Priority = request.Priority,
                            ScheduledAt = request.ScheduledAt,
                            Headers = request.GlobalHeaders,
                            Metadata = request.GlobalMetadata.Concat(recipient.Metadata).ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                        };

                        // Add unsubscribe headers if enabled
                        if (request.EnableUnsubscribe)
                        {
                            var unsubscribeUrl = $"{_configuration.GetValue<string>("Email:BaseUrl")}/unsubscribe/{message.Id}";
                            message.Headers["List-Unsubscribe"] = $"<{unsubscribeUrl}>";
                            message.Headers["List-Unsubscribe-Post"] = "List-Unsubscribe=One-Click";
                        }

                        batchMessages.Add(message);
                        messages.Add(message);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing bulk email recipient {Recipient}", recipient.To);
                        errors.Add(new BulkEmailError
                        {
                            Recipient = recipient.To,
                            Error = ex.Message,
                            Code = "PROCESSING_ERROR"
                        });
                    }
                }

                // Queue batch messages
                if (batchMessages.Any())
                {
                    foreach (var message in batchMessages)
                    {
                        if (request.ScheduledAt.HasValue && request.ScheduledAt > DateTime.UtcNow)
                        {
                            await _scheduler.ScheduleEmailAsync(message.Id, request.ScheduledAt.Value, cancellationToken);
                        }
                        else
                        {
                            await _emailQueue.EnqueueAsync(message, cancellationToken);
                        }
                    }
                }

                // Small delay between batches to avoid overwhelming the system
                if (batches.Count() > 1)
                {
                    await Task.Delay(100, cancellationToken);
                }
            }

            response.AcceptedRecipients = messages.Count;
            response.RejectedRecipients = errors.Count;
            response.MessageIds = messages.Select(m => m.Id).ToList();
            response.Errors = errors;
            response.Success = response.AcceptedRecipients > 0;

            _logger.LogInformation("Bulk email processed: {Accepted} accepted, {Rejected} rejected",
                response.AcceptedRecipients, response.RejectedRecipients);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing bulk email request");
            return new BulkEmailResponse
            {
                BatchId = Guid.NewGuid().ToString(),
                Success = false,
                TotalRecipients = request.Recipients.Count,
                Errors = new List<BulkEmailError>
                {
                    new BulkEmailError
                    {
                        Recipient = "ALL",
                        Error = ex.Message,
                        Code = "BULK_PROCESSING_ERROR"
                    }
                }
            };
        }
    }

    public async Task<EmailDeliveryReport> GetDeliveryReportAsync(string messageId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting delivery report for message {MessageId}", messageId);
            
            // This would typically query a database
            // For now, returning mock data
            await Task.Delay(50, cancellationToken);
            
            return new EmailDeliveryReport
            {
                MessageId = messageId,
                ExternalId = $"ext-{Random.Shared.Next(1000000, 9999999)}",
                To = "user@example.com",
                Subject = "Test Email",
                Status = EmailStatus.Delivered,
                StatusUpdatedAt = DateTime.UtcNow.AddMinutes(-30),
                DeliveredAt = DateTime.UtcNow.AddMinutes(-30),
                Gateway = "SendGrid",
                Events = new List<EmailEvent>
                {
                    new EmailEvent
                    {
                        Id = Guid.NewGuid().ToString(),
                        MessageId = messageId,
                        EventType = EmailEventType.Sent,
                        Timestamp = DateTime.UtcNow.AddMinutes(-31)
                    },
                    new EmailEvent
                    {
                        Id = Guid.NewGuid().ToString(),
                        MessageId = messageId,
                        EventType = EmailEventType.Delivered,
                        Timestamp = DateTime.UtcNow.AddMinutes(-30)
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting delivery report for message {MessageId}", messageId);
            throw;
        }
    }

    public async Task<IEnumerable<EmailDeliveryReport>> GetDeliveryReportsAsync(IEnumerable<string> messageIds, CancellationToken cancellationToken = default)
    {
        try
        {
            var reports = new List<EmailDeliveryReport>();
            
            foreach (var messageId in messageIds.Take(100)) // Limit to prevent abuse
            {
                var report = await GetDeliveryReportAsync(messageId, cancellationToken);
                reports.Add(report);
            }
            
            return reports;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting delivery reports");
            throw;
        }
    }

    public async Task<EmailAnalytics> GetAnalyticsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting email analytics from {StartDate} to {EndDate}", startDate, endDate);
            
            // This would typically query analytics from database
            await Task.Delay(100, cancellationToken);
            
            var totalSent = Random.Shared.Next(1000, 5000);
            var totalDelivered = (int)(totalSent * 0.95);
            var totalOpened = (int)(totalDelivered * 0.25);
            var totalClicked = (int)(totalOpened * 0.15);
            var totalBounced = totalSent - totalDelivered;
            
            return new EmailAnalytics
            {
                StartDate = startDate,
                EndDate = endDate,
                TotalSent = totalSent,
                TotalDelivered = totalDelivered,
                TotalOpened = totalOpened,
                TotalClicked = totalClicked,
                TotalBounced = totalBounced,
                TotalUnsubscribed = Random.Shared.Next(5, 25),
                DeliveryRate = (double)totalDelivered / totalSent * 100,
                OpenRate = (double)totalOpened / totalDelivered * 100,
                ClickRate = (double)totalClicked / totalOpened * 100,
                BounceRate = (double)totalBounced / totalSent * 100,
                UnsubscribeRate = Random.Shared.NextDouble() * 2,
                StatusBreakdown = new Dictionary<EmailStatus, int>
                {
                    [EmailStatus.Delivered] = totalDelivered,
                    [EmailStatus.Opened] = totalOpened,
                    [EmailStatus.Clicked] = totalClicked,
                    [EmailStatus.Bounced] = totalBounced
                },
                GatewayBreakdown = new Dictionary<string, int>
                {
                    ["SendGrid"] = totalSent / 2,
                    ["SMTP"] = totalSent / 2
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting email analytics");
            throw;
        }
    }

    public async Task<bool> ProcessWebhookAsync(string gateway, string payload, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing webhook from gateway {Gateway}", gateway);
            
            var gatewayService = _gateways.FirstOrDefault(g => g.GatewayName.Equals(gateway, StringComparison.OrdinalIgnoreCase));
            if (gatewayService == null)
            {
                _logger.LogWarning("Unknown email gateway {Gateway}", gateway);
                return false;
            }
            
            return await gatewayService.ProcessWebhookAsync(payload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook from gateway {Gateway}", gateway);
            return false;
        }
    }

    public async Task ProcessUnsubscribeAsync(UnsubscribeRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing unsubscribe request for {EmailAddress}", request.EmailAddress);
            
            await _suppressionService.SuppressAsync(
                request.EmailAddress,
                SuppressionReason.Unsubscribe,
                null,
                request.Reason,
                cancellationToken);
                
            _logger.LogInformation("Successfully processed unsubscribe for {EmailAddress}", request.EmailAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing unsubscribe for {EmailAddress}", request.EmailAddress);
            throw;
        }
    }

    public async Task<IEnumerable<EmailMessage>> GetEmailHistoryAsync(string emailAddress, int pageSize = 50, int pageNumber = 1, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting email history for {EmailAddress}", emailAddress);
            
            // This would typically query database
            await Task.Delay(100, cancellationToken);
            
            // Return mock data
            return new List<EmailMessage>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting email history for {EmailAddress}", emailAddress);
            throw;
        }
    }

    #region Private Methods

    private async Task<List<string>> ValidateEmailRequestAsync(SendEmailRequest request, CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        // Validate email addresses
        if (!IsValidEmail(request.To))
            errors.Add("Invalid 'To' email address");

        if (!string.IsNullOrEmpty(request.Cc) && !IsValidEmailList(request.Cc))
            errors.Add("Invalid 'Cc' email address(es)");

        if (!string.IsNullOrEmpty(request.Bcc) && !IsValidEmailList(request.Bcc))
            errors.Add("Invalid 'Bcc' email address(es)");

        // Validate content
        if (string.IsNullOrEmpty(request.TemplateId) && 
            string.IsNullOrEmpty(request.TextContent) && 
            string.IsNullOrEmpty(request.HtmlContent))
        {
            errors.Add("Either TemplateId or content (TextContent/HtmlContent) must be provided");
        }

        // Validate template if provided
        if (!string.IsNullOrEmpty(request.TemplateId))
        {
            var template = await _templateService.GetTemplateAsync(request.TemplateId, cancellationToken);
            if (template == null)
                errors.Add($"Template {request.TemplateId} not found");
            else if (!template.IsActive)
                errors.Add($"Template {request.TemplateId} is not active");
        }

        return errors;
    }

    private async Task<EmailMessage> CreateEmailMessageAsync(SendEmailRequest request, CancellationToken cancellationToken)
    {
        var message = new EmailMessage
        {
            Id = Guid.NewGuid().ToString(),
            To = request.To,
            Cc = request.Cc,
            Bcc = request.Bcc,
            From = request.From ?? _configuration.GetValue<string>("Email:DefaultFromAddress", "noreply@intellifin.zm"),
            ReplyTo = request.ReplyTo,
            Subject = request.Subject,
            TextContent = request.TextContent,
            HtmlContent = request.HtmlContent,
            TemplateId = request.TemplateId,
            TemplateParameters = request.TemplateParameters,
            Attachments = request.Attachments,
            Priority = request.Priority,
            ScheduledAt = request.ScheduledAt,
            Headers = request.Headers,
            Metadata = request.Metadata
        };

        // Render template if provided
        if (!string.IsNullOrEmpty(request.TemplateId))
        {
            var (subject, textContent, htmlContent) = await _templateService.RenderFullTemplateAsync(
                request.TemplateId, request.TemplateParameters, cancellationToken);
            
            message.Subject = subject;
            message.TextContent = textContent;
            message.HtmlContent = htmlContent;
        }

        return message;
    }

    private async Task<SendEmailResponse> SendImmediatelyAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var gateway = GetBestAvailableGateway();
        if (gateway == null)
        {
            return new SendEmailResponse
            {
                Success = false,
                MessageId = message.Id,
                Message = "No available email gateway",
                Errors = new List<string> { "All email gateways are unavailable" }
            };
        }

        return await gateway.SendAsync(message, cancellationToken);
    }

    private IEmailGatewayService? GetBestAvailableGateway()
    {
        return _gateways.Where(g => g.IsEnabled).OrderBy(g => Random.Shared.Next()).FirstOrDefault();
    }

    private bool IsValidEmail(string email)
    {
        if (string.IsNullOrEmpty(email))
            return false;

        var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);
        return emailRegex.IsMatch(email);
    }

    private bool IsValidEmailList(string emailList)
    {
        if (string.IsNullOrEmpty(emailList))
            return false;

        var emails = emailList.Split(',', ';').Select(e => e.Trim());
        return emails.All(IsValidEmail);
    }

    #endregion
}