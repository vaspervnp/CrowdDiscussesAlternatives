using CDA.Domain.Topics;

namespace CDA.Application.Topics;

/// <summary>
/// Who is asking, and what standing they have in the topic being asked about.
/// </summary>
/// <param name="UserId">The viewer's id, or null when nobody is signed in.</param>
/// <param name="IsAdministrator">Whether they hold the platform administrator role.</param>
/// <param name="Role">Their role in this topic, or null if they are not a member of it.</param>
public sealed record TopicViewer(Guid? UserId, bool IsAdministrator = false, TopicRole? Role = null)
{
    // The cast is required: without it `new(null)` binds to the record's copy constructor.
    public static readonly TopicViewer Anonymous = new((Guid?)null);

    public bool IsSignedIn => UserId is not null;

    public bool IsMember => Role is not null;

    public bool IsFacilitator => Role == TopicRole.Facilitator;
}
