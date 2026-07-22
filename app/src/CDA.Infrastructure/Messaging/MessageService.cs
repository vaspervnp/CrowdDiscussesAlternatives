using CDA.Application.Abstractions;
using CDA.Domain.Messaging;
using CDA.Domain.Notifications;
using CDA.Infrastructure.Notifications;
using CDA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CDA.Infrastructure.Messaging;

public sealed record ConversationSummary(
    Guid WithUserId, string WithDisplayName, string LastBody, DateTime LastAtUtc, int Unread);

public sealed record MessageView(Guid Id, bool Mine, string Body, DateTime SentAtUtc, bool IsRead);

public sealed record MessageResult(bool Succeeded, string? Error = null)
{
    public static readonly MessageResult Ok = new(true);

    public static MessageResult Refused(string reason) => new(false, reason);
}

public sealed class MessageService(CdaDbContext database, NotificationService notifications, IClock clock)
{
    public async Task<MessageResult> SendAsync(
        Guid fromUserId,
        Guid toUserId,
        string body,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return MessageResult.Refused("A message cannot be empty.");
        }

        if (body.Length > PrivateMessage.BodyMaxLength)
        {
            return MessageResult.Refused(
                $"A message is limited to {PrivateMessage.BodyMaxLength} characters.");
        }

        if (fromUserId == toUserId)
        {
            return MessageResult.Refused("A message needs someone else to go to.");
        }

        var recipient = await database.UserProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == toUserId, cancellationToken);

        if (recipient is null)
        {
            return MessageResult.Refused("No such person.");
        }

        var sender = await database.UserProfiles
            .AsNoTracking()
            .SingleAsync(p => p.Id == fromUserId, cancellationToken);

        database.PrivateMessages.Add(new PrivateMessage(fromUserId, toUserId, body, clock.UtcNow));

        notifications.Enqueue(
            toUserId,
            NotificationKind.PrivateMessage,
            $"{sender.DisplayName} sent you a message",
            $"/messages/{fromUserId}",
            actorId: fromUserId);

        await database.SaveChangesAsync(cancellationToken);

        return MessageResult.Ok;
    }

    /// <summary>Everyone this person has exchanged messages with, most recent first.</summary>
    public async Task<List<ConversationSummary>> ConversationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var messages = await database.PrivateMessages
            .AsNoTracking()
            .Where(m => m.FromUserId == userId || m.ToUserId == userId)
            .OrderByDescending(m => m.SentAtUtc)
            .ToListAsync(cancellationToken);

        var partners = messages
            .Select(m => m.FromUserId == userId ? m.ToUserId : m.FromUserId)
            .Distinct()
            .ToList();

        var names = await database.UserProfiles
            .AsNoTracking()
            .Where(p => partners.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.DisplayName, cancellationToken);

        return
        [
            .. partners.Select(partner =>
            {
                var thread = messages
                    .Where(m => m.FromUserId == partner || m.ToUserId == partner)
                    .ToList();

                return new ConversationSummary(
                    partner,
                    names.GetValueOrDefault(partner, "(unknown)"),
                    thread[0].Body,
                    thread[0].SentAtUtc,
                    thread.Count(m => m.ToUserId == userId && m.ReadAtUtc == null));
            })
            .OrderByDescending(c => c.LastAtUtc)
        ];
    }

    /// <summary>
    /// One conversation, oldest first, marking anything addressed to the reader as read.
    /// </summary>
    /// <remarks>
    /// Only messages <em>to</em> the reader are marked. Opening your own sent message does not
    /// mean the other person has read it.
    /// </remarks>
    public async Task<List<MessageView>> ConversationAsync(
        Guid userId,
        Guid partnerId,
        CancellationToken cancellationToken = default)
    {
        var messages = await database.PrivateMessages
            .Where(m => (m.FromUserId == userId && m.ToUserId == partnerId)
                || (m.FromUserId == partnerId && m.ToUserId == userId))
            .OrderBy(m => m.SentAtUtc)
            .ToListAsync(cancellationToken);

        var now = clock.UtcNow;
        var changed = false;

        foreach (var message in messages.Where(m => m.ToUserId == userId && !m.IsRead))
        {
            message.MarkRead(now);
            changed = true;
        }

        if (changed)
        {
            await database.SaveChangesAsync(cancellationToken);
        }

        return
        [
            .. messages.Select(m => new MessageView(
                m.Id, m.FromUserId == userId, m.Body, m.SentAtUtc, m.IsRead))
        ];
    }

    public Task<int> UnreadCountAsync(Guid userId, CancellationToken cancellationToken = default) =>
        database.PrivateMessages.CountAsync(
            m => m.ToUserId == userId && m.ReadAtUtc == null, cancellationToken);
}
