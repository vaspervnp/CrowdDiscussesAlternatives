using CDA.Domain.Topics;
using CDA.Infrastructure.Identity;
using CDA.Infrastructure.Persistence;
using CDA.Infrastructure.Topics;
using CDA.Infrastructure.Voting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;

namespace CDA.IntegrationTests;

/// <summary>
/// The voting machinery against real SQL. Proposals, groups, references and similarity
/// reports will all reuse it, so these are the invariants the rest of the platform leans on.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class TopicVotingTests(DatabaseFixture database) : IAsyncLifetime
{
    private CdaWebApplicationFactory _factory = null!;

    public async Task InitializeAsync()
    {
        await database.ResetAsync();
        _factory = new CdaWebApplicationFactory(database);
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private IServiceScope Scope() => _factory.Services.CreateScope();

    private async Task<Guid> UserAsync(string name)
    {
        using var scope = Scope();
        var accounts = scope.ServiceProvider.GetRequiredService<UserAccountService>();
        var result = await accounts.RegisterAsync($"{name}@example.com", name, "a long spoken passphrase");
        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        return result.UserId;
    }

    private async Task<Guid> TopicAsync(Guid ownerId, TopicVisibility visibility = TopicVisibility.Public)
    {
        using var scope = Scope();
        var topics = scope.ServiceProvider.GetRequiredService<TopicService>();
        var topic = await topics.CreateAsync("How should we reduce traffic?", "", ownerId, visibility, null);
        return topic.Id;
    }

    private async Task<VoteResult> VoteAsync(Guid topicId, Guid userId, short value)
    {
        using var scope = Scope();
        var voting = scope.ServiceProvider.GetRequiredService<TopicVotingService>();
        return await voting.CastAsync(topicId, userId, value);
    }

    private async Task<(int Score, int Count, int Rows)> TalliesAsync(Guid topicId)
    {
        await using var context = database.CreateContext();
        var topic = await context.Topics.AsNoTracking().SingleAsync(t => t.Id == topicId);
        var rows = await context.Votes.CountAsync(v => v.TopicId == topicId);
        return (topic.ScoreSum, topic.VoteCount, rows);
    }

    [Fact]
    public async Task Creating_a_topic_makes_its_creator_the_facilitator()
    {
        // Otherwise nobody can administer it and there is no interface to repair that.
        var owner = await UserAsync("owner");
        var topicId = await TopicAsync(owner);

        await using var context = database.CreateContext();
        var membership = await context.TopicMembers.SingleAsync(m => m.TopicId == topicId);

        Assert.Equal(owner, membership.UserId);
        Assert.Equal(TopicRole.Facilitator, membership.Role);
    }

    [Fact]
    public async Task A_first_vote_is_recorded_and_moves_the_tallies()
    {
        var owner = await UserAsync("owner");
        var topicId = await TopicAsync(owner);

        var result = await VoteAsync(topicId, owner, 1);

        Assert.Equal(VoteOutcome.Recorded, result.Outcome);
        Assert.Equal((1, 1, 1), await TalliesAsync(topicId));
    }

    [Fact]
    public async Task Casting_the_same_value_twice_writes_nothing()
    {
        var owner = await UserAsync("owner");
        var topicId = await TopicAsync(owner);

        await VoteAsync(topicId, owner, 1);
        var second = await VoteAsync(topicId, owner, 1);

        Assert.Equal(VoteOutcome.Unchanged, second.Outcome);
        Assert.Equal((1, 1, 1), await TalliesAsync(topicId));
    }

    [Fact]
    public async Task Changing_a_vote_replaces_it_rather_than_adding_one()
    {
        var owner = await UserAsync("owner");
        var topicId = await TopicAsync(owner);

        await VoteAsync(topicId, owner, 1);
        var changed = await VoteAsync(topicId, owner, -1);

        Assert.Equal(VoteOutcome.Changed, changed.Outcome);
        // Score swings by two; the participation count does not move.
        Assert.Equal((-1, 1, 1), await TalliesAsync(topicId));
    }

    [Fact]
    public async Task An_abstention_counts_as_participation_but_adds_nothing_to_the_score()
    {
        var owner = await UserAsync("owner");
        var topicId = await TopicAsync(owner);

        await VoteAsync(topicId, owner, 0);

        Assert.Equal((0, 1, 1), await TalliesAsync(topicId));
    }

    [Fact]
    public async Task Withdrawing_is_different_from_abstaining()
    {
        var owner = await UserAsync("owner");
        var topicId = await TopicAsync(owner);
        await VoteAsync(topicId, owner, 1);

        using var scope = Scope();
        var voting = scope.ServiceProvider.GetRequiredService<TopicVotingService>();
        var result = await voting.WithdrawAsync(topicId, owner);

        Assert.Equal(VoteOutcome.Withdrawn, result.Outcome);
        // The row is gone; an abstention would have left one behind with a count of 1.
        Assert.Equal((0, 0, 0), await TalliesAsync(topicId));
    }

    [Fact]
    public async Task Withdrawing_a_vote_that_was_never_cast_changes_nothing()
    {
        var owner = await UserAsync("owner");
        var topicId = await TopicAsync(owner);

        using var scope = Scope();
        var voting = scope.ServiceProvider.GetRequiredService<TopicVotingService>();
        var result = await voting.WithdrawAsync(topicId, owner);

        Assert.Equal(VoteOutcome.NothingToWithdraw, result.Outcome);
        Assert.Equal((0, 0, 0), await TalliesAsync(topicId));
    }

    [Fact]
    public async Task A_closed_topic_accepts_no_votes()
    {
        var owner = await UserAsync("owner");
        var topicId = await TopicAsync(owner);

        await using (var context = database.CreateContext())
        {
            var topic = await context.Topics.SingleAsync(t => t.Id == topicId);
            topic.MoveTo(TopicPhase.Closed);
            await context.SaveChangesAsync();
        }

        var result = await VoteAsync(topicId, owner, 1);

        Assert.Equal(VoteOutcome.Closed, result.Outcome);
        Assert.Equal((0, 0, 0), await TalliesAsync(topicId));
    }

    [Fact]
    public async Task Voting_on_a_topic_that_does_not_exist_reports_not_found()
    {
        var owner = await UserAsync("owner");

        var result = await VoteAsync(Guid.NewGuid(), owner, 1);

        Assert.Equal(VoteOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task Concurrent_votes_from_different_people_are_all_counted()
    {
        // The reason the tallies are updated with a relative UPDATE rather than by reading,
        // adding and writing back: read-modify-write loses all but one of these.
        var owner = await UserAsync("owner");
        var topicId = await TopicAsync(owner);

        var voters = new List<Guid> { owner };
        for (var i = 0; i < 7; i++)
        {
            voters.Add(await UserAsync($"voter{i}"));
        }

        await Task.WhenAll(voters.Select(voter => VoteAsync(topicId, voter, 1)));

        Assert.Equal((8, 8, 8), await TalliesAsync(topicId));
    }

    [Fact]
    public async Task The_same_person_voting_twice_at_once_still_holds_one_vote()
    {
        // A double-clicked vote button. The unique index settles the race; the loser applies
        // its value as a change rather than failing the request.
        var owner = await UserAsync("owner");
        var topicId = await TopicAsync(owner);

        await Task.WhenAll(
            VoteAsync(topicId, owner, 1),
            VoteAsync(topicId, owner, 1));

        var (score, count, rows) = await TalliesAsync(topicId);

        Assert.Equal(1, rows);
        Assert.Equal(1, count);
        Assert.Equal(1, score);
    }

    [Fact]
    public async Task Mixed_votes_sum_the_way_the_ranking_expects()
    {
        var owner = await UserAsync("owner");
        var topicId = await TopicAsync(owner);
        var against1 = await UserAsync("against1");
        var against2 = await UserAsync("against2");
        var neutral = await UserAsync("neutral");

        await VoteAsync(topicId, owner, 1);
        await VoteAsync(topicId, against1, -1);
        await VoteAsync(topicId, against2, -1);
        await VoteAsync(topicId, neutral, 0);

        // Four people considered it; the score is -1.
        Assert.Equal((-1, 4, 4), await TalliesAsync(topicId));
    }

    [Fact]
    public async Task The_database_refuses_a_vote_value_outside_the_permitted_range()
    {
        // Belt and braces: the domain rejects it, and so does the check constraint, so data
        // arriving by any other route cannot corrupt the ranking.
        var owner = await UserAsync("owner");
        var topicId = await TopicAsync(owner);

        await using var context = database.CreateContext();

        // Raw SQL surfaces the provider's exception rather than EF's wrapper.
        var error = await Assert.ThrowsAsync<MySqlException>(async () =>
        {
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO Votes (Id, UserId, Value, TopicId, CastAtUtc) VALUES (UUID(), {0}, 5, {1}, UTC_TIMESTAMP())",
                owner, topicId);
        });

        Assert.Contains("CK_Votes_Value", error.Message);
    }
}
