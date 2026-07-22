using CDA.Domain.References;
using CDA.Domain.Topics;
using CDA.Infrastructure.Identity;
using CDA.Infrastructure.Proposals;
using CDA.Infrastructure.References;
using CDA.Infrastructure.Topics;
using CDA.Infrastructure.Voting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CDA.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class ReferenceTests(DatabaseFixture database) : IAsyncLifetime
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

    private async Task<Guid> ProposingTopicAsync(Guid ownerId)
    {
        Guid topicId;

        using (var scope = Scope())
        {
            var topics = scope.ServiceProvider.GetRequiredService<TopicService>();
            var topic = await topics.CreateAsync("How should we reduce traffic?", "", ownerId, TopicVisibility.Public, null);
            topicId = topic.Id;

            await scope.ServiceProvider.GetRequiredService<RequirementService>()
                .AddAsync(topicId, "Must not increase journey times");
        }

        await using var context = database.CreateContext();
        var stored = await context.Topics.SingleAsync(t => t.Id == topicId);
        stored.OpenForProposals(requirementCount: 1);
        await context.SaveChangesAsync();

        return topicId;
    }

    private async Task<Guid> ProposalAsync(Guid topicId, Guid authorId, string text)
    {
        using var scope = Scope();
        var result = await scope.ServiceProvider.GetRequiredService<ProposalService>()
            .CreateAsync(topicId, authorId, text, null);
        Assert.True(result.Succeeded, result.Error);
        return result.Id;
    }

    private async Task<ReferenceResult> CiteAsync(
        Guid topicId, Guid proposalId, Guid userId, string url, string description)
    {
        using var scope = Scope();
        return await scope.ServiceProvider.GetRequiredService<ReferenceService>()
            .AttachAsync(topicId, proposalId, userId, url, description);
    }

    private async Task<VoteResult> RateAsync(Guid referenceId, ReferenceAspect aspect, Guid userId, short value)
    {
        using var scope = Scope();
        return await scope.ServiceProvider.GetRequiredService<ReferenceVotingService>()
            .CastAsync(new ReferenceVoteTarget(referenceId, aspect), userId, value);
    }

    [Fact]
    public async Task Citing_the_same_source_twice_in_a_topic_reuses_the_first_entry()
    {
        // This is what keeps a source's accumulated judgement attached to the source rather
        // than scattered across near-identical copies of its address.
        var owner = await UserAsync("owner");
        var other = await UserAsync("other");
        var topicId = await ProposingTopicAsync(owner);
        var first = await ProposalAsync(topicId, owner, "A toll fee is suggested.");
        var second = await ProposalAsync(topicId, other, "Cycle lanes should be widened.");

        var a = await CiteAsync(topicId, first, owner, "https://example.com/study", "The 2024 study");
        var b = await CiteAsync(topicId, second, other, "HTTPS://Example.com/study/?utm_source=x", "Same study");

        Assert.True(a.Succeeded, a.Error);
        Assert.True(b.Succeeded, b.Error);
        Assert.Equal(a.Id, b.Id);

        await using var context = database.CreateContext();
        Assert.Equal(1, await context.References.CountAsync(r => r.TopicId == topicId));
        Assert.Equal(2, await context.ProposalReferences.CountAsync(link => link.ReferenceId == a.Id));
    }

    [Fact]
    public async Task The_same_source_can_be_cited_in_a_different_topic()
    {
        // Uniqueness is per topic: each discussion keeps its own description and its own rating.
        var owner = await UserAsync("owner");
        var firstTopic = await ProposingTopicAsync(owner);
        var secondTopic = await ProposingTopicAsync(owner);
        var p1 = await ProposalAsync(firstTopic, owner, "A toll fee is suggested.");
        var p2 = await ProposalAsync(secondTopic, owner, "A toll fee is suggested.");

        var a = await CiteAsync(firstTopic, p1, owner, "https://example.com/study", "The study");
        var b = await CiteAsync(secondTopic, p2, owner, "https://example.com/study", "The study");

        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public async Task A_source_is_rated_on_two_independent_axes()
    {
        var owner = await UserAsync("owner");
        var critic = await UserAsync("critic");
        var topicId = await ProposingTopicAsync(owner);
        var proposal = await ProposalAsync(topicId, owner, "A toll fee is suggested.");
        var reference = await CiteAsync(topicId, proposal, owner, "https://example.com/study", "The study");

        // Accurate, but beside the point — a judgement the platform can express only because
        // the two questions are kept apart.
        await RateAsync(reference.Id, ReferenceAspect.Accuracy, critic, 1);
        await RateAsync(reference.Id, ReferenceAspect.Importance, critic, -1);

        await using var context = database.CreateContext();
        var stored = await context.References.AsNoTracking().SingleAsync(r => r.Id == reference.Id);

        Assert.Equal(1, stored.AccuracyScore);
        Assert.Equal(1, stored.AccuracyVotes);
        Assert.Equal(-1, stored.ImportanceScore);
        Assert.Equal(1, stored.ImportanceVotes);
    }

    [Fact]
    public async Task One_person_holds_one_vote_per_axis_not_one_per_source()
    {
        var owner = await UserAsync("owner");
        var critic = await UserAsync("critic");
        var topicId = await ProposingTopicAsync(owner);
        var proposal = await ProposalAsync(topicId, owner, "A toll fee is suggested.");
        var reference = await CiteAsync(topicId, proposal, owner, "https://example.com/study", "The study");

        await RateAsync(reference.Id, ReferenceAspect.Accuracy, critic, 1);
        var repeat = await RateAsync(reference.Id, ReferenceAspect.Accuracy, critic, 1);
        var changed = await RateAsync(reference.Id, ReferenceAspect.Accuracy, critic, -1);

        Assert.Equal(VoteOutcome.Unchanged, repeat.Outcome);
        Assert.Equal(VoteOutcome.Changed, changed.Outcome);

        await using var context = database.CreateContext();
        Assert.Equal(1, await context.Votes.CountAsync(
            v => v.ReferenceId == reference.Id && v.ReferenceAspect == ReferenceAspect.Accuracy));
    }

    [Fact]
    public async Task Rating_a_source_credits_whoever_cited_it()
    {
        // The standing that decides whose alternative solutions are listed first.
        var citer = await UserAsync("citer");
        var raterA = await UserAsync("raterA");
        var raterB = await UserAsync("raterB");
        var topicId = await ProposingTopicAsync(citer);
        var proposal = await ProposalAsync(topicId, citer, "A toll fee is suggested.");
        var reference = await CiteAsync(topicId, proposal, citer, "https://example.com/study", "The study");

        await RateAsync(reference.Id, ReferenceAspect.Accuracy, raterA, 1);
        await RateAsync(reference.Id, ReferenceAspect.Importance, raterA, 1);
        await RateAsync(reference.Id, ReferenceAspect.Accuracy, raterB, 1);

        await using var context = database.CreateContext();
        var reputation = await context.TopicUserReputations.AsNoTracking()
            .SingleAsync(x => x.TopicId == topicId && x.UserId == citer);

        // Both axes count: the bonus is for citing sources that are accurate *and* relevant.
        Assert.Equal(3, reputation.ReferenceScore);
    }

    [Fact]
    public async Task Withdrawing_a_rating_takes_the_credit_back()
    {
        var citer = await UserAsync("citer");
        var rater = await UserAsync("rater");
        var topicId = await ProposingTopicAsync(citer);
        var proposal = await ProposalAsync(topicId, citer, "A toll fee is suggested.");
        var reference = await CiteAsync(topicId, proposal, citer, "https://example.com/study", "The study");

        await RateAsync(reference.Id, ReferenceAspect.Accuracy, rater, 1);

        using (var scope = Scope())
        {
            await scope.ServiceProvider.GetRequiredService<ReferenceVotingService>()
                .WithdrawAsync(new ReferenceVoteTarget(reference.Id, ReferenceAspect.Accuracy), rater);
        }

        await using var context = database.CreateContext();
        var reputation = await context.TopicUserReputations.AsNoTracking()
            .SingleAsync(x => x.TopicId == topicId && x.UserId == citer);

        Assert.Equal(0, reputation.ReferenceScore);
        Assert.Equal(0, (await context.References.AsNoTracking().SingleAsync(r => r.Id == reference.Id)).AccuracyScore);
    }

    [Fact]
    public async Task The_best_regarded_citers_can_be_ranked()
    {
        var strong = await UserAsync("strong");
        var weak = await UserAsync("weak");
        var rater = await UserAsync("rater");
        var topicId = await ProposingTopicAsync(strong);
        var proposal = await ProposalAsync(topicId, strong, "A toll fee is suggested.");

        var good = await CiteAsync(topicId, proposal, strong, "https://example.com/good", "A good source");
        var poor = await CiteAsync(topicId, proposal, weak, "https://example.com/poor", "A poor source");

        await RateAsync(good.Id, ReferenceAspect.Accuracy, rater, 1);
        await RateAsync(good.Id, ReferenceAspect.Importance, rater, 1);
        await RateAsync(poor.Id, ReferenceAspect.Accuracy, rater, -1);

        using var scope = Scope();
        var top = await scope.ServiceProvider.GetRequiredService<ReferenceService>().TopCitersAsync(topicId);

        Assert.Equal(strong, top[0].UserId);
        // Negative standing does not qualify anyone for the bonus.
        Assert.DoesNotContain(top, x => x.UserId == weak);
    }

    [Fact]
    public async Task An_unusable_address_is_refused_with_a_reason()
    {
        var owner = await UserAsync("owner");
        var topicId = await ProposingTopicAsync(owner);
        var proposal = await ProposalAsync(topicId, owner, "A toll fee is suggested.");

        var result = await CiteAsync(topicId, proposal, owner, "javascript:alert(1)", "Not a source");

        Assert.False(result.Succeeded);
        Assert.Contains("http", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_source_needs_a_description()
    {
        var owner = await UserAsync("owner");
        var topicId = await ProposingTopicAsync(owner);
        var proposal = await ProposalAsync(topicId, owner, "A toll fee is suggested.");

        var result = await CiteAsync(topicId, proposal, owner, "https://example.com/x", "  ");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task A_proposal_cannot_be_cited_through_another_topic()
    {
        var owner = await UserAsync("owner");
        var intruder = await UserAsync("intruder");
        var victimTopic = await ProposingTopicAsync(owner);
        var ownTopic = await ProposingTopicAsync(intruder);
        var victimProposal = await ProposalAsync(victimTopic, owner, "Belongs elsewhere");

        var result = await CiteAsync(ownTopic, victimProposal, intruder, "https://example.com/x", "A source");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task A_vote_carries_an_aspect_only_when_it_is_about_a_reference()
    {
        // The check constraint that keeps the shared Votes table coherent: a topic vote with an
        // aspect, or a reference vote without one, would both be meaningless.
        var owner = await UserAsync("owner");
        var topicId = await ProposingTopicAsync(owner);

        await using var context = database.CreateContext();

        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO Votes (Id, UserId, Value, TopicId, ReferenceAspect, CastAtUtc) " +
                "VALUES (UUID(), {0}, 1, {1}, 0, UTC_TIMESTAMP())",
                owner, topicId));
    }
}
