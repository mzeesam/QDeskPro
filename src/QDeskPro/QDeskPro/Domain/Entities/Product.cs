namespace QDeskPro.Domain.Entities;

/// <summary>
/// Product types sold by quarries.
/// </summary>
public class Product : BaseEntity
{
    /// <summary>
    /// Name of the product (e.g., "Size 6", "Size 9", "Reject")
    /// </summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of the product
    /// </summary>
    public string? Description { get; set; }

    // Navigation property

    /// <summary>
    /// Prices for this product across different quarries
    /// </summary>
    public virtual ICollection<ProductPrice> Prices { get; set; } = new List<ProductPrice>();
}
