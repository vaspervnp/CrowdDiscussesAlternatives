using CDA.Application.Abstractions;
using CDA.Domain.Voting;
using CDA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CDA.Infrastructure.Voting;

/// <summary>
/// Voting on whether two proposals really are duplicates.
/// </summary>
/// <remarks>
/// The tally here is not a popularity score — it is the evidence each reader weighs against
/// their own threshold when deciding whether the pair should fold together for them.
/// </remarks>
public sealed class SimilarityVotingService(CdaDbContext database, IClock clock)
    : VotingService<Guid>(database, clock)
{
    protected override async Task<VotableSnapshot?> LoadAsync(
        Guid targetId,
        CancellationToken cancellationToken)
    {
        var row = await Database.SimilarityReports
            .AsNoTracking()
            .Where(r => r.Id == targetId)
            .Select(r => new { Report = r, Topic = Database.Topics.Single(t => t.Id == r.TopicId) })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        return new VotableSnapshot(
            row.Report.ScoreSum,
            row.Report.VoteCount,
            row.Topic.IsClosedAt(Clock.UtcNow) ? VoteOutcome.Closed : null);
    }

    protected override Task<Vote?> FindVoteAsync(
        Guid targetId,
        Guid userId,
        CancellationToken cancellationToken) =>
        Database.Votes.SingleOrDefaultAsync(
            v => v.SimilarityId == targetId && v.UserId == userId, cancellationToken);

    protected override Vote NewVote(Guid targetId, Guid userId, short value, DateTime atUtc) =>
        Vote.OnSimilarity(targetId, userId, value, atUtc);

    protected override async Task<(int ScoreSum, int VoteCount)> AdjustTalliesAsync(
        Guid targetId,
        int scoreDelta,
        int countDelta,
        CancellationToken cancellationToken)
    {
        if (scoreDelta != 0 || countDelta != 0)
        {
            await Database.SimilarityReports
                .Where(r => r.Id == targetId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(r => r.ScoreSum, r => r.ScoreSum + scoreDelta)
                        .SetProperty(r => r.VoteCount, r => r.VoteCount + countDelta),
                    cancellationToken);
        }

        var tallies = await Database.SimilarityReports
            .AsNoTracking()
            .Where(r => r.Id == targetId)
            .Select(r => new { r.ScoreSum, r.VoteCount })
            .SingleAsync(cancellationToken);

        return (tallies.ScoreSum, tallies.VoteCount);
    }
}
