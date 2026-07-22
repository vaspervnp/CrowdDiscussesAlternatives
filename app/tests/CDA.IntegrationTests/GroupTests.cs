using CDA.Application.Topics;
using CDA.Domain.References;
using CDA.Domain.Topics;
using CDA.Infrastructure.Groups;
using CDA.Infrastructure.Identity;
using CDA.Infrastructure.Proposals;
using CDA.Infrastructure.References;
using CDA.Infrastructure.Topics;
using CDA.Infrastructure.Voting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CDA.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class GroupTests(DatabaseFixture database) : IAsyncLifetime
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

    private async Task<GroupResult> AssembleAsync(
        Guid topicId, Guid userId, string description, Guid[] proposals, Guid? improves = null)
    {
        using var scope = Scope();
        return await scope.ServiceProvider.GetRequiredService<GroupService>()
            .CreateAsync(topicId, userId, description, proposals, improves);
    }

    private async Task<GroupPage> ListAsync(Guid topicId, Guid viewerId, GroupSort sort = GroupSort.Score)
    {
        using var scope = Scope();
        return await scope.ServiceProvider.GetRequiredService<GroupService>()
            .ListAsync(topicId, new TopicViewer(viewerId, false, TopicRole.Member), sort);
    }

    private async Task VoteAsync(Guid groupId, Guid userId, short value)
    {
        using var scope = Scope();
        await scope.ServiceProvider.GetRequiredService<GroupVotingService>().CastAsync(groupId, userId, value);
    }

    [Fact]
    public async Task An_alternative_is_a_set_of_proposals_with_a_stated_rationale()
    {
        var owner = await UserAsync("owner");
        var topicId = await ProposingTopicAsync(owner);
        var a = await ProposalAsync(topicId, owner, "A toll fee is suggested.");
        var b = await ProposalAsync(topicId, owner, "Bus frequency should double.");

        var result = await AssembleAsync(topicId, owner, "Charge for entry and fund buses with it", [a, b]);

        Assert.True(result.Succeeded, result.Error);

        using var scope = Scope();
        var group = await scope.ServiceProvider.GetRequiredService<GroupService>()
            .GetAsync(topicId, result.Id, new TopicViewer(owner, false, TopicRole.Member));

        Assert.NotNull(group);
        Assert.Equal(2, group.ProposalCount);
        Assert.Equal(2, group.Members.Count);
    }

    [Fact]
    public async Task An_alternative_needs_a_description()
    {
        // A bare list leaves everyone guessing at the reasoning that picked these proposals,
        // which is most of what tells one alternative from another.
        var owner = await UserAsync("owner");
        var topicId = await ProposingTopicAsync(owner);
        var a = await ProposalAsync(topicId, owner, "A toll fee is suggested.");
        var b = await ProposalAsync(topicId, owner, "Bus frequency should double.");

        var result = await AssembleAsync(topicId, owner, "   ", [a, b]);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task An_alternative_needs_at_least_two_proposals()
    {
        var owner = await UserAsync("owner");
        var topicId = await ProposingTopicAsync(owner);
        var a = await ProposalAsync(topicId, owner, "A toll fee is suggested.");

        var result = await AssembleAsync(topicId, owner, "Just the one", [a]);

        Assert.False(result.Succeeded);
        Assert.Contains("at least two", result.Error!);
    }

    [Fact]
    public async Task Duplicate_selections_are_folded_rather_than_counted_twice()
    {
        var owner = await UserAsync("owner");
        var topicId = await ProposingTopicAsync(owner);
        var a = await ProposalAsync(topicId, owner, "A toll fee is suggested.");
        var b = await ProposalAsync(topicId, owner, "Bus frequency should double.");

        var result = await AssembleAsync(topicId, owner, "Charge and fund", [a, b, a]);

        Assert.True(result.Succeeded, result.Error);

        await using var context = database.CreateContext();
        Assert.Equal(2, await context.GroupItems.CountAsync(item => item.GroupId == result.Id));
    }

    [Fact]
    public async Task Proposals_from_another_topic_cannot_be_assembled_in()
    {
        var owner = await UserAsync("owner");
        var here = await ProposingTopicAsync(owner);
        var elsewhere = await ProposingTopicAsync(owner);
        var mine = await ProposalAsync(here, owner, "A toll fee is suggested.");
        var theirs = await ProposalAsync(elsewhere, owner, "Something else entirely");

        var result = await AssembleAsync(here, owner, "Mixing topics", [mine, theirs]);

        Assert.False(result.Succeeded);
        Assert.Contains("belong to this topic", result.Error!);
    }

    [Fact]
    public async Task Only_the_assembler_can_change_an_alternative()
    {
        var owner = await UserAsync("owner");
        var other = await UserAsync("other");
        var topicId = await ProposingTopicAsync(owner);
        var a = await ProposalAsync(topicId, owner, "A toll fee is suggested.");
        var b = await ProposalAsync(topicId, owner, "Bus frequency should double.");
        var group = await AssembleAsync(topicId, owner, "Original rationale", [a, b]);

        using var scope = Scope();
        var result = await scope.ServiceProvider.GetRequiredService<GroupService>()
            .EditAsync(topicId, group.Id, other, "Hijacked", null);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task An_alternative_cannot_be_edited_through_another_topic()
    {
        var owner = await UserAsync("owner");
        var intruder = await UserAsync("intruder");
        var victimTopic = await ProposingTopicAsync(owner);
        var ownTopic = await ProposingTopicAsync(intruder);
        var a = await ProposalAsync(victimTopic, owner, "A toll fee is suggested.");
        var b = await ProposalAsync(victimTopic, owner, "Bus frequency should double.");
        var group = await AssembleAsync(victimTopic, owner, "Original rationale", [a, b]);

        using var scope = Scope();
        var result = await scope.ServiceProvider.GetRequiredService<GroupService>()
            .EditAsync(ownTopic, group.Id, intruder, "Tampered", null);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Alternatives_from_the_best_regarded_citers_are_listed_first()
    {
        // The advantage the platform's design gives to people who find good evidence: the
        // quality of a discussion rests on the quality of what it argues from.
        var owner = await UserAsync("owner");
        var citer = await UserAsync("citer");
        var rater = await UserAsync("rater");
        var topicId = await ProposingTopicAsync(owner);
        var a = await ProposalAsync(topicId, owner, "A toll fee is suggested.");
        var b = await ProposalAsync(topicId, owner, "Bus frequency should double.");

        // The well-sourced participant's alternative has *less* support, so ordering by score
        // alone would put it second.
        var wellSourced = await AssembleAsync(topicId, citer, "Carefully evidenced approach", [a, b]);
        var popular = await AssembleAsync(topicId, owner, "The popular approach", [a, b]);
        await VoteAsync(popular.Id, owner, 1);
        await VoteAsync(popular.Id, rater, 1);

        using (var scope = Scope())
        {
            var references = scope.ServiceProvider.GetRequiredService<ReferenceService>();
            var cited = await references.AttachAsync(topicId, a, citer, "https://example.com/study", "A study");

            var voting = scope.ServiceProvider.GetRequiredService<ReferenceVotingService>();
            await voting.CastAsync(new ReferenceVoteTarget(cited.Id, ReferenceAspect.Accuracy), rater, 1);
            await voting.CastAsync(new ReferenceVoteTarget(cited.Id, ReferenceAspect.Importance), rater, 1);
        }

        var page = await ListAsync(topicId, owner);

        Assert.Equal(wellSourced.Id, page.Items[0].Id);
        Assert.True(page.Items[0].ByTrustedCiter);
        Assert.False(page.Items[1].ByTrustedCiter);
        Assert.Contains("citer", page.TopCiters);
    }

    [Fact]
    public async Task Without_any_rated_sources_ordering_falls_back_to_support()
    {
        var owner = await UserAsync("owner");
        var voter = await UserAsync("voter");
        var topicId = await ProposingTopicAsync(owner);
        var a = await ProposalAsync(topicId, owner, "A toll fee is suggested.");
        var b = await ProposalAsync(topicId, owner, "Bus frequency should double.");

        var weak = await AssembleAsync(topicId, owner, "Weakly supported", [a, b]);
        var strong = await AssembleAsync(topicId, owner, "Strongly supported", [a, b]);
        await VoteAsync(strong.Id, voter, 1);

        var page = await ListAsync(topicId, owner);

        Assert.Equal(strong.Id, page.Items[0].Id);
        Assert.Equal(weak.Id, page.Items[1].Id);
        Assert.Empty(page.TopCiters);
    }

    [Fact]
    public async Task A_variant_records_what_it_refines()
    {
        var owner = await UserAsync("owner");
        var topicId = await ProposingTopicAsync(owner);
        var a = await ProposalAsync(topicId, owner, "A toll fee is suggested.");
        var b = await ProposalAsync(topicId, owner, "Bus frequency should double.");
        var c = await ProposalAsync(topicId, owner, "Residents are exempt from the toll.");

        var original = await AssembleAsync(topicId, owner, "Charge everyone", [a, b]);
        var variant = await AssembleAsync(topicId, owner, "Charge, but exempt residents", [a, b, c], original.Id);

        Assert.True(variant.Succeeded, variant.Error);

        using var scope = Scope();
        var view = await scope.ServiceProvider.GetRequiredService<GroupService>()
            .GetAsync(topicId, variant.Id, new TopicViewer(owner, false, TopicRole.Member));

        Assert.Equal(original.Id, view!.ImprovesGroupId);
        Assert.Equal("Charge everyone", view.ImprovesDescription);
    }

    [Fact]
    public async Task A_variant_cannot_point_at_an_alternative_in_another_topic()
    {
        var owner = await UserAsync("owner");
        var here = await ProposingTopicAsync(owner);
        var elsewhere = await ProposingTopicAsync(owner);
        var a = await ProposalAsync(here, owner, "A toll fee is suggested.");
        var b = await ProposalAsync(here, owner, "Bus frequency should double.");
        var x = await ProposalAsync(elsewhere, owner, "Something");
        var y = await ProposalAsync(elsewhere, owner, "Something else");

        var foreignGroup = await AssembleAsync(elsewhere, owner, "In another topic", [x, y]);

        var result = await AssembleAsync(here, owner, "Variant of a foreign group", [a, b], foreignGroup.Id);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Editing_after_people_have_voted_is_allowed_but_recorded()
    {
        // The interface warns and offers a variant instead; the domain records that it happened.
        var owner = await UserAsync("owner");
        var voter = await UserAsync("voter");
        var topicId = await ProposingTopicAsync(owner);
        var a = await ProposalAsync(topicId, owner, "A toll fee is suggested.");
        var b = await ProposalAsync(topicId, owner, "Bus frequency should double.");
        var group = await AssembleAsync(topicId, owner, "Original rationale", [a, b]);

        await VoteAsync(group.Id, voter, 1);

        using (var scope = Scope())
        {
            await scope.ServiceProvider.GetRequiredService<GroupService>()
                .EditAsync(topicId, group.Id, owner, "Reworded rationale", null);
        }

        await using var context = database.CreateContext();
        var stored = await context.ProposalGroups.AsNoTracking().SingleAsync(g => g.Id == group.Id);

        Assert.NotNull(stored.EditedAtUtc);
        Assert.True(stored.HasBeenJudged);
    }

    [Fact]
    public async Task Alternatives_cannot_be_assembled_before_the_topic_opens_for_proposals()
    {
        var owner = await UserAsync("owner");

        using var scope = Scope();
        var topic = await scope.ServiceProvider.GetRequiredService<TopicService>()
            .CreateAsync("Still discussing", "", owner, TopicVisibility.Public, null);

        var result = await AssembleAsync(topic.Id, owner, "Too early", [Guid.NewGuid(), Guid.NewGuid()]);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Group_tallies_survive_concurrent_voters()
    {
        var owner = await UserAsync("owner");
        var topicId = await ProposingTopicAsync(owner);
        var a = await ProposalAsync(topicId, owner, "A toll fee is suggested.");
        var b = await ProposalAsync(topicId, owner, "Bus frequency should double.");
        var group = await AssembleAsync(topicId, owner, "Charge and fund", [a, b]);

        var voters = new List<Guid> { owner };
        for (var i = 0; i < 5; i++)
        {
            voters.Add(await UserAsync($"voter{i}"));
        }

        await Task.WhenAll(voters.Select(voter => VoteAsync(group.Id, voter, 1)));

        await using var context = database.CreateContext();
        var stored = await context.ProposalGroups.AsNoTracking().SingleAsync(g => g.Id == group.Id);

        Assert.Equal(6, stored.ScoreSum);
        Assert.Equal(6, stored.VoteCount);
        Assert.Equal(6, await context.Votes.CountAsync(v => v.GroupId == group.Id));
    }
}
