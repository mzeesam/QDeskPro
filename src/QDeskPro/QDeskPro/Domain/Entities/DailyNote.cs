namespace QDeskPro.Domain.Entities;

/// <summary>
/// End-of-day notes and closing balance.
/// </summary>
public class DailyNote : BaseEntity
{
    /// <summary>
    /// Date of the note
    /// </summary>
    public DateTime? NoteDate { get; set; }

    /// <summary>
    /// Free-text notes
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Cash in hand at end of day
    /// </summary>
    public double ClosingBalance { get; set; }

    /// <summary>
    /// Quarry ID (lowercase for legacy compatibility)
    /// </summary>
    public string? quarryId { get; set; }
}
