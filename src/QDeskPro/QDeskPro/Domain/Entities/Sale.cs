namespace QDeskPro.Domain.Entities;

/// <summary>
/// Individual sale transaction (core business entity).
/// </summary>
public class Sale : BaseEntity
{
    // Transaction Details

    /// <summary>
    /// Date of the sale
    /// </summary>
    public DateTime? SaleDate { get; set; }

    /// <summary>
    /// Vehicle registration number (required)
    /// </summary>
    public string VehicleRegistration { get; set; } = string.Empty;

    // Client Details (optional)

    /// <summary>
    /// Name of the client
    /// </summary>
    public string? ClientName { get; set; }

    /// <summary>
    /// Client phone number
    /// </summary>
    public string? ClientPhone { get; set; }

    /// <summary>
    /// Destination (Kenya county)
    /// </summary>
    public string? Destination { get; set; }

    // Product Details

    /// <summary>
    /// Foreign key to product
    /// </summary>
    public string? ProductId { get; set; }

    /// <summary>
    /// Foreign key to layer
    /// </summary>
    public string? LayerId { get; set; }

    /// <summary>
    /// Number of pieces sold
    /// </summary>
    public double Quantity { get; set; }

    /// <summary>
    /// Price at time of sale
    /// </summary>
    public double PricePerUnit { get; set; }

    // Broker/Commission

    /// <summary>
    /// Foreign key to broker (nullable)
    /// </summary>
    public string? BrokerId { get; set; }

    /// <summary>
    /// Commission per piece (0 if no broker)
    /// </summary>
    public double CommissionPerUnit { get; set; }

    // Payment

    /// <summary>
    /// Payment status ("Paid" or "NotPaid")
    /// </summary>
    public string? PaymentStatus { get; set; }

    /// <summary>
    /// Payment mode ("Cash", "MPESA", "Bank Transfer")
    /// </summary>
    public string? PaymentMode { get; set; }

    /// <summary>
    /// Transaction reference
    /// </summary>
    public string? PaymentReference { get; set; }

    // Clerk

    /// <summary>
    /// Clerk who recorded the sale
    /// </summary>
    public string? ApplicationUserId { get; set; }

    /// <summary>
    /// Clerk name (denormalized for reporting)
    /// </summary>
    public string? ClerkName { get; set; }

    // Calculated property (read-only)

    /// <summary>
    /// Gross sale amount (Quantity × PricePerUnit)
    /// </summary>
    public double GrossSaleAmount => Quantity * PricePerUnit;

    // Navigation properties

    /// <summary>
    /// The product sold
    /// </summary>
    public virtual Product? Product { get; set; }

    /// <summary>
    /// The layer the product came from
    /// </summary>
    public virtual Layer? Layer { get; set; }

    /// <summary>
    /// The broker who facilitated the sale
    /// </summary>
    public virtual Broker? Broker { get; set; }

    /// <summary>
    /// The clerk who recorded the sale
    /// </summary>
    public virtual ApplicationUser? Clerk { get; set; }
}
