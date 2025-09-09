using IntelliFin.Communications.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace IntelliFin.Communications.Services;

public class EmailTemplateService : IEmailTemplateService
{
    private readonly ILogger<EmailTemplateService> _logger;
    private readonly Dictionary<string, EmailTemplate> _templates;
    private readonly object _lockObject = new object();

    public EmailTemplateService(ILogger<EmailTemplateService> logger)
    {
        _logger = logger;
        _templates = new Dictionary<string, EmailTemplate>();
        InitializeDefaultTemplates();
    }

    public async Task<EmailTemplate> CreateTemplateAsync(CreateEmailTemplateRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Creating email template: {Name}", request.Name);

            var validationErrors = ValidateTemplateRequest(request);
            if (validationErrors.Any())
            {
                throw new ArgumentException($"Template validation failed: {string.Join(", ", validationErrors)}");
            }

            var template = new EmailTemplate
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name,
                Description = request.Description,
                Subject = request.Subject,
                TextContent = request.TextContent,
                HtmlContent = request.HtmlContent,
                Category = request.Category,
                Parameters = ExtractTemplateParameters(request.Subject, request.TextContent, request.HtmlContent),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system", // In real implementation, this would come from the current user context
                Metadata = request.Metadata
            };

            lock (_lockObject)
            {
                if (_templates.ContainsKey(template.Id))
                {
                    throw new InvalidOperationException($"Template with ID {template.Id} already exists");
                }
                _templates[template.Id] = template;
            }

