namespace QDeskPro.Features.Reports.Services;

/// <summary>
/// Email configuration settings for SMTP email delivery
/// </summary>
public class EmailSettings
{
    public const string SectionName = "EmailSettings";

    /// <summary>
    /// SMTP server hostname (e.g., mail.example.com)
    /// </summary>
    public string SmtpServer { get; set; } = string.Empty;

    /// <summary>
    /// SMTP server port (587 for StartTLS, 465 for SSL, 25 for plain)
    /// </summary>
    public int SmtpPort { get; set; } = 587;

    /// <summary>
    /// Sender email address
    /// </summary>
    public string SenderEmail { get; set; } = string.Empty;

    /// <summary>
    /// Sender display name
    /// </summary>
    public string SenderName { get; set; } = string.Empty;

    /// <summary>
    /// SMTP authentication username
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// SMTP authentication password
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Use SSL/TLS for secure connection
    /// </summary>
    public bool UseSsl { get; set; } = true;

    /// <summary>
    /// Master switch to enable/disable email sending
    /// </summary>
    public bool EnableEmailSending { get; set; } = false;
}
