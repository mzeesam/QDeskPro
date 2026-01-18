namespace QDeskPro.Domain.Enums;

/// <summary>
/// Specific account types within each category for the Chart of Accounts.
/// Aligned with IFRS for SMEs Third Edition (February 2025).
/// </summary>
/// <remarks>
/// Account type codes are grouped by category:
/// - 100-199: Assets (IFRS for SMEs Section 4.2)
/// - 200-299: Liabilities (IFRS for SMEs Section 4.2)
/// - 300-399: Equity (IFRS for SMEs Section 4.2)
/// - 400-499: Revenue (IFRS for SMEs Section 23)
/// - 500-599: Cost of Sales (IFRS for SMEs Section 5)
/// - 600-699: Operating Expenses (IFRS for SMEs Section 5)
/// </remarks>
public enum AccountType
{
    // ===== ASSETS (1000-1999) =====
    // Per IFRS for SMEs Section 4.2(a) - minimum line items for assets

    /// <summary>
    /// Cash and Cash Equivalents - per IFRS for SMEs Section 4.2(a).
    /// Physical cash held by clerks and petty cash.
    /// </summary>
    Cash = 100,

    /// <summary>
    /// Bank accounts - part of Cash and Cash Equivalents.
    /// Deposited funds in bank accounts.
    /// </summary>
    Bank = 101,

    /// <summary>
    /// Trade and Other Receivables - per IFRS for SMEs Section 4.2(b).
    /// Unpaid customer sales - amounts owed to the quarry.
    /// </summary>
    AccountsReceivable = 102,

    /// <summary>
    /// Prepaid Expenses - included in Other Receivables.
    /// Advance payments for future services.
    /// </summary>
    PrepaidExpenses = 103,

    /// <summary>
    /// Property, Plant and Equipment - per IFRS for SMEs Section 4.2(c) and Section 17.
    /// Equipment, vehicles, and machinery at cost.
    /// </summary>
    FixedAssets = 104,

    /// <summary>
    /// Accumulated Depreciation - contra-asset per IFRS for SMEs Section 17.
    /// Cumulative depreciation of property, plant and equipment.
    /// </summary>
    AccumulatedDepreciation = 105,

    /// <summary>
    /// Inventories - per IFRS for SMEs Section 4.2(d) and Section 13.
    /// Stock of products held for sale.
    /// </summary>
    Inventory = 106,

    // ===== LIABILITIES (2000-2999) =====
    // Per IFRS for SMEs Section 4.2(f-j) - minimum line items for liabilities

    /// <summary>
    /// Customer Deposits - Contract liabilities per IFRS for SMEs Section 23.
    /// Prepayments received from customers - advance payments before delivery.
    /// </summary>
    CustomerDeposits = 200,

    /// <summary>
    /// Trade and Other Payables - per IFRS for SMEs Section 4.2(f).
    /// Amounts owed to suppliers and vendors.
    /// </summary>
    AccountsPayable = 201,

    /// <summary>
    /// Accrued Expenses - included in Trade and Other Payables.
    /// Expenses incurred but not yet paid (broker commissions, fees).
    /// </summary>
    AccruedExpenses = 202,

    /// <summary>
    /// Borrowings - per IFRS for SMEs Section 4.2(g) and Section 25.
    /// Borrowed funds and loans.
    /// </summary>
    LoansPayable = 203,

    /// <summary>
    /// Provisions - per IFRS for SMEs Section 4.2(h) and Section 21.
    /// Liabilities of uncertain timing or amount (e.g., site restoration).
    /// </summary>
    Provisions = 204,

    /// <summary>
    /// Current Tax Liabilities - per IFRS for SMEs Section 4.2(i).
    /// Income taxes payable.
    /// </summary>
    CurrentTaxLiabilities = 205,

    // ===== EQUITY (3000-3999) =====
    // Per IFRS for SMEs Section 4.2(k-l) and Section 22

