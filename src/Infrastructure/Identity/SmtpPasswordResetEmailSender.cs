using System.Net;
using System.Net.Mail;
using Application.Abstractions.Services;
using Microsoft.Extensions.Options;

namespace Infrastructure.Identity;

public sealed class SmtpPasswordResetEmailSender : IPasswordResetEmailSender
{
    private readonly IOptionsMonitor<SmtpOptions> _options;

    public SmtpPasswordResetEmailSender(IOptionsMonitor<SmtpOptions> options)
    {
        _options = options;
    }

    public async Task SendPasswordResetAsync(
        string recipientEmail,
        string recipientName,
        string code,
        CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;
        ValidateConfiguration(options);

        var senderEmail = string.IsNullOrWhiteSpace(options.SenderEmail)
            ? options.User
            : options.SenderEmail;
        var password = options.Host.Contains("gmail.com", StringComparison.OrdinalIgnoreCase)
            ? options.Password.Replace(" ", string.Empty)
            : options.Password;

        var body = $"""
            <div style="font-family:Arial,Helvetica,sans-serif;color:#111827;">
              <h2 style="margin:0 0 12px;">Password reset code</h2>
              <p>Hello {WebUtility.HtmlEncode(recipientName)},</p>
              <p>Your password reset code is:</p>
              <div style="font-size:32px;font-weight:bold;letter-spacing:6px;margin:18px 0;">
                {WebUtility.HtmlEncode(code)}
              </div>
              <p>This code expires in 1 hour.</p>
              <p>If you did not request this, ignore this email.</p>
            </div>
            """;

        using var message = new MailMessage
        {
            From = new MailAddress(senderEmail, options.SenderName),
            Subject = "Reset your MES Platform password",
            Body = body,
            IsBodyHtml = true
        };
        message.To.Add(new MailAddress(recipientEmail, recipientName));

        using var client = new SmtpClient(options.Host, options.Port)
        {
            EnableSsl = options.EnableSsl,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(options.User, password),
            Timeout = 15000
        };

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        await client.SendMailAsync(message, timeout.Token);
    }

    private static void ValidateConfiguration(SmtpOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Host) ||
            string.IsNullOrWhiteSpace(options.User) ||
            string.IsNullOrWhiteSpace(options.Password))
        {
            throw new InvalidOperationException(
                "SmtpSettings Host, User and Password must be configured before password reset email can be sent.");
        }
    }
}
