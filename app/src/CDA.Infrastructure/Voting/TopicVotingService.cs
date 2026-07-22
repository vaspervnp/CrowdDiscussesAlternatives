using CDA.Application.Abstractions;
using CDA.Domain.Voting;
using CDA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CDA.Infrastructure.Voting;

public enum VoteOutcome
{
    /// <summary>A first vote from this user on this target.</summary>
    Recorded,

    /// <summary>They had voted before and have now changed their mind.</summary>
    Changed,

    /// <summary>They cast the same value they already held; nothing was written.</summary>
    Unchanged,

    /// <summary>Their vote was removed.</summary>
    Withdrawn,

    /// <summary>There was no vote to withdraw.</summary>
    NothingToWithdraw,

    /// <summary>The target does not accept votes any more.</summary>
    Closed,

    /// <summary>No such target.</summary>
    NotFound,
}

public sealed record VoteResult(VoteOutcome Outcome, int ScoreSum, int VoteCount, short? MyVote);

/// <summary>
/// Records votes on topics and keeps the topic's tallies in step.
/// </summary>
/// <remarks>
/// <para>
/// This is the first user of the voting machinery; proposals, groups, references and
/// similarity reports will reuse the same table and the same arithmetic, so the invariants
/// are worth stating. A user holds at most one vote per target, enforced by a unique index.
/// Casting the value you already hold writes nothing. Withdrawing deletes the row, which is
/// distinct from voting zero — zero is a recorded abstention that still counts as
/// participation.
/// </para>
/// <para>
/// The tallies are updated with a relative <c>UPDATE … SET ScoreSum = ScoreSum + @delta</c>
/// rather than by reading, adding and writing back. Two people voting on the same topic at
/// the same moment is the ordinary case, not an edge case, and read-modify-write would
/// silently lose one of them.
/// </para>
/// </remarks>
public sealed class TopicVotingService(CdaDbContext database, IClock clock)
{
    public Task<VoteResult> CastAsync(
        Guid topicId,
        Guid userId,
        short value,
        CancellationToken cancellationToken = default) =>
        InTransactionAsync(
            async () => await CastCoreAsync(topicId, userId, value, cancellationToken),
            cancellationToken);

    public Task<VoteResult> WithdrawAsync(
        Guid topicId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        InTransactionAsync(
            async () => await WithdrawCoreAsync(topicId, userId, cancellationToken),
            cancellationToken);

    private async Task<VoteResult> CastCoreAsync(
        Guid topicId,
        Guid userId,
        short value,
        CancellationToken cancellationToken)
    {
        var topic = await database.Topics
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == topicId, cancellationToken);

        if (topic is null)
        {
            return new VoteResult(VoteOutcome.NotFound, 0, 0, null);
        }

        // Checked here as well as in the access policy: a vote is the one thing in this
        // system that must not be accepted after the discussion has ended, and the service
        // is reachable from more than one caller.
        if (topic.IsClosedAt(clock.UtcNow))
        {
            return new VoteResult(VoteOutcome.Closed, topic.ScoreSum, topic.VoteCount, null);
        }

        var existing = await database.Votes
            .SingleOrDefaultAsync(v => v.TopicId == topicId && v.UserId == userId, cancellationToken);

        if (existing is not null && existing.Value == value)
        {
            return new VoteResult(VoteOutcome.Unchanged, topic.ScoreSum, topic.VoteCount, value);
        }

        var scoreDelta = value - (existing?.Value ?? 0);
        var countDelta = existing is null ? 1 : 0;
        var outcome = existing is null ? VoteOutcome.Recorded : VoteOutcome.Changed;

        var (score, count) = await AdjustTalliesAsync(topicId, scoreDelta, countDelta, cancellationToken);

        if (existing is null)
        {
            database.Votes.Add(Vote.OnTopic(topicId, userId, value, clock.UtcNow));
        }
        else
        {
            existing.ChangeTo(value, clock.UtcNow);
        }

        await database.SaveChangesAsync(cancellationToken);

        return new VoteResult(outcome, score, count, value);
    }

