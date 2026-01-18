using Microsoft.EntityFrameworkCore;
using QDeskPro.Domain.Entities;
using QDeskPro.Domain.Enums;

namespace QDeskPro.Data.Seed;

/// <summary>
/// Seeds the default Chart of Accounts for quarry operations.
/// Creates a standardized set of ledger accounts for each quarry.
/// </summary>
/// <remarks>
/// Chart of Accounts structure aligned with IFRS for SMEs Third Edition (February 2025).
/// Follows the minimum line items specified in Section 4.2 for the Statement of Financial Position
/// and Section 5.5 for the Statement of Comprehensive Income.
///
/// Account Code Structure:
/// - 1000-1999: Assets (Current: 1000-1399, Non-Current: 1400-1999)
/// - 2000-2999: Liabilities (Current: 2000-2499, Non-Current: 2500-2999)
/// - 3000-3999: Equity
/// - 4000-4999: Revenue
/// - 5000-5999: Cost of Sales
/// - 6000-6999: Operating Expenses
/// </remarks>
public static class ChartOfAccountsSeed
{
    /// <summary>
    /// Seeds the default Chart of Accounts for a specific quarry.
    /// </summary>
    public static async Task SeedChartOfAccountsAsync(AppDbContext context, string quarryId)
    {
        // Check if accounts already exist for this quarry
        if (await context.LedgerAccounts.AnyAsync(a => a.QId == quarryId))
            return;

        var accounts = CreateDefaultAccounts(quarryId);
        context.LedgerAccounts.AddRange(accounts);
        await context.SaveChangesAsync();

        Console.WriteLine($"✓ Chart of Accounts seeded for quarry: {quarryId}");
    }

    /// <summary>
    /// Seeds Chart of Accounts for all existing quarries that don't have accounts.
    /// </summary>
    public static async Task SeedAllQuarriesAsync(AppDbContext context)
    {
        var quarryIds = await context.Quarries
            .Where(q => q.IsActive)
            .Select(q => q.Id)
            .ToListAsync();

        foreach (var quarryId in quarryIds)
        {
            await SeedChartOfAccountsAsync(context, quarryId);
        }
    }

