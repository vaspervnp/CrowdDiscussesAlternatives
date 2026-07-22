using CDA.Application.Abstractions;
using CDA.Domain.Voting;
using CDA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CDA.Infrastructure.Voting;

/// <summary>Voting on a topic's importance.</summary>
public sealed class TopicVotingService(CdaDbContext database, IClock clock)
    : VotingService<Guid>(database, clock)
{
    protected override async Task<VotableSnapshot?> LoadAsync(
        Guid targetId,
        CancellationToken cancellationToken)
    {
        var topic = await Database.Topics
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == targetId, cancellationToken);

        if (topic is null)
        {
            return null;
        }

        return new VotableSnapshot(
            topic.ScoreSum,
            topic.VoteCount,
            topic.IsClosedAt(Clock.UtcNow) ? VoteOutcome.Closed : null);
    }

    protected override Task<Vote?> FindVoteAsync(
        Guid targetId,
        Guid userId,
        CancellationToken cancellationToken) =>
        Database.Votes.SingleOrDefaultAsync(
            v => v.TopicId == targetId && v.UserId == userId, cancellationToken);

    protected override Vote NewVote(Guid targetId, Guid userId, short value, DateTime atUtc) =>
        Vote.OnTopic(targetId, userId, value, atUtc);

    protected override async Task<(int ScoreSum, int VoteCount)> AdjustTalliesAsync(
        Guid targetId,
        int scoreDelta,
        int countDelta,
        CancellationToken cancellationToken)
    {
        // Applied before the vote row is written: every concurrent voter contends for this one
        // row, so taking its lock first makes them queue rather than deadlock on two locks
        // acquired in opposite orders.
        if (scoreDelta != 0 || countDelta != 0)
        {
            await Database.Topics
                .Where(t => t.Id == targetId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(t => t.ScoreSum, t => t.ScoreSum + scoreDelta)
                        .SetProperty(t => t.VoteCount, t => t.VoteCount + countDelta),
                    cancellationToken);
        }

        var tallies = await Database.Topics
            .AsNoTracking()
            .Where(t => t.Id == targetId)
            .Select(t => new { t.ScoreSum, t.VoteCount })
            .SingleAsync(cancellationToken);

        return (tallies.ScoreSum, tallies.VoteCount);
    }
}

/// <summary>
/// Voting on a proposal.
/// </summary>
/// <remarks>
/// Refused while the proposal is still inside its editing window. Votes would otherwise attach
/// to wording that changes underneath them.
/// </remarks>
public sealed class ProposalVotingService(CdaDbContext database, IClock clock)
    : VotingService<Guid>(database, clock)
{
    protected override async Task<VotableSnapshot?> LoadAsync(
        Guid targetId,
        CancellationToken cancellationToken)
    {
        var row = await Database.Proposals
            .AsNoTracking()
            .Where(p => p.Id == targetId)
            .Select(p => new { Proposal = p, Topic = Database.Topics.Single(t => t.Id == p.TopicId) })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var now = Clock.UtcNow;

        VoteOutcome? refusal = row.Topic.IsClosedAt(now) ? VoteOutcome.Closed
            : !row.Proposal.IsLockedAt(now) ? VoteOutcome.NotOpenYet
            : null;

        return new VotableSnapshot(row.Proposal.ScoreSum, row.Proposal.VoteCount, refusal);
    }

    protected override Task<Vote?> FindVoteAsync(
        Guid targetId,
        Guid userId,
        CancellationToken cancellationToken) =>
        Database.Votes.SingleOrDefaultAsync(
            v => v.ProposalId == targetId && v.UserId == userId, cancellationToken);

    protected override Vote NewVote(Guid targetId, Guid userId, short value, DateTime atUtc) =>
        Vote.OnProposal(targetId, userId, value, atUtc);

    protected override async Task<(int ScoreSum, int VoteCount)> AdjustTalliesAsync(
        Guid targetId,
        int scoreDelta,
        int countDelta,
        CancellationToken cancellationToken)
    {
        if (scoreDelta != 0 || countDelta != 0)
        {
            await Database.Proposals
                .Where(p => p.Id == targetId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(p => p.ScoreSum, p => p.ScoreSum + scoreDelta)
                        .SetProperty(p => p.VoteCount, p => p.VoteCount + countDelta),
                    cancellationToken);
        }

        var tallies = await Database.Proposals
            .AsNoTracking()
            .Where(p => p.Id == targetId)
            .Select(p => new { p.ScoreSum, p.VoteCount })
            .SingleAsync(cancellationToken);

        return (tallies.ScoreSum, tallies.VoteCount);
    }
}