    private async Task<VoteResult> WithdrawCoreAsync(
        Guid topicId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var topic = await database.Topics
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == topicId, cancellationToken);

        if (topic is null)
        {
            return new VoteResult(VoteOutcome.NotFound, 0, 0, null);
        }

        if (topic.IsClosedAt(clock.UtcNow))
        {
            return new VoteResult(VoteOutcome.Closed, topic.ScoreSum, topic.VoteCount, null);
        }

        var existing = await database.Votes
            .SingleOrDefaultAsync(v => v.TopicId == topicId && v.UserId == userId, cancellationToken);

        if (existing is null)
        {
            return new VoteResult(VoteOutcome.NothingToWithdraw, topic.ScoreSum, topic.VoteCount, null);
        }

        var (score, count) = await AdjustTalliesAsync(topicId, -existing.Value, -1, cancellationToken);

        database.Votes.Remove(existing);
        await database.SaveChangesAsync(cancellationToken);

        return new VoteResult(VoteOutcome.Withdrawn, score, count, null);
    }

    /// <summary>
    /// Applies the tally change and returns the resulting figures.
    /// </summary>
    /// <remarks>
    /// Done before the vote row is written, on purpose. Every concurrent voter on a topic
    /// contends for this one row, so taking its lock first makes them queue behind each
    /// other; writing the vote first and the tally second makes them acquire two locks in
    /// opposite orders and deadlock instead.
    /// </remarks>
    private async Task<(int ScoreSum, int VoteCount)> AdjustTalliesAsync(
        Guid topicId,
        int scoreDelta,
        int countDelta,
        CancellationToken cancellationToken)
    {
        if (scoreDelta != 0 || countDelta != 0)
        {
            await database.Topics
                .Where(t => t.Id == topicId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(t => t.ScoreSum, t => t.ScoreSum + scoreDelta)
                        .SetProperty(t => t.VoteCount, t => t.VoteCount + countDelta),
                    cancellationToken);
        }

        var tallies = await database.Topics
            .AsNoTracking()
            .Where(t => t.Id == topicId)
            .Select(t => new { t.ScoreSum, t.VoteCount })
            .SingleAsync(cancellationToken);

        return (tallies.ScoreSum, tallies.VoteCount);
    }

    /// <summary>
    /// Runs one vote operation as a single atomic, retryable unit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The whole operation — reading the current vote, computing the delta, adjusting the
    /// tally and writing the row — happens inside the transaction and inside the retry, and
    /// the change tracker is cleared at the start of every attempt.
    /// </para>
    /// <para>
    /// That last detail is not incidental. An earlier version read the state outside the
    /// retry and only wrapped the writes: <c>SaveChanges</c> inserted the vote and the change
    /// tracker marked it saved, the tally update then deadlocked against another voter, the
    /// transaction rolled the insert back, and the retry found nothing left to save — so the
    /// tally moved while the vote row vanished. A concurrency test caught it as eight votes
    /// counted against three rows. Anything re-executed by the execution strategy has to be
    /// able to start from scratch.
    /// </para>
    /// <para>
    /// A unique-index violation is handled by retrying once as well: that is the same user
    /// voting twice at once, typically a double-clicked button, and on the second pass the
    /// value they already hold is simply reported back.
    /// </para>
    /// </remarks>
    private async Task<VoteResult> InTransactionAsync(
        Func<Task<VoteResult>> operation,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var strategy = database.Database.CreateExecutionStrategy();

                return await strategy.ExecuteAsync(async () =>
                {
                    database.ChangeTracker.Clear();

                    await using var transaction =
                        await database.Database.BeginTransactionAsync(cancellationToken);

                    var result = await operation();

                    await transaction.CommitAsync(cancellationToken);

                    return result;
                });
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                database.ChangeTracker.Clear();
            }
        }
    }
}