            _logger.LogInformation("Email template created successfully: {TemplateId}", template.Id);
            return template;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating email template: {Name}", request.Name);
            throw;
        }
    }

    public async Task<EmailTemplate> UpdateTemplateAsync(string templateId, CreateEmailTemplateRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Updating email template: {TemplateId}", templateId);

            var validationErrors = ValidateTemplateRequest(request);
            if (validationErrors.Any())
            {
                throw new ArgumentException($"Template validation failed: {string.Join(", ", validationErrors)}");
            }

            lock (_lockObject)
            {
                if (!_templates.TryGetValue(templateId, out var existingTemplate))
                {
                    throw new ArgumentException($"Template with ID {templateId} not found");
                }

                existingTemplate.Name = request.Name;
                existingTemplate.Description = request.Description;
                existingTemplate.Subject = request.Subject;
                existingTemplate.TextContent = request.TextContent;
                existingTemplate.HtmlContent = request.HtmlContent;
                existingTemplate.Category = request.Category;
                existingTemplate.Parameters = ExtractTemplateParameters(request.Subject, request.TextContent, request.HtmlContent);
                existingTemplate.LastModified = DateTime.UtcNow;
                existingTemplate.Metadata = request.Metadata;

                _templates[templateId] = existingTemplate;
            }

            var updatedTemplate = _templates[templateId];
            _logger.LogInformation("Email template updated successfully: {TemplateId}", templateId);
            return updatedTemplate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating email template: {TemplateId}", templateId);
            throw;
        }
    }

    public async Task<EmailTemplate?> GetTemplateAsync(string templateId, CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.CompletedTask; // Simulate async operation
            
            lock (_lockObject)
            {
                _templates.TryGetValue(templateId, out var template);
                return template;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving email template: {TemplateId}", templateId);
            throw;
        }
    }

    public async Task<IEnumerable<EmailTemplate>> GetTemplatesAsync(EmailCategory? category = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.CompletedTask; // Simulate async operation
            
            lock (_lockObject)
            {
                var templates = _templates.Values.AsEnumerable();
                
                if (category.HasValue)
                {
                    templates = templates.Where(t => t.Category == category.Value);
                }

                return templates.Where(t => t.IsActive).ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving email templates");
            throw;
        }
    }

    public async Task DeleteTemplateAsync(string templateId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Deleting email template: {TemplateId}", templateId);

            lock (_lockObject)
            {
                if (!_templates.TryGetValue(templateId, out var template))
                {
                    throw new ArgumentException($"Template with ID {templateId} not found");
                }

                // Soft delete by setting IsActive to false
                template.IsActive = false;
                template.LastModified = DateTime.UtcNow;
                _templates[templateId] = template;
            }

            _logger.LogInformation("Email template deleted successfully: {TemplateId}", templateId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting email template: {TemplateId}", templateId);
            throw;
        }
    }

    public async Task<string> RenderTemplateAsync(string templateId, Dictionary<string, string> parameters, CancellationToken cancellationToken = default)
    {
        try
        {
            var template = await GetTemplateAsync(templateId, cancellationToken);
            if (template == null)
            {
                throw new ArgumentException($"Template with ID {templateId} not found");
            }

            if (!template.IsActive)
            {
                throw new InvalidOperationException($"Template {templateId} is not active");
            }

            // Use HTML content if available, otherwise use text content
            var content = !string.IsNullOrEmpty(template.HtmlContent) ? template.HtmlContent : template.TextContent;
            
            if (string.IsNullOrEmpty(content))
            {
                throw new InvalidOperationException($"Template {templateId} has no content");
            }

            return RenderContent(content, parameters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rendering email template: {TemplateId}", templateId);
            throw;
        }
    }

    public async Task<(string subject, string textContent, string htmlContent)> RenderFullTemplateAsync(string templateId, Dictionary<string, string> parameters, CancellationToken cancellationToken = default)
    {
        try
        {
            var template = await GetTemplateAsync(templateId, cancellationToken);
            if (template == null)
            {
                throw new ArgumentException($"Template with ID {templateId} not found");
            }

            if (!template.IsActive)
            {
                throw new InvalidOperationException($"Template {templateId} is not active");
            }

            var renderedSubject = RenderContent(template.Subject, parameters);
            var renderedTextContent = !string.IsNullOrEmpty(template.TextContent) 
                ? RenderContent(template.TextContent, parameters) 
                : null;
            var renderedHtmlContent = !string.IsNullOrEmpty(template.HtmlContent) 
                ? RenderContent(template.HtmlContent, parameters) 
                : null;

            _logger.LogDebug("Template rendered successfully: {TemplateId}", templateId);
            return (renderedSubject, renderedTextContent ?? string.Empty, renderedHtmlContent ?? string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rendering full email template: {TemplateId}", templateId);
            throw;
        }
    }

    public async Task<List<string>> ValidateTemplateAsync(string templateId, CancellationToken cancellationToken = default)
    {
        try
        {
            var errors = new List<string>();
            var template = await GetTemplateAsync(templateId, cancellationToken);
            
            if (template == null)
            {
                errors.Add($"Template with ID {templateId} not found");
                return errors;
            }

            // Validate subject
            if (string.IsNullOrEmpty(template.Subject))
            {
                errors.Add("Template subject is required");
            }

            // Validate content
            if (string.IsNullOrEmpty(template.TextContent) && string.IsNullOrEmpty(template.HtmlContent))
            {
                errors.Add("Template must have either text content or HTML content");
            }

            // Validate template parameters
            var subjectParams = ExtractTemplateParameters(template.Subject);
            var textParams = ExtractTemplateParameters(template.TextContent);
            var htmlParams = ExtractTemplateParameters(template.HtmlContent);
            
            var allParameters = subjectParams.Concat(textParams).Concat(htmlParams).Distinct().ToList();
            
            // Check if declared parameters match actual parameters
            var declaredParams = template.Parameters ?? new List<string>();
            var missingParams = allParameters.Except(declaredParams).ToList();
            var extraParams = declaredParams.Except(allParameters).ToList();

            if (missingParams.Any())
            {
                errors.Add($"Template uses undeclared parameters: {string.Join(", ", missingParams)}");
            }

            if (extraParams.Any())
            {
                errors.Add($"Template declares unused parameters: {string.Join(", ", extraParams)}");
            }

            // Validate HTML content if present
            if (!string.IsNullOrEmpty(template.HtmlContent))
            {
                var htmlErrors = ValidateHtmlContent(template.HtmlContent);
                errors.AddRange(htmlErrors);
            }

            return errors;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating email template: {TemplateId}", templateId);
            return new List<string> { $"Error validating template: {ex.Message}" };
        }
    }

    private List<string> ValidateTemplateRequest(CreateEmailTemplateRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Name))
            errors.Add("Template name is required");

        if (string.IsNullOrWhiteSpace(request.Subject))
            errors.Add("Template subject is required");

        if (string.IsNullOrWhiteSpace(request.TextContent) && string.IsNullOrWhiteSpace(request.HtmlContent))
            errors.Add("Template must have either text content or HTML content");

        return errors;
    }

    private List<string> ExtractTemplateParameters(params string?[] contents)
    {
        var parameters = new HashSet<string>();
        var parameterRegex = new Regex(@"\{\{(\w+)\}\}", RegexOptions.Compiled);

        foreach (var content in contents)
        {
            if (string.IsNullOrEmpty(content)) continue;

            var matches = parameterRegex.Matches(content);
            foreach (Match match in matches)
            {
                parameters.Add(match.Groups[1].Value);
            }
        }

        return parameters.ToList();
    }

    private string RenderContent(string content, Dictionary<string, string> parameters)
    {
        if (string.IsNullOrEmpty(content))
            return string.Empty;

        var renderedContent = content;
        var parameterRegex = new Regex(@"\{\{(\w+)\}\}", RegexOptions.Compiled);

        renderedContent = parameterRegex.Replace(renderedContent, match =>
        {
            var paramName = match.Groups[1].Value;
            return parameters.TryGetValue(paramName, out var value) ? value : $"{{{{ {paramName} }}}}";
        });

        return renderedContent;
    }

    private List<string> ValidateHtmlContent(string htmlContent)
    {
        var errors = new List<string>();

        try
        {
            // Basic HTML validation - check for common issues
            if (!htmlContent.Contains("<html>", StringComparison.OrdinalIgnoreCase) &&
                !htmlContent.Contains("<!doctype", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("HTML content should include proper HTML document structure");
            }

            // Check for unclosed tags (basic validation)
            var openTags = new Regex(@"<(\w+)[^>]*>", RegexOptions.IgnoreCase);
            var closeTags = new Regex(@"</(\w+)>", RegexOptions.IgnoreCase);
            var selfClosingTags = new HashSet<string> { "br", "hr", "img", "input", "meta", "link" };

            var openMatches = openTags.Matches(htmlContent);
            var closeMatches = closeTags.Matches(htmlContent);

            var openTagCounts = new Dictionary<string, int>();
            var closeTagCounts = new Dictionary<string, int>();

            foreach (Match match in openMatches)
            {
                var tag = match.Groups[1].Value.ToLowerInvariant();
                if (!selfClosingTags.Contains(tag))
                {
                    openTagCounts[tag] = openTagCounts.GetValueOrDefault(tag, 0) + 1;
                }
            }

            foreach (Match match in closeMatches)
            {
                var tag = match.Groups[1].Value.ToLowerInvariant();
                closeTagCounts[tag] = closeTagCounts.GetValueOrDefault(tag, 0) + 1;
            }

            foreach (var kvp in openTagCounts)
            {
                var openCount = kvp.Value;
                var closeCount = closeTagCounts.GetValueOrDefault(kvp.Key, 0);
                
                if (openCount != closeCount)
                {
                    errors.Add($"Mismatched HTML tags for '{kvp.Key}': {openCount} opening, {closeCount} closing");
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Error validating HTML content: {ex.Message}");
        }

        return errors;
    }

    private void InitializeDefaultTemplates()
    {
        var defaultTemplates = new List<EmailTemplate>
        {
            new EmailTemplate
            {
                Id = "welcome-email",
                Name = "Welcome Email",
                Description = "Welcome email for new clients",
                Subject = "Welcome to IntelliFin - {{clientName}}",
                TextContent = "Dear {{clientName}},\n\nWelcome to IntelliFin! We're excited to have you as our client.\n\nYour account has been successfully created with the following details:\n- Client ID: {{clientId}}\n- Branch: {{branchName}}\n\nBest regards,\nIntelliFin Team",
                HtmlContent = @"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>Welcome to IntelliFin</title>
</head>
<body style=""font-family: Arial, sans-serif; margin: 0; padding: 20px;"">
    <div style=""max-width: 600px; margin: 0 auto;"">
        <h2 style=""color: #2c3e50;"">Welcome to IntelliFin</h2>
        <p>Dear {{clientName}},</p>
        <p>Welcome to IntelliFin! We're excited to have you as our client.</p>
        <p>Your account has been successfully created with the following details:</p>
        <ul>
            <li><strong>Client ID:</strong> {{clientId}}</li>
            <li><strong>Branch:</strong> {{branchName}}</li>
        </ul>
        <p>Best regards,<br>IntelliFin Team</p>
    </div>
</body>
</html>",
                Category = EmailCategory.Welcome,
                Parameters = new List<string> { "clientName", "clientId", "branchName" },
                CreatedBy = "system"
            },
            new EmailTemplate
            {
                Id = "loan-application-received",
                Name = "Loan Application Received",
                Description = "Confirmation email for loan application submission",
                Subject = "Loan Application Received - Reference {{applicationId}}",
                TextContent = "Dear {{clientName}},\n\nThank you for submitting your loan application.\n\nApplication Details:\n- Reference: {{applicationId}}\n- Amount: {{loanAmount}}\n- Type: {{loanType}}\n- Submitted: {{submissionDate}}\n\nWe will review your application and contact you within 2-3 business days.\n\nBest regards,\nIntelliFin Loan Team",
                HtmlContent = @"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>Loan Application Received</title>
</head>
<body style=""font-family: Arial, sans-serif; margin: 0; padding: 20px;"">
    <div style=""max-width: 600px; margin: 0 auto;"">
        <h2 style=""color: #2c3e50;"">Loan Application Received</h2>
        <p>Dear {{clientName}},</p>
        <p>Thank you for submitting your loan application.</p>
        <h3>Application Details:</h3>
        <ul>
            <li><strong>Reference:</strong> {{applicationId}}</li>
            <li><strong>Amount:</strong> {{loanAmount}}</li>
            <li><strong>Type:</strong> {{loanType}}</li>
            <li><strong>Submitted:</strong> {{submissionDate}}</li>
        </ul>
        <p>We will review your application and contact you within 2-3 business days.</p>
        <p>Best regards,<br>IntelliFin Loan Team</p>
    </div>
</body>
</html>",
                Category = EmailCategory.Transactional,
                Parameters = new List<string> { "clientName", "applicationId", "loanAmount", "loanType", "submissionDate" },
                CreatedBy = "system"
            },
            new EmailTemplate
            {
                Id = "payment-reminder",
                Name = "Payment Reminder",
                Description = "Payment reminder for upcoming due dates",
                Subject = "Payment Reminder - {{loanReference}}",
                TextContent = "Dear {{clientName}},\n\nThis is a friendly reminder that your loan payment is due on {{dueDate}}.\n\nPayment Details:\n- Loan Reference: {{loanReference}}\n- Amount Due: {{amountDue}}\n- Due Date: {{dueDate}}\n\nPlease ensure payment is made on time to avoid any late fees.\n\nThank you,\nIntelliFin Collections Team",
                HtmlContent = @"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>Payment Reminder</title>
</head>
<body style=""font-family: Arial, sans-serif; margin: 0; padding: 20px;"">
    <div style=""max-width: 600px; margin: 0 auto;"">
        <h2 style=""color: #e74c3c;"">Payment Reminder</h2>
        <p>Dear {{clientName}},</p>
        <p>This is a friendly reminder that your loan payment is due on <strong>{{dueDate}}</strong>.</p>
        <h3>Payment Details:</h3>
        <ul>
            <li><strong>Loan Reference:</strong> {{loanReference}}</li>
            <li><strong>Amount Due:</strong> {{amountDue}}</li>
            <li><strong>Due Date:</strong> {{dueDate}}</li>
        </ul>
        <p style=""color: #e74c3c;""><strong>Please ensure payment is made on time to avoid any late fees.</strong></p>
        <p>Thank you,<br>IntelliFin Collections Team</p>
    </div>
</body>
</html>",
                Category = EmailCategory.Reminder,
                Parameters = new List<string> { "clientName", "loanReference", "amountDue", "dueDate" },
                CreatedBy = "system"
            }
        };

        foreach (var template in defaultTemplates)
        {
            _templates[template.Id] = template;
        }

        _logger.LogInformation("Initialized {Count} default email templates", defaultTemplates.Count);
    }
}