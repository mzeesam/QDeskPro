namespace QDeskPro.Domain.Entities;

/// <summary>
/// Represents a customer prepayment/deposit made before product collection.
/// Tracks advance payments and supports multiple fulfillment trips.
/// </summary>
public class Prepayment : BaseEntity
{
    // Customer Identification
    /// <summary>
    /// Primary identifier for the customer (required)
    /// </summary>
    public string VehicleRegistration { get; set; } = "";

    /// <summary>
    /// Optional client name
    /// </summary>
    public string? ClientName { get; set; }

    /// <summary>
    /// Optional client phone number
    /// </summary>
    public string? ClientPhone { get; set; }

    // Prepayment Details
    /// <summary>
    /// Date when payment was received
    /// </summary>
    public DateTime PrepaymentDate { get; set; }

    /// <summary>
    /// Total amount customer prepaid
    /// </summary>
    public double TotalAmountPaid { get; set; }

    /// <summary>
    /// Amount already applied to sales
    /// </summary>
    public double AmountUsed { get; set; }

    /// <summary>
    /// Calculated remaining balance available for fulfillment
    /// </summary>
    public double RemainingBalance => TotalAmountPaid - AmountUsed;

    // Original Intent (for reference - can change on fulfillment)
    /// <summary>
    /// Product customer planned to buy (optional, can change during fulfillment)
    /// </summary>
    public string? IntendedProductId { get; set; }

    /// <summary>
    /// Quantity they planned (optional)
    /// </summary>
    public double? IntendedQuantity { get; set; }

    /// <summary>
    /// Price locked at prepayment time (honors original pricing)
    /// </summary>
    public double? IntendedPricePerUnit { get; set; }

    // Payment Details
    /// <summary>
    /// Cash, MPESA, Bank Transfer
    /// </summary>
    public string PaymentMode { get; set; } = "";

    /// <summary>
    /// MPESA code, bank reference, etc.
    /// </summary>
    public string? PaymentReference { get; set; }

    // Status Tracking
    /// <summary>
    /// Active: Full balance remaining (AmountUsed = 0)
    /// Partial: Partially used (0 &lt; AmountUsed &lt; TotalAmountPaid)
    /// Fulfilled: Fully used (AmountUsed = TotalAmountPaid)
    /// Refunded: Prepayment refunded to customer
    /// </summary>
    public string Status { get; set; } = "Active";

    /// <summary>
    /// Date when remaining balance reached zero
    /// </summary>
    public DateTime? FullyFulfilledDate { get; set; }

    // Clerk who recorded prepayment
    /// <summary>
    /// User ID of clerk who recorded this prepayment
    /// </summary>
    public string ApplicationUserId { get; set; } = "";

    /// <summary>
    /// Clerk's full name (denormalized for reporting)
    /// </summary>
    public string ClerkName { get; set; } = "";

    // Notes
    /// <summary>
    /// Free-text notes about this prepayment
    /// </summary>
    public string? Notes { get; set; }

    // Navigation Properties
    /// <summary>
    /// Intended product (nullable, can change during fulfillment)
    /// </summary>
    public virtual Product? IntendedProduct { get; set; }

    /// <summary>
    /// Sales that used this prepayment for fulfillment
    /// </summary>
    public virtual ICollection<Sale> FulfillmentSales { get; set; } = new List<Sale>();
}
