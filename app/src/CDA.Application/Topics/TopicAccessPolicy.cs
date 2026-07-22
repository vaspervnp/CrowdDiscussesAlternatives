using CDA.Domain.Topics;

namespace CDA.Application.Topics;

/// <summary>
/// Every authorisation decision about a topic, in one place.
/// </summary>
/// <remarks>
/// Topics are reachable through MVC pages and will be reachable through the REST API, and a
/// rule enforced in one presentation layer is not enforced at all. Keeping the decisions
/// here — rather than as <c>if</c> statements in controllers — is also what makes them
/// testable without a web request.
/// </remarks>
public static class TopicAccessPolicy
{
    /// <summary>Whether the viewer may read the topic at all.</summary>
    public static bool CanView(Topic topic, TopicViewer viewer)
    {
        ArgumentNullException.ThrowIfNull(topic);
        ArgumentNullException.ThrowIfNull(viewer);

        return topic.Visibility switch
        {
            TopicVisibility.Public => true,
            TopicVisibility.InviteOnly => viewer.IsMember || viewer.IsAdministrator,
            _ => false,
        };
    }

    /// <summary>Whether the viewer may join a topic they can see.</summary>
    public static bool CanJoin(Topic topic, TopicViewer viewer, DateTime utcNow) =>
        viewer.IsSignedIn
        && !viewer.IsMember
        && !topic.IsClosedAt(utcNow)
        // Invite-only topics are joined by invitation, not by asking.
        && topic.Visibility == TopicVisibility.Public;

    /// <summary>
    /// Whether the viewer may vote on the topic's importance.
    /// </summary>
    /// <remarks>
    /// Reading a public topic does not confer the right to rank it — that requires signing
    /// in, so that one vote means one person. A closed topic accepts no further votes.
    /// </remarks>
    public static bool CanVote(Topic topic, TopicViewer viewer, DateTime utcNow) =>
        viewer.IsSignedIn && CanView(topic, viewer) && !topic.IsClosedAt(utcNow);

    /// <summary>Whether the viewer may change the topic's settings, phase or requirements.</summary>
    public static bool CanAdminister(Topic topic, TopicViewer viewer) =>
        viewer.IsFacilitator || viewer.IsAdministrator;

    /// <summary>
    /// Whether the viewer may see the numeric tallies, as opposed to the ranking.
    /// </summary>
    /// <remarks>
    /// The facilitator is exempt because they need the figures to run the discussion, and
    /// they are the one who chose to hide them.
    /// </remarks>
    public static bool CanSeeVoteCounts(Topic topic, TopicViewer viewer, DateTime utcNow) =>
        !topic.HideVoteCountsUntilClose
        || topic.IsClosedAt(utcNow)
        || CanAdminister(topic, viewer);
}
