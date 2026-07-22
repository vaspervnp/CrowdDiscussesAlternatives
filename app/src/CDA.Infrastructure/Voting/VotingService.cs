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

    /// <summary>The target has finished and accepts no further votes.</summary>
    Closed,

    /// <summary>The target is not open to votes yet — a proposal still inside its editing window.</summary>
    NotOpenYet,

    /// <summary>No such target.</summary>
    NotFound,
}

public sealed record VoteResult(VoteOutcome Outcome, int ScoreSum, int VoteCount, short? MyVote)
{
    public bool Accepted => Outcome
        is VoteOutcome.Recorded or VoteOutcome.Changed or VoteOutcome.Unchanged or VoteOutcome.Withdrawn;
}

/// <summary>What the voting algorithm needs to know about a target before writing to it.</summary>
/// <param name="Refusal">Null when the target accepts votes; otherwise why it does not.</param>
public sealed record VotableSnapshot(int ScoreSum, int VoteCount, VoteOutcome? Refusal);

/// <summary>
/// The voting algorithm, shared by every votable thing.
/// </summary>
/// <remarks>
/// <para>
/// Topics, proposals and — later — groups, references and similarity reports all vote the same
/// way, and the parts that are easy to get wrong are the same for all of them. A user holds at
/// most one vote per target, enforced by a unique index. Casting the value you already hold
/// writes nothing. Withdrawing deletes the row, which is distinct from voting zero: zero is a
/// recorded abstention that still counts as participation.
/// </para>
/// <para>
/// Tallies are updated with a relative <c>UPDATE … SET ScoreSum = ScoreSum + @delta</c> rather
/// than by reading, adding and writing back. Several people voting on the same thing at the
/// same moment is the ordinary case, and read-modify-write silently loses all but one.
/// </para>
/// <para>
/// This lives in one place on purpose. The transaction-and-retry handling below took a real
/// bug to get right (see <see cref="InTransactionAsync"/>); duplicating it per target type
/// would mean duplicating that fix, and forgetting it in one of the copies.
/// </para>
/// </remarks>
public abstract class VotingService<TTarget>(CdaDbContext database, IClock clock)
    where TTarget : notnull
{
    protected CdaDbContext Database { get; } = database;

    protected IClock Clock { get; } = clock;

    /// <summary>Reads the target's current tallies and whether it is accepting votes.</summary>
    protected abstract Task<VotableSnapshot?> LoadAsync(TTarget target, CancellationToken cancellationToken);

    /// <summary>Finds this user's existing vote on the target, if any.</summary>
    protected abstract Task<Vote?> FindVoteAsync(TTarget target, Guid userId, CancellationToken cancellationToken);

    protected abstract Vote NewVote(TTarget target, Guid userId, short value, DateTime atUtc);

    /// <summary>Applies the tally change with a relative update and returns the new figures.</summary>
    protected abstract Task<(int ScoreSum, int VoteCount)> AdjustTalliesAsync(
        TTarget target, int scoreDelta, int countDelta, CancellationToken cancellationToken);

    public Task<VoteResult> CastAsync(
        TTarget target,
        Guid userId,
        short value,
        CancellationToken cancellationToken = default) =>
        InTransactionAsync(() => CastCoreAsync(target, userId, value, cancellationToken), cancellationToken);

    public Task<VoteResult> WithdrawAsync(
        TTarget target,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        InTransactionAsync(() => WithdrawCoreAsync(target, userId, cancellationToken), cancellationToken);

    private async Task<VoteResult> CastCoreAsync(
        TTarget target,
        Guid userId,
        short value,
        CancellationToken cancellationToken)
    {
        var snapshot = await LoadAsync(target, cancellationToken);

        if (snapshot is null)
        {
            return new VoteResult(VoteOutcome.NotFound, 0, 0, null);
        }

        // Checked here as well as in the access policy: whether something accepts votes is the
        // one rule that must not depend on the caller remembering to ask.
        if (snapshot.Refusal is { } refusal)
        {
            return new VoteResult(refusal, snapshot.ScoreSum, snapshot.VoteCount, null);
        }

        var existing = await FindVoteAsync(target, userId, cancellationToken);

        if (existing is not null && existing.Value == value)
        {
            return new VoteResult(VoteOutcome.Unchanged, snapshot.ScoreSum, snapshot.VoteCount, value);
        }

        var scoreDelta = value - (existing?.Value ?? 0);
        var countDelta = existing is null ? 1 : 0;
        var outcome = existing is null ? VoteOutcome.Recorded : VoteOutcome.Changed;

        var (score, count) = await AdjustTalliesAsync(target, scoreDelta, countDelta, cancellationToken);

        if (existing is null)
        {
            Database.Votes.Add(NewVote(target, userId, value, Clock.UtcNow));
        }
        else
        {
            existing.ChangeTo(value, Clock.UtcNow);
        }

        await Database.SaveChangesAsync(cancellationToken);

        return new VoteResult(outcome, score, count, value);
    }

    private async Task<VoteResult> WithdrawCoreAsync(
        TTarget target,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var snapshot = await LoadAsync(target, cancellationToken);

        if (snapshot is null)
        {
            return new VoteResult(VoteOutcome.NotFound, 0, 0, null);
        }

        if (snapshot.Refusal is { } refusal)
        {
            return new VoteResult(refusal, snapshot.ScoreSum, snapshot.VoteCount, null);
        }

        var existing = await FindVoteAsync(target, userId, cancellationToken);

        if (existing is null)
        {
            return new VoteResult(VoteOutcome.NothingToWithdraw, snapshot.ScoreSum, snapshot.VoteCount, null);
        }

        var (score, count) = await AdjustTalliesAsync(target, -existing.Value, -1, cancellationToken);

        Database.Votes.Remove(existing);
        await Database.SaveChangesAsync(cancellationToken);

        return new VoteResult(VoteOutcome.Withdrawn, score, count, null);
    }

    /// <summary>
    /// Runs one vote operation as a single atomic, retryable unit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The whole operation — reading the current vote, computing the delta, adjusting the tally
    /// and writing the row — happens inside the transaction and inside the retry, and the
    /// change tracker is cleared at the start of every attempt.
    /// </para>
    /// <para>
    /// That last detail is not incidental. An earlier version read the state outside the retry
    /// and wrapped only the writes: <c>SaveChanges</c> inserted the vote and the change tracker
    /// marked it saved, the tally update then deadlocked against another voter, the transaction
    /// rolled the insert back, and the retry found nothing left to save — so the tally moved
    /// while the vote row vanished. A concurrency test caught it as eight votes counted against
    /// three rows. Anything re-executed by the execution strategy has to be able to start from
    /// scratch.
    /// </para>
    /// <para>
    /// A unique-index violation is retried once as well: that is the same user voting twice at
    /// once, typically a double-clicked button, and on the second pass the value they already
    /// hold is simply reported back.
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
                var strategy = Database.Database.CreateExecutionStrategy();

                return await strategy.ExecuteAsync(async () =>
                {
                    Database.ChangeTracker.Clear();

                    await using var transaction =
                        await Database.Database.BeginTransactionAsync(cancellationToken);

                    var result = await operation();

                    await transaction.CommitAsync(cancellationToken);

                    return result;
                });
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                Database.ChangeTracker.Clear();
            }
        }
    }
}
