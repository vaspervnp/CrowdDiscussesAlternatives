using CDA.Application.Abstractions;
using CDA.Application.Topics;
using CDA.Domain.Groups;
using CDA.Domain.Topics;
using CDA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CDA.Infrastructure.Groups;

public enum GroupSort
{
    /// <summary>Most supported first.</summary>
    Score,

    /// <summary>Newest first.</summary>
    Newest,
}

/// <summary>One proposal as it appears inside an alternative solution.</summary>
public sealed record GroupMemberView(Guid ProposalId, string Text, string AuthorDisplayName, int CommentCount);

public sealed record GroupView
{
    public required Guid Id { get; init; }

    public required Guid TopicId { get; init; }

    public required string Description { get; init; }

    public required Guid CreatedByUserId { get; init; }

    public required string CreatedByDisplayName { get; init; }

    public required DateTime CreatedAtUtc { get; init; }

    public DateTime? EditedAtUtc { get; init; }

    public required int ProposalCount { get; init; }

    public int? ScoreSum { get; init; }

    public int? VoteCount { get; init; }

    public required int CommentCount { get; init; }

    public short? MyVote { get; init; }

    public required bool CanVote { get; init; }

    public required bool CanEdit { get; init; }

    /// <summary>True when its author is among the topic's best-regarded citers of sources.</summary>
    public required bool ByTrustedCiter { get; init; }

    public Guid? ImprovesGroupId { get; init; }

    public string? ImprovesDescription { get; init; }

    /// <summary>Set on the detail view only.</summary>
    public IReadOnlyList<GroupMemberView> Members { get; init; } = [];
}

public sealed record GroupPage(IReadOnlyList<GroupView> Items, string? NextCursor, IReadOnlyList<string> TopCiters);

public sealed record GroupResult(bool Succeeded, Guid Id = default, string? Error = null)
{
    public static GroupResult Ok(Guid id) => new(true, id);

    public static GroupResult Refused(string reason) => new(false, Error: reason);
}

public sealed class GroupService(CdaDbContext database, IClock clock)
{
    public const int PageSize = 25;

    /// <summary>How many best-regarded citers get their alternatives listed first.</summary>
    public const int TrustedCiterCount = 3;

    public async Task<GroupResult> CreateAsync(
        Guid topicId,
        Guid userId,
        string description,
        IReadOnlyCollection<Guid> proposalIds,
        Guid? improvesGroupId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return GroupResult.Refused(
                "Say what this combination amounts to. Without it, everyone has to guess at the " +
                "reasoning that picked these proposals, which is most of what tells one " +
                "alternative from another.");
        }

        if (description.Length > ProposalGroup.DescriptionMaxLength)
        {
            return GroupResult.Refused(
                $"The description is limited to {ProposalGroup.DescriptionMaxLength} characters.");
        }

        if (proposalIds.Count < 2)
        {
            return GroupResult.Refused(
                "An alternative solution needs at least two proposals. A single proposal is " +
                "already votable on its own.");
        }

