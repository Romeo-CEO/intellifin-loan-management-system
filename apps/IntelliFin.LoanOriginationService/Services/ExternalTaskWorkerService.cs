using IntelliFin.LoanOriginationService.Models;
using IntelliFin.Shared.DomainModels.Repositories;
using System.Text.Json;
using System.Text;

namespace IntelliFin.LoanOriginationService.Services;

public class ExternalTaskWorkerService : BackgroundService
{
    private readonly ILogger<ExternalTaskWorkerService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly string _camundaBaseUrl;
    private readonly string _workerId;

    public ExternalTaskWorkerService(
        ILogger<ExternalTaskWorkerService> logger,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        HttpClient httpClient)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _httpClient = httpClient;
        _camundaBaseUrl = _configuration.GetValue<string>("Camunda:BaseUrl") ?? "http://localhost:8080/engine-rest";
        _workerId = _configuration.GetValue<string>("Camunda:WorkerId") ?? "loan-origination-worker";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for the application to fully start
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        _logger.LogInformation("Starting external task workers for Camunda at {CamundaBaseUrl}", _camundaBaseUrl);

        // Define the topics we want to handle
        var topics = new[]
        {
            "initial-validation",
            "verify-pmec", 
            "check-credit-history",
            "notify-decision"
        };

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                foreach (var topic in topics)
                {
                    await FetchAndExecuteTask(topic, stoppingToken);
                }
                
