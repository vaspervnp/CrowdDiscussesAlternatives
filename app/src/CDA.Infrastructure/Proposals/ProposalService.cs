using CDA.Application.Abstractions;
using CDA.Application.Proposals;
using CDA.Application.Topics;
using CDA.Domain.Proposals;
using CDA.Domain.Topics;
using CDA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CDA.Infrastructure.Proposals;

public enum ProposalSort
{
    /// <summary>Most supported first.</summary>
    Score,

    /// <summary>Newest first.</summary>
    Newest,

    /// <summary>
    /// Most recently discussed first — how a returning participant catches up.
    /// </summary>
    LastCommented,
}

public sealed record ProposalPage(IReadOnlyList<ProposalView> Items, string? NextCursor);

public sealed record ProposalResult(bool Succeeded, Guid Id = default, string? Error = null)
{
    public static ProposalResult Ok(Guid id) => new(true, id);

    public static ProposalResult Refused(string reason) => new(false, Error: reason);
}

public sealed class ProposalService(CdaDbContext database, IClock clock)
{
    public const int PageSize = 25;

    /// <summary>
    /// Adds a proposal to a topic's pool.
    /// </summary>
    /// <remarks>
    /// Only while the topic is in its proposing phase: proposals are written against the
    /// requirement list, so they cannot precede it.
    /// </remarks>
    public async Task<ProposalResult> CreateAsync(
        Guid topicId,
        Guid authorId,
        string text,
        DateTime? editableUntilUtc,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return ProposalResult.Refused("A proposal cannot be empty.");
        }

        if (text.Length > Proposal.TextMaxLength)
        {
            return ProposalResult.Refused(
                $"A proposal is limited to {Proposal.TextMaxLength} characters — it is meant to be " +
                "about one sentence. Split a longer idea into separate proposals.");
        }

