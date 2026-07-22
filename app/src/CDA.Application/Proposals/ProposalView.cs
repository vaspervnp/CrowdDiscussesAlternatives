using CDA.Application.Topics;
using CDA.Domain.Proposals;

namespace CDA.Application.Proposals;

/// <summary>
/// A proposal as one particular viewer is allowed to see it.
/// </summary>
/// <remarks>
/// <see cref="ScoreSum"/> and <see cref="VoteCount"/> are null when the topic withholds its
/// tallies from this viewer, exactly as for topics — the ordering is applied before projection,
/// so a ranked list stays correct with the numbers absent.
/// </remarks>
public sealed record ProposalView
{
    public required Guid Id { get; init; }

    public required Guid TopicId { get; init; }

    public required Guid AuthorId { get; init; }

    public required string AuthorDisplayName { get; init; }

    public required string Text { get; init; }

    public required DateTime CreatedAtUtc { get; init; }

    public DateTime? EditedAtUtc { get; init; }

    /// <summary>True once the wording is final and voting has opened.</summary>
    public required bool IsLocked { get; init; }

    public required DateTime EditableUntilUtc { get; init; }

    public int? ScoreSum { get; init; }

    public int? VoteCount { get; init; }

    public required int CommentCount { get; init; }

    public DateTime? LastCommentAtUtc { get; init; }

    public short? MyVote { get; init; }

    public required bool CanVote { get; init; }

    public required bool CanEdit { get; init; }

    public required bool CanComment { get; init; }

    /// <summary>
    /// How many other proposals this one stands for, when duplicates are folded together.
    /// </summary>
    /// <remarks>
    /// Zero when it stands only for itself. Greater than zero means the reader's similarity
    /// threshold has folded a group of duplicates into this entry.
    /// </remarks>
    public int CollapsedDuplicates { get; init; }

    /// <summary>
    /// The combined score of a folded group.
    /// </summary>
    /// <remarks>
    /// Support that was split across duplicates belongs to the idea, not to whichever wording
    /// happened to be listed — showing only the representative's own score would understate it
    /// by exactly the amount the duplication cost.
    /// </remarks>
    public int? GroupScoreSum { get; init; }

    public ProposalView WithCollapse(int duplicates, int? groupScore) =>
        this with { CollapsedDuplicates = duplicates, GroupScoreSum = groupScore };

    public static ProposalView Project(
        Proposal proposal,
        string authorDisplayName,
        Domain.Topics.Topic topic,
        TopicViewer viewer,
        DateTime utcNow,
        short? myVote = null)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(topic);
        ArgumentNullException.ThrowIfNull(viewer);

        var countsVisible = TopicAccessPolicy.CanSeeVoteCounts(topic, viewer, utcNow);
        var locked = proposal.IsLockedAt(utcNow);
        var topicClosed = topic.IsClosedAt(utcNow);

        return new ProposalView
        {
            Id = proposal.Id,
            TopicId = proposal.TopicId,
            AuthorId = proposal.AuthorId,
            AuthorDisplayName = authorDisplayName,
            Text = proposal.Text,
            CreatedAtUtc = proposal.CreatedAtUtc,
            EditedAtUtc = proposal.EditedAtUtc,
            IsLocked = locked,
            EditableUntilUtc = proposal.EditableUntilUtc,
            ScoreSum = countsVisible ? proposal.ScoreSum : null,
            VoteCount = countsVisible ? proposal.VoteCount : null,
            CommentCount = proposal.CommentCount,
            LastCommentAtUtc = proposal.LastCommentAtUtc,
            MyVote = myVote,

            // Voting opens only once the wording has stopped moving.
            CanVote = locked && !topicClosed && viewer.IsSignedIn && TopicAccessPolicy.CanView(topic, viewer),
            CanEdit = !locked && !topicClosed && viewer.UserId == proposal.AuthorId,

            // Commenting is the point during the editing window: it is how the author finds
            // out what to improve.
            CanComment = !topicClosed && viewer.IsSignedIn && TopicAccessPolicy.CanView(topic, viewer),
        };
    }
}
