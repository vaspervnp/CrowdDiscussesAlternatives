namespace CDA.Domain.Topics;

/// <summary>
/// A participant's membership of one topic, and their standing in it.
/// </summary>
/// <remarks>
/// Nearly every authorisation decision in the platform routes through this: who may read an
/// invite-only topic, who may post, who may set requirements or close the discussion.
/// </remarks>
public sealed class TopicMember
{
    private TopicMember()
    {
        // EF Core.
    }

    public TopicMember(Guid topicId, Guid userId, TopicRole role, DateTime joinedAtUtc)
    {
        TopicId = topicId;
        UserId = userId;
        Role = role;
        JoinedAtUtc = joinedAtUtc;
    }

    public Guid TopicId { get; private set; }

    public Guid UserId { get; private set; }

    public TopicRole Role { get; private set; }

    public DateTime JoinedAtUtc { get; private set; }

    public void ChangeRole(TopicRole role) => Role = role;
}
