using IntelliFin.Communications.Models;

namespace IntelliFin.Communications.Services;

public interface ISmsTemplateService
{
    Task<SmsTemplate?> GetTemplateAsync(string templateId, CancellationToken cancellationToken = default);
    
    Task<SmsTemplate?> GetTemplateByTypeAsync(SmsNotificationType notificationType, string language = "en", 
        CancellationToken cancellationToken = default);
    
    Task<List<SmsTemplate>> GetAllTemplatesAsync(CancellationToken cancellationToken = default);
    
    Task<string> RenderTemplateAsync(string templateId, Dictionary<string, object> templateData, 
        CancellationToken cancellationToken = default);
    
    Task<string> RenderTemplateContentAsync(string templateContent, Dictionary<string, object> templateData, 
        CancellationToken cancellationToken = default);
    
    Task<SmsTemplate> CreateTemplateAsync(SmsTemplate template, CancellationToken cancellationToken = default);
    
    Task<SmsTemplate> UpdateTemplateAsync(SmsTemplate template, CancellationToken cancellationToken = default);
    
    Task<bool> DeleteTemplateAsync(string templateId, CancellationToken cancellationToken = default);
    
    Task<bool> ValidateTemplateAsync(SmsTemplate template, CancellationToken cancellationToken = default);
    
    Task<List<string>> GetTemplateVariablesAsync(string templateContent, CancellationToken cancellationToken = default);
    
    Task<bool> TestTemplateAsync(string templateId, Dictionary<string, object> testData, 
        CancellationToken cancellationToken = default);
}