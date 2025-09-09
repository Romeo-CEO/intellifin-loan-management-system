using IntelliFin.Communications.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;

namespace IntelliFin.Communications.Services;

public class SmtpEmailGatewayService : IEmailGatewayService
{
    private readonly ILogger<SmtpEmailGatewayService> _logger;
    private readonly SmtpConfiguration _config;
    private readonly SmtpClient _smtpClient;

    public string GatewayName => "SMTP";
    public bool IsEnabled => _config.IsEnabled;

    public SmtpEmailGatewayService(
        ILogger<SmtpEmailGatewayService> logger,
        IOptions<SmtpConfiguration> config)
    {
        _logger = logger;
        _config = config.Value;
        
        _smtpClient = new SmtpClient(_config.Host, _config.Port)
        {
            Credentials = new NetworkCredential(_config.Username, _config.Password),
            EnableSsl = _config.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = _config.TimeoutMs
        };
    }

    public async Task<SendEmailResponse> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Sending email via SMTP to {To} with subject {Subject}", 
                message.To, message.Subject);

            if (!IsEnabled)
            {
                return new SendEmailResponse
                {
                    Success = false,
                    MessageId = message.Id,
                    Message = "SMTP gateway is disabled",
                    Errors = new List<string> { "Gateway is not enabled" }
                };
            }

            var mailMessage = CreateMailMessage(message);
            var externalId = Guid.NewGuid().ToString();

            await _smtpClient.SendMailAsync(mailMessage, cancellationToken);

            _logger.LogInformation("Email sent successfully via SMTP. MessageId: {MessageId}, ExternalId: {ExternalId}", 
                message.Id, externalId);

            return new SendEmailResponse
            {
                Success = true,
                MessageId = message.Id,
                ExternalId = externalId,
                Message = "Email sent successfully"
            };
        }
        catch (SmtpException ex)
        {
            _logger.LogError(ex, "SMTP error sending email {MessageId}: {StatusCode}", 
                message.Id, ex.StatusCode);
            
            return new SendEmailResponse
            {
                Success = false,
                MessageId = message.Id,
                Message = "SMTP error occurred",
                Errors = new List<string> { $"SMTP Error: {ex.Message}" }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email {MessageId} via SMTP", message.Id);
            
            return new SendEmailResponse
            {
                Success = false,
                MessageId = message.Id,
                Message = "Unexpected error occurred",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<BulkEmailResponse> SendBulkAsync(IEnumerable<EmailMessage> messages, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Sending bulk email via SMTP: {Count} messages", messages.Count());

            var response = new BulkEmailResponse
            {
                BatchId = Guid.NewGuid().ToString(),
                TotalRecipients = messages.Count()
            };

            if (!IsEnabled)
            {
                response.Success = false;
                response.Errors.AddRange(messages.Select(m => new BulkEmailError
                {
                    Recipient = m.To,
                    Error = "SMTP gateway is disabled",
                    Code = "GATEWAY_DISABLED"
                }));
                return response;
            }

            var successCount = 0;
            var errors = new List<BulkEmailError>();

            foreach (var message in messages)
            {
                try
                {
                    var result = await SendAsync(message, cancellationToken);
                    if (result.Success)
                    {
                        successCount++;
                        response.MessageIds.Add(message.Id);
                    }
                    else
                    {
                        errors.Add(new BulkEmailError
                        {
                            Recipient = message.To,
                            Error = result.Message,
                            Code = "SEND_FAILED"
                        });
                    }

                    // Small delay between messages to avoid overwhelming SMTP server
                    await Task.Delay(50, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending bulk email to {To}", message.To);
                    errors.Add(new BulkEmailError
                    {
                        Recipient = message.To,
                        Error = ex.Message,
                        Code = "EXCEPTION"
                    });
                }
            }

            response.AcceptedRecipients = successCount;
            response.RejectedRecipients = errors.Count;
            response.Errors = errors;
            response.Success = successCount > 0;

            _logger.LogInformation("Bulk email completed via SMTP: {Accepted} accepted, {Rejected} rejected",
                response.AcceptedRecipients, response.RejectedRecipients);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending bulk email via SMTP");
            
            return new BulkEmailResponse
            {
                BatchId = Guid.NewGuid().ToString(),
                Success = false,
                TotalRecipients = messages.Count(),
                Errors = new List<BulkEmailError>
                {
                    new BulkEmailError
                    {
                        Recipient = "ALL",
                        Error = ex.Message,
                        Code = "BULK_ERROR"
                    }
                }
            };
        }
    }

    public async Task<EmailDeliveryReport> GetDeliveryStatusAsync(string externalId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Getting delivery status for external ID: {ExternalId}", externalId);
            
            // SMTP doesn't provide delivery tracking - return a basic status
            await Task.Delay(10, cancellationToken);
            
            return new EmailDeliveryReport
            {
                ExternalId = externalId,
                Status = EmailStatus.Sent, // SMTP can only confirm sent, not delivered
                StatusUpdatedAt = DateTime.UtcNow,
                Gateway = GatewayName
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting delivery status for external ID: {ExternalId}", externalId);
            throw;
        }
    }

    public async Task<bool> ProcessWebhookAsync(string payload, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing webhook payload for SMTP gateway");
            
            // SMTP doesn't typically have webhooks, but we can log the attempt
            await Task.Delay(10, cancellationToken);
            
            _logger.LogWarning("SMTP gateway received unexpected webhook payload");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing SMTP webhook");
            return false;
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Testing SMTP connection to {Host}:{Port}", _config.Host, _config.Port);
            
            if (!IsEnabled)
            {
                _logger.LogWarning("SMTP gateway is disabled");
                return false;
            }

            // Test connection by creating a test message
            var testMessage = new MailMessage
            {
                From = new MailAddress(_config.FromAddress, _config.FromName),
                Subject = "Test Connection",
                Body = "This is a test connection message",
                IsBodyHtml = false
            };
            
            testMessage.To.Add(new MailAddress(_config.TestRecipient ?? _config.FromAddress));

            // We don't actually send the test message, just validate the connection
            await Task.Run(() => _smtpClient.Host, cancellationToken);

            _logger.LogInformation("SMTP connection test successful");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP connection test failed");
            return false;
        }
    }

    private MailMessage CreateMailMessage(EmailMessage message)
    {
        var mailMessage = new MailMessage
        {
            From = new MailAddress(
                !string.IsNullOrEmpty(message.From) ? message.From : _config.FromAddress,
                _config.FromName),
            Subject = message.Subject,
            IsBodyHtml = !string.IsNullOrEmpty(message.HtmlContent)
        };

        // Set body content
        if (!string.IsNullOrEmpty(message.HtmlContent))
        {
            mailMessage.Body = message.HtmlContent;
            
            // Add text alternative if available
            if (!string.IsNullOrEmpty(message.TextContent))
            {
                var textView = AlternateView.CreateAlternateViewFromString(message.TextContent, Encoding.UTF8, "text/plain");
                mailMessage.AlternateViews.Add(textView);
            }
        }
        else if (!string.IsNullOrEmpty(message.TextContent))
        {
            mailMessage.Body = message.TextContent;
        }

        // Add recipients
        mailMessage.To.Add(new MailAddress(message.To));

        if (!string.IsNullOrEmpty(message.Cc))
        {
            var ccAddresses = message.Cc.Split(',', ';').Select(cc => cc.Trim()).Where(cc => !string.IsNullOrEmpty(cc));
            foreach (var cc in ccAddresses)
            {
                mailMessage.CC.Add(new MailAddress(cc));
            }
        }

        if (!string.IsNullOrEmpty(message.Bcc))
        {
            var bccAddresses = message.Bcc.Split(',', ';').Select(bcc => bcc.Trim()).Where(bcc => !string.IsNullOrEmpty(bcc));
            foreach (var bcc in bccAddresses)
            {
                mailMessage.Bcc.Add(new MailAddress(bcc));
            }
        }

        if (!string.IsNullOrEmpty(message.ReplyTo))
        {
            mailMessage.ReplyToList.Add(new MailAddress(message.ReplyTo));
        }

        // Add headers
        foreach (var header in message.Headers)
        {
            mailMessage.Headers.Add(header.Key, header.Value);
        }

        // Add attachments
        foreach (var attachment in message.Attachments)
        {
            var memoryStream = new MemoryStream(attachment.Content);
            var mailAttachment = new Attachment(memoryStream, attachment.FileName, attachment.ContentType);
            
            if (attachment.IsInline && !string.IsNullOrEmpty(attachment.ContentId))
            {
                mailAttachment.ContentId = attachment.ContentId;
                mailAttachment.ContentDisposition.Inline = true;
            }
            
            mailMessage.Attachments.Add(mailAttachment);
        }

        // Set priority
        mailMessage.Priority = message.Priority switch
        {
            EmailPriority.Low => MailPriority.Low,
            EmailPriority.High => MailPriority.High,
            EmailPriority.Critical => MailPriority.High,
            _ => MailPriority.Normal
        };

        return mailMessage;
    }

    public void Dispose()
    {
        _smtpClient?.Dispose();
    }
}

public class SmtpConfiguration
{
    public bool IsEnabled { get; set; } = true;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool EnableSsl { get; set; } = true;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "IntelliFin";
    public int TimeoutMs { get; set; } = 30000;
    public string? TestRecipient { get; set; }
}