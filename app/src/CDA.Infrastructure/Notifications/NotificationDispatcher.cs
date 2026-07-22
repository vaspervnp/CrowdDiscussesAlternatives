using CDA.Application.Abstractions;
using CDA.Domain.Notifications;
using CDA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CDA.Infrastructure.Notifications;

/// <summary>
/// Drains unsent notifications into email.
/// </summary>
/// <remarks>
/// <para>
/// The queue is the notifications table itself: a row with no <c>EmailedAtUtc</c> is waiting to
/// go. Keeping it there rather than in a separate outbox means a queued email can never refer to
/// something that was rolled back, because the two commit together.
/// </para>
/// <para>
/// A row is stamped as sent <em>before</em> the message goes out. That is deliberate: the two
/// failure modes are sending twice and sending nothing, and stamping afterwards risks an endless
/// loop of duplicates if the send succeeds and the stamp fails. Someone missing one notification
/// is a smaller harm than the platform mailing them the same thing every minute.
/// </para>
/// </remarks>
public sealed class NotificationDispatcher(
    IServiceScopeFactory scopes,
    ILogger<NotificationDispatcher> logger) : BackgroundService
{
    /// <summary>How often the queue is looked at.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    /// <summary>How long a digest waits before it is worth sending.</summary>
    public static readonly TimeSpan DigestWindow = TimeSpan.FromHours(24);

    /// <summary>Never send more than this in one pass; the next pass takes the rest.</summary>
    public const int BatchSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception error)
            {
                // A failing pass must not take the host down with it; the next one tries again.
                logger.LogError(error, "The notification dispatcher failed a pass");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    public async Task DispatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var email = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        if (!email.CanDeliver)
        {
            // No mail host: leave everything queued rather than stamping it sent. If one is
            // configured later, the backlog goes out instead of having been silently discarded.
            return;
        }

        var database = scope.ServiceProvider.GetRequiredService<CdaDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var now = clock.UtcNow;

        var pending = await database.Notifications
            .Where(n => n.EmailedAtUtc == null)
            .OrderBy(n => n.CreatedAtUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            return;
        }

        var userIds = pending.Select(n => n.UserId).Distinct().ToList();

        var preferences = await database.NotificationPreferences
            .AsNoTracking()
            .Where(p => userIds.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, p => p.Delivery, cancellationToken);

        var addresses = await database.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id) && u.Email != null)
            .ToDictionaryAsync(u => u.Id, u => u.Email!, cancellationToken);

        foreach (var group in pending.GroupBy(n => n.UserId))
        {
            var delivery = preferences.GetValueOrDefault(group.Key, NotificationDelivery.Daily);

            if (delivery == NotificationDelivery.None || !addresses.TryGetValue(group.Key, out var address))
            {
                // Not going out by email, but it stays in the platform's own list — so stamp it
                // to keep it out of the queue rather than reconsidering it every minute.
                foreach (var notification in group)
                {
                    notification.MarkEmailed(now);
                }

                continue;
            }

            var items = group.OrderBy(n => n.CreatedAtUtc).ToList();

            if (delivery == NotificationDelivery.Daily
                && now - items[0].CreatedAtUtc < DigestWindow)
            {
                // Still gathering; leave them queued for a later pass.
                continue;
            }

            foreach (var notification in items)
            {
                notification.MarkEmailed(now);
            }

            await database.SaveChangesAsync(cancellationToken);

            try
            {
                await email.SendAsync(address, SubjectFor(items), BodyFor(items), cancellationToken);
            }
            catch (Exception error)
            {
                // Already stamped, so this is not retried. See the note on the class about
                // preferring one lost message to an endless loop of duplicates.
                logger.LogError(error, "Failed to email {Count} notifications to {User}",
                    items.Count, group.Key);
            }
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    private static string SubjectFor(IReadOnlyList<Notification> items) => items.Count == 1
        ? items[0].Summary
        : $"{items.Count} things happened on Crowd Discusses Alternatives";

    private static string BodyFor(IReadOnlyList<Notification> items)
    {
        var lines = items.Select(item => $"- {item.Summary}\n  {item.Link}");

        return string.Join("\n\n", lines)
            + "\n\nYou can change how often you get these on your profile.";
    }
}
