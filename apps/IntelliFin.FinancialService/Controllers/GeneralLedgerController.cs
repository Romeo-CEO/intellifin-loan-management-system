using IntelliFin.FinancialService.Models;
using IntelliFin.FinancialService.Services;
using Microsoft.AspNetCore.Mvc;

namespace IntelliFin.FinancialService.Controllers;

[ApiController]
[Route("api/gl")]
public class GeneralLedgerController : ControllerBase
{
    private readonly IGeneralLedgerService _generalLedgerService;
    private readonly ILogger<GeneralLedgerController> _logger;

    public GeneralLedgerController(IGeneralLedgerService generalLedgerService, ILogger<GeneralLedgerController> logger)
    {
        _generalLedgerService = generalLedgerService;
        _logger = logger;
    }

    /// <summary>
    /// Get account balance
    /// </summary>
    [HttpGet("accounts/{accountId}/balance")]
    public async Task<ActionResult<decimal>> GetAccountBalance(int accountId, [FromQuery] DateTime? asOfDate = null)
    {
        try
        {
            var balance = await _generalLedgerService.GetAccountBalanceAsync(accountId, asOfDate);
            return Ok(balance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting balance for account {AccountId}", accountId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get all GL accounts
    /// </summary>
    [HttpGet("accounts")]
    public async Task<ActionResult<IEnumerable<GLAccount>>> GetAccounts()
    {
        try
        {
            var accounts = await _generalLedgerService.GetAccountsAsync();
            return Ok(accounts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting GL accounts");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get specific GL account
    /// </summary>
    [HttpGet("accounts/{accountId}")]
    public async Task<ActionResult<GLAccount>> GetAccount(int accountId)
    {
        try
        {
            var account = await _generalLedgerService.GetAccountAsync(accountId);
            if (account == null)
            {
                return NotFound($"Account {accountId} not found");
            }
            return Ok(account);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting account {AccountId}", accountId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Post journal entry
    /// </summary>
    [HttpPost("journal-entries")]
    public async Task<ActionResult<JournalEntryResult>> PostJournalEntry([FromBody] CreateJournalEntryRequest request)
    {
        try
        {
            var result = await _generalLedgerService.PostJournalEntryAsync(request);
            if (result.Success)
            {
                return Ok(result);
            }
            return BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting journal entry");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get journal entries for an account
    /// </summary>
    [HttpGet("accounts/{accountId}/journal-entries")]
    public async Task<ActionResult<IEnumerable<JournalEntry>>> GetJournalEntries(
        int accountId, 
        [FromQuery] DateTime? fromDate = null, 
        [FromQuery] DateTime? toDate = null)
    {
        try
        {
            var entries = await _generalLedgerService.GetJournalEntriesAsync(accountId, fromDate, toDate);
            return Ok(entries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting journal entries for account {AccountId}", accountId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Generate trial balance report
    /// </summary>
    [HttpGet("reports/trial-balance")]
    public async Task<ActionResult<TrialBalanceReport>> GenerateTrialBalance([FromQuery] DateTime asOfDate)
    {
        try
        {
            var report = await _generalLedgerService.GenerateTrialBalanceAsync(asOfDate);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating trial balance for {AsOfDate}", asOfDate);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Generate BoZ compliance report
    /// </summary>
    [HttpGet("reports/boz")]
    public async Task<ActionResult<BoZReport>> GenerateBoZReport([FromQuery] DateTime reportDate)
    {
        try
        {
            var report = await _generalLedgerService.GenerateBoZReportAsync(reportDate);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating BoZ report for {ReportDate}", reportDate);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Validate journal entry
    /// </summary>
    [HttpPost("journal-entries/validate")]
    public async Task<ActionResult<bool>> ValidateJournalEntry([FromBody] CreateJournalEntryRequest request)
    {
        try
        {
            var isValid = await _generalLedgerService.ValidateJournalEntryAsync(request);
            return Ok(isValid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating journal entry");
            return StatusCode(500, "Internal server error");
        }
    }
}
