using CDA.Application.Abstractions;
using CDA.Application.Topics;
using CDA.Domain.Topics;
using CDA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CDA.Infrastructure.Topics;

public enum TopicSort
{
    /// <summary>Most important first — the ranking the platform exists to produce.</summary>
    Importance,

    /// <summary>Newest first.</summary>
    Newest,
}

/// <summary>One page of topics, plus the cursor that fetches the next.</summary>
public sealed record TopicPage(IReadOnlyList<TopicView> Items, string? NextCursor);

public sealed class TopicService(CdaDbContext database, IClock clock)
{
    public const int PageSize = 25;

    /// <summary>
    /// Creates a topic and makes its creator the facilitator.
    /// </summary>
    /// <remarks>
    /// Both rows are written together: a topic whose facilitator is not a member cannot be
    /// administered by anyone, and there is no interface to repair that.
    /// </remarks>
    public async Task<Topic> CreateAsync(
        string subject,
        string description,
        Guid createdByUserId,
        TopicVisibility visibility,
        DateTime? closesAtUtc,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var topic = new Topic(Guid.NewGuid(), subject, description, createdByUserId, now, visibility, closesAtUtc);

        database.Topics.Add(topic);
        database.TopicMembers.Add(new TopicMember(topic.Id, createdByUserId, TopicRole.Facilitator, now));

        await database.SaveChangesAsync(cancellationToken);

        return topic;
    }

    public async Task<bool> JoinAsync(Guid topicId, Guid userId, CancellationToken cancellationToken = default)
    {
        var topic = await database.Topics
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == topicId, cancellationToken);

        if (topic is null)
        {
            return false;
        }

        var viewer = await ViewerForAsync(topic, userId, isAdministrator: false, cancellationToken);

        if (!TopicAccessPolicy.CanJoin(topic, viewer, clock.UtcNow))
        {
            return false;
        }

        database.TopicMembers.Add(new TopicMember(topicId, userId, TopicRole.Member, clock.UtcNow));

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Joined twice concurrently; the composite primary key settled it.
            return true;
        }

        return true;
    }

    /// <summary>Builds the viewer record for a topic, including the caller's role in it.</summary>
    public async Task<TopicViewer> ViewerForAsync(
        Topic topic,
        Guid? userId,
        bool isAdministrator,
        CancellationToken cancellationToken = default)
    {
        if (userId is not { } id)
        {
            return TopicViewer.Anonymous;
        }

        var membership = await database.TopicMembers
            .AsNoTracking()
            .SingleOrDefaultAsync(m => m.TopicId == topic.Id && m.UserId == id, cancellationToken);

        return new TopicViewer(id, isAdministrator, membership?.Role);
    }

    /// <summary>
    /// Lists the topics this caller may see, most important first by default.
    /// </summary>
    /// <remarks>
    /// Visibility is applied as a filter in the query, not after loading: an invite-only
    /// topic the caller is not in must not travel as far as the application. Paging is by
    /// keyset rather than by offset, because the target is a platform with thousands of
    /// topics and OFFSET degrades linearly while also skipping or repeating rows when the
    /// ranking shifts between pages — which, on a list ordered by live vote counts, it does.
    /// </remarks>
    public async Task<TopicPage> ListAsync(
        Guid? userId,
        bool isAdministrator,
        TopicSort sort = TopicSort.Importance,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var visible = database.Topics.AsNoTracking().Where(topic =>
            topic.Visibility == TopicVisibility.Public
            || isAdministrator
            || (userId != null && database.TopicMembers
                    .Any(m => m.TopicId == topic.Id && m.UserId == userId)));

        visible = sort switch
        {
            TopicSort.Newest => ApplyNewestKeyset(visible, cursor),
            _ => ApplyImportanceKeyset(visible, cursor),
        };

        var rows = await visible.Take(PageSize + 1).ToListAsync(cancellationToken);

        var hasMore = rows.Count > PageSize;
        var page = hasMore ? rows.Take(PageSize).ToList() : rows;

        var memberships = userId is { } uid
            ? await database.TopicMembers.AsNoTracking()
                .Where(m => m.UserId == uid && page.Select(t => t.Id).Contains(m.TopicId))
                .ToDictionaryAsync(m => m.TopicId, m => m.Role, cancellationToken)
            : [];

        var myVotes = userId is { } voter
            ? await database.Votes.AsNoTracking()
                .Where(v => v.UserId == voter && v.TopicId != null && page.Select(t => t.Id).Contains(v.TopicId!.Value))
                .ToDictionaryAsync(v => v.TopicId!.Value, v => v.Value, cancellationToken)
            : [];

        var now = clock.UtcNow;

        var items = page
            .Select(topic => TopicView.Project(
                topic,
                new TopicViewer(userId, isAdministrator, memberships.TryGetValue(topic.Id, out var role) ? role : null),
                now,
                myVotes.TryGetValue(topic.Id, out var vote) ? vote : null))
            .ToList();

        var next = hasMore && page.Count > 0 ? EncodeCursor(sort, page[^1]) : null;

        return new TopicPage(items, next);
    }

    private static IQueryable<Topic> ApplyImportanceKeyset(IQueryable<Topic> query, string? cursor)
    {
        if (TryDecodeCursor(cursor, out var score, out var id))
        {
            // Strictly "after" the last row in the same order the rows are sorted by.
            query = query.Where(t => t.ScoreSum < score || (t.ScoreSum == score && t.Id.CompareTo(id) > 0));
        }

        return query.OrderByDescending(t => t.ScoreSum).ThenBy(t => t.Id);
    }

    private static IQueryable<Topic> ApplyNewestKeyset(IQueryable<Topic> query, string? cursor)
    {
        if (TryDecodeCursor(cursor, out var ticks, out var id))
        {
            var createdAt = new DateTime(ticks, DateTimeKind.Utc);
            query = query.Where(t => t.CreatedAtUtc < createdAt || (t.CreatedAtUtc == createdAt && t.Id.CompareTo(id) > 0));
        }

        return query.OrderByDescending(t => t.CreatedAtUtc).ThenBy(t => t.Id);
    }

    private static string EncodeCursor(TopicSort sort, Topic last) =>
        sort == TopicSort.Newest
            ? $"{last.CreatedAtUtc.Ticks}:{last.Id}"
            : $"{last.ScoreSum}:{last.Id}";

    private static bool TryDecodeCursor(string? cursor, out long value, out Guid id)
    {
        value = 0;
        id = Guid.Empty;

        if (string.IsNullOrWhiteSpace(cursor))
        {
            return false;
        }

        var parts = cursor.Split(':', 2);

        // A cursor comes from the query string, so it is attacker-controlled. A malformed one
        // starts from the beginning rather than throwing.
        return parts.Length == 2
            && long.TryParse(parts[0], out value)
            && Guid.TryParse(parts[1], out id);
    }
}
