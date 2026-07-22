namespace CDA.Application.Abstractions;

/// <summary>How the platform gets mail out, when it has anywhere to send it.</summary>
public interface IEmailSender
{
    /// <summary>
    /// Whether mail can actually be delivered.
    /// </summary>
    /// <remarks>
    /// Deliberately visible to the rest of the application. Several things — account
    /// confirmation, password reset, email notification — are only honest to offer when this is
    /// true, and silently pretending to send is worse than saying plainly that it is off.
    /// </remarks>
    bool CanDeliver { get; }

    Task SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken = default);
}
