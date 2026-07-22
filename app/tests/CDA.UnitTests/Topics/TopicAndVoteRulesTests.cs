using CDA.Domain.Topics;
using CDA.Domain.Voting;

namespace CDA.UnitTests.Topics;

public class TopicAndVoteRulesTests
{
    private static readonly DateTime Now = new(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData((short)-1)]
    [InlineData((short)0)]
    [InlineData((short)1)]
    public void A_vote_accepts_the_three_valid_values(short value)
    {
        var vote = Vote.OnTopic(Guid.NewGuid(), Guid.NewGuid(), value, Now);

        Assert.Equal(value, vote.Value);
    }

    [Theory]
    [InlineData((short)2)]
    [InlineData((short)-2)]
    [InlineData((short)100)]
    public void A_vote_rejects_anything_else(short value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Vote.OnTopic(Guid.NewGuid(), Guid.NewGuid(), value, Now));
    }

    [Fact]
    public void Changing_a_vote_revalidates_the_value()
    {
        var vote = Vote.OnTopic(Guid.NewGuid(), Guid.NewGuid(), 1, Now);

        Assert.Throws<ArgumentOutOfRangeException>(() => vote.ChangeTo(5, Now));
        Assert.Equal((short)1, vote.Value);
    }

    [Fact]
    public void Phases_move_forward_only()
    {
        // Reopening would resurrect votes cast on the understanding that it had ended.
        var topic = new Topic(Guid.NewGuid(), "Subject", "", Guid.NewGuid(), Now, TopicVisibility.Public);
        topic.MoveTo(TopicPhase.Proposing);
        topic.MoveTo(TopicPhase.Closed);

        Assert.Throws<InvalidOperationException>(() => topic.MoveTo(TopicPhase.Proposing));
        Assert.Throws<InvalidOperationException>(() => topic.MoveTo(TopicPhase.Discussing));
    }

    [Fact]
    public void Staying_in_the_same_phase_is_allowed()
    {
        var topic = new Topic(Guid.NewGuid(), "Subject", "", Guid.NewGuid(), Now, TopicVisibility.Public);

        topic.MoveTo(TopicPhase.Discussing);

        Assert.Equal(TopicPhase.Discussing, topic.Phase);
    }

    [Fact]
    public void A_topic_is_closed_by_its_phase_or_by_its_date_whichever_comes_first()
    {
        var dated = new Topic(
            Guid.NewGuid(), "Subject", "", Guid.NewGuid(), Now.AddDays(-2),
            TopicVisibility.Public, closesAtUtc: Now.AddDays(1));

        Assert.False(dated.IsClosedAt(Now));
        Assert.True(dated.IsClosedAt(Now.AddDays(2)));

        dated.MoveTo(TopicPhase.Closed);
        Assert.True(dated.IsClosedAt(Now));
    }

    [Fact]
    public void A_topic_with_no_completion_date_stays_open_until_it_is_closed()
    {
        var open = new Topic(Guid.NewGuid(), "Subject", "", Guid.NewGuid(), Now, TopicVisibility.Public);

        Assert.False(open.IsClosedAt(Now.AddYears(5)));
    }

    [Fact]
    public void A_topic_requires_a_subject()
    {
        Assert.Throws<ArgumentException>(
            () => new Topic(Guid.NewGuid(), "   ", "", Guid.NewGuid(), Now, TopicVisibility.Public));
    }
}
