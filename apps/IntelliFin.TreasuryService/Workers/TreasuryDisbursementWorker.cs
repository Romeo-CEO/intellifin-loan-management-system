using IntelliFin.TreasuryService.Events;
using IntelliFin.TreasuryService.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace IntelliFin.TreasuryService.Workers;

/// <summary>
/// Worker for handling treasury disbursement service tasks
/// TODO: Integrate with Camunda BPMN workflow engine when available
/// </summary>
public class TreasuryDisbursementWorker
{
    private readonly ITreasuryService _treasuryService;
    private readonly ILoanDisbursementService _disbursementService;
    private readonly ILogger<TreasuryDisbursementWorker> _logger;

    public TreasuryDisbursementWorker(
        ITreasuryService treasuryService,
        ILoanDisbursementService disbursementService,
        ILogger<TreasuryDisbursementWorker> logger)
    {
        _treasuryService = treasuryService;
        _disbursementService = disbursementService;
        _logger = logger;
    }

    public async Task HandleDisbursementTaskAsync(string taskType, Dictionary<string, object> variables)
    {
        var correlationId = variables["correlationId"]?.ToString() ?? Guid.NewGuid().ToString();

        _logger.LogInformation(
            "Processing treasury disbursement task: Type={TaskType}, CorrelationId={CorrelationId}",
            taskType, correlationId);

        try
        {
            switch (taskType)
            {
                case "validate-disbursement":
                    await HandleValidateDisbursement(variables, correlationId);
                    break;

                case "check-funding":
                    await HandleCheckFunding(variables, correlationId);
                    break;

                case "execute-disbursement":
                    await HandleExecuteDisbursement(variables, correlationId);
                    break;

                case "update-records":
                    await HandleUpdateRecords(variables, correlationId);
                    break;

                default:
                    _logger.LogWarning("Unknown task type: {TaskType}", taskType);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing treasury disbursement task: {TaskType}", taskType);
            throw; // Re-throw for caller to handle
        }
    }

    private async Task HandleValidateDisbursement(Dictionary<string, object> variables, string correlationId)
    {
        // Extract disbursement data from variables
        var disbursementId = variables["disbursementId"]?.ToString();
        var loanId = variables["loanId"]?.ToString();
        var clientId = variables["clientId"]?.ToString();
        var amount = decimal.Parse(variables["amount"]?.ToString() ?? "0");
        var bankAccount = variables["bankAccountNumber"]?.ToString();
        var bankCode = variables["bankCode"]?.ToString();

        _logger.LogInformation(
            "Validating disbursement: DisbursementId={DisbursementId}, LoanId={LoanId}, Amount={Amount}",
            disbursementId, loanId, amount);

        // Basic validation
        if (string.IsNullOrEmpty(disbursementId) || string.IsNullOrEmpty(loanId) ||
            string.IsNullOrEmpty(clientId) || amount <= 0)
        {
            _logger.LogWarning("Invalid disbursement data provided: DisbursementId={DisbursementId}", disbursementId);
            return;
        }

        // Check for existing disbursement
        var existingDisbursement = await _disbursementService.GetByDisbursementIdAsync(Guid.Parse(disbursementId));
        if (existingDisbursement != null)
        {
            _logger.LogWarning("Disbursement already exists - duplicate detected: DisbursementId={DisbursementId}", disbursementId);
            return;
        }

        _logger.LogInformation("Disbursement validation passed: DisbursementId={DisbursementId}", disbursementId);
    }

    private async Task HandleCheckFunding(Dictionary<string, object> variables, string correlationId)
    {
        var disbursementId = variables["disbursementId"]?.ToString();
        var amount = decimal.Parse(variables["amount"]?.ToString() ?? "0");
        var bankAccount = variables["bankAccountNumber"]?.ToString();

        _logger.LogInformation(
            "Checking funding for disbursement: DisbursementId={DisbursementId}, Amount={Amount}",
            disbursementId, amount);

        // TODO: Implement actual funding source validation
        // For now, assume funding is available
        bool fundingAvailable = true;

        _logger.LogInformation(
            "Funding check completed: DisbursementId={DisbursementId}, FundingAvailable={FundingAvailable}",
            disbursementId, fundingAvailable);
    }

    private async Task HandleExecuteDisbursement(Dictionary<string, object> variables, string correlationId)
    {
        var disbursementId = variables["disbursementId"]?.ToString();
        var amount = decimal.Parse(variables["amount"]?.ToString() ?? "0");
        var bankAccount = variables["bankAccountNumber"]?.ToString();
        var bankCode = variables["bankCode"]?.ToString();

        _logger.LogInformation(
            "Executing disbursement: DisbursementId={DisbursementId}, Amount={Amount}, BankAccount={BankAccount}",
            disbursementId, amount, bankAccount);

        try
        {
            // Create treasury transaction
            var transaction = await _treasuryService.CreateTransactionAsync(
                "Disbursement",
                amount,
                "MWK",
                correlationId);

            // Update disbursement status
            await _disbursementService.UpdateStatusAsync(
                Guid.Parse(disbursementId),
                "Processed",
                "System");

            _logger.LogInformation(
                "Disbursement executed successfully: DisbursementId={DisbursementId}, TransactionId={TransactionId}",
                disbursementId, transaction.TransactionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute disbursement: {DisbursementId}", disbursementId);
            throw; // Re-throw for caller to handle
        }
    }

    private async Task HandleUpdateRecords(Dictionary<string, object> variables, string correlationId)
    {
        var disbursementId = variables["disbursementId"]?.ToString();
        var executionResult = variables["executionResult"]?.ToString();

        _logger.LogInformation(
            "Updating records for disbursement: DisbursementId={DisbursementId}, Result={ExecutionResult}",
            disbursementId, executionResult);

        // Update final status based on execution result
        if (executionResult == "SUCCESS")
        {
            await _disbursementService.UpdateStatusAsync(
                Guid.Parse(disbursementId),
                "Completed",
                "System");
        }
        else
        {
            await _disbursementService.UpdateStatusAsync(
                Guid.Parse(disbursementId),
                "Failed",
                "System");
        }

        _logger.LogInformation("Records updated for disbursement: DisbursementId={DisbursementId}", disbursementId);
    }
}