                // Wait before next polling cycle
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("External task worker service is shutting down");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in external task worker service");
        }
    }

    private async Task FetchAndExecuteTask(string topicName, CancellationToken cancellationToken)
    {
        try
        {
            var fetchRequest = new
            {
                workerId = _workerId,
                maxTasks = 1,
                usePriority = true,
                topics = new[]
                {
                    new
                    {
                        topicName,
                        lockDuration = 60000, // 1 minute
                        variables = new[] { "applicationId", "loanAmount", "productType", "creditScore" }
                    }
                }
            };

            var json = JsonSerializer.Serialize(fetchRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{_camundaBaseUrl}/external-task/fetchAndLock", content, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                return; // No tasks available or error
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var tasks = JsonSerializer.Deserialize<ExternalTask[]>(responseContent);

            if (tasks?.Length > 0)
            {
                var task = tasks[0];
                _logger.LogInformation("Fetched external task {TaskId} for topic {TopicName}", task.Id, topicName);
                await HandleTask(task, topicName, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching task for topic {TopicName}", topicName);
        }
    }

    private async Task HandleTask(ExternalTask task, string topicName, CancellationToken cancellationToken)
    {
        try
        {
            switch (topicName)
            {
                case "initial-validation":
                    await HandleInitialValidation(task, cancellationToken);
                    break;
                case "verify-pmec":
                    await HandlePmecVerification(task, cancellationToken);
                    break;
                case "check-credit-history":
                    await HandleCreditHistoryCheck(task, cancellationToken);
                    break;
                case "notify-decision":
                    await HandleNotifyDecision(task, cancellationToken);
                    break;
                default:
                    _logger.LogWarning("Unknown topic {TopicName}", topicName);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling task {TaskId}", task.Id);
            await HandleTaskFailure(task.Id, "PROCESSING_ERROR", ex.Message, cancellationToken);
        }
    }

    private async Task HandleInitialValidation(ExternalTask task, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ExternalTaskWorkerService>>();
        var loanApplicationService = scope.ServiceProvider.GetRequiredService<ILoanApplicationService>();

        try
        {
            var applicationId = GetVariableValue(task.Variables, "applicationId");
            if (!Guid.TryParse(applicationId, out var appId))
            {
                await HandleTaskFailure(task.Id, "INVALID_APPLICATION_ID", "Invalid application ID", cancellationToken);
                return;
            }

            logger.LogInformation("Processing initial validation for application {ApplicationId}", appId);

            var validationResult = await loanApplicationService.ValidateApplicationAsync(appId, cancellationToken);
            
            var variables = new Dictionary<string, object>
            {
                ["validationPassed"] = validationResult.IsValid,
                ["validationErrors"] = validationResult.Errors?.Select(e => e.Message).ToArray() ?? Array.Empty<string>(),
                ["validationCompletedAt"] = DateTime.UtcNow.ToString("O")
            };

            await CompleteTask(task.Id, variables, cancellationToken);
            logger.LogInformation("Initial validation completed for application {ApplicationId}", appId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing initial validation");
            await HandleTaskFailure(task.Id, "VALIDATION_ERROR", ex.Message, cancellationToken);
        }
    }

    private async Task HandlePmecVerification(ExternalTask task, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ExternalTaskWorkerService>>();

        try
        {
            var applicationId = GetVariableValue(task.Variables, "applicationId");
            logger.LogInformation("Processing PMEC verification for application {ApplicationId}", applicationId);

            // TODO: Implement actual PMEC service integration
            // For now, simulate the verification process
            await Task.Delay(1000, cancellationToken);

            var variables = new Dictionary<string, object>
            {
                ["pmecVerified"] = true,
                ["pmecEmployeeId"] = "EMP-12345",
                ["pmecVerificationCompletedAt"] = DateTime.UtcNow.ToString("O")
            };

            await CompleteTask(task.Id, variables, cancellationToken);
            logger.LogInformation("PMEC verification completed for application {ApplicationId}", applicationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PMEC verification");
            await HandleTaskFailure(task.Id, "PMEC_ERROR", ex.Message, cancellationToken);
        }
    }

    private async Task HandleCreditHistoryCheck(ExternalTask task, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ExternalTaskWorkerService>>();
        var creditAssessmentService = scope.ServiceProvider.GetRequiredService<ICreditAssessmentService>();

        try
        {
            var applicationId = GetVariableValue(task.Variables, "applicationId");
            if (!Guid.TryParse(applicationId, out var appId))
            {
                await HandleTaskFailure(task.Id, "INVALID_APPLICATION_ID", "Invalid application ID", cancellationToken);
                return;
            }

            logger.LogInformation("Processing credit history check for application {ApplicationId}", appId);

            var applicationRepository = scope.ServiceProvider.GetRequiredService<ILoanApplicationRepository>();
            var application = await applicationRepository.GetByIdAsync(appId, cancellationToken);
            
            if (application == null)
            {
                await HandleTaskFailure(task.Id, "APPLICATION_NOT_FOUND", $"Application {appId} not found", cancellationToken);
                return;
            }

            var creditBureauData = await creditAssessmentService.GetCreditBureauDataAsync(application.ClientId, cancellationToken);

            var variables = new Dictionary<string, object>
            {
                ["creditHistoryChecked"] = true,
                ["hasCreditHistory"] = creditBureauData != null,
                ["creditScore"] = creditBureauData?.CreditScore ?? 0,
                ["creditHistoryCheckCompletedAt"] = DateTime.UtcNow.ToString("O")
            };

            await CompleteTask(task.Id, variables, cancellationToken);
            logger.LogInformation("Credit history check completed for application {ApplicationId}", appId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing credit history check");
            await HandleTaskFailure(task.Id, "CREDIT_CHECK_ERROR", ex.Message, cancellationToken);
        }
    }

    private async Task HandleNotifyDecision(ExternalTask task, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ExternalTaskWorkerService>>();

        try
        {
            var applicationId = GetVariableValue(task.Variables, "applicationId");
            logger.LogInformation("Processing decision notification for application {ApplicationId}", applicationId);

            // TODO: Implement communications service integration
            await Task.Delay(500, cancellationToken);

            var variables = new Dictionary<string, object>
            {
                ["notificationSent"] = true,
                ["notificationSentAt"] = DateTime.UtcNow.ToString("O")
            };

            await CompleteTask(task.Id, variables, cancellationToken);
            logger.LogInformation("Decision notification sent for application {ApplicationId}", applicationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing decision notification");
            await HandleTaskFailure(task.Id, "NOTIFICATION_ERROR", ex.Message, cancellationToken);
        }
    }

    private async Task CompleteTask(string taskId, Dictionary<string, object> variables, CancellationToken cancellationToken)
    {
        try
        {
            var completeRequest = new
            {
                workerId = _workerId,
                variables = variables.ToDictionary(
                    kvp => kvp.Key, 
                    kvp => new { value = kvp.Value, type = GetVariableType(kvp.Value) })
            };

            var json = JsonSerializer.Serialize(completeRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{_camundaBaseUrl}/external-task/{taskId}/complete", content, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to complete task {TaskId}. Response: {ErrorContent}", taskId, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing task {TaskId}", taskId);
        }
    }

    private async Task HandleTaskFailure(string taskId, string errorCode, string errorMessage, CancellationToken cancellationToken)
    {
        try
        {
            var failureRequest = new
            {
                workerId = _workerId,
                errorMessage,
                errorCode,
                retries = 3,
                retryTimeout = 10000
            };

            var json = JsonSerializer.Serialize(failureRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            await _httpClient.PostAsync($"{_camundaBaseUrl}/external-task/{taskId}/failure", content, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling task failure for {TaskId}", taskId);
        }
    }

    private static string GetVariableValue(Dictionary<string, ExternalTaskVariable>? variables, string key)
    {
        return variables?.TryGetValue(key, out var variable) == true ? variable.Value?.ToString() ?? "" : "";
    }

    private static string GetVariableType(object value)
    {
        return value switch
        {
            string => "String",
            int or long => "Long", 
            double or decimal => "Double",
            bool => "Boolean",
            DateTime => "Date",
            _ => "String"
        };
    }
}

public class ExternalTask
{
    public string Id { get; set; } = "";
    public string TopicName { get; set; } = "";
    public string WorkerId { get; set; } = "";
    public long LockExpirationTime { get; set; }
    public string ProcessInstanceId { get; set; } = "";
    public string ExecutionId { get; set; } = "";
    public Dictionary<string, ExternalTaskVariable>? Variables { get; set; }
}

public class ExternalTaskVariable
{
    public object? Value { get; set; }
    public string Type { get; set; } = "";
}