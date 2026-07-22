using CDA.Domain.References;
using CDA.Domain.Similarity;

namespace CDA.Domain.Voting;

/// <summary>
/// One participant's opinion of one votable thing.
/// </summary>
/// <remarks>
/// <para>
/// A single table serves every votable target — topics now, proposals, groups, references
/// and similarity reports later. Each target type gets its own nullable foreign key, with a
/// check constraint requiring exactly one of them to be set, so the database still enforces
/// referential integrity. Adding a target means adding a column and extending that
/// constraint; the columns are nullable from the outset because widening them later, once
/// this is the largest table in the schema, would be an expensive migration.
/// </para>
/// <para>
/// A user has at most one row per target, enforced by a unique index rather than by
/// application logic.
/// </para>
/// </remarks>
public sealed class Vote
{
    public const short Against = -1;
    public const short Abstain = 0;
    public const short For = 1;

    private Vote()
    {
        // EF Core.
    }

    private Vote(Guid userId, short value, DateTime castAtUtc)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Value = Validated(value);
        CastAtUtc = castAtUtc;
    }

    public static Vote OnTopic(Guid topicId, Guid userId, short value, DateTime castAtUtc) =>
        new(userId, value, castAtUtc) { TopicId = topicId };

    public static Vote OnProposal(Guid proposalId, Guid userId, short value, DateTime castAtUtc) =>
        new(userId, value, castAtUtc) { ProposalId = proposalId };

    public static Vote OnSimilarity(Guid similarityId, Guid userId, short value, DateTime castAtUtc) =>
        new(userId, value, castAtUtc) { SimilarityId = similarityId };

    public static Vote OnReference(
        Guid referenceId,
        ReferenceAspect aspect,
        Guid userId,
        short value,
        DateTime castAtUtc) =>
        new(userId, value, castAtUtc) { ReferenceId = referenceId, ReferenceAspect = aspect };

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    /// <summary>
    /// -1, 0 or +1.
    /// </summary>
    /// <remarks>
    /// Zero is a recorded abstention, not the absence of a vote: it adds nothing to the
    /// score but does count as participation, so "fifty people considered this and twenty
    /// were neutral" stays distinguishable from "thirty people saw it". Withdrawing removes
    /// the row; voting zero does not.
    /// </remarks>
    public short Value { get; private set; }

    public DateTime CastAtUtc { get; private set; }

    // Exactly one target is set. See the remarks on the class.
    public Guid? TopicId { get; private set; }

    public Guid? ProposalId { get; private set; }

    public Guid? SimilarityId { get; private set; }

    public Guid? ReferenceId { get; private set; }

    /// <summary>
    /// Which question this vote answers. Set only for references, which are judged on two
    /// independent axes and therefore take two votes per person.
    /// </summary>
    public ReferenceAspect? ReferenceAspect { get; private set; }

    public void ChangeTo(short value, DateTime atUtc)
    {
        Value = Validated(value);
        CastAtUtc = atUtc;
    }

    private static short Validated(short value) => value switch
    {
        Against or Abstain or For => value,
        _ => throw new ArgumentOutOfRangeException(
            nameof(value), value, "A vote must be -1, 0 or +1."),
    };
}
