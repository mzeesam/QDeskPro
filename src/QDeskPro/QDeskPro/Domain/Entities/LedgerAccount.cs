using QDeskPro.Domain.Enums;

namespace QDeskPro.Domain.Entities;

/// <summary>
/// Represents an account in the Chart of Accounts for double-entry bookkeeping.
/// </summary>
public class LedgerAccount : BaseEntity
{
    /// <summary>
    /// Account code (e.g., "1000", "4000", "6100")
    /// </summary>
    public string AccountCode { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the account (e.g., "Cash on Hand", "Sales Revenue")
    /// </summary>
    public string AccountName { get; set; } = string.Empty;

    /// <summary>
    /// Major accounting category (Assets, Liabilities, Equity, Revenue, CostOfSales, Expenses)
    /// </summary>
    public AccountCategory Category { get; set; }

    /// <summary>
    /// Specific account type within the category
    /// </summary>
    public AccountType Type { get; set; }

    /// <summary>
    /// Parent account ID for sub-accounts (hierarchical structure)
    /// </summary>
    public string? ParentAccountId { get; set; }

    /// <summary>
    /// System accounts cannot be deleted or modified
    /// </summary>
    public bool IsSystemAccount { get; set; }

    /// <summary>
    /// Sort order for display in reports
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Optional description of the account's purpose
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Normal balance type: true = Debit, false = Credit
    /// Assets and Expenses have debit normal balance.
    /// Liabilities, Equity, and Revenue have credit normal balance.
    /// </summary>
    public bool IsDebitNormal { get; set; }

    // Navigation properties

    /// <summary>
    /// Parent account for hierarchical structure
    /// </summary>
    public virtual LedgerAccount? ParentAccount { get; set; }

    /// <summary>
    /// Child accounts
    /// </summary>
    public virtual ICollection<LedgerAccount> ChildAccounts { get; set; } = new List<LedgerAccount>();

    /// <summary>
    /// Journal entry lines affecting this account
    /// </summary>
    public virtual ICollection<JournalEntryLine> JournalEntryLines { get; set; } = new List<JournalEntryLine>();
}
