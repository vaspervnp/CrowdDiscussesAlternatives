using CDA.Application.Abstractions;
using CDA.Domain.References;
using CDA.Domain.Voting;
using CDA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CDA.Infrastructure.Voting;

/// <summary>
/// A reference together with the question being answered about it.
/// </summary>
/// <remarks>
/// References are the one thing judged on two independent axes, so a person holds two votes on
/// each: one on whether it is accurate, one on whether it matters here. That is why the voting
/// algorithm is generic over its target key rather than always taking a single id.
/// </remarks>
public readonly record struct ReferenceVoteTarget(Guid ReferenceId, ReferenceAspect Aspect);

public sealed class ReferenceVotingService(CdaDbContext database, IClock clock)
    : VotingService<ReferenceVoteTarget>(database, clock)
{
    protected override async Task<VotableSnapshot?> LoadAsync(
        ReferenceVoteTarget target,
        CancellationToken cancellationToken)
    {
        var row = await Database.References
            .AsNoTracking()
            .Where(r => r.Id == target.ReferenceId)
            .Select(r => new { Reference = r, Topic = Database.Topics.Single(t => t.Id == r.TopicId) })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var (score, votes) = target.Aspect == ReferenceAspect.Accuracy
            ? (row.Reference.AccuracyScore, row.Reference.AccuracyVotes)
            : (row.Reference.ImportanceScore, row.Reference.ImportanceVotes);

        // A reference is judged as soon as it is cited — unlike a proposal there is no wording
        // of its own that might still change.
        return new VotableSnapshot(
            score,
            votes,
            row.Topic.IsClosedAt(Clock.UtcNow) ? VoteOutcome.Closed : null);
    }

    protected override Task<Vote?> FindVoteAsync(
        ReferenceVoteTarget target,
        Guid userId,
        CancellationToken cancellationToken) =>
        Database.Votes.SingleOrDefaultAsync(
            v => v.ReferenceId == target.ReferenceId
                && v.ReferenceAspect == target.Aspect
                && v.UserId == userId,
            cancellationToken);

    protected override Vote NewVote(ReferenceVoteTarget target, Guid userId, short value, DateTime atUtc) =>
        Vote.OnReference(target.ReferenceId, target.Aspect, userId, value, atUtc);

    /// <summary>
    /// Applies the tally change to the right axis, and credits the citer's standing in the topic.
    /// </summary>
    /// <remarks>
    /// The reputation row moves in the same transaction as the vote. It decides whose
    /// alternative solutions are shown first, so letting it drift from the votes it is derived
    /// from would quietly distort the ordering with nothing to recompute it.
    /// </remarks>
    protected override async Task<(int ScoreSum, int VoteCount)> AdjustTalliesAsync(
        ReferenceVoteTarget target,
        int scoreDelta,
        int countDelta,
        CancellationToken cancellationToken)
    {
        if (scoreDelta != 0 || countDelta != 0)
        {
            if (target.Aspect == ReferenceAspect.Accuracy)
            {
                await Database.References
                    .Where(r => r.Id == target.ReferenceId)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(r => r.AccuracyScore, r => r.AccuracyScore + scoreDelta)
                            .SetProperty(r => r.AccuracyVotes, r => r.AccuracyVotes + countDelta),
                        cancellationToken);
            }
            else
            {
                await Database.References
                    .Where(r => r.Id == target.ReferenceId)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(r => r.ImportanceScore, r => r.ImportanceScore + scoreDelta)
                            .SetProperty(r => r.ImportanceVotes, r => r.ImportanceVotes + countDelta),
                        cancellationToken);
            }

            if (scoreDelta != 0)
            {
                await CreditCiterAsync(target.ReferenceId, scoreDelta, cancellationToken);
            }
        }

        var tallies = await Database.References
            .AsNoTracking()
            .Where(r => r.Id == target.ReferenceId)
            .Select(r => new
            {
                Score = target.Aspect == ReferenceAspect.Accuracy ? r.AccuracyScore : r.ImportanceScore,
                Votes = target.Aspect == ReferenceAspect.Accuracy ? r.AccuracyVotes : r.ImportanceVotes,
            })
            .SingleAsync(cancellationToken);

        return (tallies.Score, tallies.Votes);
    }

    private async Task CreditCiterAsync(Guid referenceId, int scoreDelta, CancellationToken cancellationToken)
    {
        var owner = await Database.References
            .AsNoTracking()
            .Where(r => r.Id == referenceId)
            .Select(r => new { r.TopicId, r.CreatedByUserId })
            .SingleAsync(cancellationToken);

        var updated = await Database.TopicUserReputations
            .Where(x => x.TopicId == owner.TopicId && x.UserId == owner.CreatedByUserId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.ReferenceScore, x => x.ReferenceScore + scoreDelta),
                cancellationToken);

        if (updated == 0)
        {
            // First vote on anything this person has cited in this topic.
            var reputation = new TopicUserReputation(owner.TopicId, owner.CreatedByUserId);
            reputation.Apply(scoreDelta);
            Database.TopicUserReputations.Add(reputation);
            await Database.SaveChangesAsync(cancellationToken);
        }
    }
}
