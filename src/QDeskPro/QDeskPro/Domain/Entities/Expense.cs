namespace QDeskPro.Domain.Entities;

/// <summary>
/// Manual expense entries by clerks.
/// </summary>
public class Expense : BaseEntity
{
    /// <summary>
    /// Date of the expense
    /// </summary>
    public DateTime? ExpenseDate { get; set; }

    /// <summary>
    /// Description of the expense
    /// </summary>
    public string Item { get; set; } = string.Empty;

    /// <summary>
    /// Amount in KES
    /// </summary>
    public double Amount { get; set; }

    /// <summary>
    /// Expense category
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Payment reference
    /// </summary>
    public string? TxnReference { get; set; }

    /// <summary>
    /// Clerk who recorded the expense
    /// </summary>
    public string? ApplicationUserId { get; set; }

    /// <summary>
    /// Optional notes for this expense
    /// </summary>
    public string? Notes { get; set; }
}
