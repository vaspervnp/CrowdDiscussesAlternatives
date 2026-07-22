using CDA.Application.Topics;
using CDA.Domain.Topics;

namespace CDA.UnitTests.Topics;

public class TopicAccessPolicyTests
{
    private static readonly DateTime Now = new(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid OwnerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid StrangerId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static Topic Topic(
        TopicVisibility visibility = TopicVisibility.Public,
        DateTime? closesAt = null) =>
        new(Guid.NewGuid(), "How should we reduce traffic?", "", OwnerId, Now.AddDays(-1), visibility, closesAt);

    private static TopicViewer Member => new(StrangerId, false, TopicRole.Member);

    private static TopicViewer Facilitator => new(OwnerId, false, TopicRole.Facilitator);

    private static TopicViewer Stranger => new(StrangerId);

    private static TopicViewer Administrator => new(StrangerId, IsAdministrator: true);

    [Fact]
    public void A_public_topic_is_readable_by_anyone_including_anonymous()
    {
        var topic = Topic();

        Assert.True(TopicAccessPolicy.CanView(topic, TopicViewer.Anonymous));
        Assert.True(TopicAccessPolicy.CanView(topic, Stranger));
    }

    [Fact]
    public void An_invite_only_topic_is_readable_only_by_its_members()
    {
        var topic = Topic(TopicVisibility.InviteOnly);

        Assert.False(TopicAccessPolicy.CanView(topic, TopicViewer.Anonymous));
        Assert.False(TopicAccessPolicy.CanView(topic, Stranger));
        Assert.True(TopicAccessPolicy.CanView(topic, Member));
        Assert.True(TopicAccessPolicy.CanView(topic, Administrator));
    }

    [Fact]
    public void Reading_a_public_topic_does_not_confer_the_right_to_rank_it()
    {
        // One vote should mean one person, which anonymous access cannot guarantee.
        var topic = Topic();

        Assert.True(TopicAccessPolicy.CanView(topic, TopicViewer.Anonymous));
        Assert.False(TopicAccessPolicy.CanVote(topic, TopicViewer.Anonymous, Now));
        Assert.True(TopicAccessPolicy.CanVote(topic, Stranger, Now));
    }

    [Fact]
    public void Nobody_can_vote_on_an_invite_only_topic_they_cannot_read()
    {
        var topic = Topic(TopicVisibility.InviteOnly);

        Assert.False(TopicAccessPolicy.CanVote(topic, Stranger, Now));
        Assert.True(TopicAccessPolicy.CanVote(topic, Member, Now));
    }

    [Fact]
    public void A_topic_past_its_completion_date_accepts_no_votes()
    {
        var topic = Topic(closesAt: Now.AddDays(-1));

        Assert.True(topic.IsClosedAt(Now));
        Assert.False(TopicAccessPolicy.CanVote(topic, Member, Now));
    }

    [Fact]
    public void A_topic_moved_to_closed_accepts_no_votes()
    {
        var topic = Topic();
        topic.Close();

        Assert.False(TopicAccessPolicy.CanVote(topic, Member, Now));
    }

    [Fact]
    public void Only_facilitators_and_administrators_administer_a_topic()
    {
        var topic = Topic();

        Assert.True(TopicAccessPolicy.CanAdminister(topic, Facilitator));
        Assert.True(TopicAccessPolicy.CanAdminister(topic, Administrator));
        Assert.False(TopicAccessPolicy.CanAdminister(topic, Member));
        Assert.False(TopicAccessPolicy.CanAdminister(topic, TopicViewer.Anonymous));
    }

    [Fact]
    public void Invite_only_topics_are_joined_by_invitation_not_by_asking()
    {
        Assert.False(TopicAccessPolicy.CanJoin(Topic(TopicVisibility.InviteOnly), Stranger, Now));
        Assert.True(TopicAccessPolicy.CanJoin(Topic(), Stranger, Now));
    }

    [Fact]
    public void Existing_members_and_anonymous_visitors_cannot_join()
    {
        var topic = Topic();

        Assert.False(TopicAccessPolicy.CanJoin(topic, Member, Now));
        Assert.False(TopicAccessPolicy.CanJoin(topic, TopicViewer.Anonymous, Now));
    }

    [Fact]
    public void A_closed_topic_cannot_be_joined()
    {
        Assert.False(TopicAccessPolicy.CanJoin(Topic(closesAt: Now.AddMinutes(-1)), Stranger, Now));
    }

    [Fact]
    public void Vote_counts_are_visible_by_default()
    {
        Assert.True(TopicAccessPolicy.CanSeeVoteCounts(Topic(), Stranger, Now));
    }

    [Fact]
    public void A_topic_that_hides_its_tallies_hides_them_from_participants_while_open()
    {
        var topic = Topic();
        topic.SetVoteCountsHidden(true);

        Assert.False(TopicAccessPolicy.CanSeeVoteCounts(topic, Member, Now));
        Assert.False(TopicAccessPolicy.CanSeeVoteCounts(topic, TopicViewer.Anonymous, Now));
    }

    [Fact]
    public void The_facilitator_still_sees_hidden_tallies()
    {
        // They need the figures to run the discussion, and they chose to hide them.
        var topic = Topic();
        topic.SetVoteCountsHidden(true);

        Assert.True(TopicAccessPolicy.CanSeeVoteCounts(topic, Facilitator, Now));
        Assert.True(TopicAccessPolicy.CanSeeVoteCounts(topic, Administrator, Now));
    }

    [Fact]
    public void Hidden_tallies_are_revealed_once_the_topic_closes()
    {
        var topic = Topic(closesAt: Now.AddMinutes(-1));
        topic.SetVoteCountsHidden(true);

        Assert.True(TopicAccessPolicy.CanSeeVoteCounts(topic, Member, Now));
    }
}
