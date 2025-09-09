using System.Text;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using Polly;
using Polly.Extensions.Http;
using IntelliFin.FinancialService.Models;

namespace IntelliFin.FinancialService.Services;

/// <summary>
/// JasperReports Server REST API client
/// </summary>
public class JasperReportsClient : IJasperReportsClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<JasperReportsClient> _logger;
    private readonly IConfiguration _configuration;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;

    public JasperReportsClient(HttpClient httpClient, ILogger<JasperReportsClient> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;

        // Configure HttpClient for JasperReports Server
        var jasperServerUrl = _configuration.GetConnectionString("JasperReportsServer") ?? "http://localhost:8080/jasperserver";
        var username = _configuration["JasperReports:Username"] ?? "jasperadmin";
        var password = _configuration["JasperReports:Password"] ?? "jasperadmin";

        _httpClient.BaseAddress = new Uri(jasperServerUrl);
        _httpClient.Timeout = TimeSpan.FromMinutes(5); // Increased timeout for large reports
        
        // Basic authentication
        var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // Initialize retry policy
        _retryPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .Or<HttpRequestException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, duration, retryCount, context) =>
                {
                    _logger.LogWarning("JasperReports request failed. Retry {RetryCount} in {Delay}ms", 
                        retryCount, duration.TotalMilliseconds);
                });
    }

    public async Task<byte[]> ExecuteReportAsync(string reportPath, Dictionary<string, object> parameters, string format = "pdf")
    {
        try
        {
            _logger.LogInformation("Executing JasperReports report: {ReportPath} in format: {Format}", reportPath, format);

            // Validate inputs
            if (string.IsNullOrWhiteSpace(reportPath))
                throw new ArgumentException("Report path cannot be null or empty", nameof(reportPath));
            
            if (!IsValidFormat(format))
                throw new ArgumentException($"Unsupported format: {format}. Supported formats: pdf, xlsx, csv, html", nameof(format));

            // Build the request URL for report execution
            var url = $"/rest_v2/reports{reportPath}.{format.ToLower()}";
            
            // Add parameters as query string
            if (parameters.Any())
            {
                var queryParams = string.Join("&", parameters.Select(kvp => 
                    $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value?.ToString() ?? "")}"));
                url += $"?{queryParams}";
            }

            // Execute with retry policy
            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                var httpResponse = await _httpClient.GetAsync(url);
                
                if (!httpResponse.IsSuccessStatusCode)
                {
                    var errorContent = await httpResponse.Content.ReadAsStringAsync();
                    _logger.LogWarning("JasperReports request failed with status {StatusCode}: {ErrorContent}", 
                        httpResponse.StatusCode, errorContent);
                }
                
                return httpResponse;
            });

            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsByteArrayAsync();
            
            // Validate content
            if (content.Length == 0)
                throw new InvalidOperationException($"JasperReports returned empty content for report: {reportPath}");
            
            _logger.LogInformation("Successfully executed JasperReports report: {ReportPath}, Content size: {Size} bytes", 
                reportPath, content.Length);
            
            return content;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error executing JasperReports report: {ReportPath}", reportPath);
            throw new InvalidOperationException($"Failed to execute report {reportPath}: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "Timeout executing JasperReports report: {ReportPath}", reportPath);
            throw new TimeoutException($"Report execution timed out for {reportPath}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing JasperReports report: {ReportPath}", reportPath);
            throw;
        }
    }

    public async Task<List<string>> GetAvailableReportsAsync()
    {
        try
        {
            _logger.LogInformation("Retrieving available reports from JasperReports Server");

            var response = await _httpClient.GetAsync("/rest_v2/resources?type=reportUnit");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var reports = JsonConvert.DeserializeObject<JasperResourceList>(content);

            var reportPaths = reports?.ResourceLookup?.Select(r => r.Uri).ToList() ?? new List<string>();
            
            _logger.LogInformation("Retrieved {Count} available reports from JasperReports Server", reportPaths.Count);
            
            return reportPaths;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available reports from JasperReports Server");
            return new List<string>();
        }
    }

    public async Task<bool> UploadReportTemplateAsync(string reportPath, byte[] jrxmlContent)
    {
        try
        {
            _logger.LogInformation("Uploading report template to JasperReports Server: {ReportPath}", reportPath);

            // This is a simplified implementation - in production, you'd need to handle
            // the complete JasperReports Server resource creation API
            var content = new ByteArrayContent(jrxmlContent);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/xml");

            var response = await _httpClient.PutAsync($"/rest_v2/resources{reportPath}", content);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully uploaded report template: {ReportPath}", reportPath);
                return true;
            }
            else
            {
                _logger.LogWarning("Failed to upload report template: {ReportPath}, Status: {StatusCode}", reportPath, response.StatusCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading report template: {ReportPath}", reportPath);
            return false;
        }
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            _logger.LogInformation("Testing connection to JasperReports Server");

            var response = await _retryPolicy.ExecuteAsync(async () =>
                await _httpClient.GetAsync("/rest_v2/serverInfo"));
            
            var isConnected = response.IsSuccessStatusCode;

            if (isConnected)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("JasperReports Server connection successful. Server info: {ServerInfo}", content);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("JasperReports Server connection failed. Status: {StatusCode}, Error: {ErrorContent}", 
                    response.StatusCode, errorContent);
            }

            return isConnected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing JasperReports Server connection");
            return false;
        }
    }

    private static bool IsValidFormat(string format)
    {
        var validFormats = new[] { "pdf", "xlsx", "csv", "html", "xls", "docx", "rtf", "odt", "ods" };
        return validFormats.Contains(format.ToLower());
    }

    public async Task<JasperServerInfo> GetServerInfoAsync()
    {
        try
        {
            _logger.LogInformation("Retrieving JasperReports Server information");

            var response = await _retryPolicy.ExecuteAsync(async () =>
                await _httpClient.GetAsync("/rest_v2/serverInfo"));
            
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            var serverInfo = JsonConvert.DeserializeObject<JasperServerInfo>(content) ?? new JasperServerInfo();

            _logger.LogInformation("Retrieved JasperReports Server info: Version {Version}, Edition {Edition}", 
                serverInfo.Version, serverInfo.Edition);

            return serverInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving JasperReports Server information");
            return new JasperServerInfo { Version = "Unknown", Edition = "Unknown", Build = "Unknown" };
        }
    }
}

// Helper classes for JasperReports API responses
internal class JasperResourceList
{
    public List<JasperResource>? ResourceLookup { get; set; }
}

internal class JasperResource
{
    public string Uri { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class JasperServerInfo
{
    public string Version { get; set; } = string.Empty;
    public string Edition { get; set; } = string.Empty;
    public string Build { get; set; } = string.Empty;
    public string LicenseType { get; set; } = string.Empty;
    public bool IsExpired { get; set; }
    public string Features { get; set; } = string.Empty;
}