        var topic = await database.Topics
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == topicId, cancellationToken);

        if (topic is null)
        {
            return GroupResult.Refused("No such topic.");
        }

        var now = clock.UtcNow;

        if (topic.IsClosedAt(now))
        {
            return GroupResult.Refused("This topic has closed.");
        }

        if (topic.Phase != TopicPhase.Proposing)
        {
            return GroupResult.Refused("This topic is not open for proposals yet.");
        }

        // Every member must come from this topic's pool; the ids arrive from a form.
        var distinct = proposalIds.Distinct().ToList();

        var valid = await database.Proposals
            .AsNoTracking()
            .Where(p => p.TopicId == topicId && distinct.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        if (valid.Count != distinct.Count)
        {
            return GroupResult.Refused("Every proposal must belong to this topic.");
        }

        if (improvesGroupId is { } parent)
        {
            var parentExists = await database.ProposalGroups
                .AsNoTracking()
                .AnyAsync(g => g.Id == parent && g.TopicId == topicId, cancellationToken);

            if (!parentExists)
            {
                return GroupResult.Refused("The alternative this improves on must belong to this topic.");
            }
        }

        var isMember = await database.TopicMembers
            .AnyAsync(m => m.TopicId == topicId && m.UserId == userId, cancellationToken);

        if (!isMember)
        {
            if (topic.Visibility != TopicVisibility.Public)
            {
                return GroupResult.Refused("Only members can assemble alternatives in this topic.");
            }

            database.TopicMembers.Add(new TopicMember(topicId, userId, TopicRole.Member, now));
        }

        var group = new ProposalGroup(Guid.NewGuid(), topicId, userId, description, now, improvesGroupId);
        database.ProposalGroups.Add(group);

        foreach (var proposalId in valid)
        {
            database.GroupItems.Add(new GroupItem(group.Id, proposalId));
        }

        await database.SaveChangesAsync(cancellationToken);

        return GroupResult.Ok(group.Id);
    }

    public async Task<GroupResult> EditAsync(
        Guid topicId,
        Guid groupId,
        Guid userId,
        string description,
        IReadOnlyCollection<Guid>? proposalIds,
        CancellationToken cancellationToken = default)
    {
        var group = await database.ProposalGroups
            .SingleOrDefaultAsync(g => g.Id == groupId && g.TopicId == topicId, cancellationToken);

        if (group is null)
        {
            return GroupResult.Refused("No such alternative.");
        }

        if (group.CreatedByUserId != userId)
        {
            return GroupResult.Refused("Only the person who assembled this alternative can change it.");
        }

        if (string.IsNullOrWhiteSpace(description) || description.Length > ProposalGroup.DescriptionMaxLength)
        {
            return GroupResult.Refused(
                $"The description must be between 1 and {ProposalGroup.DescriptionMaxLength} characters.");
        }

        group.Edit(description, clock.UtcNow);

        if (proposalIds is { Count: > 0 })
        {
            var distinct = proposalIds.Distinct().ToList();

            var valid = await database.Proposals
                .AsNoTracking()
                .Where(p => p.TopicId == topicId && distinct.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);

            if (valid.Count != distinct.Count || valid.Count < 2)
            {
                return GroupResult.Refused("Every proposal must belong to this topic, and there must be at least two.");
            }

            var existing = await database.GroupItems
                .Where(item => item.GroupId == groupId)
                .ToListAsync(cancellationToken);

            database.GroupItems.RemoveRange(existing.Where(item => !valid.Contains(item.ProposalId)));

            foreach (var added in valid.Where(id => existing.All(item => item.ProposalId != id)))
            {
                database.GroupItems.Add(new GroupItem(groupId, added));
            }

            group.RecordMembershipChange(clock.UtcNow);
        }

        await database.SaveChangesAsync(cancellationToken);

        return GroupResult.Ok(groupId);
    }

    /// <summary>
    /// Lists a topic's alternative solutions.
    /// </summary>
    /// <remarks>
    /// Alternatives assembled by the topic's best-regarded citers of sources are listed first,
    /// before the chosen ordering applies. That advantage is deliberate and comes straight from
    /// the platform's design: the quality of a discussion rests on the quality of what it argues
    /// from, so the people who do the work of finding good evidence get a small structural say
    /// in what gets read first.
    /// </remarks>
    public async Task<GroupPage> ListAsync(
        Guid topicId,
        TopicViewer viewer,
        GroupSort sort = GroupSort.Score,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var trusted = await database.TopicUserReputations
            .AsNoTracking()
            .Where(x => x.TopicId == topicId && x.ReferenceScore > 0)
            .OrderByDescending(x => x.ReferenceScore)
            .ThenBy(x => x.UserId)
            .Take(TrustedCiterCount)
            .Select(x => x.UserId)
            .ToListAsync(cancellationToken);

        var query = database.ProposalGroups.AsNoTracking().Where(g => g.TopicId == topicId);

        // Priority is part of the sort key, so it is part of the cursor too — otherwise the
        // second page would restart from the trusted citers' alternatives.
        query = sort switch
        {
            GroupSort.Newest => ApplyKeyset(query, trusted, cursor, byScore: false),
            _ => ApplyKeyset(query, trusted, cursor, byScore: true),
        };

        var rows = await query
            .Take(PageSize + 1)
            .Join(database.UserProfiles.AsNoTracking(),
                g => g.CreatedByUserId, profile => profile.Id,
                (g, profile) => new { Group = g, profile.DisplayName })
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > PageSize;
        var page = hasMore ? rows.Take(PageSize).ToList() : rows;

        var topic = await database.Topics.AsNoTracking()
            .SingleAsync(t => t.Id == topicId, cancellationToken);

        var ids = page.Select(r => r.Group.Id).ToList();

        var counts = await database.GroupItems
            .AsNoTracking()
            .Where(item => ids.Contains(item.GroupId))
            .GroupBy(item => item.GroupId)
            .Select(g => new { GroupId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.GroupId, x => x.Count, cancellationToken);

        var myVotes = viewer.UserId is { } voter
            ? await database.Votes.AsNoTracking()
                .Where(v => v.UserId == voter && v.GroupId != null && ids.Contains(v.GroupId!.Value))
                .ToDictionaryAsync(v => v.GroupId!.Value, v => v.Value, cancellationToken)
            : [];

        var now = clock.UtcNow;
        var countsVisible = TopicAccessPolicy.CanSeeVoteCounts(topic, viewer, now);
        var closed = topic.IsClosedAt(now);

        var items = page.Select(row => new GroupView
        {
            Id = row.Group.Id,
            TopicId = topicId,
            Description = row.Group.Description,
            CreatedByUserId = row.Group.CreatedByUserId,
            CreatedByDisplayName = row.DisplayName,
            CreatedAtUtc = row.Group.CreatedAtUtc,
            EditedAtUtc = row.Group.EditedAtUtc,
            ProposalCount = counts.GetValueOrDefault(row.Group.Id),
            ScoreSum = countsVisible ? row.Group.ScoreSum : null,
            VoteCount = countsVisible ? row.Group.VoteCount : null,
            CommentCount = row.Group.CommentCount,
            MyVote = myVotes.TryGetValue(row.Group.Id, out var vote) ? vote : null,
            CanVote = viewer.IsSignedIn && !closed,
            CanEdit = viewer.UserId == row.Group.CreatedByUserId && !closed,
            ByTrustedCiter = trusted.Contains(row.Group.CreatedByUserId),
            ImprovesGroupId = row.Group.ImprovesGroupId,
        }).ToList();

        var next = hasMore && page.Count > 0
            ? EncodeCursor(trusted, page[^1].Group, sort)
            : null;

        var citerNames = trusted.Count == 0
            ? []
            : await database.UserProfiles.AsNoTracking()
                .Where(p => trusted.Contains(p.Id))
                .Select(p => p.DisplayName)
                .ToListAsync(cancellationToken);

        return new GroupPage(items, next, citerNames);
    }

    /// <summary>
    /// One alternative in full, including its member proposals.
    /// </summary>
    /// <remarks>
    /// The members carry their comment counts because the documents this grew out of are
    /// explicit that opening one alternative should also surface the discussion attached to the
    /// proposals inside it — the argument about a combination is largely the argument about its
    /// parts.
    /// </remarks>
    public async Task<GroupView?> GetAsync(
        Guid topicId,
        Guid groupId,
        TopicViewer viewer,
        CancellationToken cancellationToken = default)
    {
        var row = await database.ProposalGroups
            .AsNoTracking()
            .Where(g => g.Id == groupId && g.TopicId == topicId)
            .Join(database.UserProfiles.AsNoTracking(),
                g => g.CreatedByUserId, profile => profile.Id,
                (g, profile) => new { Group = g, profile.DisplayName })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        // Projected to an anonymous shape and mapped afterwards: EF cannot translate a chain of
        // joins whose result selector calls a record constructor.
        var memberRows = await database.GroupItems
            .AsNoTracking()
            .Where(item => item.GroupId == groupId)
            .Join(database.Proposals.AsNoTracking(),
                item => item.ProposalId, proposal => proposal.Id, (item, proposal) => proposal)
            .Join(database.UserProfiles.AsNoTracking(),
                proposal => proposal.AuthorId, profile => profile.Id,
                (proposal, profile) => new
                {
                    proposal.Id,
                    proposal.Text,
                    profile.DisplayName,
                    proposal.CommentCount,
                })
            .OrderByDescending(m => m.CommentCount)
            .ToListAsync(cancellationToken);

        var members = memberRows
            .Select(m => new GroupMemberView(m.Id, m.Text, m.DisplayName, m.CommentCount))
            .ToList();

        var topic = await database.Topics.AsNoTracking()
            .SingleAsync(t => t.Id == topicId, cancellationToken);

        var trusted = await database.TopicUserReputations
            .AsNoTracking()
            .Where(x => x.TopicId == topicId && x.ReferenceScore > 0)
            .OrderByDescending(x => x.ReferenceScore)
            .ThenBy(x => x.UserId)
            .Take(TrustedCiterCount)
            .Select(x => x.UserId)
            .ToListAsync(cancellationToken);

        var myVote = viewer.UserId is { } userId
            ? await database.Votes.AsNoTracking()
                .Where(v => v.GroupId == groupId && v.UserId == userId)
                .Select(v => (short?)v.Value)
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        var parentDescription = row.Group.ImprovesGroupId is { } parent
            ? await database.ProposalGroups.AsNoTracking()
                .Where(g => g.Id == parent).Select(g => g.Description)
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        var now = clock.UtcNow;
        var countsVisible = TopicAccessPolicy.CanSeeVoteCounts(topic, viewer, now);
        var closed = topic.IsClosedAt(now);

        return new GroupView
        {
            Id = row.Group.Id,
            TopicId = topicId,
            Description = row.Group.Description,
            CreatedByUserId = row.Group.CreatedByUserId,
            CreatedByDisplayName = row.DisplayName,
            CreatedAtUtc = row.Group.CreatedAtUtc,
            EditedAtUtc = row.Group.EditedAtUtc,
            ProposalCount = members.Count,
            ScoreSum = countsVisible ? row.Group.ScoreSum : null,
            VoteCount = countsVisible ? row.Group.VoteCount : null,
            CommentCount = row.Group.CommentCount,
            MyVote = myVote,
            CanVote = viewer.IsSignedIn && !closed,
            CanEdit = viewer.UserId == row.Group.CreatedByUserId && !closed,
            ByTrustedCiter = trusted.Contains(row.Group.CreatedByUserId),
            ImprovesGroupId = row.Group.ImprovesGroupId,
            ImprovesDescription = parentDescription,
            Members = members,
        };
    }

    private static IQueryable<ProposalGroup> ApplyKeyset(
        IQueryable<ProposalGroup> query,
        List<Guid> trusted,
        string? cursor,
        bool byScore)
    {
        if (TryDecode(cursor, out var priority, out var value, out var id))
        {
            query = byScore
                ? query.Where(g =>
                    (trusted.Contains(g.CreatedByUserId) ? 0 : 1) > priority
                    || ((trusted.Contains(g.CreatedByUserId) ? 0 : 1) == priority
                        && (g.ScoreSum < value || (g.ScoreSum == value && g.Id.CompareTo(id) > 0))))
                : query.Where(g =>
                    (trusted.Contains(g.CreatedByUserId) ? 0 : 1) > priority
                    || ((trusted.Contains(g.CreatedByUserId) ? 0 : 1) == priority
                        && (g.CreatedAtUtc.Ticks < value
                            || (g.CreatedAtUtc.Ticks == value && g.Id.CompareTo(id) > 0))));
        }

        return byScore
            ? query
                .OrderBy(g => trusted.Contains(g.CreatedByUserId) ? 0 : 1)
                .ThenByDescending(g => g.ScoreSum)
                .ThenBy(g => g.Id)
            : query
                .OrderBy(g => trusted.Contains(g.CreatedByUserId) ? 0 : 1)
                .ThenByDescending(g => g.CreatedAtUtc)
                .ThenBy(g => g.Id);
    }

    private static string EncodeCursor(List<Guid> trusted, ProposalGroup last, GroupSort sort)
    {
        var priority = trusted.Contains(last.CreatedByUserId) ? 0 : 1;
        var value = sort == GroupSort.Newest ? last.CreatedAtUtc.Ticks : last.ScoreSum;

        return $"{priority}:{value}:{last.Id}";
    }

    private static bool TryDecode(string? cursor, out int priority, out long value, out Guid id)
    {
        priority = 0;
        value = 0;
        id = Guid.Empty;

        if (string.IsNullOrWhiteSpace(cursor))
        {
            return false;
        }

        var parts = cursor.Split(':', 3);

        // Attacker-controlled: a malformed cursor starts from the beginning rather than throwing.
        return parts.Length == 3
            && int.TryParse(parts[0], out priority)
            && long.TryParse(parts[1], out value)
            && Guid.TryParse(parts[2], out id);
    }
}
