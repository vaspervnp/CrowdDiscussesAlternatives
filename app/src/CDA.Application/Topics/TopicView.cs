using CDA.Domain.Topics;

namespace CDA.Application.Topics;

/// <summary>
/// A topic as one particular viewer is allowed to see it.
/// </summary>
/// <remarks>
/// <see cref="ScoreSum"/> and <see cref="VoteCount"/> are null when the topic withholds its
/// tallies and the viewer is not entitled to them. The ranking is still correct — the list
/// arrives in score order — but the numbers are absent, which is the whole point of the
/// setting. Because the projection produces nulls rather than the caller choosing not to
/// render them, neither a Razor view nor an API response can leak the figures.
/// </remarks>
public sealed record TopicView
{
    public required Guid Id { get; init; }

    public required string Subject { get; init; }

    public required string Description { get; init; }

    public required TopicPhase Phase { get; init; }

    public required TopicVisibility Visibility { get; init; }

    public required DateTime CreatedAtUtc { get; init; }

    public DateTime? ClosesAtUtc { get; init; }

    public required bool IsClosed { get; init; }

    public int? ScoreSum { get; init; }

    public int? VoteCount { get; init; }

    /// <summary>The viewer's own vote, or null if they have not cast one.</summary>
    public short? MyVote { get; init; }

    public required bool CanVote { get; init; }

    public required bool CanJoin { get; init; }

    public required bool CanAdminister { get; init; }

    public required bool IsMember { get; init; }

    public static TopicView Project(
        Topic topic,
        TopicViewer viewer,
        DateTime utcNow,
        short? myVote = null)
    {
        ArgumentNullException.ThrowIfNull(topic);
        ArgumentNullException.ThrowIfNull(viewer);

        var countsVisible = TopicAccessPolicy.CanSeeVoteCounts(topic, viewer, utcNow);

        return new TopicView
        {
            Id = topic.Id,
            Subject = topic.Subject,
            Description = topic.Description,
            Phase = topic.Phase,
            Visibility = topic.Visibility,
            CreatedAtUtc = topic.CreatedAtUtc,
            ClosesAtUtc = topic.ClosesAtUtc,
            IsClosed = topic.IsClosedAt(utcNow),
            ScoreSum = countsVisible ? topic.ScoreSum : null,
            VoteCount = countsVisible ? topic.VoteCount : null,
            // A viewer always knows how they voted; that discloses nothing about anyone else.
            MyVote = myVote,
            CanVote = TopicAccessPolicy.CanVote(topic, viewer, utcNow),
            CanJoin = TopicAccessPolicy.CanJoin(topic, viewer, utcNow),
            CanAdminister = TopicAccessPolicy.CanAdminister(topic, viewer),
            IsMember = viewer.IsMember,
        };
    }
}
