namespace QDeskPro.Domain.Entities;

/// <summary>
/// A quarry operation site with fee configurations.
/// </summary>
public class Quarry : BaseEntity
{
    /// <summary>
    /// Name of the quarry (e.g., "Thika - Komu")
    /// </summary>
    public string QuarryName { get; set; } = string.Empty;

    /// <summary>
    /// Physical location of the quarry
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Owner/Manager who created this quarry
    /// </summary>
    public string? ManagerId { get; set; }

    /// <summary>
    /// Per-unit fee for loaders (e.g., 50 KES)
    /// </summary>
    public double? LoadersFee { get; set; }

    /// <summary>
    /// Per-unit land rate fee (e.g., 10 KES)
    /// </summary>
    public double? LandRateFee { get; set; }

    /// <summary>
    /// Alternative rate for Reject products (e.g., 5 KES)
    /// </summary>
    public double? RejectsFee { get; set; }

    /// <summary>
    /// Comma-separated emails for reports
    /// </summary>
    public string? EmailRecipients { get; set; }

    /// <summary>
    /// Whether daily report delivery is enabled
    /// </summary>
    public bool DailyReportEnabled { get; set; }

    /// <summary>
    /// Time of day to send daily report
    /// </summary>
    public TimeSpan? DailyReportTime { get; set; }

    // Navigation properties

    /// <summary>
    /// Quarry owner (Manager)
    /// </summary>
    public virtual ApplicationUser? Manager { get; set; }

    /// <summary>
    /// Layers in this quarry
    /// </summary>
    public virtual ICollection<Layer> Layers { get; set; } = new List<Layer>();

    /// <summary>
    /// Brokers assigned to this quarry
    /// </summary>
    public virtual ICollection<Broker> Brokers { get; set; } = new List<Broker>();

    /// <summary>
    /// Product prices for this quarry
    /// </summary>
    public virtual ICollection<ProductPrice> ProductPrices { get; set; } = new List<ProductPrice>();

    /// <summary>
    /// Users assigned to this quarry
    /// </summary>
    public virtual ICollection<UserQuarry> UserQuarries { get; set; } = new List<UserQuarry>();
}
