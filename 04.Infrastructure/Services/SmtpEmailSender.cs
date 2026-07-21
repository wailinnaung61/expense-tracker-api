using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Application.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace _04.Infrastructure.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly EmailSettings _settings;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailSettings> options, ILogger<SmtpEmailSender> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string toAddress, string subject, string bodyHtml, CancellationToken ct = default)
    {
        var smtp = _settings.Smtp;
        if (string.IsNullOrWhiteSpace(smtp.Host) || string.IsNullOrWhiteSpace(smtp.FromAddress))
            throw new InvalidOperationException("SMTP Host and FromAddress must be configured.");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(smtp.FromName, smtp.FromAddress));
        message.To.Add(MailboxAddress.Parse(toAddress));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = bodyHtml };

        using var client = new SmtpClient();
        var secure = smtp.UseSsl
            ? (smtp.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls)
            : SecureSocketOptions.None;

        await client.ConnectAsync(smtp.Host, smtp.Port, secure, ct);

        if (!string.IsNullOrWhiteSpace(smtp.Username))
            await client.AuthenticateAsync(smtp.Username, smtp.Password, ct);

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);

        _logger.LogInformation("SMTP email sent to {To} subject={Subject}", toAddress, subject);
    }
}
