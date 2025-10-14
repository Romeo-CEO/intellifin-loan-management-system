using IntelliFin.FinancialService.Exceptions;
using IntelliFin.FinancialService.Models;
using IntelliFin.Shared.Audit;
using IntelliFin.Shared.DomainModels.Repositories;
using IntelliFin.Shared.DomainModels.Entities;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace IntelliFin.FinancialService.Services;

public class GeneralLedgerService : IGeneralLedgerService
{
    private readonly ILogger<GeneralLedgerService> _logger;
    private readonly IGLAccountRepository _accountRepository;
    private readonly IGLEntryRepository _entryRepository;
    private readonly IAuditClient _auditClient;



    public GeneralLedgerService(
        ILogger<GeneralLedgerService> logger,
        IGLAccountRepository accountRepository,
        IGLEntryRepository entryRepository,
        IAuditClient auditClient)
    {
        _logger = logger;
        _accountRepository = accountRepository;
        _entryRepository = entryRepository;
        _auditClient = auditClient;
    }

    // Mapping methods between service models and domain entities
    private static IntelliFin.FinancialService.Models.GLAccount MapToServiceModel(IntelliFin.Shared.DomainModels.Entities.GLAccount domainAccount)
    {
        return new IntelliFin.FinancialService.Models.GLAccount
        {
            Id = int.Parse(domainAccount.AccountCode), // Use AccountCode as the service model ID
            Code = domainAccount.AccountCode,
            Name = domainAccount.Name,
            Type = MapAccountType(domainAccount.Category),
            IsActive = domainAccount.IsActive,
            Balance = domainAccount.CurrentBalance,
            IsContraAccount = domainAccount.IsContraAccount,
            CreatedAt = domainAccount.CreatedAt,
            LastModified = domainAccount.LastModified
        };
    }

    private static AccountType MapAccountType(string category)
    {
        return category.ToLower() switch
        {
            "asset" => AccountType.Asset,
            "liability" => AccountType.Liability,
            "equity" => AccountType.Equity,
            "income" => AccountType.Income,
            "expense" => AccountType.Expense,
            _ => AccountType.Asset
        };
    }

    private static JournalEntry MapToServiceJournalEntry(IntelliFin.Shared.DomainModels.Entities.GLEntry domainEntry)
    {
        // For simplicity, we'll map the first debit and credit lines
        var debitLine = domainEntry.Lines.FirstOrDefault(l => l.DebitAmount > 0);
        var creditLine = domainEntry.Lines.FirstOrDefault(l => l.CreditAmount > 0);

        return new JournalEntry
        {
            Id = int.Parse(domainEntry.EntryNumber),
            DebitAccountId = debitLine != null ? int.Parse(debitLine.GLAccount?.AccountCode ?? "0") : 0,
            CreditAccountId = creditLine != null ? int.Parse(creditLine.GLAccount?.AccountCode ?? "0") : 0,
            Amount = domainEntry.TotalAmount,
            Currency = "ZMW",
            ValueDate = domainEntry.TransactionDate,
            TransactionDate = domainEntry.TransactionDate,
            Reference = domainEntry.Reference,
            Description = domainEntry.Description,
            BatchId = domainEntry.BatchId,
            CreatedAt = domainEntry.CreatedAt,
            CreatedBy = domainEntry.CreatedBy
        };
    }

    public async Task<decimal> GetAccountBalanceAsync(int accountId, DateTime? asOfDate = null)
    {
        try
        {
            _logger.LogInformation("Getting balance for account {AccountId} as of {AsOfDate}", accountId, asOfDate);

            // Get account by code (using accountId as the account code)
            var domainAccount = await _accountRepository.GetByCodeAsync(accountId.ToString());
            if (domainAccount == null)
            {
                throw new KeyNotFoundException($"Account {accountId} not found");
            }

            // If no specific date requested, return current balance
            if (asOfDate == null)
            {
                return domainAccount.CurrentBalance;
            }

            // Calculate balance as of specific date by processing journal entries
            var relevantEntries = await _entryRepository.GetByAccountIdAsync(domainAccount.Id, null, asOfDate);

            var accountType = MapAccountType(domainAccount.Category);
            var balance = 0m;

            foreach (var entry in relevantEntries)
            {
                foreach (var line in entry.Lines.Where(l => l.GLAccountId == domainAccount.Id))
                {
                    // Debit increases assets/expenses, decreases liabilities/equity/income
                    if (line.DebitAmount > 0)
                    {
                        balance += accountType == AccountType.Asset || accountType == AccountType.Expense
                            ? line.DebitAmount : -line.DebitAmount;
                    }

                    // Credit decreases assets/expenses, increases liabilities/equity/income
                    if (line.CreditAmount > 0)
                    {
                        balance += accountType == AccountType.Asset || accountType == AccountType.Expense
                            ? -line.CreditAmount : line.CreditAmount;
                    }
                }
            }

            return balance;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting balance for account {AccountId}", accountId);
            throw;
        }
    }

