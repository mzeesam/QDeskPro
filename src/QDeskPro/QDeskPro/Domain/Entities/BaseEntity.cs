namespace QDeskPro.Domain.Entities;

/// <summary>
/// Base class for all domain entities with consistent audit tracking.
/// </summary>
public abstract class BaseEntity
{
    /// <summary>
    /// GUID primary key
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Soft delete flag
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Date when the entity was created
    /// </summary>
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User ID who created the entity
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Date when the entity was last modified
    /// </summary>
    public DateTime? DateModified { get; set; }

    /// <summary>
    /// User ID who last modified the entity
    /// </summary>
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Date stamp in "yyyyMMdd" format for daily grouping
    /// </summary>
    public string? DateStamp { get; set; }

    /// <summary>
    /// Quarry ID for multi-tenant isolation
    /// </summary>
    public string? QId { get; set; }
}
