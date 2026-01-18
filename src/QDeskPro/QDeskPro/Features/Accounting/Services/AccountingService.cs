using Microsoft.EntityFrameworkCore;
using QDeskPro.Data;
using QDeskPro.Data.Seed;
using QDeskPro.Domain.Entities;
using QDeskPro.Domain.Enums;
using BankingEntity = QDeskPro.Domain.Entities.Banking;

namespace QDeskPro.Features.Accounting.Services;

/// <summary>
/// Service for managing accounting operations including Chart of Accounts,
/// Journal Entries, and automatic entry generation from transactions.
/// </summary>
public interface IAccountingService
{
    // Chart of Accounts
    Task<List<LedgerAccount>> GetChartOfAccountsAsync(string quarryId);
    Task<LedgerAccount?> GetAccountByIdAsync(string accountId);
    Task<LedgerAccount?> GetAccountByCodeAsync(string quarryId, string accountCode);
    Task<LedgerAccount> CreateAccountAsync(LedgerAccount account);
    Task<LedgerAccount> UpdateAccountAsync(LedgerAccount account);
    Task DeleteAccountAsync(string accountId);

    // Journal Entries
    Task<List<JournalEntry>> GetJournalEntriesAsync(string quarryId, DateTime from, DateTime to);
    Task<JournalEntry?> GetJournalEntryByIdAsync(string entryId);
    Task<JournalEntry> CreateManualJournalEntryAsync(JournalEntry entry, string userId);
    Task<JournalEntry> UpdateJournalEntryAsync(JournalEntry entry, string userId);
    Task DeleteJournalEntryAsync(string entryId);
    Task PostJournalEntryAsync(string entryId, string userId);
    Task UnpostJournalEntryAsync(string entryId, string userId);

    // Auto-generation from transactions
    Task<JournalEntry?> GenerateJournalEntryForSaleAsync(Sale sale, string quarryId);
    Task<JournalEntry?> GenerateJournalEntryForExpenseAsync(Expense expense, string quarryId);
    Task<JournalEntry?> GenerateJournalEntryForBankingAsync(BankingEntity banking, string quarryId);
    Task<JournalEntry?> GenerateJournalEntryForPrepaymentAsync(Prepayment prepayment, string quarryId);
    Task<JournalEntry?> GenerateJournalEntryForCollectionAsync(Sale sale, string quarryId);
    Task RegenerateAllJournalEntriesAsync(string quarryId, DateTime from, DateTime to);

    // Account balances
    Task<double> GetAccountBalanceAsync(string accountId, DateTime asOfDate);
    Task<Dictionary<string, double>> GetAllAccountBalancesAsync(string quarryId, DateTime asOfDate);

    // Accounting periods
    Task<List<AccountingPeriod>> GetAccountingPeriodsAsync(string quarryId, int? fiscalYear = null);
    Task<AccountingPeriod?> GetCurrentPeriodAsync(string quarryId);
    Task ClosePeriodAsync(string periodId, string userId, string? closingNotes = null);
    Task ReopenPeriodAsync(string periodId, string userId);

    // Initialization
    Task InitializeChartOfAccountsAsync(string quarryId);
}

/// <summary>
/// Implementation of the accounting service.
/// </summary>
public class AccountingService : IAccountingService
{
    private readonly AppDbContext _context;
    private readonly ILogger<AccountingService> _logger;

    public AccountingService(AppDbContext context, ILogger<AccountingService> logger)
    {
        _context = context;
        _logger = logger;
    }

    #region Chart of Accounts

    public async Task<List<LedgerAccount>> GetChartOfAccountsAsync(string quarryId)
    {
        return await _context.LedgerAccounts
            .Where(a => a.QId == quarryId && a.IsActive)
            .OrderBy(a => a.DisplayOrder)
            .ThenBy(a => a.AccountCode)
            .ToListAsync();
    }

    public async Task<LedgerAccount?> GetAccountByIdAsync(string accountId)
    {
        return await _context.LedgerAccounts
            .FirstOrDefaultAsync(a => a.Id == accountId && a.IsActive);
    }

    public async Task<LedgerAccount?> GetAccountByCodeAsync(string quarryId, string accountCode)
    {
        return await _context.LedgerAccounts
            .FirstOrDefaultAsync(a => a.QId == quarryId && a.AccountCode == accountCode && a.IsActive);
    }

    public async Task<LedgerAccount> CreateAccountAsync(LedgerAccount account)
    {
        account.Id = Guid.NewGuid().ToString();
        account.DateCreated = DateTime.UtcNow;
        account.IsActive = true;

        _context.LedgerAccounts.Add(account);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created ledger account {AccountCode} - {AccountName} for quarry {QuarryId}",
            account.AccountCode, account.AccountName, account.QId);

