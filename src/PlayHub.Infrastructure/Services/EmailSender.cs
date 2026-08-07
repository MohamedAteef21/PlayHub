using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using PlayHub.Application.Alerts;
using PlayHub.Domain.Entities;

namespace PlayHub.Infrastructure.Services;

public class EmailSender : IEmailSender
{
    public Task SendAsync(
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

        return SendWithCredentialsAsync(
            settings.SmtpUsername,
            settings.SmtpPassword,
            settings.SenderDisplayName,
            toEmail,
            subject,
            bodyText,
            pdfAttachment,
            pdfFileName,
            ct);
    }

    public async Task SendWithCredentialsAsync(
        string smtpUsername,
        string smtpPassword,
        string? senderDisplayName,
        string toEmail,
        string subject,
        string bodyText,
        byte[]? pdfAttachment = null,
        string? pdfFileName = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(smtpUsername) || string.IsNullOrWhiteSpace(smtpPassword))
            throw new InvalidOperationException("Gmail SMTP credentials are not configured.");

        if (string.IsNullOrWhiteSpace(toEmail))
            throw new InvalidOperationException("Recipient email is required.");

        const string host = "smtp.gmail.com";
        const int port = 587;

        var message = new MimeMessage();
        var fromName = string.IsNullOrWhiteSpace(senderDisplayName)
            ? "PlayHub System"
            : senderDisplayName.Trim();
        message.From.Add(new MailboxAddress(fromName, smtpUsername.Trim()));
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
        await client.AuthenticateAsync(smtpUsername.Trim(), smtpPassword, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}
