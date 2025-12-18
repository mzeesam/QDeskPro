namespace QDeskPro.Domain.Enums;

/// <summary>
/// Payment status for sales transactions.
/// </summary>
public enum PaymentStatus
{
    /// <summary>
    /// Payment has been received
    /// </summary>
    Paid,

    /// <summary>
    /// Credit sale - payment pending
    /// </summary>
    NotPaid
}
