using CDA.Application.Topics;
using CDA.Domain.Proposals;
using CDA.Domain.Topics;
using CDA.Infrastructure.Discussion;
using CDA.Infrastructure.Identity;
using CDA.Infrastructure.Proposals;
using CDA.Infrastructure.Topics;
using CDA.Infrastructure.Voting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CDA.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class ProposalTests(DatabaseFixture database) : IAsyncLifetime
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

    /// <summary>A topic already past its discussion phase, so proposals are open.</summary>
    private async Task<Guid> ProposingTopicAsync(Guid ownerId, TopicVisibility visibility = TopicVisibility.Public)
    {
        Guid topicId;

        using (var scope = Scope())
        {
            var topics = scope.ServiceProvider.GetRequiredService<TopicService>();
            var topic = await topics.CreateAsync("How should we reduce traffic?", "", ownerId, visibility, null);
            topicId = topic.Id;

            var requirements = scope.ServiceProvider.GetRequiredService<RequirementService>();
            await requirements.AddAsync(topicId, "Must not increase journey times");
        }

        await using var context = database.CreateContext();
        var stored = await context.Topics.SingleAsync(t => t.Id == topicId);
        stored.OpenForProposals(requirementCount: 1);
        await context.SaveChangesAsync();

        return topicId;
    }

    private async Task<ProposalResult> AddAsync(Guid topicId, Guid authorId, string text, int? days = null)
    {
        using var scope = Scope();
        var proposals = scope.ServiceProvider.GetRequiredService<ProposalService>();
        return await proposals.CreateAsync(
            topicId, authorId, text, days is { } d ? DateTime.UtcNow.AddDays(d) : null);
    }

    private async Task<VoteResult> VoteAsync(Guid proposalId, Guid userId, short value)
    {
        using var scope = Scope();
        return await scope.ServiceProvider.GetRequiredService<ProposalVotingService>()
            .CastAsync(proposalId, userId, value);
    }

    private async Task LockAsync(Guid proposalId)
    {
        await using var context = database.CreateContext();
        var proposal = await context.Proposals.SingleAsync(p => p.Id == proposalId);
        proposal.LockNow(DateTime.UtcNow);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Proposals_cannot_be_added_before_the_requirements_are_published()
    {
        // They are written against the requirement list, so they cannot precede it.
        var owner = await UserAsync("owner");

        using var scope = Scope();
        var topics = scope.ServiceProvider.GetRequiredService<TopicService>();
        var topic = await topics.CreateAsync("Still discussing", "", owner, TopicVisibility.Public, null);

        var result = await AddAsync(topic.Id, owner, "Too early");

        Assert.False(result.Succeeded);
        Assert.Contains("requirements", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_proposal_longer_than_a_sentence_is_refused()
    {
        var owner = await UserAsync("owner");
        var topicId = await ProposingTopicAsync(owner);

        var result = await AddAsync(topicId, owner, new string('x', Proposal.TextMaxLength + 1));

        Assert.False(result.Succeeded);
        Assert.Contains("one sentence", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_proposal_cannot_be_voted_on_while_it_is_still_editable()
    {
        // The rule the platform's documents are explicit about: comment yes, vote no.
        var owner = await UserAsync("owner");
        var voter = await UserAsync("voter");
        var topicId = await ProposingTopicAsync(owner);
        var proposal = await AddAsync(topicId, owner, "A toll fee is suggested.");

        var result = await VoteAsync(proposal.Id, voter, 1);

        Assert.Equal(VoteOutcome.NotOpenYet, result.Outcome);

        await using var context = database.CreateContext();
        Assert.Equal(0, await context.Votes.CountAsync(v => v.ProposalId == proposal.Id));
    }

    [Fact]
    public async Task Commenting_is_allowed_while_a_proposal_is_still_editable()
    {
        var owner = await UserAsync("owner");
        var reviewer = await UserAsync("reviewer");
        var topicId = await ProposingTopicAsync(owner);
        var proposal = await AddAsync(topicId, owner, "A toll fee is suggested.");

        using var scope = Scope();
        var comments = scope.ServiceProvider.GetRequiredService<CommentService>();
        var result = await comments.PostToProposalAsync(topicId, proposal.Id, reviewer, "How much?");

        Assert.True(result.Succeeded, result.Error);
    }

    [Fact]
    public async Task Locking_opens_voting()
    {
        var owner = await UserAsync("owner");
        var voter = await UserAsync("voter");
        var topicId = await ProposingTopicAsync(owner);
        var proposal = await AddAsync(topicId, owner, "A toll fee is suggested.");

        await LockAsync(proposal.Id);

        var result = await VoteAsync(proposal.Id, voter, 1);

        Assert.Equal(VoteOutcome.Recorded, result.Outcome);
        Assert.Equal(1, result.ScoreSum);
    }

    [Fact]
    public async Task Only_the_author_can_edit_a_proposal()
    {
        var owner = await UserAsync("owner");
        var other = await UserAsync("other");
        var topicId = await ProposingTopicAsync(owner);
        var proposal = await AddAsync(topicId, owner, "Original wording");

        using var scope = Scope();
        var proposals = scope.ServiceProvider.GetRequiredService<ProposalService>();
        var result = await proposals.EditAsync(topicId, proposal.Id, other, "Hijacked");

        Assert.False(result.Succeeded);

        await using var context = database.CreateContext();
        Assert.Equal("Original wording", (await context.Proposals.SingleAsync(p => p.Id == proposal.Id)).Text);
    }

    [Fact]
    public async Task A_locked_proposal_cannot_be_reworded()
    {
        // Otherwise the wording people voted on could change after the fact.
        var owner = await UserAsync("owner");
        var topicId = await ProposingTopicAsync(owner);
        var proposal = await AddAsync(topicId, owner, "Original wording");
        await LockAsync(proposal.Id);

        using var scope = Scope();
        var proposals = scope.ServiceProvider.GetRequiredService<ProposalService>();
        var result = await proposals.EditAsync(topicId, proposal.Id, owner, "Changed after the vote");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task A_proposal_cannot_be_reached_through_another_topic()
    {
        var owner = await UserAsync("owner");
        var intruder = await UserAsync("intruder");
        var victimTopic = await ProposingTopicAsync(owner);
        var ownTopic = await ProposingTopicAsync(intruder);

        var victimProposal = await AddAsync(victimTopic, owner, "Belongs to another topic");

        using var scope = Scope();
        var proposals = scope.ServiceProvider.GetRequiredService<ProposalService>();

        var edited = await proposals.EditAsync(ownTopic, victimProposal.Id, intruder, "Tampered");
        var locked = await proposals.LockAsync(ownTopic, victimProposal.Id, intruder);

        Assert.False(edited.Succeeded);
        Assert.False(locked.Succeeded);

        await using var context = database.CreateContext();
        var survivor = await context.Proposals.SingleAsync(p => p.Id == victimProposal.Id);
        Assert.Equal("Belongs to another topic", survivor.Text);
        Assert.False(survivor.ManuallyLocked);
    }

    [Fact]
    public async Task Commenting_on_a_proposal_updates_the_recently_discussed_ordering()
    {
        var owner = await UserAsync("owner");
        var topicId = await ProposingTopicAsync(owner);
        var quiet = await AddAsync(topicId, owner, "Nobody discusses this one");
        var busy = await AddAsync(topicId, owner, "This one gets talked about");

        using var scope = Scope();
        var comments = scope.ServiceProvider.GetRequiredService<CommentService>();
        await comments.PostToProposalAsync(topicId, busy.Id, owner, "Worth arguing about");

        var proposals = scope.ServiceProvider.GetRequiredService<ProposalService>();
        var page = await proposals.ListAsync(
            topicId, new TopicViewer(owner, false, TopicRole.Facilitator), ProposalSort.LastCommented);

        // Never-commented proposals sort last: the ordering exists to surface live argument.
        Assert.Equal(busy.Id, page.Items[0].Id);
        Assert.Equal(quiet.Id, page.Items[1].Id);
        Assert.Equal(1, page.Items[0].CommentCount);
    }

    [Fact]
    public async Task Proposals_can_be_ordered_by_support_and_filtered_by_author()
    {
        var owner = await UserAsync("owner");
        var other = await UserAsync("other");
        var topicId = await ProposingTopicAsync(owner);

        var weak = await AddAsync(topicId, owner, "Weakly supported");
        var strong = await AddAsync(topicId, other, "Strongly supported");
        await LockAsync(weak.Id);
        await LockAsync(strong.Id);

        await VoteAsync(strong.Id, owner, 1);
        await VoteAsync(strong.Id, other, 1);
        await VoteAsync(weak.Id, owner, -1);

        using var scope = Scope();
        var proposals = scope.ServiceProvider.GetRequiredService<ProposalService>();
        var viewer = new TopicViewer(owner, false, TopicRole.Facilitator);

        var byScore = await proposals.ListAsync(topicId, viewer, ProposalSort.Score);
        Assert.Equal([strong.Id, weak.Id], byScore.Items.Select(p => p.Id));

        var mine = await proposals.ListAsync(topicId, viewer, ProposalSort.Score, authorId: other);
        Assert.Equal([strong.Id], mine.Items.Select(p => p.Id));
    }

    [Fact]
    public async Task Proposal_tallies_survive_concurrent_voters()
    {
        // The shared voting algorithm, exercised through its second target type.
        var owner = await UserAsync("owner");
        var topicId = await ProposingTopicAsync(owner);
        var proposal = await AddAsync(topicId, owner, "A toll fee is suggested.");
        await LockAsync(proposal.Id);

        var voters = new List<Guid> { owner };
        for (var i = 0; i < 5; i++)
        {
            voters.Add(await UserAsync($"voter{i}"));
        }

        await Task.WhenAll(voters.Select(voter => VoteAsync(proposal.Id, voter, 1)));

        await using var context = database.CreateContext();
        var stored = await context.Proposals.AsNoTracking().SingleAsync(p => p.Id == proposal.Id);
        var rows = await context.Votes.CountAsync(v => v.ProposalId == proposal.Id);

        Assert.Equal(6, stored.ScoreSum);
        Assert.Equal(6, stored.VoteCount);
        Assert.Equal(6, rows);
    }

    [Fact]
    public async Task A_vote_must_attach_to_exactly_one_kind_of_target()
    {
        // The check constraint that keeps the shared Votes table honest as targets are added.
        var owner = await UserAsync("owner");
        var topicId = await ProposingTopicAsync(owner);
        var proposal = await AddAsync(topicId, owner, "A toll fee is suggested.");

        await using var context = database.CreateContext();

        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO Votes (Id, UserId, Value, TopicId, ProposalId, CastAtUtc) " +
                "VALUES (UUID(), {0}, 1, {1}, {2}, UTC_TIMESTAMP())",
                owner, topicId, proposal.Id));
    }
}
