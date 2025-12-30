namespace QDeskPro.Domain.Enums;

/// <summary>
/// Major accounting categories for the Chart of Accounts.
/// </summary>
public enum AccountCategory
{
    /// <summary>
    /// Assets - resources owned by the business (Cash, Receivables, Equipment)
    /// </summary>
    Assets = 1,

    /// <summary>
    /// Liabilities - obligations owed to others (Payables, Customer Deposits)
    /// </summary>
    Liabilities = 2,

    /// <summary>
    /// Equity - owner's stake in the business (Capital, Retained Earnings)
    /// </summary>
    Equity = 3,

    /// <summary>
    /// Revenue - income from sales and other sources
    /// </summary>
    Revenue = 4,

    /// <summary>
    /// Cost of Sales - direct costs of goods sold (Commissions, Loaders Fees)
    /// </summary>
    CostOfSales = 5,

    /// <summary>
    /// Expenses - operating costs of running the business
    /// </summary>
    Expenses = 6
}
