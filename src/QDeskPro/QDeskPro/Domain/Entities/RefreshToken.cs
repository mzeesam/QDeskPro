namespace QDeskPro.Domain.Entities;

/// <summary>
/// Refresh token for persistent authentication across browser sessions
/// </summary>
public class RefreshToken
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Token { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; } = false;
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByToken { get; set; }
    public string? DeviceInfo { get; set; } // Browser/device identifier
    public string? IpAddress { get; set; }

    // Navigation
    public virtual ApplicationUser User { get; set; } = null!;

    public bool IsActive => !IsRevoked && ExpiresAt > DateTime.UtcNow;
}
