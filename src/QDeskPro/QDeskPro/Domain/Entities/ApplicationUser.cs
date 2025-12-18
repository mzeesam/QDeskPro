using Microsoft.AspNetCore.Identity;

namespace QDeskPro.Domain.Entities;

/// <summary>
/// Extends ASP.NET Identity User for quarry personnel.
/// </summary>
public class ApplicationUser : IdentityUser
{
    /// <summary>
    /// Display name of the user
    /// </summary>
    public string? FullName { get; set; }

    /// <summary>
    /// Job title/role description
    /// </summary>
    public string? Position { get; set; }

    /// <summary>
    /// Primary assigned quarry (for clerks)
    /// </summary>
    public string? QuarryId { get; set; }

    /// <summary>
    /// Account active status
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Quarries owned by this manager (one-to-many)
    /// </summary>
    public virtual ICollection<Quarry> OwnedQuarries { get; set; } = new List<Quarry>();

    /// <summary>
    /// Quarries assigned to this user
    /// </summary>
    public virtual ICollection<UserQuarry> QuarryAssignments { get; set; } = new List<UserQuarry>();
}
