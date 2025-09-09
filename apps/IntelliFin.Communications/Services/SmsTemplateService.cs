using IntelliFin.Communications.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace IntelliFin.Communications.Services;

public class SmsTemplateService : ISmsTemplateService
{
    private readonly ILogger<SmsTemplateService> _logger;
    private readonly IDistributedCache _cache;
    private readonly Dictionary<SmsNotificationType, Dictionary<string, SmsTemplate>> _defaultTemplates;
    private const int MaxSmsLength = 160;

    public SmsTemplateService(ILogger<SmsTemplateService> logger, IDistributedCache cache)
    {
        _logger = logger;
        _cache = cache;
        _defaultTemplates = InitializeDefaultTemplates();
    }

    public async Task<SmsTemplate?> GetTemplateAsync(string templateId, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = $"sms:template:{templateId}";
            var cachedTemplate = await _cache.GetStringAsync(cacheKey, cancellationToken);
            
            if (!string.IsNullOrEmpty(cachedTemplate))
            {
                return JsonSerializer.Deserialize<SmsTemplate>(cachedTemplate);
            }

            // If not in cache, this would typically query a database
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving SMS template {TemplateId}", templateId);
            return null;
        }
    }

    public async Task<SmsTemplate?> GetTemplateByTypeAsync(SmsNotificationType notificationType, 
        string language = "en", CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = $"sms:template:type:{notificationType}:{language}";
            var cachedTemplate = await _cache.GetStringAsync(cacheKey, cancellationToken);
            
            if (!string.IsNullOrEmpty(cachedTemplate))
            {
                return JsonSerializer.Deserialize<SmsTemplate>(cachedTemplate);
            }

            // Return default template if available
            if (_defaultTemplates.TryGetValue(notificationType, out var languageTemplates) &&
                languageTemplates.TryGetValue(language, out var template))
            {
                // Cache the default template
                await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(template),
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) },
                    cancellationToken);
                
                return template;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving SMS template for type {NotificationType}, language {Language}",
                notificationType, language);
            return null;
        }
    }

    public async Task<List<SmsTemplate>> GetAllTemplatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = "sms:templates:all";
            var cachedTemplates = await _cache.GetStringAsync(cacheKey, cancellationToken);
            
            if (!string.IsNullOrEmpty(cachedTemplates))
            {
                return JsonSerializer.Deserialize<List<SmsTemplate>>(cachedTemplates) ?? new List<SmsTemplate>();
            }

            // Return default templates
            var allTemplates = _defaultTemplates
                .SelectMany(kvp => kvp.Value.Values)
                .ToList();

            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(allTemplates),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) },
                cancellationToken);

            return allTemplates;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all SMS templates");
            return new List<SmsTemplate>();
        }
    }

    public async Task<string> RenderTemplateAsync(string templateId, Dictionary<string, object> templateData, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var template = await GetTemplateAsync(templateId, cancellationToken);
            if (template == null)
            {
                _logger.LogWarning("SMS template not found: {TemplateId}", templateId);
                return "Template not found";
            }

            return await RenderTemplateContentAsync(template.Template, templateData, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rendering SMS template {TemplateId}", templateId);
            return "Error rendering template";
        }
    }

    public async Task<string> RenderTemplateContentAsync(string templateContent, Dictionary<string, object> templateData, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var rendered = templateContent;
            
            // Replace template variables in the format {{variableName}}
            var variablePattern = @"\{\{([^}]+)\}\}";
            var matches = Regex.Matches(templateContent, variablePattern);
            
            foreach (Match match in matches)
            {
                var variableName = match.Groups[1].Value.Trim();
                if (templateData.TryGetValue(variableName, out var value))
                {
                    rendered = rendered.Replace(match.Value, value?.ToString() ?? "");
                }
                else
                {
                    _logger.LogWarning("Template variable not provided: {VariableName}", variableName);
                    rendered = rendered.Replace(match.Value, $"[{variableName}]");
                }
            }

            // Truncate if too long
            if (rendered.Length > MaxSmsLength)
            {
                rendered = rendered[..(MaxSmsLength - 3)] + "...";
                _logger.LogWarning("SMS message truncated to {MaxLength} characters", MaxSmsLength);
            }

            return rendered;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rendering SMS template content");
            return "Error rendering message";
        }
    }

    public async Task<SmsTemplate> CreateTemplateAsync(SmsTemplate template, CancellationToken cancellationToken = default)
    {
        try
        {
            template.Id = Guid.NewGuid().ToString();
            template.CreatedAt = DateTime.UtcNow;
            template.UpdatedAt = DateTime.UtcNow;

            // Validate template
            if (!await ValidateTemplateAsync(template, cancellationToken))
            {
                throw new ArgumentException("Invalid template format");
            }

            // Cache the template
            var cacheKey = $"sms:template:{template.Id}";
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(template),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) },
                cancellationToken);

            // Invalidate all templates cache
            await _cache.RemoveAsync("sms:templates:all", cancellationToken);

            _logger.LogInformation("SMS template created: {TemplateId}", template.Id);
            return template;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating SMS template");
            throw;
        }
    }

    public async Task<SmsTemplate> UpdateTemplateAsync(SmsTemplate template, CancellationToken cancellationToken = default)
    {
        try
        {
            template.UpdatedAt = DateTime.UtcNow;

            // Validate template
            if (!await ValidateTemplateAsync(template, cancellationToken))
            {
                throw new ArgumentException("Invalid template format");
            }

            // Update cache
            var cacheKey = $"sms:template:{template.Id}";
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(template),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) },
                cancellationToken);

            // Invalidate related caches
            await _cache.RemoveAsync("sms:templates:all", cancellationToken);
            await _cache.RemoveAsync($"sms:template:type:{template.NotificationType}:{template.Language}", cancellationToken);

            _logger.LogInformation("SMS template updated: {TemplateId}", template.Id);
            return template;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating SMS template {TemplateId}", template.Id);
            throw;
        }
    }

    public async Task<bool> DeleteTemplateAsync(string templateId, CancellationToken cancellationToken = default)
    {
        try
        {
            var template = await GetTemplateAsync(templateId, cancellationToken);
            if (template == null)
                return false;

            // Remove from cache
            var cacheKey = $"sms:template:{templateId}";
            await _cache.RemoveAsync(cacheKey, cancellationToken);

            // Invalidate related caches
            await _cache.RemoveAsync("sms:templates:all", cancellationToken);
            await _cache.RemoveAsync($"sms:template:type:{template.NotificationType}:{template.Language}", cancellationToken);

            _logger.LogInformation("SMS template deleted: {TemplateId}", templateId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting SMS template {TemplateId}", templateId);
            return false;
        }
    }

    public async Task<bool> ValidateTemplateAsync(SmsTemplate template, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(template.Name) || 
                string.IsNullOrWhiteSpace(template.Template))
            {
                return false;
            }

            // Check for valid variable syntax
            var variablePattern = @"\{\{([^}]+)\}\}";
            var matches = Regex.Matches(template.Template, variablePattern);
            
            foreach (Match match in matches)
            {
                var variableName = match.Groups[1].Value.Trim();
                if (string.IsNullOrWhiteSpace(variableName))
                {
                    return false;
                }
            }

            // Validate template length (allow for variable expansion)
            var estimatedLength = template.Template.Length + (matches.Count * 20); // Assume 20 chars per variable
            if (estimatedLength > MaxSmsLength * 2) // Allow some buffer
            {
                _logger.LogWarning("Template may be too long after variable expansion: {TemplateId}", template.Id);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating SMS template");
            return false;
        }
    }

    public async Task<List<string>> GetTemplateVariablesAsync(string templateContent, CancellationToken cancellationToken = default)
    {
        try
        {
            var variables = new List<string>();
            var variablePattern = @"\{\{([^}]+)\}\}";
            var matches = Regex.Matches(templateContent, variablePattern);
            
            foreach (Match match in matches)
            {
                var variableName = match.Groups[1].Value.Trim();
                if (!variables.Contains(variableName))
                {
                    variables.Add(variableName);
                }
            }

            return variables;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting template variables");
            return new List<string>();
        }
    }

    public async Task<bool> TestTemplateAsync(string templateId, Dictionary<string, object> testData, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var rendered = await RenderTemplateAsync(templateId, testData, cancellationToken);
            
            // Test successful if rendering completes without error and produces reasonable output
            return !string.IsNullOrWhiteSpace(rendered) && 
                   !rendered.Contains("Template not found") && 
                   !rendered.Contains("Error rendering");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing SMS template {TemplateId}", templateId);
            return false;
        }
    }

    private Dictionary<SmsNotificationType, Dictionary<string, SmsTemplate>> InitializeDefaultTemplates()
    {
        return new Dictionary<SmsNotificationType, Dictionary<string, SmsTemplate>>
        {
            [SmsNotificationType.LoanApplicationStatus] = new()
            {
                ["en"] = new SmsTemplate
                {
                    Id = "loan_status_en",
                    Name = "Loan Application Status",
                    NotificationType = SmsNotificationType.LoanApplicationStatus,
                    Template = "IntelliFin: Your loan application {{applicationNumber}} status: {{status}}. {{message}}",
                    Language = "en",
                    RequiredFields = ["applicationNumber", "status", "message"],
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            },
            [SmsNotificationType.PaymentReminder] = new()
            {
                ["en"] = new SmsTemplate
                {
                    Id = "payment_reminder_en",
                    Name = "Payment Reminder",
                    NotificationType = SmsNotificationType.PaymentReminder,
                    Template = "IntelliFin: Payment of ZMW {{amount}} due on {{dueDate}} for loan {{loanNumber}}. Please pay to avoid late fees.",
                    Language = "en",
                    RequiredFields = ["amount", "dueDate", "loanNumber"],
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            },
            [SmsNotificationType.PaymentConfirmation] = new()
            {
                ["en"] = new SmsTemplate
                {
                    Id = "payment_confirmation_en",
                    Name = "Payment Confirmation",
                    NotificationType = SmsNotificationType.PaymentConfirmation,
                    Template = "IntelliFin: Payment received! ZMW {{amount}} for loan {{loanNumber}} on {{paymentDate}}. Balance: ZMW {{remainingBalance}}",
                    Language = "en",
                    RequiredFields = ["amount", "loanNumber", "paymentDate", "remainingBalance"],
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            },
            [SmsNotificationType.OverduePayment] = new()
            {
                ["en"] = new SmsTemplate
                {
                    Id = "overdue_payment_en",
                    Name = "Overdue Payment",
                    NotificationType = SmsNotificationType.OverduePayment,
                    Template = "IntelliFin: URGENT - Payment of ZMW {{amount}} for loan {{loanNumber}} is {{daysOverdue}} days overdue. Please pay immediately.",
                    Language = "en",
                    RequiredFields = ["amount", "loanNumber", "daysOverdue"],
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            },
            [SmsNotificationType.LoanApproval] = new()
            {
                ["en"] = new SmsTemplate
                {
                    Id = "loan_approval_en",
                    Name = "Loan Approval",
                    NotificationType = SmsNotificationType.LoanApproval,
                    Template = "IntelliFin: Congratulations! Your loan application {{applicationNumber}} for ZMW {{amount}} has been approved.",
                    Language = "en",
                    RequiredFields = ["applicationNumber", "amount"],
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            },
            [SmsNotificationType.LoanDisbursement] = new()
            {
                ["en"] = new SmsTemplate
                {
                    Id = "loan_disbursement_en",
                    Name = "Loan Disbursement",
                    NotificationType = SmsNotificationType.LoanDisbursement,
                    Template = "IntelliFin: ZMW {{amount}} has been disbursed to your account. Loan Number: {{loanNumber}}. Thank you for choosing IntelliFin!",
                    Language = "en",
                    RequiredFields = ["amount", "loanNumber"],
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            },
            [SmsNotificationType.PmecDeductionStatus] = new()
            {
                ["en"] = new SmsTemplate
                {
                    Id = "pmec_deduction_en",
                    Name = "PMEC Deduction Status",
                    NotificationType = SmsNotificationType.PmecDeductionStatus,
                    Template = "IntelliFin: PMEC deduction {{status}} for ZMW {{amount}} on {{deductionDate}}. Loan {{loanNumber}} balance: ZMW {{balance}}",
                    Language = "en",
                    RequiredFields = ["status", "amount", "deductionDate", "loanNumber", "balance"],
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            },
            [SmsNotificationType.AccountBalance] = new()
            {
                ["en"] = new SmsTemplate
                {
                    Id = "account_balance_en",
                    Name = "Account Balance",
                    NotificationType = SmsNotificationType.AccountBalance,
                    Template = "IntelliFin: Account balance as of {{date}}: ZMW {{balance}}. Next payment due: {{nextDueDate}}",
                    Language = "en",
                    RequiredFields = ["date", "balance", "nextDueDate"],
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            }
        };
    }
}