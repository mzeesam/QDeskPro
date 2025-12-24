namespace QDeskPro.Features.Reports.Services;

/// <summary>
/// Interface for queuing email messages for background delivery
/// </summary>
public interface IEmailQueue
{
    /// <summary>
    /// Queue an email message for background delivery
    /// </summary>
    /// <param name="to">Recipient email address</param>
    /// <param name="subject">Email subject</param>
    /// <param name="htmlBody">HTML email body</param>
    /// <param name="attachmentData">Optional attachment data</param>
    /// <param name="attachmentFileName">Optional attachment filename</param>
    /// <param name="attachmentContentType">Optional attachment content type</param>
    void QueueEmail(
        string to,
        string subject,
        string htmlBody,
        byte[]? attachmentData = null,
        string? attachmentFileName = null,
        string? attachmentContentType = null);

    /// <summary>
    /// Queue an email with multiple recipients
    /// </summary>
    /// <param name="recipients">List of recipient email addresses</param>
    /// <param name="subject">Email subject</param>
    /// <param name="htmlBody">HTML email body</param>
    /// <param name="attachmentData">Optional attachment data</param>
    /// <param name="attachmentFileName">Optional attachment filename</param>
    /// <param name="attachmentContentType">Optional attachment content type</param>
    void QueueEmail(
        List<string> recipients,
        string subject,
        string htmlBody,
        byte[]? attachmentData = null,
        string? attachmentFileName = null,
        string? attachmentContentType = null);

    /// <summary>
    /// Dequeue an email message for processing
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Email message or null if queue is empty</returns>
    Task<EmailMessage?> DequeueAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Email message for queuing
/// </summary>
public class EmailMessage
{
    public List<string> Recipients { get; set; } = new();
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public byte[]? AttachmentData { get; set; }
    public string? AttachmentFileName { get; set; }
    public string? AttachmentContentType { get; set; }
    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;
}