    /// <summary>
    /// Owner's Equity - Issued capital per IFRS for SMEs Section 4.2(k).
    /// Initial and additional capital investment.
    /// </summary>
    OwnersEquity = 300,

    /// <summary>
    /// Retained Earnings - per IFRS for SMEs Section 4.2(l).
    /// Accumulated profits from prior periods.
    /// </summary>
    RetainedEarnings = 301,

    /// <summary>
    /// Current Year Earnings - tracks current period profit/loss.
    /// Closed to Retained Earnings at period end.
    /// </summary>
    CurrentYearEarnings = 302,

    // ===== REVENUE (4000-4999) =====
    // Per IFRS for SMEs Section 5.5 and Section 23 - Revenue from Contracts with Customers

    /// <summary>
    /// Revenue - per IFRS for SMEs Section 5.5(a) and Section 23.
    /// Income from product sales recognized when control transfers.
    /// </summary>
    SalesRevenue = 400,

    /// <summary>
    /// Other Income - per IFRS for SMEs Section 5.5(a).
    /// Miscellaneous income sources not from primary operations.
    /// </summary>
    OtherIncome = 401,

    // ===== COST OF SALES (5000-5999) =====
    // Per IFRS for SMEs Section 5.5(b) - presented by function

    /// <summary>
    /// Commission Expense - direct cost of sales.
    /// Broker commissions on sales transactions.
    /// </summary>
    CommissionExpense = 500,

    /// <summary>
    /// Loaders Fees - direct cost of sales.
    /// Per-unit fees for loader operations during product extraction.
    /// </summary>
    LoadersFees = 501,

    /// <summary>
    /// Land Rate Fees - direct cost of sales.
    /// Per-unit land rate/royalty charges.
    /// </summary>
    LandRateFees = 502,

    // ===== OPERATING EXPENSES (6000-6999) =====
    // Per IFRS for SMEs Section 5 - Operating expenses by function

    /// <summary>
    /// Fuel Expense - operating expense.
    /// Fuel costs for machines and operational equipment.
    /// </summary>
    FuelExpense = 600,

    /// <summary>
    /// Transportation Hire - operating expense.
    /// Hired transport and logistics costs.
    /// </summary>
    TransportationHire = 601,

    /// <summary>
    /// Maintenance and Repairs - operating expense.
    /// Equipment maintenance, repairs, and servicing.
    /// </summary>
    MaintenanceRepairs = 602,

    /// <summary>
    /// Consumables and Utilities - operating expense.
    /// Operational supplies, electricity, and utilities.
    /// </summary>
    ConsumablesUtilities = 603,

    /// <summary>
    /// Administrative Expenses - operating expense.
    /// Office, administrative, and general management costs.
    /// </summary>
    AdministrativeExpenses = 604,

    /// <summary>
    /// Marketing Expenses - operating expense.
    /// Advertising, promotion, and customer acquisition costs.
    /// </summary>
    MarketingExpenses = 605,

    /// <summary>
    /// Employee Benefits Expense - per IFRS for SMEs Section 28.
    /// Wages, salaries, and employee-related costs.
    /// </summary>
    WagesSalaries = 606,

    /// <summary>
    /// Finance Costs - per IFRS for SMEs Section 5.5(b).
    /// Bank charges, interest, and financing fees.
    /// </summary>
    BankCharges = 607,

    /// <summary>
    /// Taxes and Levies - operating expense.
    /// Cess, road fees, and government levies (non-income tax).
    /// </summary>
    CessRoadFees = 608,

    /// <summary>
    /// Other Expenses - operating expense.
    /// Miscellaneous operational costs not classified elsewhere.
    /// </summary>
    MiscellaneousExpenses = 609,

    /// <summary>
    /// Depreciation Expense - per IFRS for SMEs Section 17.15.
    /// Systematic allocation of depreciable amount of PPE.
    /// </summary>
    DepreciationExpense = 610
}
