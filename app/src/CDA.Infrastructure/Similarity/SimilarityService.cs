using CDA.Application.Abstractions;
using CDA.Domain.Similarity;
using CDA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CDA.Infrastructure.Similarity;

/// <summary>A similarity report as shown against one of its two proposals.</summary>
public sealed record SimilarityView(
    Guid Id,
    Guid OtherProposalId,
    string OtherProposalText,
    string ReportedByDisplayName,
    Guid ReportedByUserId,
    Guid? BetterWrittenProposalId,
    string? Justification,
    int ScoreSum,
    int VoteCount,
    short? MyVote,
    bool IsActive,
    /// <summary>This viewer's vote on the other proposal, when it differs from theirs on this one.</summary>
    short? MyVoteOnOther,
    short? MyVoteOnThis);

public sealed record SimilarityResult(bool Succeeded, Guid Id = default, string? Error = null)
{
    public static SimilarityResult Ok(Guid id) => new(true, id);

    public static SimilarityResult Refused(string reason) => new(false, Error: reason);
}

public sealed class SimilarityService(CdaDbContext database, IClock clock)
{
    public async Task<SimilarityResult> ReportAsync(
        Guid topicId,
        Guid firstProposalId,
        Guid secondProposalId,
        Guid userId,
        Guid? betterWrittenProposalId,
        string? justification,
        CancellationToken cancellationToken = default)
    {
        if (firstProposalId == secondProposalId)
        {
            return SimilarityResult.Refused("A proposal cannot be similar to itself.");
        }

        // Both must belong to the topic the caller was authorised against.
        var found = await database.Proposals
            .AsNoTracking()
            .Where(p => p.TopicId == topicId && (p.Id == firstProposalId || p.Id == secondProposalId))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        if (found.Count != 2)
        {
            return SimilarityResult.Refused("Both proposals must belong to this topic.");
        }

        var topic = await database.Topics
            .AsNoTracking()
            .SingleAsync(t => t.Id == topicId, cancellationToken);

        if (topic.IsClosedAt(clock.UtcNow))
        {
            return SimilarityResult.Refused("This topic has closed.");
        }

        if (justification is { Length: > SimilarityReport.JustificationMaxLength })
        {
            return SimilarityResult.Refused(
                $"The justification is limited to {SimilarityReport.JustificationMaxLength} characters.");
        }

        SimilarityReport report;
        try
        {
            report = SimilarityReport.Between(
                topicId, firstProposalId, secondProposalId, userId,
                betterWrittenProposalId, justification, clock.UtcNow);
        }
        catch (ArgumentException error)
        {
            return SimilarityResult.Refused(error.Message);
        }

        var existing = await database.SimilarityReports
            .AsNoTracking()
            .SingleOrDefaultAsync(
                r => r.ProposalAId == report.ProposalAId && r.ProposalBId == report.ProposalBId,
                cancellationToken);

        if (existing is not null)
        {
            // Someone has already made this claim. Adding a second row would split the votes
            // that decide whether it takes effect — vote on the existing one instead.
            return SimilarityResult.Refused(
                "These two are already reported as similar. Vote on that report instead of adding another.");
        }

        database.SimilarityReports.Add(report);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            database.ChangeTracker.Clear();
            return SimilarityResult.Refused("Someone reported that pair a moment ago — reload the page.");
        }

        return SimilarityResult.Ok(report.Id);
    }

    /// <summary>
    /// The reports that clear a given threshold in a topic, as graph edges.
    /// </summary>
    /// <remarks>
    /// The threshold is the reader's, not the platform's: how much agreement a claim needs
    /// before it folds two proposals together is a judgement, and the documents this system
    /// grew out of are explicit that it belongs to the person reading.
    /// </remarks>
    public async Task<List<SimilarityEdge>> ActiveEdgesAsync(
        Guid topicId,
        int threshold,
        CancellationToken cancellationToken = default)
    {
        var rows = await database.SimilarityReports
            .AsNoTracking()
            .Where(r => r.TopicId == topicId && r.ScoreSum >= threshold)
            .Select(r => new { r.ProposalAId, r.ProposalBId, r.BetterWrittenProposalId })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => new SimilarityEdge(r.ProposalAId, r.ProposalBId, r.BetterWrittenProposalId))];
    }

    /// <summary>
    /// Reports involving one proposal, with the viewer's votes on both sides of each pair.
    /// </summary>
    /// <remarks>
    /// The votes on both sides are carried so the page can point out when someone has agreed
    /// that two proposals are the same while supporting them differently — the split that
    /// similarity reporting exists to prevent.
    /// </remarks>
    public async Task<List<SimilarityView>> ForProposalAsync(
        Guid proposalId,
        Guid? viewerId,
        int threshold,
        CancellationToken cancellationToken = default)
    {
        var reports = await database.SimilarityReports
            .AsNoTracking()
            .Where(r => r.ProposalAId == proposalId || r.ProposalBId == proposalId)
            .OrderByDescending(r => r.ScoreSum)
            .ToListAsync(cancellationToken);

        if (reports.Count == 0)
        {
            return [];
        }

        var otherIds = reports.Select(r => r.Other(proposalId)).ToList();

        var texts = await database.Proposals
            .AsNoTracking()
            .Where(p => otherIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Text, cancellationToken);

        var reporters = await database.UserProfiles
            .AsNoTracking()
            .Where(p => reports.Select(r => r.ReportedByUserId).Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.DisplayName, cancellationToken);

        var myVotes = viewerId is { } voter
            ? await database.Votes.AsNoTracking()
                .Where(v => v.UserId == voter
                    && (v.SimilarityId != null && reports.Select(r => r.Id).Contains(v.SimilarityId!.Value)
                        || v.ProposalId != null
                            && (v.ProposalId == proposalId || otherIds.Contains(v.ProposalId!.Value))))
                .Select(v => new { v.SimilarityId, v.ProposalId, v.Value })
                .ToListAsync(cancellationToken)
            : [];

        var myVoteOnThis = myVotes.FirstOrDefault(v => v.ProposalId == proposalId)?.Value;

        return
        [
            .. reports.Select(report =>
            {
                var otherId = report.Other(proposalId);

                return new SimilarityView(
                    report.Id,
                    otherId,
                    texts.GetValueOrDefault(otherId, "(removed)"),
                    reporters.GetValueOrDefault(report.ReportedByUserId, "(unknown)"),
                    report.ReportedByUserId,
                    report.BetterWrittenProposalId,
                    report.Justification,
                    report.ScoreSum,
                    report.VoteCount,
                    myVotes.FirstOrDefault(v => v.SimilarityId == report.Id)?.Value,
                    report.IsActiveAt(threshold),
                    myVotes.FirstOrDefault(v => v.ProposalId == otherId)?.Value,
                    myVoteOnThis);
            })
        ];
    }
}
