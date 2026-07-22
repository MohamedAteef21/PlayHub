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

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(
            AlertSettingsService.FixedSenderDisplayName,
            settings.SmtpUsername.Trim()));
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
        await client.ConnectAsync(
            AlertSettingsService.FixedSmtpHost,
            AlertSettingsService.FixedSmtpPort,
            SecureSocketOptions.StartTls,
            ct);
        await client.AuthenticateAsync(settings.SmtpUsername.Trim(), settings.SmtpPassword, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}
