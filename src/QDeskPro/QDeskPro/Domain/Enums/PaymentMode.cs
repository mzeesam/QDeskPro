namespace QDeskPro.Domain.Enums;

/// <summary>
/// Payment methods accepted for sales.
/// </summary>
public enum PaymentMode
{
    /// <summary>
    /// Cash payment
    /// </summary>
    Cash,

    /// <summary>
    /// Mobile money payment (Kenya)
    /// </summary>
    MPESA,

    /// <summary>
    /// Bank transfer payment
    /// </summary>
    BankTransfer
}
