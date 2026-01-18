namespace QDeskPro.Domain.Entities;

/// <summary>
/// Banking/deposit transactions.
/// </summary>
public class Banking : BaseEntity
{
    /// <summary>
    /// Date of the banking transaction
    /// </summary>
    public DateTime? BankingDate { get; set; }

    /// <summary>
    /// Description (e.g., "Daily deposit")
    /// </summary>
    public string? Item { get; set; }

    /// <summary>
    /// Balance brought forward (optional)
    /// </summary>
    public double BalanceBF { get; set; }

    /// <summary>
    /// Amount deposited
    /// </summary>
    public double AmountBanked { get; set; }

    /// <summary>
    /// Bank reference number
    /// </summary>
    public string? TxnReference { get; set; }

    /// <summary>
    /// Short reference code
    /// </summary>
    public string? RefCode { get; set; }

    /// <summary>
    /// Clerk who recorded the banking
    /// </summary>
    public string? ApplicationUserId { get; set; }

    /// <summary>
    /// Optional notes for this banking record
    /// </summary>
    public string? Notes { get; set; }
}
