using CDA.Application.Abstractions;
using CDA.Domain.Notifications;
using CDA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CDA.Infrastructure.Notifications;

public sealed record NotificationView(
    Guid Id,
    NotificationKind Kind,
    string Summary,
    string Link,
    string? ActorDisplayName,
    DateTime CreatedAtUtc,
    bool IsRead);

public sealed class NotificationService(CdaDbContext database, IClock clock)
{
    public const int PageSize = 50;

    /// <summary>
    /// Records that something happened which one person should know about.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Added to the current unit of work rather than saved here, so the notification commits
    /// with whatever caused it. A notification about a comment that was rolled back would send
    /// someone to a page that does not exist.
    /// </para>
    /// <para>
    /// Nobody is notified about their own doing. Being told that you commented on your own
    /// proposal is noise, and noise is what makes people stop reading notifications.
    /// </para>
    /// </remarks>
    public void Enqueue(
        Guid userId,
        NotificationKind kind,
        string summary,
        string link,
        Guid? topicId = null,
        Guid? actorId = null)
    {
        if (actorId == userId)
        {
            return;
        }

        database.Notifications.Add(
            new Notification(userId, kind, summary, link, clock.UtcNow, topicId, actorId));
    }

    public async Task<List<NotificationView>> ForUserAsync(
        Guid userId,
        bool unreadOnly = false,
        CancellationToken cancellationToken = default)
    {
        var query = database.Notifications.AsNoTracking().Where(n => n.UserId == userId);

        if (unreadOnly)
        {
            query = query.Where(n => n.ReadAtUtc == null);
        }

        var rows = await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(PageSize)
            .ToListAsync(cancellationToken);

        var actors = await database.UserProfiles
            .AsNoTracking()
            .Where(p => rows.Select(n => n.ActorId).Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.DisplayName, cancellationToken);

        return
        [
            .. rows.Select(n => new NotificationView(
                n.Id,
                n.Kind,
                n.Summary,
                n.Link,
                n.ActorId is { } actor ? actors.GetValueOrDefault(actor) : null,
                n.CreatedAtUtc,
                n.IsRead))
        ];
    }

    public Task<int> UnreadCountAsync(Guid userId, CancellationToken cancellationToken = default) =>
        database.Notifications.CountAsync(n => n.UserId == userId && n.ReadAtUtc == null, cancellationToken);

    public async Task MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        await database.Notifications
            .Where(n => n.UserId == userId && n.ReadAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.ReadAtUtc, now), cancellationToken);
    }

    public async Task<NotificationDelivery> DeliveryFor(Guid userId, CancellationToken cancellationToken = default)
    {
        var preference = await database.NotificationPreferences
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        return preference?.Delivery ?? NotificationDelivery.Daily;
    }

    public async Task SetDeliveryAsync(
        Guid userId,
        NotificationDelivery delivery,
        CancellationToken cancellationToken = default)
    {
        var preference = await database.NotificationPreferences
            .SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        if (preference is null)
        {
            database.NotificationPreferences.Add(new NotificationPreference(userId, delivery));
        }
        else
        {
            preference.ChangeTo(delivery);
        }

        await database.SaveChangesAsync(cancellationToken);
    }
}
