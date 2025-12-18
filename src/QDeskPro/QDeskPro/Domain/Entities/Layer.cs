namespace QDeskPro.Domain.Entities;

/// <summary>
/// Mining layer within a quarry (for tracking excavation progress).
/// </summary>
public class Layer : BaseEntity
{
    /// <summary>
    /// Layer level identifier (e.g., "Layer -1", "Layer -2")
    /// </summary>
    public string LayerLevel { get; set; } = string.Empty;

    /// <summary>
    /// Date when excavation started on this layer
    /// </summary>
    public DateTime? DateStarted { get; set; }

    /// <summary>
    /// Optional: Length of the layer in meters/feet
    /// </summary>
    public double? LayerLength { get; set; }

    /// <summary>
    /// Foreign key to the quarry
    /// </summary>
    public string? QuarryId { get; set; }

    // Navigation property

    /// <summary>
    /// The quarry this layer belongs to
    /// </summary>
    public virtual Quarry? Quarry { get; set; }
}