    private static List<LedgerAccount> CreateDefaultAccounts(string quarryId)
    {
        var now = DateTime.UtcNow;
        var accounts = new List<LedgerAccount>();
        int displayOrder = 0;

        // ===== ASSETS (1000-1999) =====
        // Per IFRS for SMEs Section 4.2 - Statement of Financial Position minimum line items

        // Current Assets (1000-1399)
        accounts.Add(CreateAccount(quarryId, "1000", "Cash and Cash Equivalents", AccountCategory.Assets, AccountType.Cash,
            "Cash on hand - per IFRS for SMEs Section 4.2(a). Physical cash held by clerks.", true, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "1010", "Bank Account", AccountCategory.Assets, AccountType.Bank,
            "Bank balances - part of Cash and Cash Equivalents per IFRS for SMEs.", true, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "1100", "Trade and Other Receivables", AccountCategory.Assets, AccountType.AccountsReceivable,
            "Trade receivables - per IFRS for SMEs Section 4.2(b). Unpaid customer sales.", true, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "1200", "Prepayments", AccountCategory.Assets, AccountType.PrepaidExpenses,
            "Prepaid expenses - included in Other Receivables. Advance payments for services.", true, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "1300", "Inventories", AccountCategory.Assets, AccountType.Inventory,
            "Inventories - per IFRS for SMEs Section 4.2(d) and Section 13. Stock of products held for sale.", true, true, ++displayOrder, now));

        // Non-Current Assets (1400-1999)
        accounts.Add(CreateAccount(quarryId, "1500", "Property, Plant and Equipment", AccountCategory.Assets, AccountType.FixedAssets,
            "PPE at cost - per IFRS for SMEs Section 4.2(c) and Section 17. Equipment, vehicles, machinery.", true, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "1510", "Accumulated Depreciation", AccountCategory.Assets, AccountType.AccumulatedDepreciation,
            "Accumulated depreciation - contra-asset per IFRS for SMEs Section 17. Reduces PPE carrying amount.", false, true, ++displayOrder, now));

        // ===== LIABILITIES (2000-2999) =====
        // Per IFRS for SMEs Section 4.2(f-j) - minimum line items for liabilities

        // Current Liabilities (2000-2499)
        accounts.Add(CreateAccount(quarryId, "2000", "Contract Liabilities", AccountCategory.Liabilities, AccountType.CustomerDeposits,
            "Contract liabilities - per IFRS for SMEs Section 23. Customer prepayments before delivery.", false, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "2100", "Trade and Other Payables", AccountCategory.Liabilities, AccountType.AccountsPayable,
            "Trade payables - per IFRS for SMEs Section 4.2(f). Amounts owed to suppliers.", false, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "2110", "Accrued Expenses", AccountCategory.Liabilities, AccountType.AccruedExpenses,
            "Accrued expenses - part of Trade and Other Payables. Broker commissions, fees payable.", false, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "2200", "Current Tax Liabilities", AccountCategory.Liabilities, AccountType.CurrentTaxLiabilities,
            "Current tax liabilities - per IFRS for SMEs Section 4.2(i). Income taxes payable.", false, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "2300", "Provisions", AccountCategory.Liabilities, AccountType.Provisions,
            "Provisions - per IFRS for SMEs Section 4.2(h) and Section 21. Liabilities of uncertain timing.", false, true, ++displayOrder, now));

        // Non-Current Liabilities (2500-2999)
        accounts.Add(CreateAccount(quarryId, "2500", "Borrowings", AccountCategory.Liabilities, AccountType.LoansPayable,
            "Borrowings - per IFRS for SMEs Section 4.2(g) and Section 25. Loans and borrowed funds.", false, true, ++displayOrder, now));

        // ===== EQUITY (3000-3999) =====
        // Per IFRS for SMEs Section 4.2(k-l) and Section 22

        accounts.Add(CreateAccount(quarryId, "3000", "Share Capital", AccountCategory.Equity, AccountType.OwnersEquity,
            "Issued capital - per IFRS for SMEs Section 4.2(k). Owner's investment in the quarry.", false, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "3100", "Retained Earnings", AccountCategory.Equity, AccountType.RetainedEarnings,
            "Retained earnings - per IFRS for SMEs Section 4.2(l). Accumulated profits from prior periods.", false, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "3200", "Current Year Earnings", AccountCategory.Equity, AccountType.CurrentYearEarnings,
            "Current period profit/loss. Closed to Retained Earnings at period end.", false, true, ++displayOrder, now));

        // ===== REVENUE (4000-4999) =====
        // Per IFRS for SMEs Section 5.5(a) and Section 23 - Revenue from Contracts with Customers

        accounts.Add(CreateAccount(quarryId, "4000", "Revenue", AccountCategory.Revenue, AccountType.SalesRevenue,
            "Revenue - per IFRS for SMEs Section 5.5(a). Total income from product sales.", false, true, ++displayOrder, now));

        // Sub-accounts for each product type (disaggregation of revenue)
        accounts.Add(CreateAccount(quarryId, "4010", "Revenue - Size 6", AccountCategory.Revenue, AccountType.SalesRevenue,
            "Revenue from Size 6 ballast sales - per IFRS for SMEs Section 23.", false, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "4020", "Revenue - Size 9", AccountCategory.Revenue, AccountType.SalesRevenue,
            "Revenue from Size 9 ballast sales - per IFRS for SMEs Section 23.", false, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "4030", "Revenue - Size 4", AccountCategory.Revenue, AccountType.SalesRevenue,
            "Revenue from Size 4 ballast sales - per IFRS for SMEs Section 23.", false, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "4040", "Revenue - Reject", AccountCategory.Revenue, AccountType.SalesRevenue,
            "Revenue from Reject product sales - per IFRS for SMEs Section 23.", false, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "4050", "Revenue - Hardcore", AccountCategory.Revenue, AccountType.SalesRevenue,
            "Revenue from Hardcore product sales - per IFRS for SMEs Section 23.", false, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "4060", "Revenue - Beam", AccountCategory.Revenue, AccountType.SalesRevenue,
            "Revenue from Beam product sales - per IFRS for SMEs Section 23.", false, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "4500", "Other Income", AccountCategory.Revenue, AccountType.OtherIncome,
            "Other income - per IFRS for SMEs Section 5.5(a). Miscellaneous non-operating income.", false, true, ++displayOrder, now));

        // ===== COST OF SALES (5000-5999) =====
        // Per IFRS for SMEs Section 5.5(b) - presented by function

        accounts.Add(CreateAccount(quarryId, "5000", "Commission Expense", AccountCategory.CostOfSales, AccountType.CommissionExpense,
            "Commission expense - direct cost of sales. Broker commissions on transactions.", true, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "5100", "Loaders Fees", AccountCategory.CostOfSales, AccountType.LoadersFees,
            "Loaders fees - direct cost of sales. Per-unit fees for loader operations.", true, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "5200", "Land Rate Fees", AccountCategory.CostOfSales, AccountType.LandRateFees,
            "Land rate fees - direct cost of sales. Per-unit land rate/royalty charges.", true, true, ++displayOrder, now));

        // ===== OPERATING EXPENSES (6000-6999) =====
        // Per IFRS for SMEs Section 5 - Operating expenses by function

        accounts.Add(CreateAccount(quarryId, "6000", "Fuel Expense", AccountCategory.Expenses, AccountType.FuelExpense,
            "Fuel expense - operating costs. Fuel for machines and equipment.", true, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "6100", "Transportation Hire", AccountCategory.Expenses, AccountType.TransportationHire,
            "Transportation hire - operating costs. Hired transport and logistics.", true, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "6200", "Maintenance and Repairs", AccountCategory.Expenses, AccountType.MaintenanceRepairs,
            "Maintenance and repairs - operating costs. Equipment servicing.", true, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "6300", "Consumables and Utilities", AccountCategory.Expenses, AccountType.ConsumablesUtilities,
            "Consumables and utilities - operating costs. Supplies and electricity.", true, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "6400", "Administrative Expenses", AccountCategory.Expenses, AccountType.AdministrativeExpenses,
            "Administrative expenses - operating costs. Office and management.", true, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "6500", "Marketing Expenses", AccountCategory.Expenses, AccountType.MarketingExpenses,
            "Marketing expenses - operating costs. Advertising and promotion.", true, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "6600", "Employee Benefits Expense", AccountCategory.Expenses, AccountType.WagesSalaries,
            "Employee benefits expense - per IFRS for SMEs Section 28. Wages and salaries.", true, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "6700", "Finance Costs", AccountCategory.Expenses, AccountType.BankCharges,
            "Finance costs - per IFRS for SMEs Section 5.5(b). Bank charges and fees.", true, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "6800", "Taxes and Levies", AccountCategory.Expenses, AccountType.CessRoadFees,
            "Taxes and levies - operating costs. Cess, road fees, government levies.", true, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "6900", "Other Expenses", AccountCategory.Expenses, AccountType.MiscellaneousExpenses,
            "Other expenses - operating costs. Miscellaneous not classified elsewhere.", true, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "6950", "Depreciation Expense", AccountCategory.Expenses, AccountType.DepreciationExpense,
            "Depreciation expense - per IFRS for SMEs Section 17.15. PPE depreciation.", true, true, ++displayOrder, now));

        return accounts;
    }

