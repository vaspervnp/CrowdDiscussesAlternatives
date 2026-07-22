namespace CDA.Domain.Similarity;

/// <summary>
/// One participant's claim that two proposals say the same thing.
/// </summary>
/// <remarks>
/// <para>
/// A claim, not a fact. The platform never merges proposals on its own; it records that someone
/// thinks two are duplicates, lets others vote on whether they agree, and leaves each reader to
/// decide how much agreement is enough before the pair folds together for them.
/// </para>
/// <para>
/// The pair is stored in a fixed order so that "A is like B" and "B is like A" are the same
/// row. Without that a proposal could accumulate several reports of the same claim, each with
/// its own votes, and the threshold would be measured against a divided tally.
/// </para>
/// </remarks>
public sealed class SimilarityReport
{
    public const int JustificationMaxLength = 1000;

    private SimilarityReport()
    {
        // EF Core.
    }

    private SimilarityReport(
        Guid id,
        Guid topicId,
        Guid proposalAId,
        Guid proposalBId,
        Guid reportedByUserId,
        Guid? betterWrittenProposalId,
        string? justification,
        DateTime createdAtUtc)
    {
        Id = id;
        TopicId = topicId;
        ProposalAId = proposalAId;
        ProposalBId = proposalBId;
        ReportedByUserId = reportedByUserId;
        BetterWrittenProposalId = betterWrittenProposalId;
        Justification = string.IsNullOrWhiteSpace(justification) ? null : justification.Trim();
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>
    /// Creates a report, putting the pair into its canonical order.
    /// </summary>
    public static SimilarityReport Between(
        Guid topicId,
        Guid firstProposalId,
        Guid secondProposalId,
        Guid reportedByUserId,
        Guid? betterWrittenProposalId,
        string? justification,
        DateTime createdAtUtc)
    {
        if (firstProposalId == secondProposalId)
        {
            throw new ArgumentException("A proposal cannot be similar to itself.", nameof(secondProposalId));
        }

        if (betterWrittenProposalId is { } preferred
            && preferred != firstProposalId && preferred != secondProposalId)
        {
            throw new ArgumentException(
                "The better-written proposal must be one of the pair.", nameof(betterWrittenProposalId));
        }

        // Ordering by id makes the pair canonical; see the remarks on the class.
        var (a, b) = firstProposalId.CompareTo(secondProposalId) < 0
            ? (firstProposalId, secondProposalId)
            : (secondProposalId, firstProposalId);

        return new SimilarityReport(
            Guid.NewGuid(), topicId, a, b, reportedByUserId, betterWrittenProposalId,
            justification, createdAtUtc);
    }

    public Guid Id { get; private set; }

    public Guid TopicId { get; private set; }

    public Guid ProposalAId { get; private set; }

    public Guid ProposalBId { get; private set; }

    public Guid ReportedByUserId { get; private set; }

    /// <summary>
    /// Which of the pair the reporter judged better written, if either.
    /// </summary>
    /// <remarks>
    /// Used to decide which of a duplicate group is the one shown. The reporter is the person
    /// who read both closely enough to notice they matched, so their judgement is the best
    /// signal available.
    /// </remarks>
    public Guid? BetterWrittenProposalId { get; private set; }

    /// <summary>Why the reporter thinks they are the same. Optional but strongly encouraged.</summary>
    public string? Justification { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    // Maintained transactionally with every vote, like every other tally.
    public int ScoreSum { get; private set; }

    public int VoteCount { get; private set; }

    /// <summary>Whether this report carries enough agreement to take effect at the given threshold.</summary>
    public bool IsActiveAt(int threshold) => ScoreSum >= threshold;

    public void ApplyVoteDelta(int scoreDelta, int countDelta)
    {
        ScoreSum += scoreDelta;
        VoteCount += countDelta;
    }

    public bool Involves(Guid proposalId) => ProposalAId == proposalId || ProposalBId == proposalId;

    public Guid Other(Guid proposalId) => ProposalAId == proposalId ? ProposalBId : ProposalAId;
}
