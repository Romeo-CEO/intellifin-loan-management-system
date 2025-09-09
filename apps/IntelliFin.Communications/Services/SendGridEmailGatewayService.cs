using IntelliFin.Communications.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace IntelliFin.Communications.Services;

public class SendGridEmailGatewayService : IEmailGatewayService
{
    private readonly ILogger<SendGridEmailGatewayService> _logger;
    private readonly HttpClient _httpClient;
    private readonly SendGridConfiguration _config;

    public string GatewayName => "SendGrid";
    public bool IsEnabled => _config.IsEnabled && !string.IsNullOrEmpty(_config.ApiKey);

    public SendGridEmailGatewayService(
        ILogger<SendGridEmailGatewayService> logger,
        HttpClient httpClient,
        IOptions<SendGridConfiguration> config)
    {
        _logger = logger;
        _httpClient = httpClient;
        _config = config.Value;
        
        ConfigureHttpClient();
    }

    public async Task<SendEmailResponse> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Sending email via SendGrid to {To} with subject {Subject}", 
                message.To, message.Subject);

            if (!IsEnabled)
            {
                return new SendEmailResponse
                {
                    Success = false,
                    MessageId = message.Id,
                    Message = "SendGrid gateway is disabled or not configured",
                    Errors = new List<string> { "Gateway is not enabled or API key is missing" }
                };
            }

            var sendGridMessage = CreateSendGridMessage(message);
            var json = JsonSerializer.Serialize(sendGridMessage, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("mail/send", content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var externalId = ExtractMessageId(response);
                
                _logger.LogInformation("Email sent successfully via SendGrid. MessageId: {MessageId}, ExternalId: {ExternalId}", 
                    message.Id, externalId);

                return new SendEmailResponse
                {
                    Success = true,
                    MessageId = message.Id,
                    ExternalId = externalId,
                    Message = "Email sent successfully"
                };
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var errors = ParseSendGridErrors(errorContent);
                
                _logger.LogError("SendGrid API error sending email {MessageId}: {StatusCode} - {Errors}", 
                    message.Id, response.StatusCode, string.Join(", ", errors));

                return new SendEmailResponse
                {
                    Success = false,
                    MessageId = message.Id,
                    Message = $"SendGrid API error: {response.StatusCode}",
                    Errors = errors
                };
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error sending email {MessageId} via SendGrid", message.Id);
            
            return new SendEmailResponse
            {
                Success = false,
                MessageId = message.Id,
                Message = "Network error occurred",
                Errors = new List<string> { $"HTTP Error: {ex.Message}" }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email {MessageId} via SendGrid", message.Id);
            
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
            _logger.LogInformation("Sending bulk email via SendGrid: {Count} messages", messages.Count());

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
                    Error = "SendGrid gateway is disabled or not configured",
                    Code = "GATEWAY_DISABLED"
                }));
                return response;
            }

            // SendGrid supports bulk sending, but we'll batch them for better control
            var batches = messages.Chunk(100); // SendGrid limit is 1000, but we'll use smaller batches
            var successCount = 0;
            var errors = new List<BulkEmailError>();

            foreach (var batch in batches)
            {
                try
                {
                    var sendGridBulkMessage = CreateSendGridBulkMessage(batch);
                    var json = JsonSerializer.Serialize(sendGridBulkMessage, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = false
                    });

                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var httpResponse = await _httpClient.PostAsync("mail/send", content, cancellationToken);

                    if (httpResponse.IsSuccessStatusCode)
                    {
                        successCount += batch.Count();
                        response.MessageIds.AddRange(batch.Select(m => m.Id));
                    }
                    else
                    {
                        var errorContent = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
                        var batchErrors = ParseSendGridErrors(errorContent);
                        
                        foreach (var message in batch)
                        {
                            errors.Add(new BulkEmailError
                            {
                                Recipient = message.To,
                                Error = string.Join("; ", batchErrors),
                                Code = $"SENDGRID_{httpResponse.StatusCode}"
                            });
                        }
                    }

                    // Small delay between batches
                    if (batches.Count() > 1)
                    {
                        await Task.Delay(100, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending bulk email batch via SendGrid");
                    
                    foreach (var message in batch)
                    {
                        errors.Add(new BulkEmailError
                        {
                            Recipient = message.To,
                            Error = ex.Message,
                            Code = "BATCH_ERROR"
                        });
                    }
                }
            }

            response.AcceptedRecipients = successCount;
            response.RejectedRecipients = errors.Count;
            response.Errors = errors;
            response.Success = successCount > 0;

            _logger.LogInformation("Bulk email completed via SendGrid: {Accepted} accepted, {Rejected} rejected",
                response.AcceptedRecipients, response.RejectedRecipients);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending bulk email via SendGrid");
            
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
            
            // This would typically call SendGrid's Event API
            // For now, return a mock status
            await Task.Delay(50, cancellationToken);
            
            return new EmailDeliveryReport
            {
                ExternalId = externalId,
                Status = EmailStatus.Delivered,
                StatusUpdatedAt = DateTime.UtcNow.AddMinutes(-5),
                DeliveredAt = DateTime.UtcNow.AddMinutes(-5),
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
            _logger.LogInformation("Processing SendGrid webhook payload");
            
            var events = JsonSerializer.Deserialize<SendGridEvent[]>(payload);
            if (events == null || !events.Any())
            {
                _logger.LogWarning("Invalid or empty SendGrid webhook payload");
                return false;
            }

            foreach (var evt in events)
            {
                _logger.LogDebug("Processing SendGrid event: {Event} for {Email}", evt.Event, evt.Email);
                
                // In a real implementation, this would update the database with delivery status
                // For now, just log the event
                _logger.LogInformation("SendGrid event processed: {Event} for message {MessageId}", 
                    evt.Event, evt.MessageId);
            }

            return true;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing SendGrid webhook payload");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing SendGrid webhook");
            return false;
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Testing SendGrid connection");
            
            if (!IsEnabled)
            {
                _logger.LogWarning("SendGrid gateway is disabled or not configured");
                return false;
            }

            // Test with a simple GET request to the API
            var response = await _httpClient.GetAsync("user/profile", cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("SendGrid connection test successful");
                return true;
            }
            else
            {
                _logger.LogError("SendGrid connection test failed: {StatusCode}", response.StatusCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendGrid connection test failed");
            return false;
        }
    }

    private void ConfigureHttpClient()
    {
        _httpClient.BaseAddress = new Uri("https://api.sendgrid.com/v3/");
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.ApiKey}");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "IntelliFin/1.0");
        _httpClient.Timeout = TimeSpan.FromMilliseconds(_config.TimeoutMs);
    }

    private object CreateSendGridMessage(EmailMessage message)
    {
        var sendGridMessage = new
        {
            personalizations = new[]
            {
                new
                {
                    to = new[] { new { email = message.To } },
                    cc = !string.IsNullOrEmpty(message.Cc) 
                        ? message.Cc.Split(',', ';').Select(cc => new { email = cc.Trim() }).ToArray()
                        : null,
                    bcc = !string.IsNullOrEmpty(message.Bcc)
                        ? message.Bcc.Split(',', ';').Select(bcc => new { email = bcc.Trim() }).ToArray()
                        : null,
                    subject = message.Subject,
                    headers = message.Headers.Any() ? message.Headers : null,
                    custom_args = message.Metadata.Any() ? message.Metadata : null
                }
            },
            from = new
            {
                email = !string.IsNullOrEmpty(message.From) ? message.From : _config.FromAddress,
                name = _config.FromName
            },
            reply_to = !string.IsNullOrEmpty(message.ReplyTo) ? new { email = message.ReplyTo } : null,
            content = CreateContent(message),
            attachments = message.Attachments.Any() ? CreateAttachments(message.Attachments) : null,
            tracking_settings = new
            {
                click_tracking = new { enable = _config.EnableClickTracking },
                open_tracking = new { enable = _config.EnableOpenTracking },
                subscription_tracking = new { enable = false }
            },
            mail_settings = new
            {
                footer = _config.EnableFooter ? new { enable = true, text = _config.FooterText } : null
            }
        };

        return sendGridMessage;
    }

    private object CreateSendGridBulkMessage(IEnumerable<EmailMessage> messages)
    {
        // For bulk messages, we'll create personalizations for each recipient
        var personalizations = messages.Select(message => new
        {
            to = new[] { new { email = message.To } },
            cc = !string.IsNullOrEmpty(message.Cc)
                ? message.Cc.Split(',', ';').Select(cc => new { email = cc.Trim() }).ToArray()
                : null,
            bcc = !string.IsNullOrEmpty(message.Bcc)
                ? message.Bcc.Split(',', ';').Select(bcc => new { email = bcc.Trim() }).ToArray()
                : null,
            subject = message.Subject,
            headers = message.Headers.Any() ? message.Headers : null,
            custom_args = message.Metadata.Any() ? message.Metadata : null
        }).ToArray();

        // Use the first message as template for common properties
        var firstMessage = messages.First();

        var sendGridBulkMessage = new
        {
            personalizations,
            from = new
            {
                email = !string.IsNullOrEmpty(firstMessage.From) ? firstMessage.From : _config.FromAddress,
                name = _config.FromName
            },
            reply_to = !string.IsNullOrEmpty(firstMessage.ReplyTo) ? new { email = firstMessage.ReplyTo } : null,
            content = CreateContent(firstMessage),
            tracking_settings = new
            {
                click_tracking = new { enable = _config.EnableClickTracking },
                open_tracking = new { enable = _config.EnableOpenTracking },
                subscription_tracking = new { enable = false }
            }
        };

        return sendGridBulkMessage;
    }

    private object[] CreateContent(EmailMessage message)
    {
        var content = new List<object>();

        if (!string.IsNullOrEmpty(message.TextContent))
        {
            content.Add(new { type = "text/plain", value = message.TextContent });
        }

        if (!string.IsNullOrEmpty(message.HtmlContent))
        {
            content.Add(new { type = "text/html", value = message.HtmlContent });
        }

        return content.ToArray();
    }

    private object[] CreateAttachments(IEnumerable<EmailAttachment> attachments)
    {
        return attachments.Select(att => new
        {
            content = Convert.ToBase64String(att.Content),
            filename = att.FileName,
            type = att.ContentType,
            disposition = att.IsInline ? "inline" : "attachment",
            content_id = att.ContentId
        }).ToArray();
    }

    private string ExtractMessageId(HttpResponseMessage response)
    {
        // SendGrid returns message ID in the X-Message-Id header
        if (response.Headers.TryGetValues("X-Message-Id", out var values))
        {
            return values.FirstOrDefault() ?? Guid.NewGuid().ToString();
        }

        return Guid.NewGuid().ToString();
    }

    private List<string> ParseSendGridErrors(string errorContent)
    {
        try
        {
            var errorResponse = JsonSerializer.Deserialize<SendGridErrorResponse>(errorContent);
            if (errorResponse?.Errors != null && errorResponse.Errors.Any())
            {
                return errorResponse.Errors.Select(e => e.Message).ToList();
            }
        }
        catch (JsonException)
        {
            // If parsing fails, return the raw content
        }

        return new List<string> { errorContent };
    }

    private class SendGridErrorResponse
    {
        public SendGridError[] Errors { get; set; } = Array.Empty<SendGridError>();
    }

    private class SendGridError
    {
        public string Message { get; set; } = string.Empty;
        public string Field { get; set; } = string.Empty;
    }

    private class SendGridEvent
    {
        public string Event { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string MessageId { get; set; } = string.Empty;
        public long Timestamp { get; set; }
        public Dictionary<string, object> CustomArgs { get; set; } = new();
    }
}

public class SendGridConfiguration
{
    public bool IsEnabled { get; set; } = false;
    public string ApiKey { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "IntelliFin";
    public int TimeoutMs { get; set; } = 30000;
    public bool EnableClickTracking { get; set; } = true;
    public bool EnableOpenTracking { get; set; } = true;
    public bool EnableFooter { get; set; } = false;
    public string FooterText { get; set; } = string.Empty;
}