    private static LedgerAccount CreateAccount(
        string quarryId,
        string code,
        string name,
        AccountCategory category,
        AccountType type,
        string description,
        bool isDebitNormal,
        bool isSystemAccount,
        int displayOrder,
        DateTime createdDate)
    {
        return new LedgerAccount
        {
            Id = Guid.NewGuid().ToString(),
            QId = quarryId,
            AccountCode = code,
            AccountName = name,
            Category = category,
            Type = type,
            Description = description,
            IsDebitNormal = isDebitNormal,
            IsSystemAccount = isSystemAccount,
            DisplayOrder = displayOrder,
            IsActive = true,
            DateCreated = createdDate,
            CreatedBy = "System"
        };
    }

    /// <summary>
    /// Gets the account code for a specific expense category.
    /// Used to map manual expenses to the correct ledger account.
    /// </summary>
    public static string GetExpenseAccountCode(string expenseCategory)
    {
        return expenseCategory switch
        {
            "Fuel" => "6000",
            "Transportation Hire" => "6100",
            "Maintenance and Repairs" => "6200",
            "Consumables and Utilities" => "6300",
            "Administrative" => "6400",
            "Marketing" => "6500",
            "Wages" => "6600",
            "Bank Charges" => "6700",
            "Cess and Road Fees" => "6800",
            "Commission" => "5000",
            "Loaders Fees" => "5100",
            _ => "6900" // Miscellaneous
        };
    }

    /// <summary>
    /// Gets the account code for a specific product's sales revenue.
    /// </summary>
    public static string GetProductSalesAccountCode(string productName)
    {
        return productName.ToLower() switch
        {
            "size 6" => "4010",
            "size 9" => "4020",
            "size 4" => "4030",
            "reject" => "4040",
            "hardcore" => "4050",
            "beam" => "4060",
            _ => "4000" // General Sales Revenue
        };
    }
}
