namespace QDeskPro.Shared.Models;

public class JwtSettings
{
    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "QDeskPro";
    public string Audience { get; set; } = "QDeskProUsers";
    public int ExpirationMinutes { get; set; } = 60; // 1 hour
    public int RefreshTokenExpirationDays { get; set; } = 7; // 7 days
}
