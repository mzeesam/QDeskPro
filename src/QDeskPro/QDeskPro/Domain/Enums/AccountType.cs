namespace QDeskPro.Domain.Enums;

/// <summary>
/// Specific account types within each category for the Chart of Accounts.
/// </summary>
public enum AccountType
{
    // ===== ASSETS (1000-1999) =====

    /// <summary>
    /// Cash on hand - physical cash held by clerks
    /// </summary>
    Cash = 100,

    /// <summary>
    /// Bank accounts - deposited funds
    /// </summary>
    Bank = 101,

    /// <summary>
    /// Accounts Receivable - unpaid customer sales
    /// </summary>
    AccountsReceivable = 102,

    /// <summary>
    /// Prepaid Expenses - advance payments for future services
    /// </summary>
    PrepaidExpenses = 103,

    /// <summary>
    /// Fixed Assets - equipment, vehicles, machinery
    /// </summary>
    FixedAssets = 104,

    // ===== LIABILITIES (2000-2999) =====

    /// <summary>
    /// Customer Deposits - prepayments received from customers
    /// </summary>
    CustomerDeposits = 200,

    /// <summary>
    /// Accounts Payable - amounts owed to suppliers
    /// </summary>
    AccountsPayable = 201,

    /// <summary>
    /// Accrued Expenses - expenses incurred but not yet paid (commissions, fees)
    /// </summary>
    AccruedExpenses = 202,

    /// <summary>
    /// Loans Payable - borrowed funds
    /// </summary>
    LoansPayable = 203,

    // ===== EQUITY (3000-3999) =====

    /// <summary>
    /// Owner's Equity - initial and additional capital investment
    /// </summary>
    OwnersEquity = 300,

    /// <summary>
    /// Retained Earnings - accumulated profits from prior periods
    /// </summary>
    RetainedEarnings = 301,

    // ===== REVENUE (4000-4999) =====

    /// <summary>
    /// Sales Revenue - income from product sales
    /// </summary>
    SalesRevenue = 400,

    /// <summary>
    /// Other Income - miscellaneous income sources
    /// </summary>
    OtherIncome = 401,

    // ===== COST OF SALES (5000-5999) =====

    /// <summary>
    /// Commission Expense - broker commissions on sales
    /// </summary>
    CommissionExpense = 500,

    /// <summary>
    /// Loaders Fees - per-unit fees for loaders
    /// </summary>
    LoadersFees = 501,

    /// <summary>
    /// Land Rate Fees - per-unit land rate charges
    /// </summary>
    LandRateFees = 502,

    // ===== OPERATING EXPENSES (6000-6999) =====

    /// <summary>
    /// Fuel Expense - fuel costs for operations
    /// </summary>
    FuelExpense = 600,

    /// <summary>
    /// Transportation Hire - hired transport costs
    /// </summary>
    TransportationHire = 601,

    /// <summary>
    /// Maintenance and Repairs - equipment maintenance
    /// </summary>
    MaintenanceRepairs = 602,

    /// <summary>
    /// Consumables and Utilities - operational supplies
    /// </summary>
    ConsumablesUtilities = 603,

    /// <summary>
    /// Administrative Expenses - office and admin costs
    /// </summary>
    AdministrativeExpenses = 604,

    /// <summary>
    /// Marketing Expenses - advertising and promotion
    /// </summary>
    MarketingExpenses = 605,

    /// <summary>
    /// Wages and Salaries - employee compensation
    /// </summary>
    WagesSalaries = 606,

    /// <summary>
    /// Bank Charges - banking fees and charges
    /// </summary>
    BankCharges = 607,

    /// <summary>
    /// Cess and Road Fees - government levies
    /// </summary>
    CessRoadFees = 608,

    /// <summary>
    /// Miscellaneous Expenses - other operational costs
    /// </summary>
    MiscellaneousExpenses = 609
}
