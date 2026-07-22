using CDA.Application.Topics;
using CDA.Domain.Topics;

namespace CDA.UnitTests.Topics;

public class TopicViewTests
{
    private static readonly DateTime Now = new(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid OwnerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MemberId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static Topic Scored(int score, int count)
    {
        var topic = new Topic(Guid.NewGuid(), "Subject", "", OwnerId, Now.AddDays(-1), TopicVisibility.Public);
        topic.ApplyVoteDelta(score, count);
        return topic;
    }

    [Fact]
    public void The_projection_carries_tallies_when_they_are_not_hidden()
    {
        var view = TopicView.Project(Scored(7, 12), new TopicViewer(MemberId), Now);

        Assert.Equal(7, view.ScoreSum);
        Assert.Equal(12, view.VoteCount);
    }

    [Fact]
    public void The_projection_removes_tallies_rather_than_leaving_them_to_the_caller()
    {
        // The point of doing it here: the same topic is reachable from Razor and from the
        // API, and a rule applied in one of them is not applied at all.
        var topic = Scored(7, 12);
        topic.SetVoteCountsHidden(true);

        var view = TopicView.Project(topic, new TopicViewer(MemberId), Now);

        Assert.Null(view.ScoreSum);
        Assert.Null(view.VoteCount);
    }

    [Fact]
    public void A_viewer_always_learns_their_own_vote_even_when_tallies_are_hidden()
    {
        // Their own vote discloses nothing about anyone else's.
        var topic = Scored(7, 12);
        topic.SetVoteCountsHidden(true);

        var view = TopicView.Project(topic, new TopicViewer(MemberId), Now, myVote: -1);

        Assert.Null(view.ScoreSum);
        Assert.Equal((short)-1, view.MyVote);
    }

    [Fact]
    public void Hiding_tallies_does_not_disturb_the_ranking()
    {
        var strong = Scored(50, 60);
        var weak = Scored(2, 60);
        strong.SetVoteCountsHidden(true);
        weak.SetVoteCountsHidden(true);

        // Ordering is done on the entity, before projection; the projection only strips the
        // numbers. Ranked lists therefore stay correct with the figures withheld.
        var ordered = new[] { weak, strong }
            .OrderByDescending(t => t.ScoreSum)
            .Select(t => TopicView.Project(t, new TopicViewer(MemberId), Now))
            .ToList();

        Assert.Equal(strong.Id, ordered[0].Id);
        Assert.All(ordered, view => Assert.Null(view.ScoreSum));
    }
}
