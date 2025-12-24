using System.Threading.Channels;

namespace QDeskPro.Features.Reports.Services;

/// <summary>
/// Background service for processing queued emails asynchronously
/// </summary>
public class EmailQueueService : BackgroundService, IEmailQueue
{
    private readonly Channel<EmailMessage> _queue;
    private readonly ILogger<EmailQueueService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public EmailQueueService(
        ILogger<EmailQueueService> logger,
        IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;

        // Create unbounded channel for email queue
        var options = new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = true
        };
        _queue = Channel.CreateUnbounded<EmailMessage>(options);
    }

    /// <summary>
    /// Queue an email for background delivery
    /// </summary>
    public void QueueEmail(
        string to,
        string subject,
        string htmlBody,
        byte[]? attachmentData = null,
        string? attachmentFileName = null,
        string? attachmentContentType = null)
    {
        QueueEmail(
            new List<string> { to },
            subject,
            htmlBody,
            attachmentData,
            attachmentFileName,
            attachmentContentType);
    }

    /// <summary>
    /// Queue an email with multiple recipients
    /// </summary>
    public void QueueEmail(
        List<string> recipients,
        string subject,
        string htmlBody,
        byte[]? attachmentData = null,
        string? attachmentFileName = null,
        string? attachmentContentType = null)
    {
        if (recipients == null || recipients.Count == 0)
        {
            _logger.LogWarning("Attempted to queue email with no recipients");
            return;
        }

        var message = new EmailMessage
        {
            Recipients = recipients,
            Subject = subject,
            HtmlBody = htmlBody,
            AttachmentData = attachmentData,
            AttachmentFileName = attachmentFileName,
            AttachmentContentType = attachmentContentType,
            QueuedAt = DateTime.UtcNow
        };

        if (_queue.Writer.TryWrite(message))
        {
            _logger.LogInformation("Email queued for delivery to {Recipients}: {Subject}",
                string.Join(", ", recipients), subject);
        }
        else
        {
            _logger.LogError("Failed to queue email to {Recipients}: {Subject}",
                string.Join(", ", recipients), subject);
        }
    }

    /// <summary>
    /// Dequeue an email message
    /// </summary>
    public async Task<EmailMessage?> DequeueAsync(CancellationToken cancellationToken)
    {
        var message = await _queue.Reader.ReadAsync(cancellationToken);
        return message;
    }

    /// <summary>
    /// Background task to process email queue
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Email Queue Service started");

        await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                // Create a scope to resolve scoped services (like ReportEmailService)
                using var scope = _serviceScopeFactory.CreateScope();
                var emailService = scope.ServiceProvider.GetRequiredService<ReportEmailService>();

                // Send the email
                var success = await emailService.SendEmailAsync(
                    message.Recipients,
                    message.Subject,
                    message.HtmlBody,
                    message.AttachmentData,
                    message.AttachmentFileName,
                    message.AttachmentContentType);

                if (success)
                {
                    var queueTime = DateTime.UtcNow - message.QueuedAt;
                    _logger.LogInformation(
                        "Email processed successfully after {QueueTime}ms in queue: {Subject} to {Recipients}",
                        queueTime.TotalMilliseconds,
                        message.Subject,
                        string.Join(", ", message.Recipients));
                }
                else
                {
                    _logger.LogWarning("Email processing failed: {Subject} to {Recipients}",
                        message.Subject,
                        string.Join(", ", message.Recipients));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing email from queue: {Subject}",
                    message.Subject);
            }

            // Small delay between emails to avoid overwhelming SMTP server
            await Task.Delay(100, stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Email Queue Service stopping. Waiting for queue to drain...");

        // Complete the channel writer to signal no more messages
        _queue.Writer.Complete();

        // Wait for the background task to complete
        await base.StopAsync(cancellationToken);

        _logger.LogInformation("Email Queue Service stopped");
    }
}