        var topic = await database.Topics
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == topicId, cancellationToken);

        if (topic is null)
        {
            return ProposalResult.Refused("No such topic.");
        }

        var now = clock.UtcNow;

        if (topic.IsClosedAt(now))
        {
            return ProposalResult.Refused("This topic has closed.");
        }

        if (topic.Phase != TopicPhase.Proposing)
        {
            return ProposalResult.Refused(
                "This topic is still agreeing its requirements. Proposals open once the " +
                "facilitator has published them.");
        }

        var isMember = await database.TopicMembers
            .AnyAsync(m => m.TopicId == topicId && m.UserId == authorId, cancellationToken);

        if (!isMember)
        {
            if (topic.Visibility != TopicVisibility.Public)
            {
                return ProposalResult.Refused("Only members can add proposals to this topic.");
            }

            // Same rule as commenting: taking part in a public topic joins it.
            database.TopicMembers.Add(new TopicMember(topicId, authorId, TopicRole.Member, now));
        }

        Proposal proposal;
        try
        {
            proposal = new Proposal(Guid.NewGuid(), topicId, authorId, text, now, editableUntilUtc);
        }
        catch (ArgumentOutOfRangeException error)
        {
            return ProposalResult.Refused(error.Message);
        }

        database.Proposals.Add(proposal);
        await database.SaveChangesAsync(cancellationToken);

        return ProposalResult.Ok(proposal.Id);
    }

    public async Task<ProposalResult> EditAsync(
        Guid topicId,
        Guid proposalId,
        Guid userId,
        string text,
        CancellationToken cancellationToken = default)
    {
        // Scoped to the topic the caller was authorised against — the proposal id arrives in
        // the route and must not be trusted to belong here.
        var proposal = await database.Proposals
            .SingleOrDefaultAsync(p => p.Id == proposalId && p.TopicId == topicId, cancellationToken);

        if (proposal is null)
        {
            return ProposalResult.Refused("No such proposal.");
        }

        if (proposal.AuthorId != userId)
        {
            return ProposalResult.Refused("Only the author can edit a proposal.");
        }

        if (string.IsNullOrWhiteSpace(text) || text.Length > Proposal.TextMaxLength)
        {
            return ProposalResult.Refused(
                $"A proposal must be between 1 and {Proposal.TextMaxLength} characters.");
        }

        try
        {
            proposal.Edit(text, clock.UtcNow);
        }
        catch (InvalidOperationException error)
        {
            return ProposalResult.Refused(error.Message);
        }

        await database.SaveChangesAsync(cancellationToken);

        return ProposalResult.Ok(proposal.Id);
    }

    /// <summary>Ends the editing window early, opening the proposal to votes.</summary>
    public async Task<ProposalResult> LockAsync(
        Guid topicId,
        Guid proposalId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var proposal = await database.Proposals
            .SingleOrDefaultAsync(p => p.Id == proposalId && p.TopicId == topicId, cancellationToken);

        if (proposal is null)
        {
            return ProposalResult.Refused("No such proposal.");
        }

        if (proposal.AuthorId != userId)
        {
            return ProposalResult.Refused("Only the author can lock a proposal.");
        }

        proposal.LockNow(clock.UtcNow);
        await database.SaveChangesAsync(cancellationToken);

        return ProposalResult.Ok(proposal.Id);
    }

    public async Task<ProposalView?> GetAsync(
        Guid topicId,
        Guid proposalId,
        TopicViewer viewer,
        CancellationToken cancellationToken = default)
    {
        var row = await database.Proposals
            .AsNoTracking()
            .Where(p => p.Id == proposalId && p.TopicId == topicId)
            .Join(database.UserProfiles.AsNoTracking(),
                p => p.AuthorId, profile => profile.Id,
                (p, profile) => new { Proposal = p, profile.DisplayName })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var topic = await database.Topics
            .AsNoTracking()
            .SingleAsync(t => t.Id == topicId, cancellationToken);

        var myVote = viewer.UserId is { } userId
            ? await database.Votes.AsNoTracking()
                .Where(v => v.ProposalId == proposalId && v.UserId == userId)
                .Select(v => (short?)v.Value)
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        return ProposalView.Project(row.Proposal, row.DisplayName, topic, viewer, clock.UtcNow, myVote);
    }

    /// <summary>
    /// Lists a topic's proposals.
    /// </summary>
    /// <remarks>
    /// The three orderings come straight from what the platform is for: by support, to see what
    /// the crowd favours; by date, to see what is new; and by most recently discussed, which is
    /// how someone who has been away catches up with an argument in progress. Filtering by
    /// author answers "what has this person put forward".
    /// </remarks>
    public async Task<ProposalPage> ListAsync(
        Guid topicId,
        TopicViewer viewer,
        ProposalSort sort = ProposalSort.Score,
        Guid? authorId = null,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var query = database.Proposals.AsNoTracking().Where(p => p.TopicId == topicId);

        if (authorId is { } author)
        {
            query = query.Where(p => p.AuthorId == author);
        }

        query = sort switch
        {
            ProposalSort.Newest => ApplyNewest(query, cursor),
            ProposalSort.LastCommented => ApplyLastCommented(query, cursor),
            _ => ApplyScore(query, cursor),
        };

        var rows = await query
            .Take(PageSize + 1)
            .Join(database.UserProfiles.AsNoTracking(),
                p => p.AuthorId, profile => profile.Id,
                (p, profile) => new { Proposal = p, profile.DisplayName })
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > PageSize;
        var page = hasMore ? rows.Take(PageSize).ToList() : rows;

        var topic = await database.Topics.AsNoTracking()
            .SingleAsync(t => t.Id == topicId, cancellationToken);

        var myVotes = viewer.UserId is { } voter
            ? await database.Votes.AsNoTracking()
                .Where(v => v.UserId == voter && v.ProposalId != null
                    && page.Select(r => r.Proposal.Id).Contains(v.ProposalId!.Value))
                .ToDictionaryAsync(v => v.ProposalId!.Value, v => v.Value, cancellationToken)
            : [];

        var now = clock.UtcNow;

        var items = page
            .Select(row => ProposalView.Project(
                row.Proposal,
                row.DisplayName,
                topic,
                viewer,
                now,
                myVotes.TryGetValue(row.Proposal.Id, out var vote) ? vote : null))
            .ToList();

        var next = hasMore && page.Count > 0 ? EncodeCursor(sort, page[^1].Proposal) : null;

        return new ProposalPage(items, next);
    }

    private static IQueryable<Proposal> ApplyScore(IQueryable<Proposal> query, string? cursor)
    {
        if (TryDecode(cursor, out var score, out var id))
        {
            query = query.Where(p => p.ScoreSum < score || (p.ScoreSum == score && p.Id.CompareTo(id) > 0));
        }

        return query.OrderByDescending(p => p.ScoreSum).ThenBy(p => p.Id);
    }

    private static IQueryable<Proposal> ApplyNewest(IQueryable<Proposal> query, string? cursor)
    {
        if (TryDecode(cursor, out var ticks, out var id))
        {
            var createdAt = new DateTime(ticks, DateTimeKind.Utc);
            query = query.Where(p => p.CreatedAtUtc < createdAt
                || (p.CreatedAtUtc == createdAt && p.Id.CompareTo(id) > 0));
        }

        return query.OrderByDescending(p => p.CreatedAtUtc).ThenBy(p => p.Id);
    }

    private static IQueryable<Proposal> ApplyLastCommented(IQueryable<Proposal> query, string? cursor)
    {
        if (TryDecode(cursor, out var ticks, out var id))
        {
            var last = new DateTime(ticks, DateTimeKind.Utc);
            query = query.Where(p => p.LastCommentAtUtc < last
                || (p.LastCommentAtUtc == last && p.Id.CompareTo(id) > 0));
        }

        // Never-commented proposals sort last rather than first: the ordering exists to surface
        // live argument, and a null would otherwise outrank every real conversation.
        return query
            .OrderByDescending(p => p.LastCommentAtUtc ?? DateTime.MinValue)
            .ThenBy(p => p.Id);
    }

    private static string EncodeCursor(ProposalSort sort, Proposal last) => sort switch
    {
        ProposalSort.Newest => $"{last.CreatedAtUtc.Ticks}:{last.Id}",
        ProposalSort.LastCommented => $"{(last.LastCommentAtUtc ?? DateTime.MinValue).Ticks}:{last.Id}",
        _ => $"{last.ScoreSum}:{last.Id}",
    };

    private static bool TryDecode(string? cursor, out long value, out Guid id)
    {
        value = 0;
        id = Guid.Empty;

        if (string.IsNullOrWhiteSpace(cursor))
        {
            return false;
        }

        var parts = cursor.Split(':', 2);

        // Attacker-controlled: a malformed cursor starts from the beginning rather than throwing.
        return parts.Length == 2
            && long.TryParse(parts[0], out value)
            && Guid.TryParse(parts[1], out id);
    }
}
