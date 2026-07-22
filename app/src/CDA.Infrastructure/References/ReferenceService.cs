using CDA.Application.Abstractions;
using CDA.Domain.References;
using CDA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CDA.Infrastructure.References;

/// <summary>A reference as shown against a proposal, with this viewer's own two votes.</summary>
public sealed record ReferenceView(
    Guid Id,
    string Url,
    string Description,
    string CitedByDisplayName,
    Guid CitedByUserId,
    int AccuracyScore,
    int AccuracyVotes,
    int ImportanceScore,
    int ImportanceVotes,
    short? MyAccuracyVote,
    short? MyImportanceVote,
    int UsedByProposalCount);

public sealed record ReferenceResult(bool Succeeded, Guid Id = default, string? Error = null)
{
    public static ReferenceResult Ok(Guid id) => new(true, id);

    public static ReferenceResult Refused(string reason) => new(false, Error: reason);
}

public sealed class ReferenceService(CdaDbContext database, IClock clock)
{
    /// <summary>
    /// Cites a source in support of a proposal.
    /// </summary>
    /// <remarks>
    /// If the same source has already been cited in this topic — after canonicalisation — the
    /// existing reference is attached rather than a second one created. That is what keeps a
    /// source's accumulated judgement attached to the source rather than scattered across
    /// near-identical copies of its address.
    /// </remarks>
    public async Task<ReferenceResult> AttachAsync(
        Guid topicId,
        Guid proposalId,
        Guid userId,
        string url,
        string description,
        CancellationToken cancellationToken = default)
    {
        if (!ReferenceUrl.TryCanonicalize(url, out var canonical, out var urlError))
        {
            return ReferenceResult.Refused(urlError);
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return ReferenceResult.Refused("Say what this source is, so others know whether to open it.");
        }

        if (description.Length > Reference.DescriptionMaxLength)
        {
            return ReferenceResult.Refused(
                $"The description is limited to {Reference.DescriptionMaxLength} characters.");
        }

        // Scoped to the topic the caller was authorised against.
        var proposal = await database.Proposals
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == proposalId && p.TopicId == topicId, cancellationToken);

        if (proposal is null)
        {
            return ReferenceResult.Refused("No such proposal.");
        }

        var topic = await database.Topics
            .AsNoTracking()
            .SingleAsync(t => t.Id == topicId, cancellationToken);

        var now = clock.UtcNow;

        if (topic.IsClosedAt(now))
        {
            return ReferenceResult.Refused("This topic has closed.");
        }

        var existing = await database.References
            .SingleOrDefaultAsync(r => r.TopicId == topicId && r.CanonicalUrl == canonical, cancellationToken);

        var reference = existing;

        if (reference is null)
        {
            reference = new Reference(Guid.NewGuid(), topicId, canonical, description, userId, now);
            database.References.Add(reference);
        }

        var alreadyAttached = await database.ProposalReferences
            .AnyAsync(link => link.ProposalId == proposalId && link.ReferenceId == reference.Id, cancellationToken);

        if (!alreadyAttached)
        {
            database.ProposalReferences.Add(
                new ProposalReference(proposalId, reference.Id, userId, now));
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two people cited the same source at the same moment; the unique index settled it.
            database.ChangeTracker.Clear();
            return ReferenceResult.Refused("That source was just added by someone else — reload the page.");
        }

        return ReferenceResult.Ok(reference.Id);
    }

    public async Task<List<ReferenceView>> ForProposalAsync(
        Guid proposalId,
        Guid? viewerId,
        CancellationToken cancellationToken = default)
    {
        var rows = await database.ProposalReferences
            .AsNoTracking()
            .Where(link => link.ProposalId == proposalId)
            .Join(database.References.AsNoTracking(),
                link => link.ReferenceId, reference => reference.Id, (link, reference) => reference)
            .Join(database.UserProfiles.AsNoTracking(),
                reference => reference.CreatedByUserId, profile => profile.Id,
                (reference, profile) => new { Reference = reference, profile.DisplayName })
            // Most useful sources first: a reference the crowd rates highly on both axes is the
            // one a reader should open.
            .OrderByDescending(x => x.Reference.AccuracyScore + x.Reference.ImportanceScore)
            .ThenBy(x => x.Reference.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var ids = rows.Select(r => r.Reference.Id).ToList();

        var myVotes = viewerId is { } voter
            ? await database.Votes.AsNoTracking()
                .Where(v => v.UserId == voter && v.ReferenceId != null && ids.Contains(v.ReferenceId!.Value))
                .Select(v => new { v.ReferenceId, v.ReferenceAspect, v.Value })
                .ToListAsync(cancellationToken)
            : [];

        var usage = await database.ProposalReferences
            .AsNoTracking()
            .Where(link => ids.Contains(link.ReferenceId))
            .GroupBy(link => link.ReferenceId)
            .Select(group => new { ReferenceId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.ReferenceId, x => x.Count, cancellationToken);

        return
        [
            .. rows.Select(row => new ReferenceView(
                row.Reference.Id,
                row.Reference.CanonicalUrl,
                row.Reference.Description,
                row.DisplayName,
                row.Reference.CreatedByUserId,
                row.Reference.AccuracyScore,
                row.Reference.AccuracyVotes,
                row.Reference.ImportanceScore,
                row.Reference.ImportanceVotes,
                myVotes.FirstOrDefault(v => v.ReferenceId == row.Reference.Id
                    && v.ReferenceAspect == ReferenceAspect.Accuracy)?.Value,
                myVotes.FirstOrDefault(v => v.ReferenceId == row.Reference.Id
                    && v.ReferenceAspect == ReferenceAspect.Importance)?.Value,
                usage.GetValueOrDefault(row.Reference.Id, 1)))
        ];
    }

    /// <summary>
    /// The participants whose sources this topic rates most highly.
    /// </summary>
    /// <remarks>
    /// Phase 7 uses this to decide whose alternative solutions appear first — a deliberate
    /// advantage for people who cite well, on the reasoning that the quality of a discussion
    /// rests on the quality of what it argues from.
    /// </remarks>
    public Task<List<TopicUserReputation>> TopCitersAsync(
        Guid topicId,
        int count = 3,
        CancellationToken cancellationToken = default) =>
        database.TopicUserReputations
            .AsNoTracking()
            .Where(x => x.TopicId == topicId && x.ReferenceScore > 0)
            .OrderByDescending(x => x.ReferenceScore)
            .ThenBy(x => x.UserId)
            .Take(count)
            .ToListAsync(cancellationToken);
}
