using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using PlayHub.Application.Alerts;
using PlayHub.Domain.Entities;

namespace PlayHub.Infrastructure.Services;

public class EmailSender : IEmailSender
{
    public async Task SendAsync(
        MasterAlertSettings settings,
        string toEmail,
        string subject,
        string bodyText,
        byte[]? pdfAttachment = null,
        string? pdfFileName = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(settings.SmtpUsername) || string.IsNullOrWhiteSpace(settings.SmtpPassword))
            throw new InvalidOperationException("Gmail SMTP credentials are not configured.");

        if (string.IsNullOrWhiteSpace(toEmail))
            throw new InvalidOperationException("Recipient email is required.");

        var host = string.IsNullOrWhiteSpace(settings.SmtpHost) ? "smtp.gmail.com" : settings.SmtpHost.Trim();
        var port = settings.SmtpPort > 0 ? settings.SmtpPort : 587;

        var message = new MimeMessage();
        var fromName = string.IsNullOrWhiteSpace(settings.SenderDisplayName)
            ? "PlayHub"
            : settings.SenderDisplayName.Trim();
        message.From.Add(new MailboxAddress(fromName, settings.SmtpUsername.Trim()));
        message.To.Add(MailboxAddress.Parse(toEmail.Trim()));
        message.Subject = subject;

        var builder = new BodyBuilder { TextBody = bodyText };
        if (pdfAttachment is { Length: > 0 })
        {
            builder.Attachments.Add(
                string.IsNullOrWhiteSpace(pdfFileName) ? "invoice.pdf" : pdfFileName,
                pdfAttachment,
                new ContentType("application", "pdf"));
        }

        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(host, port, SecureSocketOptions.StartTls, ct);
        await client.AuthenticateAsync(settings.SmtpUsername.Trim(), settings.SmtpPassword, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}
