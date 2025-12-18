namespace QDeskPro.Domain.Entities;

/// <summary>
/// Daily fuel consumption tracking.
/// </summary>
public class FuelUsage : BaseEntity
{
    /// <summary>
    /// Date of the fuel usage record
    /// </summary>
    public DateTime? UsageDate { get; set; }

    /// <summary>
    /// Opening balance (liters)
    /// </summary>
    public double OldStock { get; set; }

    /// <summary>
    /// New fuel received (liters)
    /// </summary>
    public double NewStock { get; set; }

    /// <summary>
    /// Fuel used by machines
    /// </summary>
    public double MachinesLoaded { get; set; }

    /// <summary>
    /// Fuel used by wheel loaders
    /// </summary>
    public double WheelLoadersLoaded { get; set; }

    /// <summary>
    /// Clerk who recorded the fuel usage
    /// </summary>
    public string? ApplicationUserId { get; set; }

    // Calculated properties

    /// <summary>
    /// Total stock (OldStock + NewStock)
    /// </summary>
    public double TotalStock => OldStock + NewStock;

    /// <summary>
    /// Total fuel used (MachinesLoaded + WheelLoadersLoaded)
    /// </summary>
    public double Used => MachinesLoaded + WheelLoadersLoaded;

    /// <summary>
    /// Remaining balance (TotalStock - Used)
    /// </summary>
    public double Balance => TotalStock - Used;
}
