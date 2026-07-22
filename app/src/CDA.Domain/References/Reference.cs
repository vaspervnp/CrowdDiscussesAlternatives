namespace CDA.Domain.References;

/// <summary>
/// What a reference is being judged on.
/// </summary>
/// <remarks>
/// The two questions are genuinely separate, and conflating them loses the interesting cases:
/// a source can be entirely accurate and beside the point, or highly relevant and unreliable.
/// Persisted — do not renumber.
/// </remarks>
public enum ReferenceAspect
{
    /// <summary>Is what it says true and trustworthy?</summary>
    Accuracy = 0,

    /// <summary>Does it matter to this topic?</summary>
    Importance = 1,
}

/// <summary>
/// A source cited in support of a proposal.
/// </summary>
/// <remarks>
/// <para>
/// A reference belongs to a topic rather than to a single proposal, and is attached to
/// proposals through <see cref="ProposalReference"/>. The same study usually supports several
/// proposals in the same discussion; storing it once means it is evaluated once, and the
/// judgement of its quality follows it wherever it is cited.
/// </para>
/// <para>
/// Its URL is unique within its topic, in canonical form — see <see cref="ReferenceUrl"/>.
/// </para>
/// </remarks>
public sealed class Reference
{
    public const int DescriptionMaxLength = 500;

    private Reference()
    {
        // EF Core.
        CanonicalUrl = null!;
        Description = null!;
    }

    public Reference(
        Guid id,
        Guid topicId,
        string canonicalUrl,
        string description,
        Guid createdByUserId,
        DateTime createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        Id = id;
        TopicId = topicId;
        CanonicalUrl = canonicalUrl;
        Description = description.Trim();
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid TopicId { get; private set; }

    /// <summary>Unique within the topic.</summary>
    public string CanonicalUrl { get; private set; }

    /// <summary>What this source is, in the words of whoever cited it first.</summary>
    public string Description { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    // Two independent tallies; see ReferenceAspect.
    public int AccuracyScore { get; private set; }

    public int AccuracyVotes { get; private set; }

    public int ImportanceScore { get; private set; }

    public int ImportanceVotes { get; private set; }

    /// <summary>The two scores together — what a citer's standing in the topic is built from.</summary>
    public int CombinedScore => AccuracyScore + ImportanceScore;

    public void ApplyVoteDelta(ReferenceAspect aspect, int scoreDelta, int countDelta)
    {
        switch (aspect)
        {
            case ReferenceAspect.Accuracy:
                AccuracyScore += scoreDelta;
                AccuracyVotes += countDelta;
                break;
            case ReferenceAspect.Importance:
                ImportanceScore += scoreDelta;
                ImportanceVotes += countDelta;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(aspect), aspect, "Unknown aspect.");
        }
    }
}

/// <summary>Links a reference to a proposal it supports.</summary>
public sealed class ProposalReference
{
    private ProposalReference()
    {
        // EF Core.
    }

    public ProposalReference(Guid proposalId, Guid referenceId, Guid addedByUserId, DateTime addedAtUtc)
    {
        ProposalId = proposalId;
        ReferenceId = referenceId;
        AddedByUserId = addedByUserId;
        AddedAtUtc = addedAtUtc;
    }

    public Guid ProposalId { get; private set; }

    public Guid ReferenceId { get; private set; }

    public Guid AddedByUserId { get; private set; }

    public DateTime AddedAtUtc { get; private set; }
}

/// <summary>
/// How well regarded one participant's sources are within one topic.
/// </summary>
/// <remarks>
/// Maintained transactionally alongside reference votes, and credited to whoever first cited
/// the source. It exists because the platform's design gives people whose references the crowd
/// trusts a small structural advantage: when alternative solutions are listed, those from the
/// three best-regarded citers are shown first. The incentive is deliberate — the quality of a
/// discussion rests on the quality of what it is arguing from.
/// </remarks>
public sealed class TopicUserReputation
{
    private TopicUserReputation()
    {
        // EF Core.
    }

    public TopicUserReputation(Guid topicId, Guid userId)
    {
        TopicId = topicId;
        UserId = userId;
    }

    public Guid TopicId { get; private set; }

    public Guid UserId { get; private set; }

    /// <summary>Sum of accuracy and importance scores across the references this user cited first.</summary>
    public int ReferenceScore { get; private set; }

    public void Apply(int delta) => ReferenceScore += delta;
}
