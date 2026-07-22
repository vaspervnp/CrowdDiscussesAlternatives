namespace CDA.Domain.Groups;

/// <summary>
/// One alternative solution: a set of proposals, taken together.
/// </summary>
/// <remarks>
/// <para>
/// This is what the platform exists to produce. A solution is not written by one person and
/// then accepted or rejected wholesale — it is assembled from the shared pool, so that someone
/// who disagrees with one part of an answer can put together a different set rather than
/// rejecting the whole thing. The differences between competing answers then show up as
/// differences in membership, which is far easier to argue about than two essays.
/// </para>
/// <para>
/// The order of the proposals inside a group carries no meaning; it is a set, not a list.
/// </para>
/// </remarks>
public sealed class ProposalGroup
{
    public const int DescriptionMaxLength = 2000;

    private ProposalGroup()
    {
        // EF Core.
        Description = null!;
    }

    public ProposalGroup(
        Guid id,
        Guid topicId,
        Guid createdByUserId,
        string description,
        DateTime createdAtUtc,
        Guid? improvesGroupId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        Id = id;
        TopicId = topicId;
        CreatedByUserId = createdByUserId;
        Description = description.Trim();
        CreatedAtUtc = createdAtUtc;
        ImprovesGroupId = improvesGroupId;
    }

    public Guid Id { get; private set; }

    public Guid TopicId { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    /// <summary>
    /// What this combination amounts to, and why these proposals belong together.
    /// </summary>
    /// <remarks>
    /// Required. A bare list of proposals leaves everyone to guess at the reasoning that
    /// selected them, which is most of what distinguishes one alternative from another.
    /// </remarks>
    public string Description { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? EditedAtUtc { get; private set; }

    /// <summary>
    /// The group this one is a variant of, if it is a refinement rather than a fresh answer.
    /// </summary>
    /// <remarks>
    /// Marking a variant as such keeps the list readable: a reader can see that six of the
    /// eight alternatives are adjustments of two underlying approaches, which is a different
    /// picture from eight unrelated answers.
    /// </remarks>
    public Guid? ImprovesGroupId { get; private set; }

    // Maintained transactionally with every vote and comment.
    public int ScoreSum { get; private set; }

    public int VoteCount { get; private set; }

    public int CommentCount { get; private set; }

    public DateTime? LastCommentAtUtc { get; private set; }

    /// <summary>
    /// Whether people have already judged this combination.
    /// </summary>
    /// <remarks>
    /// Editing after this point changes what those votes were cast on. The platform allows it —
    /// the creator may be answering criticism — but says so plainly and offers the alternative
    /// of forking a variant instead.
    /// </remarks>
    public bool HasBeenJudged => VoteCount > 0;

    public void Edit(string description, DateTime atUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        Description = description.Trim();
        EditedAtUtc = atUtc;
    }

    public void ApplyVoteDelta(int scoreDelta, int countDelta)
    {
        ScoreSum += scoreDelta;
        VoteCount += countDelta;
    }

    public void RecordComment(DateTime atUtc)
    {
        CommentCount++;
        LastCommentAtUtc = atUtc;
    }

    public void RecordMembershipChange(DateTime atUtc) => EditedAtUtc = atUtc;
}

/// <summary>Membership of a proposal in an alternative solution.</summary>
public sealed class GroupItem
{
    private GroupItem()
    {
        // EF Core.
    }

    public GroupItem(Guid groupId, Guid proposalId)
    {
        GroupId = groupId;
        ProposalId = proposalId;
    }

    public Guid GroupId { get; private set; }

    public Guid ProposalId { get; private set; }
}