        return account;
    }

    public async Task<LedgerAccount> UpdateAccountAsync(LedgerAccount account)
    {
        var existing = await _context.LedgerAccounts.FindAsync(account.Id);
        if (existing == null)
            throw new InvalidOperationException($"Account {account.Id} not found");

        if (existing.IsSystemAccount)
            throw new InvalidOperationException("Cannot modify system accounts");

        existing.AccountName = account.AccountName;
        existing.Description = account.Description;
        existing.DisplayOrder = account.DisplayOrder;
        existing.DateModified = DateTime.UtcNow;
        existing.ModifiedBy = account.ModifiedBy;

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAccountAsync(string accountId)
    {
        var account = await _context.LedgerAccounts.FindAsync(accountId);
        if (account == null)
            throw new InvalidOperationException($"Account {accountId} not found");

        if (account.IsSystemAccount)
            throw new InvalidOperationException("Cannot delete system accounts");

        // Check if account has any journal entry lines
        var hasEntries = await _context.JournalEntryLines.AnyAsync(l => l.LedgerAccountId == accountId);
        if (hasEntries)
            throw new InvalidOperationException("Cannot delete account with existing journal entries");

        account.IsActive = false;
        account.DateModified = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    #endregion

    #region Journal Entries

    public async Task<List<JournalEntry>> GetJournalEntriesAsync(string quarryId, DateTime from, DateTime to)
    {
        return await _context.JournalEntries
            .Include(j => j.Lines)
                .ThenInclude(l => l.LedgerAccount)
            .Where(j => j.QId == quarryId && j.IsActive)
            .Where(j => j.EntryDate >= from && j.EntryDate <= to)
            .OrderByDescending(j => j.EntryDate)
            .ThenByDescending(j => j.DateCreated)
            .ToListAsync();
    }

    public async Task<JournalEntry?> GetJournalEntryByIdAsync(string entryId)
    {
        return await _context.JournalEntries
            .Include(j => j.Lines)
                .ThenInclude(l => l.LedgerAccount)
            .FirstOrDefaultAsync(j => j.Id == entryId && j.IsActive);
    }

    public async Task<JournalEntry> CreateManualJournalEntryAsync(JournalEntry entry, string userId)
    {
        entry.Id = Guid.NewGuid().ToString();
        entry.EntryType = "Manual";
        entry.Reference = await GenerateJournalReferenceAsync(entry.QId!, "ADJ");
        entry.FiscalYear = entry.EntryDate.Year;
        entry.FiscalPeriod = entry.EntryDate.Month;
        entry.DateCreated = DateTime.UtcNow;
        entry.CreatedBy = userId;
        entry.IsActive = true;

        // Calculate totals
        entry.TotalDebit = entry.Lines.Sum(l => l.DebitAmount);
        entry.TotalCredit = entry.Lines.Sum(l => l.CreditAmount);

        if (!entry.IsBalanced)
            throw new InvalidOperationException("Journal entry must be balanced (debits = credits)");

        // Set line IDs and numbers
        int lineNum = 1;
        foreach (var line in entry.Lines)
        {
            line.Id = Guid.NewGuid().ToString();
            line.JournalEntryId = entry.Id;
            line.LineNumber = lineNum++;
            line.DateCreated = DateTime.UtcNow;
            line.CreatedBy = userId;
            line.IsActive = true;
        }

        _context.JournalEntries.Add(entry);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created manual journal entry {Reference} for quarry {QuarryId}",
            entry.Reference, entry.QId);

        return entry;
    }

    public async Task<JournalEntry> UpdateJournalEntryAsync(JournalEntry entry, string userId)
    {
        var existing = await _context.JournalEntries
            .Include(j => j.Lines)
            .FirstOrDefaultAsync(j => j.Id == entry.Id);

        if (existing == null)
            throw new InvalidOperationException($"Journal entry {entry.Id} not found");

        if (existing.IsPosted)
            throw new InvalidOperationException("Cannot modify posted journal entries");

        existing.EntryDate = entry.EntryDate;
        existing.Description = entry.Description;
        existing.FiscalYear = entry.EntryDate.Year;
        existing.FiscalPeriod = entry.EntryDate.Month;
        existing.DateModified = DateTime.UtcNow;
        existing.ModifiedBy = userId;

        // Remove old lines and add new ones
        _context.JournalEntryLines.RemoveRange(existing.Lines);

        int lineNum = 1;
        foreach (var line in entry.Lines)
        {
            line.Id = Guid.NewGuid().ToString();
            line.JournalEntryId = entry.Id;
            line.LineNumber = lineNum++;
            line.DateCreated = DateTime.UtcNow;
            line.CreatedBy = userId;
            line.IsActive = true;
            _context.JournalEntryLines.Add(line);
        }

        existing.TotalDebit = entry.Lines.Sum(l => l.DebitAmount);
        existing.TotalCredit = entry.Lines.Sum(l => l.CreditAmount);

        if (!existing.IsBalanced)
            throw new InvalidOperationException("Journal entry must be balanced (debits = credits)");

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteJournalEntryAsync(string entryId)
    {
        var entry = await _context.JournalEntries.FindAsync(entryId);
        if (entry == null)
            throw new InvalidOperationException($"Journal entry {entryId} not found");

        if (entry.IsPosted)
            throw new InvalidOperationException("Cannot delete posted journal entries");

        if (entry.EntryType == "Auto")
            throw new InvalidOperationException("Cannot delete auto-generated journal entries");

        entry.IsActive = false;
        entry.DateModified = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task PostJournalEntryAsync(string entryId, string userId)
    {
        var entry = await _context.JournalEntries.FindAsync(entryId);
        if (entry == null)
            throw new InvalidOperationException($"Journal entry {entryId} not found");

        if (entry.IsPosted)
            throw new InvalidOperationException("Journal entry is already posted");

        entry.IsPosted = true;
        entry.PostedBy = userId;
        entry.PostedDate = DateTime.UtcNow;
        entry.DateModified = DateTime.UtcNow;
        entry.ModifiedBy = userId;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Posted journal entry {Reference}", entry.Reference);
    }

    public async Task UnpostJournalEntryAsync(string entryId, string userId)
    {
        var entry = await _context.JournalEntries.FindAsync(entryId);
        if (entry == null)
            throw new InvalidOperationException($"Journal entry {entryId} not found");

        if (!entry.IsPosted)
            throw new InvalidOperationException("Journal entry is not posted");

        // Check if period is closed
        var period = await GetPeriodForDateAsync(entry.QId!, entry.EntryDate);
        if (period?.IsClosed == true)
            throw new InvalidOperationException("Cannot unpost entry in a closed period");

        entry.IsPosted = false;
        entry.PostedBy = null;
        entry.PostedDate = null;
        entry.DateModified = DateTime.UtcNow;
        entry.ModifiedBy = userId;

        await _context.SaveChangesAsync();
    }

    #endregion

    #region Auto-generation from transactions

    public async Task<JournalEntry?> GenerateJournalEntryForSaleAsync(Sale sale, string quarryId)
    {
        // Check if entry already exists for this sale
        var existing = await _context.JournalEntries
            .FirstOrDefaultAsync(j => j.SourceEntityType == "Sale" && j.SourceEntityId == sale.Id);
        if (existing != null)
            return existing;

        var quarry = await _context.Quarries.FindAsync(quarryId);
        if (quarry == null) return null;

        var product = await _context.Products.FindAsync(sale.ProductId);

        var entry = new JournalEntry
        {
            Id = Guid.NewGuid().ToString(),
            QId = quarryId,
            EntryDate = sale.SaleDate ?? DateTime.Today,
            Reference = await GenerateJournalReferenceAsync(quarryId, "SL"),
            Description = $"Sale - {sale.VehicleRegistration} - {product?.ProductName ?? "Product"} x {sale.Quantity:N0}",
            EntryType = "Auto",
            SourceEntityType = "Sale",
            SourceEntityId = sale.Id,
            FiscalYear = (sale.SaleDate ?? DateTime.Today).Year,
            FiscalPeriod = (sale.SaleDate ?? DateTime.Today).Month,
            IsPosted = true, // Auto-posted
            PostedDate = DateTime.UtcNow,
            DateCreated = DateTime.UtcNow,
            IsActive = true
        };

        var lines = new List<JournalEntryLine>();
        var grossAmount = sale.Quantity * sale.PricePerUnit;

        // Determine cash or receivable based on payment status
        if (sale.PaymentStatus == "Paid")
        {
            // DR Cash (1000)
            var cashAccount = await GetAccountByCodeAsync(quarryId, "1000");
            if (cashAccount != null)
            {
                lines.Add(CreateJournalLine(entry.Id, cashAccount.Id, grossAmount, 0, $"Cash from sale {sale.VehicleRegistration}"));
            }
        }
        else
        {
            // DR Accounts Receivable (1100)
            var arAccount = await GetAccountByCodeAsync(quarryId, "1100");
            if (arAccount != null)
            {
                lines.Add(CreateJournalLine(entry.Id, arAccount.Id, grossAmount, 0, $"A/R from sale {sale.VehicleRegistration}"));
            }
        }

        // CR Sales Revenue (4000 or product-specific)
        var revenueCode = ChartOfAccountsSeed.GetProductSalesAccountCode(product?.ProductName ?? "");
        var revenueAccount = await GetAccountByCodeAsync(quarryId, revenueCode);
        if (revenueAccount != null)
        {
            lines.Add(CreateJournalLine(entry.Id, revenueAccount.Id, 0, grossAmount, $"Revenue from {product?.ProductName ?? "Product"}"));
        }

        // Commission expense
        if (sale.CommissionPerUnit > 0)
        {
            var commissionAmount = sale.Quantity * sale.CommissionPerUnit;
            var commissionAccount = await GetAccountByCodeAsync(quarryId, "5000");
            var accruedAccount = await GetAccountByCodeAsync(quarryId, "2100");
            if (commissionAccount != null && accruedAccount != null)
            {
                lines.Add(CreateJournalLine(entry.Id, commissionAccount.Id, commissionAmount, 0, "Commission expense"));
                lines.Add(CreateJournalLine(entry.Id, accruedAccount.Id, 0, commissionAmount, "Commission payable"));
            }
        }

        // Loaders fee expense
        if (quarry.LoadersFee > 0)
        {
            var loadersFeeAmount = sale.Quantity * quarry.LoadersFee.Value;
            var loadersAccount = await GetAccountByCodeAsync(quarryId, "5100");
            var accruedAccount = await GetAccountByCodeAsync(quarryId, "2100");
            if (loadersAccount != null && accruedAccount != null)
            {
                lines.Add(CreateJournalLine(entry.Id, loadersAccount.Id, loadersFeeAmount, 0, "Loaders fee expense"));
                lines.Add(CreateJournalLine(entry.Id, accruedAccount.Id, 0, loadersFeeAmount, "Loaders fee payable"));
            }
        }

        // Land rate fee expense
        var landRateFee = product?.ProductName.ToLower().Contains("reject") == true
            ? quarry.RejectsFee ?? 0
            : quarry.LandRateFee ?? 0;

        if (landRateFee > 0)
        {
            var landRateFeeAmount = sale.Quantity * landRateFee;
            var landRateAccount = await GetAccountByCodeAsync(quarryId, "5200");
            var accruedAccount = await GetAccountByCodeAsync(quarryId, "2100");
            if (landRateAccount != null && accruedAccount != null)
            {
                lines.Add(CreateJournalLine(entry.Id, landRateAccount.Id, landRateFeeAmount, 0, "Land rate fee expense"));
                lines.Add(CreateJournalLine(entry.Id, accruedAccount.Id, 0, landRateFeeAmount, "Land rate fee payable"));
            }
        }

        entry.Lines = lines;
        entry.TotalDebit = lines.Sum(l => l.DebitAmount);
        entry.TotalCredit = lines.Sum(l => l.CreditAmount);

        _context.JournalEntries.Add(entry);
        await _context.SaveChangesAsync();

        return entry;
    }

    public async Task<JournalEntry?> GenerateJournalEntryForExpenseAsync(Expense expense, string quarryId)
    {
        var existing = await _context.JournalEntries
            .FirstOrDefaultAsync(j => j.SourceEntityType == "Expense" && j.SourceEntityId == expense.Id);
        if (existing != null)
            return existing;

        var entry = new JournalEntry
        {
            Id = Guid.NewGuid().ToString(),
            QId = quarryId,
            EntryDate = expense.ExpenseDate ?? DateTime.Today,
            Reference = await GenerateJournalReferenceAsync(quarryId, "EX"),
            Description = $"Expense - {expense.Item}",
            EntryType = "Auto",
            SourceEntityType = "Expense",
            SourceEntityId = expense.Id,
            FiscalYear = (expense.ExpenseDate ?? DateTime.Today).Year,
            FiscalPeriod = (expense.ExpenseDate ?? DateTime.Today).Month,
            IsPosted = true,
            PostedDate = DateTime.UtcNow,
            DateCreated = DateTime.UtcNow,
            IsActive = true
        };

        var lines = new List<JournalEntryLine>();

        // Get expense account based on category
        var expenseCode = ChartOfAccountsSeed.GetExpenseAccountCode(expense.Category ?? "Miscellaneous");
        var expenseAccount = await GetAccountByCodeAsync(quarryId, expenseCode);
        var cashAccount = await GetAccountByCodeAsync(quarryId, "1000");

        if (expenseAccount != null && cashAccount != null)
        {
            // DR Expense account
            lines.Add(CreateJournalLine(entry.Id, expenseAccount.Id, expense.Amount, 0, expense.Item));
            // CR Cash
            lines.Add(CreateJournalLine(entry.Id, cashAccount.Id, 0, expense.Amount, $"Cash paid for {expense.Item}"));
        }

        entry.Lines = lines;
        entry.TotalDebit = lines.Sum(l => l.DebitAmount);
        entry.TotalCredit = lines.Sum(l => l.CreditAmount);

        _context.JournalEntries.Add(entry);
        await _context.SaveChangesAsync();

        return entry;
    }

    public async Task<JournalEntry?> GenerateJournalEntryForBankingAsync(BankingEntity banking, string quarryId)
    {
        var existing = await _context.JournalEntries
            .FirstOrDefaultAsync(j => j.SourceEntityType == "Banking" && j.SourceEntityId == banking.Id);
        if (existing != null)
            return existing;

        var entry = new JournalEntry
        {
            Id = Guid.NewGuid().ToString(),
            QId = quarryId,
            EntryDate = banking.BankingDate ?? DateTime.Today,
            Reference = await GenerateJournalReferenceAsync(quarryId, "BK"),
            Description = $"Bank Deposit - {banking.TxnReference ?? "Deposit"}",
            EntryType = "Auto",
            SourceEntityType = "Banking",
            SourceEntityId = banking.Id,
            FiscalYear = (banking.BankingDate ?? DateTime.Today).Year,
            FiscalPeriod = (banking.BankingDate ?? DateTime.Today).Month,
            IsPosted = true,
            PostedDate = DateTime.UtcNow,
            DateCreated = DateTime.UtcNow,
            IsActive = true
        };

        var lines = new List<JournalEntryLine>();

        var bankAccount = await GetAccountByCodeAsync(quarryId, "1010");
        var cashAccount = await GetAccountByCodeAsync(quarryId, "1000");

        if (bankAccount != null && cashAccount != null)
        {
            // DR Bank Account
            lines.Add(CreateJournalLine(entry.Id, bankAccount.Id, banking.AmountBanked, 0, $"Deposit ref: {banking.TxnReference}"));
            // CR Cash
            lines.Add(CreateJournalLine(entry.Id, cashAccount.Id, 0, banking.AmountBanked, "Cash deposited"));
        }

        entry.Lines = lines;
        entry.TotalDebit = lines.Sum(l => l.DebitAmount);
        entry.TotalCredit = lines.Sum(l => l.CreditAmount);

        _context.JournalEntries.Add(entry);
        await _context.SaveChangesAsync();

        return entry;
    }

    public async Task<JournalEntry?> GenerateJournalEntryForPrepaymentAsync(Prepayment prepayment, string quarryId)
    {
        var existing = await _context.JournalEntries
            .FirstOrDefaultAsync(j => j.SourceEntityType == "Prepayment" && j.SourceEntityId == prepayment.Id);
        if (existing != null)
            return existing;

        var entry = new JournalEntry
        {
            Id = Guid.NewGuid().ToString(),
            QId = quarryId,
            EntryDate = prepayment.PrepaymentDate,
            Reference = await GenerateJournalReferenceAsync(quarryId, "PP"),
            Description = $"Prepayment - {prepayment.VehicleRegistration}",
            EntryType = "Auto",
            SourceEntityType = "Prepayment",
            SourceEntityId = prepayment.Id,
            FiscalYear = prepayment.PrepaymentDate.Year,
            FiscalPeriod = prepayment.PrepaymentDate.Month,
            IsPosted = true,
            PostedDate = DateTime.UtcNow,
            DateCreated = DateTime.UtcNow,
            IsActive = true
        };

        var lines = new List<JournalEntryLine>();

        var cashAccount = await GetAccountByCodeAsync(quarryId, "1000");
        var customerDepositsAccount = await GetAccountByCodeAsync(quarryId, "2000");

        if (cashAccount != null && customerDepositsAccount != null)
        {
            // DR Cash
            lines.Add(CreateJournalLine(entry.Id, cashAccount.Id, prepayment.TotalAmountPaid, 0, $"Prepayment from {prepayment.VehicleRegistration}"));
            // CR Customer Deposits (Liability)
            lines.Add(CreateJournalLine(entry.Id, customerDepositsAccount.Id, 0, prepayment.TotalAmountPaid, "Customer deposit liability"));
        }

        entry.Lines = lines;
        entry.TotalDebit = lines.Sum(l => l.DebitAmount);
        entry.TotalCredit = lines.Sum(l => l.CreditAmount);

        _context.JournalEntries.Add(entry);
        await _context.SaveChangesAsync();

        return entry;
    }

    public async Task<JournalEntry?> GenerateJournalEntryForCollectionAsync(Sale sale, string quarryId)
    {
        // A collection is when payment was received after the original sale date
        if (sale.PaymentReceivedDate == null || sale.PaymentStatus != "Paid" || sale.PaymentReceivedDate == sale.SaleDate)
            return null;

        var collectionAmount = sale.GrossSaleAmount;
        if (collectionAmount <= 0)
            return null;

        var reference = $"COL-{sale.Id}";
        var existing = await _context.JournalEntries
            .FirstOrDefaultAsync(j => j.SourceEntityType == "Collection" && j.SourceEntityId == sale.Id);
        if (existing != null)
            return existing;

        var entry = new JournalEntry
        {
            Id = Guid.NewGuid().ToString(),
            QId = quarryId,
            EntryDate = sale.PaymentReceivedDate.Value,
            Reference = await GenerateJournalReferenceAsync(quarryId, "CL"),
            Description = $"Collection - {sale.VehicleRegistration}",
            EntryType = "Auto",
            SourceEntityType = "Collection",
            SourceEntityId = sale.Id,
            FiscalYear = sale.PaymentReceivedDate.Value.Year,
            FiscalPeriod = sale.PaymentReceivedDate.Value.Month,
            IsPosted = true,
            PostedDate = DateTime.UtcNow,
            DateCreated = DateTime.UtcNow,
            IsActive = true
        };

        var lines = new List<JournalEntryLine>();

        var cashAccount = await GetAccountByCodeAsync(quarryId, "1000");
        var arAccount = await GetAccountByCodeAsync(quarryId, "1100");

        if (cashAccount != null && arAccount != null)
        {
            // DR Cash
            lines.Add(CreateJournalLine(entry.Id, cashAccount.Id, collectionAmount, 0, $"Collection from {sale.VehicleRegistration}"));
            // CR Accounts Receivable
            lines.Add(CreateJournalLine(entry.Id, arAccount.Id, 0, collectionAmount, "Reduce A/R"));
        }

        entry.Lines = lines;
        entry.TotalDebit = lines.Sum(l => l.DebitAmount);
        entry.TotalCredit = lines.Sum(l => l.CreditAmount);

        _context.JournalEntries.Add(entry);
        await _context.SaveChangesAsync();

        return entry;
    }

    public async Task RegenerateAllJournalEntriesAsync(string quarryId, DateTime from, DateTime to)
    {
        _logger.LogInformation("Regenerating journal entries for quarry {QuarryId} from {From} to {To}",
            quarryId, from, to);

        // Set extended timeout for long-running operation
        _context.Database.SetCommandTimeout(TimeSpan.FromMinutes(10));

        try
        {
            // Pre-load and cache all accounts for this quarry (avoids repeated queries)
            var accountCache = await _context.LedgerAccounts
                .Where(a => a.QId == quarryId && a.IsActive)
                .ToDictionaryAsync(a => a.AccountCode, a => a);

            // Pre-load quarry settings
            var quarry = await _context.Quarries.FindAsync(quarryId);
            if (quarry == null)
            {
                _logger.LogWarning("Quarry {QuarryId} not found", quarryId);
                return;
            }

            // Get existing journal entry source IDs to avoid duplicates
            var existingSourceIds = await _context.JournalEntries
                .Where(j => j.QId == quarryId && j.IsActive)
                .Where(j => j.EntryDate >= from && j.EntryDate <= to)
                .Select(j => new { j.SourceEntityType, j.SourceEntityId })
                .ToListAsync();

            var existingSet = existingSourceIds
                .Where(e => !string.IsNullOrEmpty(e.SourceEntityType) && !string.IsNullOrEmpty(e.SourceEntityId))
                .Select(e => $"{e.SourceEntityType}:{e.SourceEntityId}")
                .ToHashSet();

            // Get reference counter
            var year = DateTime.Today.Year;
            var existingCount = await _context.JournalEntries
                .CountAsync(j => j.QId == quarryId && j.FiscalYear == year);
            var referenceCounter = existingCount;

            // Batch size for processing
            const int batchSize = 100;
            var newEntries = new List<JournalEntry>();
            var processedCount = 0;

            // Process Sales
            var sales = await _context.Sales
                .Include(s => s.Product)
                .Where(s => s.QId == quarryId && s.IsActive)
                .Where(s => s.SaleDate >= from && s.SaleDate <= to)
                .ToListAsync();

            _logger.LogInformation("Processing {Count} sales for journal entries", sales.Count);

            foreach (var sale in sales)
            {
                // Skip if already has journal entry
                if (existingSet.Contains($"Sale:{sale.Id}"))
                    continue;

                var entry = CreateSaleJournalEntry(sale, quarry, accountCache, ref referenceCounter);
                if (entry != null)
                {
                    newEntries.Add(entry);
                    processedCount++;
                }

                // Check for collection entry
                if (sale.PaymentReceivedDate != null && sale.PaymentStatus == "Paid" &&
                    sale.PaymentReceivedDate != sale.SaleDate &&
                    !existingSet.Contains($"Collection:{sale.Id}"))
                {
                    var collectionEntry = CreateCollectionJournalEntry(sale, accountCache, ref referenceCounter);
                    if (collectionEntry != null)
                    {
                        newEntries.Add(collectionEntry);
                        processedCount++;
                    }
                }

                // Save in batches
                if (newEntries.Count >= batchSize)
                {
                    _context.JournalEntries.AddRange(newEntries);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Saved batch of {Count} journal entries", newEntries.Count);
                    newEntries.Clear();
                }
            }

            // Process Expenses
            var expenses = await _context.Expenses
                .Where(e => e.QId == quarryId && e.IsActive)
                .Where(e => e.ExpenseDate >= from && e.ExpenseDate <= to)
                .ToListAsync();

            _logger.LogInformation("Processing {Count} expenses for journal entries", expenses.Count);

            foreach (var expense in expenses)
            {
                if (existingSet.Contains($"Expense:{expense.Id}"))
                    continue;

                var entry = CreateExpenseJournalEntry(expense, accountCache, ref referenceCounter);
                if (entry != null)
                {
                    newEntries.Add(entry);
                    processedCount++;
                }

                if (newEntries.Count >= batchSize)
                {
                    _context.JournalEntries.AddRange(newEntries);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Saved batch of {Count} journal entries", newEntries.Count);
                    newEntries.Clear();
                }
            }

            // Process Banking records
            var bankings = await _context.Bankings
                .Where(b => b.QId == quarryId && b.IsActive)
                .Where(b => b.BankingDate >= from && b.BankingDate <= to)
                .ToListAsync();

            _logger.LogInformation("Processing {Count} banking records for journal entries", bankings.Count);

            foreach (var banking in bankings)
            {
                if (existingSet.Contains($"Banking:{banking.Id}"))
                    continue;

                var entry = CreateBankingJournalEntry(banking, accountCache, ref referenceCounter);
                if (entry != null)
                {
                    newEntries.Add(entry);
                    processedCount++;
                }

                if (newEntries.Count >= batchSize)
                {
                    _context.JournalEntries.AddRange(newEntries);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Saved batch of {Count} journal entries", newEntries.Count);
                    newEntries.Clear();
                }
            }

            // Process Prepayments
            var prepayments = await _context.Prepayments
                .Where(p => p.QId == quarryId && p.IsActive)
                .Where(p => p.PrepaymentDate >= from && p.PrepaymentDate <= to)
                .ToListAsync();

            _logger.LogInformation("Processing {Count} prepayments for journal entries", prepayments.Count);

            foreach (var prepayment in prepayments)
            {
                if (existingSet.Contains($"Prepayment:{prepayment.Id}"))
                    continue;

                var entry = CreatePrepaymentJournalEntry(prepayment, accountCache, ref referenceCounter);
                if (entry != null)
                {
                    newEntries.Add(entry);
                    processedCount++;
                }

                if (newEntries.Count >= batchSize)
                {
                    _context.JournalEntries.AddRange(newEntries);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Saved batch of {Count} journal entries", newEntries.Count);
                    newEntries.Clear();
                }
            }

            // Save remaining entries
            if (newEntries.Any())
            {
                _context.JournalEntries.AddRange(newEntries);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Saved final batch of {Count} journal entries", newEntries.Count);
            }

            _logger.LogInformation("Journal entry regeneration completed for quarry {QuarryId}. Created {Count} new entries.",
                quarryId, processedCount);
        }
        finally
        {
            // Reset timeout to default
            _context.Database.SetCommandTimeout(TimeSpan.FromSeconds(30));
        }
    }

    #region Bulk Entry Creation Helpers (No DB calls)

    private JournalEntry? CreateSaleJournalEntry(Sale sale, Quarry quarry, Dictionary<string, LedgerAccount> accountCache, ref int referenceCounter)
    {
        referenceCounter++;
        var entry = new JournalEntry
        {
            Id = Guid.NewGuid().ToString(),
            QId = quarry.Id,
            EntryDate = sale.SaleDate ?? DateTime.Today,
            Reference = $"SL-{DateTime.Today.Year}-{referenceCounter:D5}",
            Description = $"Sale - {sale.VehicleRegistration} - {sale.Product?.ProductName ?? "Product"} x {sale.Quantity:N0}",
            EntryType = "Auto",
            SourceEntityType = "Sale",
            SourceEntityId = sale.Id,
            FiscalYear = (sale.SaleDate ?? DateTime.Today).Year,
            FiscalPeriod = (sale.SaleDate ?? DateTime.Today).Month,
            IsPosted = true,
            PostedDate = DateTime.UtcNow,
            DateCreated = DateTime.UtcNow,
            IsActive = true,
            Lines = new List<JournalEntryLine>()
        };

        var grossAmount = sale.Quantity * sale.PricePerUnit;

        // DR Cash or AR
        var cashOrArCode = sale.PaymentStatus == "Paid" ? "1000" : "1100";
        if (accountCache.TryGetValue(cashOrArCode, out var cashOrArAccount))
        {
            entry.Lines.Add(CreateBulkJournalLine(entry.Id, cashOrArAccount.Id, grossAmount, 0,
                sale.PaymentStatus == "Paid" ? $"Cash from sale {sale.VehicleRegistration}" : $"A/R from sale {sale.VehicleRegistration}"));
        }

        // CR Sales Revenue
        var revenueCode = ChartOfAccountsSeed.GetProductSalesAccountCode(sale.Product?.ProductName ?? "");
        if (accountCache.TryGetValue(revenueCode, out var revenueAccount))
        {
            entry.Lines.Add(CreateBulkJournalLine(entry.Id, revenueAccount.Id, 0, grossAmount, $"Revenue from {sale.Product?.ProductName ?? "Product"}"));
        }

        // Commission expense
        if (sale.CommissionPerUnit > 0)
        {
            var commissionAmount = sale.Quantity * sale.CommissionPerUnit;
            if (accountCache.TryGetValue("5000", out var commissionAccount) && accountCache.TryGetValue("2100", out var accruedAccount))
            {
                entry.Lines.Add(CreateBulkJournalLine(entry.Id, commissionAccount.Id, commissionAmount, 0, "Commission expense"));
                entry.Lines.Add(CreateBulkJournalLine(entry.Id, accruedAccount.Id, 0, commissionAmount, "Commission payable"));
            }
        }

        // Loaders fee expense
        if (quarry.LoadersFee > 0)
        {
            var loadersFeeAmount = sale.Quantity * quarry.LoadersFee.Value;
            if (accountCache.TryGetValue("5100", out var loadersAccount) && accountCache.TryGetValue("2100", out var accruedAccount))
            {
                entry.Lines.Add(CreateBulkJournalLine(entry.Id, loadersAccount.Id, loadersFeeAmount, 0, "Loaders fee expense"));
                entry.Lines.Add(CreateBulkJournalLine(entry.Id, accruedAccount.Id, 0, loadersFeeAmount, "Loaders fee payable"));
            }
        }

        // Land rate fee expense
        var isReject = sale.Product?.ProductName?.ToLower().Contains("reject") == true;
        var landRateFee = isReject ? (quarry.RejectsFee ?? 0) : (quarry.LandRateFee ?? 0);
        if (landRateFee > 0)
        {
            var landRateFeeAmount = sale.Quantity * landRateFee;
            if (accountCache.TryGetValue("5200", out var landRateAccount) && accountCache.TryGetValue("2100", out var accruedAccount))
            {
                entry.Lines.Add(CreateBulkJournalLine(entry.Id, landRateAccount.Id, landRateFeeAmount, 0, "Land rate fee expense"));
                entry.Lines.Add(CreateBulkJournalLine(entry.Id, accruedAccount.Id, 0, landRateFeeAmount, "Land rate fee payable"));
            }
        }

        entry.TotalDebit = entry.Lines.Sum(l => l.DebitAmount);
        entry.TotalCredit = entry.Lines.Sum(l => l.CreditAmount);

        return entry.Lines.Any() ? entry : null;
    }

    private JournalEntry? CreateCollectionJournalEntry(Sale sale, Dictionary<string, LedgerAccount> accountCache, ref int referenceCounter)
    {
        var collectionAmount = sale.GrossSaleAmount;
        if (collectionAmount <= 0 || sale.PaymentReceivedDate == null) return null;

        referenceCounter++;
        var entry = new JournalEntry
        {
            Id = Guid.NewGuid().ToString(),
            QId = sale.QId,
            EntryDate = sale.PaymentReceivedDate.Value,
            Reference = $"CL-{DateTime.Today.Year}-{referenceCounter:D5}",
            Description = $"Collection - {sale.VehicleRegistration}",
            EntryType = "Auto",
            SourceEntityType = "Collection",
            SourceEntityId = sale.Id,
            FiscalYear = sale.PaymentReceivedDate.Value.Year,
            FiscalPeriod = sale.PaymentReceivedDate.Value.Month,
            IsPosted = true,
            PostedDate = DateTime.UtcNow,
            DateCreated = DateTime.UtcNow,
            IsActive = true,
            Lines = new List<JournalEntryLine>()
        };

        if (accountCache.TryGetValue("1000", out var cashAccount) && accountCache.TryGetValue("1100", out var arAccount))
        {
            entry.Lines.Add(CreateBulkJournalLine(entry.Id, cashAccount.Id, collectionAmount, 0, $"Collection from {sale.VehicleRegistration}"));
            entry.Lines.Add(CreateBulkJournalLine(entry.Id, arAccount.Id, 0, collectionAmount, "Reduce A/R"));
        }

        entry.TotalDebit = entry.Lines.Sum(l => l.DebitAmount);
        entry.TotalCredit = entry.Lines.Sum(l => l.CreditAmount);

        return entry.Lines.Any() ? entry : null;
    }

    private JournalEntry? CreateExpenseJournalEntry(Expense expense, Dictionary<string, LedgerAccount> accountCache, ref int referenceCounter)
    {
        referenceCounter++;
        var entry = new JournalEntry
        {
            Id = Guid.NewGuid().ToString(),
            QId = expense.QId,
            EntryDate = expense.ExpenseDate ?? DateTime.Today,
            Reference = $"EX-{DateTime.Today.Year}-{referenceCounter:D5}",
            Description = $"Expense - {expense.Item}",
            EntryType = "Auto",
            SourceEntityType = "Expense",
            SourceEntityId = expense.Id,
            FiscalYear = (expense.ExpenseDate ?? DateTime.Today).Year,
            FiscalPeriod = (expense.ExpenseDate ?? DateTime.Today).Month,
            IsPosted = true,
            PostedDate = DateTime.UtcNow,
            DateCreated = DateTime.UtcNow,
            IsActive = true,
            Lines = new List<JournalEntryLine>()
        };

        var expenseCode = ChartOfAccountsSeed.GetExpenseAccountCode(expense.Category ?? "Miscellaneous");
        if (accountCache.TryGetValue(expenseCode, out var expenseAccount) && accountCache.TryGetValue("1000", out var cashAccount))
        {
            entry.Lines.Add(CreateBulkJournalLine(entry.Id, expenseAccount.Id, expense.Amount, 0, expense.Item));
            entry.Lines.Add(CreateBulkJournalLine(entry.Id, cashAccount.Id, 0, expense.Amount, $"Cash paid for {expense.Item}"));
        }

        entry.TotalDebit = entry.Lines.Sum(l => l.DebitAmount);
        entry.TotalCredit = entry.Lines.Sum(l => l.CreditAmount);

        return entry.Lines.Any() ? entry : null;
    }

    private JournalEntry? CreateBankingJournalEntry(BankingEntity banking, Dictionary<string, LedgerAccount> accountCache, ref int referenceCounter)
    {
        referenceCounter++;
        var entry = new JournalEntry
        {
            Id = Guid.NewGuid().ToString(),
            QId = banking.QId,
            EntryDate = banking.BankingDate ?? DateTime.Today,
            Reference = $"BK-{DateTime.Today.Year}-{referenceCounter:D5}",
            Description = $"Bank Deposit - {banking.TxnReference ?? "Deposit"}",
            EntryType = "Auto",
            SourceEntityType = "Banking",
            SourceEntityId = banking.Id,
            FiscalYear = (banking.BankingDate ?? DateTime.Today).Year,
            FiscalPeriod = (banking.BankingDate ?? DateTime.Today).Month,
            IsPosted = true,
            PostedDate = DateTime.UtcNow,
            DateCreated = DateTime.UtcNow,
            IsActive = true,
            Lines = new List<JournalEntryLine>()
        };

        if (accountCache.TryGetValue("1010", out var bankAccount) && accountCache.TryGetValue("1000", out var cashAccount))
        {
            entry.Lines.Add(CreateBulkJournalLine(entry.Id, bankAccount.Id, banking.AmountBanked, 0, $"Deposit ref: {banking.TxnReference}"));
            entry.Lines.Add(CreateBulkJournalLine(entry.Id, cashAccount.Id, 0, banking.AmountBanked, "Cash deposited"));
        }

        entry.TotalDebit = entry.Lines.Sum(l => l.DebitAmount);
        entry.TotalCredit = entry.Lines.Sum(l => l.CreditAmount);

        return entry.Lines.Any() ? entry : null;
    }

    private JournalEntry? CreatePrepaymentJournalEntry(Prepayment prepayment, Dictionary<string, LedgerAccount> accountCache, ref int referenceCounter)
    {
        referenceCounter++;
        var entry = new JournalEntry
        {
            Id = Guid.NewGuid().ToString(),
            QId = prepayment.QId,
            EntryDate = prepayment.PrepaymentDate,
            Reference = $"PP-{DateTime.Today.Year}-{referenceCounter:D5}",
            Description = $"Prepayment - {prepayment.VehicleRegistration}",
            EntryType = "Auto",
            SourceEntityType = "Prepayment",
            SourceEntityId = prepayment.Id,
            FiscalYear = prepayment.PrepaymentDate.Year,
            FiscalPeriod = prepayment.PrepaymentDate.Month,
            IsPosted = true,
            PostedDate = DateTime.UtcNow,
            DateCreated = DateTime.UtcNow,
            IsActive = true,
            Lines = new List<JournalEntryLine>()
        };

        if (accountCache.TryGetValue("1000", out var cashAccount) && accountCache.TryGetValue("2000", out var customerDepositsAccount))
        {
            entry.Lines.Add(CreateBulkJournalLine(entry.Id, cashAccount.Id, prepayment.TotalAmountPaid, 0, $"Prepayment from {prepayment.VehicleRegistration}"));
            entry.Lines.Add(CreateBulkJournalLine(entry.Id, customerDepositsAccount.Id, 0, prepayment.TotalAmountPaid, "Customer deposit liability"));
        }

        entry.TotalDebit = entry.Lines.Sum(l => l.DebitAmount);
        entry.TotalCredit = entry.Lines.Sum(l => l.CreditAmount);

        return entry.Lines.Any() ? entry : null;
    }

    private static JournalEntryLine CreateBulkJournalLine(string entryId, string accountId, double debit, double credit, string? memo)
    {
        return new JournalEntryLine
        {
            Id = Guid.NewGuid().ToString(),
            JournalEntryId = entryId,
            LedgerAccountId = accountId,
            DebitAmount = debit,
            CreditAmount = credit,
            Memo = memo,
            DateCreated = DateTime.UtcNow,
            IsActive = true
        };
    }

    #endregion

    #endregion

    #region Account Balances

    public async Task<double> GetAccountBalanceAsync(string accountId, DateTime asOfDate)
    {
        var account = await _context.LedgerAccounts.FindAsync(accountId);
        if (account == null) return 0;

        var lines = await _context.JournalEntryLines
            .Include(l => l.JournalEntry)
            .Where(l => l.LedgerAccountId == accountId)
            .Where(l => l.JournalEntry.IsPosted && l.JournalEntry.IsActive)
            .Where(l => l.JournalEntry.EntryDate <= asOfDate)
            .ToListAsync();

        var totalDebits = lines.Sum(l => l.DebitAmount);
        var totalCredits = lines.Sum(l => l.CreditAmount);

        // For debit-normal accounts (Assets, Expenses): Balance = Debits - Credits
        // For credit-normal accounts (Liabilities, Equity, Revenue): Balance = Credits - Debits
        return account.IsDebitNormal
            ? totalDebits - totalCredits
            : totalCredits - totalDebits;
    }

    public async Task<Dictionary<string, double>> GetAllAccountBalancesAsync(string quarryId, DateTime asOfDate)
    {
        var accounts = await GetChartOfAccountsAsync(quarryId);
        var balances = new Dictionary<string, double>();

        foreach (var account in accounts)
        {
            balances[account.Id] = await GetAccountBalanceAsync(account.Id, asOfDate);
        }

        return balances;
    }

    #endregion

    #region Accounting Periods

    public async Task<List<AccountingPeriod>> GetAccountingPeriodsAsync(string quarryId, int? fiscalYear = null)
    {
        var query = _context.AccountingPeriods
            .Where(p => p.QId == quarryId && p.IsActive);

        if (fiscalYear.HasValue)
            query = query.Where(p => p.FiscalYear == fiscalYear.Value);

        return await query
            .OrderBy(p => p.FiscalYear)
            .ThenBy(p => p.PeriodNumber)
            .ToListAsync();
    }

    public async Task<AccountingPeriod?> GetCurrentPeriodAsync(string quarryId)
    {
        var today = DateTime.Today;
        return await _context.AccountingPeriods
            .FirstOrDefaultAsync(p =>
                p.QId == quarryId &&
                p.IsActive &&
                p.StartDate <= today &&
                p.EndDate >= today);
    }

    public async Task ClosePeriodAsync(string periodId, string userId, string? closingNotes = null)
    {
        var period = await _context.AccountingPeriods.FindAsync(periodId);
        if (period == null)
            throw new InvalidOperationException($"Period {periodId} not found");

        if (period.IsClosed)
            throw new InvalidOperationException("Period is already closed");

        period.IsClosed = true;
        period.ClosedBy = userId;
        period.ClosedDate = DateTime.UtcNow;
        period.ClosingNotes = closingNotes;
        period.DateModified = DateTime.UtcNow;
        period.ModifiedBy = userId;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Closed accounting period {PeriodName} for quarry {QuarryId}",
            period.PeriodName, period.QId);
    }

    public async Task ReopenPeriodAsync(string periodId, string userId)
    {
        var period = await _context.AccountingPeriods.FindAsync(periodId);
        if (period == null)
            throw new InvalidOperationException($"Period {periodId} not found");

        if (!period.IsClosed)
            throw new InvalidOperationException("Period is not closed");

        period.IsClosed = false;
        period.ClosedBy = null;
        period.ClosedDate = null;
        period.DateModified = DateTime.UtcNow;
        period.ModifiedBy = userId;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Reopened accounting period {PeriodName} for quarry {QuarryId}",
            period.PeriodName, period.QId);
    }

    #endregion

    #region Initialization

    public async Task InitializeChartOfAccountsAsync(string quarryId)
    {
        await ChartOfAccountsSeed.SeedChartOfAccountsAsync(_context, quarryId);
    }

    #endregion

    #region Private Helpers

    private async Task<string> GenerateJournalReferenceAsync(string quarryId, string prefix)
    {
        var year = DateTime.Today.Year;
        var count = await _context.JournalEntries
            .CountAsync(j => j.QId == quarryId && j.FiscalYear == year) + 1;

        return $"{prefix}-{year}-{count:D5}";
    }

    private static JournalEntryLine CreateJournalLine(string entryId, string accountId, double debit, double credit, string? memo)
    {
        return new JournalEntryLine
        {
            Id = Guid.NewGuid().ToString(),
            JournalEntryId = entryId,
            LedgerAccountId = accountId,
            DebitAmount = debit,
            CreditAmount = credit,
            Memo = memo,
            DateCreated = DateTime.UtcNow,
            IsActive = true
        };
    }

    private async Task<AccountingPeriod?> GetPeriodForDateAsync(string quarryId, DateTime date)
    {
        return await _context.AccountingPeriods
            .FirstOrDefaultAsync(p =>
                p.QId == quarryId &&
                p.IsActive &&
                p.StartDate <= date &&
                p.EndDate >= date);
    }

    #endregion
}
