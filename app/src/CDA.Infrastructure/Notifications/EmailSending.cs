using CDA.Application.Abstractions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace CDA.Infrastructure.Notifications;

/// <summary>Where mail goes, and who it comes from.</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string? Host { get; set; }

    public int Port { get; set; } = 587;

    public string? UserName { get; set; }

    public string? Password { get; set; }

    public string FromAddress { get; set; } = "noreply@localhost";

    public string FromName { get; set; } = "Crowd Discusses Alternatives";

    /// <summary>Configured when there is a host to send through.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);
}

/// <summary>
/// The sender used when no mail host is configured.
/// </summary>
/// <remarks>
/// It writes what it would have sent to the log and reports that it cannot deliver, so the rest
/// of the application can tell the difference and say so. The alternative — quietly accepting
/// mail and dropping it — produces a platform that looks like it notifies people and does not,
/// which is the worst of the three possible behaviours.
/// </remarks>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public bool CanDeliver => false;

    public Task SendAsync(
        string toAddress,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "No mail host is configured, so this email was not sent. To: {To}. Subject: {Subject}",
            toAddress, subject);

        return Task.CompletedTask;
    }
}

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(EmailOptions options, ILogger<SmtpEmailSender> logger)
    {
        // Only ever constructed when a host is configured; asserting it here means the send
        // path does not have to keep re-checking something that cannot change.
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Host);

        _options = options;
        _logger = logger;
    }

    public bool CanDeliver => true;

    public async Task SendAsync(
        string toAddress,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(MailboxAddress.Parse(toAddress));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();

        // StartTls, not None: these messages carry the content of private topics.
        await client.ConnectAsync(_options.Host!, _options.Port, SecureSocketOptions.StartTls, cancellationToken);

        if (!string.IsNullOrWhiteSpace(_options.UserName))
        {
            await client.AuthenticateAsync(_options.UserName, _options.Password ?? string.Empty, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);

        _logger.LogInformation("Sent {Subject} to {To}", subject, toAddress);
    }
}
