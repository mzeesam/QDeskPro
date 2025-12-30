using Microsoft.EntityFrameworkCore;
using QDeskPro.Domain.Entities;
using QDeskPro.Domain.Enums;

namespace QDeskPro.Data.Seed;

/// <summary>
/// Seeds the default Chart of Accounts for quarry operations.
/// Creates a standardized set of ledger accounts for each quarry.
/// </summary>
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
        accounts.Add(CreateAccount(quarryId, "1000", "Cash on Hand", AccountCategory.Assets, AccountType.Cash,
            "Physical cash held by clerks", true, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "1010", "Bank Account", AccountCategory.Assets, AccountType.Bank,
            "Deposited funds in bank accounts", true, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "1100", "Accounts Receivable", AccountCategory.Assets, AccountType.AccountsReceivable,
            "Unpaid customer sales - amounts owed to the quarry", true, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "1200", "Prepaid Expenses", AccountCategory.Assets, AccountType.PrepaidExpenses,
            "Advance payments for future services", true, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "1500", "Fixed Assets", AccountCategory.Assets, AccountType.FixedAssets,
            "Equipment, vehicles, and machinery", true, true, ++displayOrder, now));

        // ===== LIABILITIES (2000-2999) =====
        accounts.Add(CreateAccount(quarryId, "2000", "Customer Deposits", AccountCategory.Liabilities, AccountType.CustomerDeposits,
            "Prepayments received from customers - advance payments", false, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "2100", "Accrued Expenses", AccountCategory.Liabilities, AccountType.AccruedExpenses,
            "Expenses incurred but not yet paid (broker commissions, fees)", false, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "2200", "Accounts Payable", AccountCategory.Liabilities, AccountType.AccountsPayable,
            "Amounts owed to suppliers and vendors", false, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "2300", "Loans Payable", AccountCategory.Liabilities, AccountType.LoansPayable,
            "Borrowed funds and loans", false, true, ++displayOrder, now));

        // ===== EQUITY (3000-3999) =====
        accounts.Add(CreateAccount(quarryId, "3000", "Owner's Equity", AccountCategory.Equity, AccountType.OwnersEquity,
            "Owner's capital investment in the quarry", false, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "3100", "Retained Earnings", AccountCategory.Equity, AccountType.RetainedEarnings,
            "Accumulated profits from prior periods", false, true, ++displayOrder, now));

        // ===== REVENUE (4000-4999) =====
        accounts.Add(CreateAccount(quarryId, "4000", "Sales Revenue", AccountCategory.Revenue, AccountType.SalesRevenue,
            "Total income from all product sales", false, true, ++displayOrder, now));

        // Sub-accounts for each product type
        accounts.Add(CreateAccount(quarryId, "4010", "Sales - Size 6", AccountCategory.Revenue, AccountType.SalesRevenue,
            "Revenue from Size 6 ballast sales", false, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "4020", "Sales - Size 9", AccountCategory.Revenue, AccountType.SalesRevenue,
            "Revenue from Size 9 ballast sales", false, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "4030", "Sales - Size 4", AccountCategory.Revenue, AccountType.SalesRevenue,
            "Revenue from Size 4 ballast sales", false, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "4040", "Sales - Reject", AccountCategory.Revenue, AccountType.SalesRevenue,
            "Revenue from Reject product sales", false, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "4050", "Sales - Hardcore", AccountCategory.Revenue, AccountType.SalesRevenue,
            "Revenue from Hardcore product sales", false, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "4060", "Sales - Beam", AccountCategory.Revenue, AccountType.SalesRevenue,
            "Revenue from Beam product sales", false, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "4500", "Other Income", AccountCategory.Revenue, AccountType.OtherIncome,
            "Miscellaneous income sources", false, true, ++displayOrder, now));

        // ===== COST OF SALES (5000-5999) =====
        accounts.Add(CreateAccount(quarryId, "5000", "Commission Expense", AccountCategory.CostOfSales, AccountType.CommissionExpense,
            "Broker commissions on sales", true, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "5100", "Loaders Fees", AccountCategory.CostOfSales, AccountType.LoadersFees,
            "Per-unit fees for loaders", true, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "5200", "Land Rate Fees", AccountCategory.CostOfSales, AccountType.LandRateFees,
            "Per-unit land rate charges", true, true, ++displayOrder, now));

        // ===== OPERATING EXPENSES (6000-6999) =====
        accounts.Add(CreateAccount(quarryId, "6000", "Fuel Expense", AccountCategory.Expenses, AccountType.FuelExpense,
            "Fuel costs for machines and operations", true, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "6100", "Transportation Hire", AccountCategory.Expenses, AccountType.TransportationHire,
            "Hired transport costs", true, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "6200", "Maintenance and Repairs", AccountCategory.Expenses, AccountType.MaintenanceRepairs,
            "Equipment maintenance and repairs", true, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "6300", "Consumables and Utilities", AccountCategory.Expenses, AccountType.ConsumablesUtilities,
            "Operational supplies and utilities", true, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "6400", "Administrative Expenses", AccountCategory.Expenses, AccountType.AdministrativeExpenses,
            "Office and administrative costs", true, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "6500", "Marketing Expenses", AccountCategory.Expenses, AccountType.MarketingExpenses,
            "Advertising and promotional costs", true, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "6600", "Wages and Salaries", AccountCategory.Expenses, AccountType.WagesSalaries,
            "Employee compensation and wages", true, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "6700", "Bank Charges", AccountCategory.Expenses, AccountType.BankCharges,
            "Banking fees and transaction charges", true, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "6800", "Cess and Road Fees", AccountCategory.Expenses, AccountType.CessRoadFees,
            "Government levies and road fees", true, true, ++displayOrder, now));

        accounts.Add(CreateAccount(quarryId, "6900", "Miscellaneous Expenses", AccountCategory.Expenses, AccountType.MiscellaneousExpenses,
            "Other operational costs not classified elsewhere", true, true, ++displayOrder, now));

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