    public async Task<JournalEntryResult> PostJournalEntryAsync(CreateJournalEntryRequest request)
    {
        try
        {
            _logger.LogInformation("Posting journal entry: Debit {DebitAccount}, Credit {CreditAccount}, Amount {Amount}",
                request.DebitAccountId, request.CreditAccountId, request.Amount);

            // Validate the journal entry
            var isValid = await ValidateJournalEntryAsync(request);
            if (!isValid)
            {
                return new JournalEntryResult
                {
                    Success = false,
                    Message = "Journal entry validation failed",
                    Errors = new List<string> { "Invalid account IDs or amount" }
                };
            }

            // Get the accounts
            var debitAccount = await _accountRepository.GetByCodeAsync(request.DebitAccountId.ToString());
            var creditAccount = await _accountRepository.GetByCodeAsync(request.CreditAccountId.ToString());

            if (debitAccount == null || creditAccount == null)
            {
                return new JournalEntryResult
                {
                    Success = false,
                    Message = "One or more accounts not found",
                    Errors = new List<string> { "Invalid account IDs" }
                };
            }

            // Generate entry number
            var entryNumber = await _entryRepository.GenerateEntryNumberAsync();

            // Create journal entry (BoZ requirement)
            var journalEntry = new IntelliFin.Shared.DomainModels.Entities.GLEntry
            {
                Id = Guid.NewGuid(),
                EntryNumber = entryNumber,
                TransactionDate = DateTime.UtcNow,
                Description = request.Description,
                Reference = request.Reference ?? GenerateReference(),
                TotalAmount = request.Amount,
                Status = "Posted",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = request.CreatedBy ?? "system",
                BatchId = request.BatchId ?? string.Empty,
                Lines = new List<GLEntryLine>
                {
                    // Debit line
                    new GLEntryLine
                    {
                        Id = Guid.NewGuid(),
                        GLAccountId = debitAccount.Id,
                        DebitAmount = request.Amount,
                        CreditAmount = 0,
                        Description = request.Description,
                        Reference = request.Reference ?? GenerateReference(),
                        CreatedAt = DateTime.UtcNow
                    },
                    // Credit line
                    new GLEntryLine
                    {
                        Id = Guid.NewGuid(),
                        GLAccountId = creditAccount.Id,
                        DebitAmount = 0,
                        CreditAmount = request.Amount,
                        Description = request.Description,
                        Reference = request.Reference ?? GenerateReference(),
                        CreatedAt = DateTime.UtcNow
                    }
                }
            };

            // Save the journal entry to database
            var savedEntry = await _entryRepository.CreateAsync(journalEntry);

            // Update account balances
            var debitAccountType = MapAccountType(debitAccount.Category);
            var creditAccountType = MapAccountType(creditAccount.Category);

            var debitBalanceChange = debitAccountType == AccountType.Asset || debitAccountType == AccountType.Expense
                ? request.Amount : -request.Amount;
            var creditBalanceChange = creditAccountType == AccountType.Asset || creditAccountType == AccountType.Expense
                ? -request.Amount : request.Amount;

            await _accountRepository.UpdateBalanceAsync(debitAccount.Id, debitAccount.CurrentBalance + debitBalanceChange);
            await _accountRepository.UpdateBalanceAsync(creditAccount.Id, creditAccount.CurrentBalance + creditBalanceChange);

            await ForwardAuditAsync(
                request.CreatedBy ?? "system",
                "JournalEntryPosted",
                "JournalEntry",
                savedEntry.EntryNumber,
                new
                {
                    request.DebitAccountId,
                    request.CreditAccountId,
                    request.Amount,
                    savedEntry.Reference,
                    savedEntry.CreatedAt
                });

            _logger.LogInformation("Journal entry {JournalEntryId} posted successfully", savedEntry.Id);

            return new JournalEntryResult
            {
                Success = true,
                JournalEntryId = int.Parse(savedEntry.EntryNumber), // Use entry number as the service model ID
                Message = "Journal entry posted successfully",
                Reference = savedEntry.Reference,
                PostedAt = savedEntry.CreatedAt
            };
        }
        catch (AuditForwardingException ex)
        {
            _logger.LogError(ex, "Audit forwarding failed for journal entry");
            return new JournalEntryResult
            {
                Success = false,
                Message = "Audit forwarding to Admin Service failed",
                Errors = new List<string> { ex.Message }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting journal entry");
            return new JournalEntryResult
            {
                Success = false,
                Message = "An error occurred while posting the journal entry",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<IEnumerable<IntelliFin.FinancialService.Models.GLAccount>> GetAccountsAsync()
    {
        _logger.LogInformation("Getting all GL accounts");

        var domainAccounts = await _accountRepository.GetActiveByCategoryAsync("Asset");
        var liabilityAccounts = await _accountRepository.GetActiveByCategoryAsync("Liability");
        var equityAccounts = await _accountRepository.GetActiveByCategoryAsync("Equity");
        var incomeAccounts = await _accountRepository.GetActiveByCategoryAsync("Income");
        var expenseAccounts = await _accountRepository.GetActiveByCategoryAsync("Expense");

        var allAccounts = domainAccounts
            .Concat(liabilityAccounts)
            .Concat(equityAccounts)
            .Concat(incomeAccounts)
            .Concat(expenseAccounts);

        return allAccounts.Select(MapToServiceModel);
    }

    public async Task<IntelliFin.FinancialService.Models.GLAccount?> GetAccountAsync(int accountId)
    {
        _logger.LogInformation("Getting GL account {AccountId}", accountId);

        var domainAccount = await _accountRepository.GetByCodeAsync(accountId.ToString());
        if (domainAccount == null)
        {
            return null;
        }

        return MapToServiceModel(domainAccount);
    }

    public async Task<IEnumerable<JournalEntry>> GetJournalEntriesAsync(int accountId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        _logger.LogInformation("Getting journal entries for account {AccountId} from {FromDate} to {ToDate}",
            accountId, fromDate, toDate);

        // Get the account by code
        var domainAccount = await _accountRepository.GetByCodeAsync(accountId.ToString());
        if (domainAccount == null)
        {
            return new List<JournalEntry>();
        }

        // Get journal entries for the account
        var domainEntries = await _entryRepository.GetByAccountIdAsync(domainAccount.Id, fromDate, toDate);

        return domainEntries.Select(MapToServiceJournalEntry);
    }

    public async Task<TrialBalanceReport> GenerateTrialBalanceAsync(DateTime asOfDate)
    {
        _logger.LogInformation("Generating trial balance as of {AsOfDate}", asOfDate);

        // Get all active accounts
        var allAccounts = await GetAccountsAsync();
        var trialBalanceItems = new List<TrialBalanceItem>();

        decimal totalDebits = 0;
        decimal totalCredits = 0;

        foreach (var account in allAccounts)
        {
            var balance = await GetAccountBalanceAsync(account.Id, asOfDate);

            var item = new TrialBalanceItem
            {
                AccountCode = account.Code,
                AccountName = account.Name
            };

            // For trial balance, we show the natural balance side
            if (account.Type == AccountType.Asset || account.Type == AccountType.Expense)
            {
                if (balance >= 0)
                {
                    item.DebitBalance = balance;
                    totalDebits += balance;
                }
                else
                {
                    item.CreditBalance = Math.Abs(balance);
                    totalCredits += Math.Abs(balance);
                }
            }
            else // Liability, Equity, Income
            {
                if (balance >= 0)
                {
                    item.CreditBalance = balance;
                    totalCredits += balance;
                }
                else
                {
                    item.DebitBalance = Math.Abs(balance);
                    totalDebits += Math.Abs(balance);
                }
            }

            trialBalanceItems.Add(item);
        }

        return new TrialBalanceReport
        {
            AsOfDate = asOfDate,
            Items = trialBalanceItems,
            TotalDebits = totalDebits,
            TotalCredits = totalCredits
        };
    }

    public async Task<BoZReport> GenerateBoZReportAsync(DateTime reportDate)
    {
        _logger.LogInformation("Generating BoZ report for {ReportDate}", reportDate);

        // Get all accounts and calculate totals by category
        var allAccounts = await GetAccountsAsync();
        var balances = new Dictionary<string, decimal>();

        decimal totalAssets = 0;
        decimal totalLiabilities = 0;
        decimal totalEquity = 0;
        decimal totalIncome = 0;
        decimal totalExpenses = 0;

        foreach (var account in allAccounts)
        {
            var balance = await GetAccountBalanceAsync(account.Id, reportDate);

            switch (account.Type)
            {
                case AccountType.Asset:
                    totalAssets += balance;
                    break;
                case AccountType.Liability:
                    totalLiabilities += balance;
                    break;
                case AccountType.Equity:
                    totalEquity += balance;
                    break;
                case AccountType.Income:
                    totalIncome += balance;
                    break;
                case AccountType.Expense:
                    totalExpenses += balance;
                    break;
            }
        }

        balances["Total Assets"] = totalAssets;
        balances["Total Liabilities"] = totalLiabilities;
        balances["Total Equity"] = totalEquity;
        balances["Total Income"] = totalIncome;
        balances["Total Expenses"] = totalExpenses;
        balances["Net Income"] = totalIncome - totalExpenses;

        // Calculate key ratios for compliance
        var complianceNotes = new List<string>();
        if (totalAssets > 0)
        {
            var equityRatio = totalEquity / totalAssets;
            complianceNotes.Add($"Equity Ratio: {equityRatio:P2}");

            if (equityRatio >= 0.10m)
                complianceNotes.Add("Capital adequacy ratio meets BoZ minimum requirements");
            else
                complianceNotes.Add("WARNING: Capital adequacy ratio below BoZ minimum requirements");
        }

        return new BoZReport
        {
            ReportDate = reportDate,
            ReportType = "Prudential Report",
            Balances = balances,
            ComplianceNotes = complianceNotes
        };
    }

    private async Task ForwardAuditAsync(string actor, string action, string entityType, string entityId, object eventData)
    {
        var payload = new AuditEventPayload
        {
            Actor = string.IsNullOrWhiteSpace(actor) ? "system" : actor,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            EventData = eventData,
            Timestamp = DateTime.UtcNow
        };

        try
        {
            await _auditClient.LogEventAsync(payload);
        }
        catch (Exception ex)
        {
            throw new AuditForwardingException("Failed to forward audit event to Admin Service", ex);
        }
    }

    public async Task<bool> ValidateJournalEntryAsync(CreateJournalEntryRequest request)
    {
        try
        {
            _logger.LogInformation("Validating journal entry");
            
            // Basic validation
            if (request.Amount <= 0)
            {
                _logger.LogWarning("Invalid amount: {Amount}", request.Amount);
                return false;
            }
            
            if (request.DebitAccountId == request.CreditAccountId)
            {
                _logger.LogWarning("Debit and credit accounts cannot be the same");
                return false;
            }
            
            if (string.IsNullOrWhiteSpace(request.Description))
            {
                _logger.LogWarning("Description is required");
                return false;
            }

            // Validate account existence and status
            var debitAccount = await _accountRepository.GetByCodeAsync(request.DebitAccountId.ToString());
            if (debitAccount == null || !debitAccount.IsActive)
            {
                _logger.LogWarning("Invalid or inactive debit account: {AccountId}", request.DebitAccountId);
                return false;
            }

            var creditAccount = await _accountRepository.GetByCodeAsync(request.CreditAccountId.ToString());
            if (creditAccount == null || !creditAccount.IsActive)
            {
                _logger.LogWarning("Invalid or inactive credit account: {AccountId}", request.CreditAccountId);
                return false;
            }

            // BoZ business rules validation
            if (!await ValidateBoZBusinessRules(MapToServiceModel(debitAccount), MapToServiceModel(creditAccount), request.Amount))
            {
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating journal entry");
            return false;
        }
    }

    private async Task<bool> ValidateBoZBusinessRules(IntelliFin.FinancialService.Models.GLAccount debitAccount, IntelliFin.FinancialService.Models.GLAccount creditAccount, decimal amount)
    {
        try
        {
            // BoZ Rule 1: Large transactions require additional approval
            if (amount > 100000m)
            {
                _logger.LogInformation("Large transaction detected: {Amount}. Additional approval may be required.", amount);
                // In production, this would trigger approval workflow
            }

            // BoZ Rule 2: Cash transactions above certain limit require documentation
            if ((debitAccount.Code == "1001" || creditAccount.Code == "1001") && amount > 50000m)
            {
                _logger.LogInformation("Large cash transaction: {Amount}. Documentation required.", amount);
            }

            // BoZ Rule 3: Loan loss provision rules
            if (debitAccount.Code == "5002" || creditAccount.Code == "5002")
            {
                _logger.LogInformation("Loan loss provision transaction. Ensuring BoZ compliance.");
                // Additional provisioning validations would go here
            }

            await Task.Delay(1);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating BoZ business rules");
            return false;
        }
    }

    private string GenerateReference()
    {
        return $"JE{DateTime.UtcNow:yyyyMMddHHmmss}{Guid.NewGuid().ToString("N")[..4]}";
    }
}
