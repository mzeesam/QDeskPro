namespace QDeskPro.Domain.Entities;

/// <summary>
/// Many-to-many relationship for user quarry assignments.
/// </summary>
public class UserQuarry : BaseEntity
{
    /// <summary>
    /// ApplicationUser ID
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Quarry ID
    /// </summary>
    public string? QuarryId { get; set; }

    /// <summary>
    /// Whether this is the user's primary quarry assignment
    /// </summary>
    public bool IsPrimary { get; set; }

    // Navigation properties

    /// <summary>
    /// The user assigned to the quarry
    /// </summary>
    public virtual ApplicationUser? User { get; set; }

    /// <summary>
    /// The quarry the user is assigned to
    /// </summary>
    public virtual Quarry? Quarry { get; set; }
}
