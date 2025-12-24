using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace QDeskPro.Features.Reports.Services;

/// <summary>
/// Service for sending report emails using SMTP
/// </summary>
public class ReportEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<ReportEmailService> _logger;

    public ReportEmailService(
        IOptions<EmailSettings> settings,
        ILogger<ReportEmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Send an email with optional attachment
    /// </summary>
    public async Task<bool> SendEmailAsync(
        string to,
        string subject,
        string htmlBody,
        byte[]? attachmentData = null,
        string? attachmentFileName = null,
        string? attachmentContentType = null)
    {
        return await SendEmailAsync(
            new List<string> { to },
            subject,
            htmlBody,
            attachmentData,
            attachmentFileName,
            attachmentContentType);
    }

    /// <summary>
    /// Send an email to multiple recipients with optional attachment
    /// </summary>
    public async Task<bool> SendEmailAsync(
        List<string> recipients,
        string subject,
        string htmlBody,
        byte[]? attachmentData = null,
        string? attachmentFileName = null,
        string? attachmentContentType = null)
    {
        if (!_settings.EnableEmailSending)
        {
            _logger.LogWarning("Email sending is disabled in configuration. Email to {Recipients} not sent.",
                string.Join(", ", recipients));
            return false;
        }

        if (string.IsNullOrWhiteSpace(_settings.SmtpServer))
        {
            _logger.LogError("SMTP server not configured. Cannot send email.");
            return false;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));

            foreach (var recipient in recipients)
            {
                if (!string.IsNullOrWhiteSpace(recipient))
                {
                    message.To.Add(MailboxAddress.Parse(recipient));
                }
            }

            if (message.To.Count == 0)
            {
                _logger.LogWarning("No valid recipients provided for email");
                return false;
            }

            message.Subject = subject;

            // Build message body
            var builder = new BodyBuilder
            {
                HtmlBody = WrapInTemplate(subject, htmlBody)
            };

            // Add attachment if provided
            if (attachmentData != null && !string.IsNullOrWhiteSpace(attachmentFileName))
            {
                builder.Attachments.Add(
                    attachmentFileName,
                    attachmentData,
                    ContentType.Parse(attachmentContentType ?? "application/octet-stream"));
            }

            message.Body = builder.ToMessageBody();

            // Send email using SMTP
            using var client = new SmtpClient();

            // Determine secure socket options based on port
            var secureSocketOptions = _settings.SmtpPort switch
            {
                465 => SecureSocketOptions.SslOnConnect,
                587 => SecureSocketOptions.StartTls,
                25 => _settings.UseSsl ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.None,
                _ => _settings.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto
            };

            await client.ConnectAsync(_settings.SmtpServer, _settings.SmtpPort, secureSocketOptions);

            // Authenticate if credentials provided
            if (!string.IsNullOrWhiteSpace(_settings.Username) && !string.IsNullOrWhiteSpace(_settings.Password))
            {
                await client.AuthenticateAsync(_settings.Username, _settings.Password);
            }

            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email sent successfully to {Recipients}: {Subject}",
                string.Join(", ", recipients), subject);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Recipients}: {Subject}",
                string.Join(", ", recipients), subject);
            return false;
        }
    }

    /// <summary>
    /// Wrap HTML content in email template
    /// </summary>
    private string WrapInTemplate(string subject, string htmlBody)
    {
        return $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{subject}</title>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
            background-color: #f5f5f5;
        }}
        .email-container {{
            background-color: #ffffff;
            border-radius: 8px;
            padding: 30px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }}
        .email-header {{
            border-bottom: 3px solid #1976D2;
            padding-bottom: 20px;
            margin-bottom: 30px;
        }}
        .email-header h1 {{
            color: #1976D2;
            margin: 0;
            font-size: 24px;
        }}
        .email-content {{
            margin-bottom: 30px;
        }}
        .email-footer {{
            border-top: 1px solid #e0e0e0;
            padding-top: 20px;
            margin-top: 30px;
            font-size: 12px;
            color: #666;
            text-align: center;
        }}
        table {{
            border-collapse: collapse;
            width: 100%;
            margin: 15px 0;
        }}
        th, td {{
            border: 1px solid #ddd;
            padding: 12px;
            text-align: left;
        }}
        th {{
            background-color: #1976D2;
            color: white;
            font-weight: 600;
        }}
        tr:nth-child(even) {{
            background-color: #f9f9f9;
        }}
    </style>
</head>
<body>
    <div class=""email-container"">
        <div class=""email-header"">
            <h1>QDeskPro</h1>
            <p style=""margin: 5px 0 0 0; color: #666;"">Quarry Management System</p>
        </div>
        <div class=""email-content"">
            {htmlBody}
        </div>
        <div class=""email-footer"">
            <p>This is an automated message from QDeskPro. Please do not reply to this email.</p>
            <p>&copy; {DateTime.Now.Year} QDeskPro. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";
    }
}
