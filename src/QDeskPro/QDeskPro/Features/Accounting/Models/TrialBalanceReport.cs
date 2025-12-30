using QDeskPro.Domain.Enums;

namespace QDeskPro.Features.Accounting.Models;

/// <summary>
/// Trial Balance Report - lists all accounts with debit and credit balances.
/// Ensures that total debits equal total credits (balanced books).
/// </summary>
public class TrialBalanceReport
{
    /// <summary>
    /// The quarry this report is generated for.
    /// </summary>
    public string QuarryId { get; set; } = string.Empty;

    /// <summary>
    /// Quarry name for display.
    /// </summary>
    public string QuarryName { get; set; } = string.Empty;

    /// <summary>
    /// As-of date for the trial balance.
    /// </summary>
    public DateTime AsOfDate { get; set; }

    /// <summary>
    /// Report generation timestamp.
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Individual account lines.
    /// </summary>
    public List<TrialBalanceLine> Lines { get; set; } = new();

    /// <summary>
    /// Sum of all debit balances.
    /// </summary>
    public double TotalDebits => Lines.Sum(l => l.DebitBalance);

    /// <summary>
    /// Sum of all credit balances.
    /// </summary>
    public double TotalCredits => Lines.Sum(l => l.CreditBalance);

    /// <summary>
    /// Whether the trial balance is balanced (debits = credits within tolerance).
    /// </summary>
    public bool IsBalanced => Math.Abs(TotalDebits - TotalCredits) < 0.01;

    /// <summary>
    /// Difference between debits and credits (should be 0 if balanced).
    /// </summary>
    public double Difference => TotalDebits - TotalCredits;
}

/// <summary>
/// Individual line item in the Trial Balance report.
/// </summary>
public class TrialBalanceLine
{
    /// <summary>
    /// Account code (e.g., "1000", "4000").
    /// </summary>
    public string AccountCode { get; set; } = string.Empty;

    /// <summary>
    /// Account name (e.g., "Cash on Hand").
    /// </summary>
    public string AccountName { get; set; } = string.Empty;

    /// <summary>
    /// Account category for grouping.
    /// </summary>
    public AccountCategory Category { get; set; }

    /// <summary>
    /// Debit balance (positive if debit normal and has positive balance).
    /// </summary>
    public double DebitBalance { get; set; }

    /// <summary>
    /// Credit balance (positive if credit normal and has positive balance).
    /// </summary>
    public double CreditBalance { get; set; }

    /// <summary>
    /// Net balance (for sorting and verification).
    /// </summary>
    public double NetBalance => DebitBalance - CreditBalance;
}
