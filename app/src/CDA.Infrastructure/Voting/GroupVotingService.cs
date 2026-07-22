using CDA.Application.Abstractions;
using CDA.Domain.Voting;
using CDA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CDA.Infrastructure.Voting;

/// <summary>
/// Voting on an alternative solution as a whole.
/// </summary>
/// <remarks>
/// Unlike a proposal there is no editing window: an alternative is votable from the moment it
/// is assembled. Its description can still be changed by whoever built it, which the interface
/// flags once votes exist — a group is a claim about a combination, and the combination itself
/// is what people are judging.
/// </remarks>
public sealed class GroupVotingService(CdaDbContext database, IClock clock)
    : VotingService<Guid>(database, clock)
{
    protected override async Task<VotableSnapshot?> LoadAsync(
        Guid targetId,
        CancellationToken cancellationToken)
    {
        var row = await Database.ProposalGroups
            .AsNoTracking()
            .Where(g => g.Id == targetId)
            .Select(g => new { Group = g, Topic = Database.Topics.Single(t => t.Id == g.TopicId) })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        return new VotableSnapshot(
            row.Group.ScoreSum,
            row.Group.VoteCount,
            row.Topic.IsClosedAt(Clock.UtcNow) ? VoteOutcome.Closed : null);
    }

    protected override Task<Vote?> FindVoteAsync(
        Guid targetId,
        Guid userId,
        CancellationToken cancellationToken) =>
        Database.Votes.SingleOrDefaultAsync(
            v => v.GroupId == targetId && v.UserId == userId, cancellationToken);

    protected override Vote NewVote(Guid targetId, Guid userId, short value, DateTime atUtc) =>
        Vote.OnGroup(targetId, userId, value, atUtc);

    protected override async Task<(int ScoreSum, int VoteCount)> AdjustTalliesAsync(
        Guid targetId,
        int scoreDelta,
        int countDelta,
        CancellationToken cancellationToken)
    {
        if (scoreDelta != 0 || countDelta != 0)
        {
            await Database.ProposalGroups
                .Where(g => g.Id == targetId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(g => g.ScoreSum, g => g.ScoreSum + scoreDelta)
                        .SetProperty(g => g.VoteCount, g => g.VoteCount + countDelta),
                    cancellationToken);
        }

        var tallies = await Database.ProposalGroups
            .AsNoTracking()
            .Where(g => g.Id == targetId)
            .Select(g => new { g.ScoreSum, g.VoteCount })
            .SingleAsync(cancellationToken);

        return (tallies.ScoreSum, tallies.VoteCount);
    }
}
