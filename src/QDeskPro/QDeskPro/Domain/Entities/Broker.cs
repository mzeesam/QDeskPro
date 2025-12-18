namespace QDeskPro.Domain.Entities;

/// <summary>
/// Sales brokers who earn commission on sales they facilitate.
/// </summary>
public class Broker : BaseEntity
{
    /// <summary>
    /// Full name of the broker
    /// </summary>
    public string BrokerName { get; set; } = string.Empty;

    /// <summary>
    /// Contact phone number
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Assigned quarry ID (lowercase for legacy compatibility)
    /// </summary>
    public string? quarryId { get; set; }

    // Navigation property

    /// <summary>
    /// The quarry this broker is assigned to
    /// </summary>
    public virtual Quarry? Quarry { get; set; }
}
