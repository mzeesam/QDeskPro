namespace QDeskPro.Domain.Entities;

/// <summary>
/// Product pricing per quarry (allows different quarries to have different prices).
/// </summary>
public class ProductPrice : BaseEntity
{
    /// <summary>
    /// Foreign key to the product
    /// </summary>
    public string? ProductId { get; set; }

    /// <summary>
    /// Foreign key to the quarry
    /// </summary>
    public string? QuarryId { get; set; }

    /// <summary>
    /// Price per unit in KES
    /// </summary>
    public double Price { get; set; }

    // Navigation properties

    /// <summary>
    /// The product this price is for
    /// </summary>
    public virtual Product? Product { get; set; }

    /// <summary>
    /// The quarry this price applies to
    /// </summary>
    public virtual Quarry? Quarry { get; set; }
}
