using CDA.Domain.Topics;
using CDA.Infrastructure.Discussion;
using CDA.Infrastructure.Groups;
using CDA.Infrastructure.Identity;
using CDA.Infrastructure.Proposals;
using CDA.Infrastructure.Search;
using CDA.Infrastructure.Topics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CDA.IntegrationTests;

/// <summary>
/// Search against the real full-text index. The parser is unit-tested on its own; this checks
/// that what it produces actually finds things in MariaDB.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class SearchTests(DatabaseFixture database) : IAsyncLifetime
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

    private async Task CommentOnProposalAsync(Guid topicId, Guid proposalId, Guid userId, string body)
    {
        using var scope = Scope();
        var result = await scope.ServiceProvider.GetRequiredService<CommentService>()
            .PostToProposalAsync(topicId, proposalId, userId, body);
        Assert.True(result.Succeeded, result.Error);
    }

    private async Task CommentOnTopicAsync(Guid topicId, Guid userId, string body)
    {
        using var scope = Scope();
        var result = await scope.ServiceProvider.GetRequiredService<CommentService>()
            .PostToTopicAsync(topicId, userId, body);
        Assert.True(result.Succeeded, result.Error);
    }

    private async Task<SearchResults> SearchAsync(
        Guid topicId, string query, Guid? author = null,
        SearchResultMode mode = SearchResultMode.Comments)
    {
        using var scope = Scope();
        return await scope.ServiceProvider.GetRequiredService<CommentSearchService>()
            .SearchAsync(topicId, query, author, mode);
    }

    [Fact]
    public async Task And_requires_every_word_to_appear()
    {
        var owner = await UserAsync("owner");
        var topicId = await ProposingTopicAsync(owner);
        var proposal = await ProposalAsync(topicId, owner, "A toll fee is suggested.");

        await CommentOnProposalAsync(topicId, proposal, owner, "Congestion charging worked in other cities");
        await CommentOnProposalAsync(topicId, proposal, owner, "Congestion is worse at rush hour");

        var both = await SearchAsync(topicId, "congestion AND charging");
        var either = await SearchAsync(topicId, "congestion");

        Assert.Single(both.Comments);
        Assert.Equal(2, either.Comments.Count);
    }

    [Fact]
    public async Task Or_matches_either_word()
    {
        var owner = await UserAsync("owner");
        var topicId = await ProposingTopicAsync(owner);
        var proposal = await ProposalAsync(topicId, owner, "A toll fee is suggested.");

        await CommentOnProposalAsync(topicId, proposal, owner, "The buses are the answer");
        await CommentOnProposalAsync(topicId, proposal, owner, "Trams would work better");
        await CommentOnProposalAsync(topicId, proposal, owner, "Neither of those helps");

        var results = await SearchAsync(topicId, "buses OR trams");

        Assert.Equal(2, results.Comments.Count);
    }

    [Fact]
    public async Task Brackets_group_alternatives_inside_a_requirement()
    {
        var owner = await UserAsync("owner");
        var topicId = await ProposingTopicAsync(owner);
        var proposal = await ProposalAsync(topicId, owner, "A toll fee is suggested.");

        await CommentOnProposalAsync(topicId, proposal, owner, "The toll should fund buses");
        await CommentOnProposalAsync(topicId, proposal, owner, "The toll should fund trams");
        await CommentOnProposalAsync(topicId, proposal, owner, "The toll should fund nothing");
        await CommentOnProposalAsync(topicId, proposal, owner, "Buses are already funded");

        var results = await SearchAsync(topicId, "toll AND (buses OR trams)");

        Assert.Equal(2, results.Comments.Count);
    }

    [Fact]
    public async Task A_minus_excludes_matches()
    {
        var owner = await UserAsync("owner");
        var topicId = await ProposingTopicAsync(owner);
        var proposal = await ProposalAsync(topicId, owner, "A toll fee is suggested.");

        await CommentOnProposalAsync(topicId, proposal, owner, "Charging worked well in London");
        await CommentOnProposalAsync(topicId, proposal, owner, "Charging worked well in Stockholm");

        var results = await SearchAsync(topicId, "charging -london");

        Assert.Single(results.Comments);
        Assert.Contains("Stockholm", results.Comments[0].Body);
    }

    [Fact]
    public async Task A_quoted_phrase_matches_the_words_in_order()
    {
        var owner = await UserAsync("owner");
        var topicId = await ProposingTopicAsync(owner);
        var proposal = await ProposalAsync(topicId, owner, "A toll fee is suggested.");

        await CommentOnProposalAsync(topicId, proposal, owner, "The congestion charge is the model here");
        await CommentOnProposalAsync(topicId, proposal, owner, "Charge people for congestion at peak times");

        var results = await SearchAsync(topicId, "\"congestion charge\"");

        Assert.Single(results.Comments);
    }

    [Fact]
    public async Task Results_can_be_returned_as_the_proposals_that_were_tagged()
    {
        // The workflow the platform's documents describe: mark proposals with a word in a
        // comment, then pull them back out by that word.
        var owner = await UserAsync("owner");
        var topicId = await ProposingTopicAsync(owner);
        var first = await ProposalAsync(topicId, owner, "A toll fee is suggested.");
        var second = await ProposalAsync(topicId, owner, "Bus frequency should double.");
        var third = await ProposalAsync(topicId, owner, "Pedestrianise the centre.");

        await CommentOnProposalAsync(topicId, first, owner, "cons: unpopular with commuters");
        await CommentOnProposalAsync(topicId, first, owner, "cons: expensive to enforce");
        await CommentOnProposalAsync(topicId, second, owner, "pros: cheap and quick");
        await CommentOnProposalAsync(topicId, third, owner, "no opinion yet");

        var tagged = await SearchAsync(topicId, "pros OR cons", mode: SearchResultMode.Proposals);

        Assert.Equal(2, tagged.Proposals.Count);
        // Ordered by how many comments matched, so the most heavily tagged is first.
        Assert.Equal(first, tagged.Proposals[0].ProposalId);
        Assert.Equal(2, tagged.Proposals[0].MatchingComments);
        Assert.NotEmpty(tagged.Proposals[0].Excerpts);
    }

    [Fact]
    public async Task A_search_can_be_limited_to_one_persons_comments()
    {
        // Which is what makes marker words usable as private labels.
        var owner = await UserAsync("owner");
        var other = await UserAsync("other");
        var topicId = await ProposingTopicAsync(owner);
        var proposal = await ProposalAsync(topicId, owner, "A toll fee is suggested.");

        await CommentOnProposalAsync(topicId, proposal, owner, "cons: my own reservation");
        await CommentOnProposalAsync(topicId, proposal, other, "cons: someone else's reservation");

        var mine = await SearchAsync(topicId, "cons", author: owner);
        var everyone = await SearchAsync(topicId, "cons");

        Assert.Single(mine.Comments);
        Assert.Contains("my own", mine.Comments[0].Body);
        Assert.Equal(2, everyone.Comments.Count);
    }

    [Fact]
    public async Task Search_covers_comments_wherever_they_are_attached()
    {
        var owner = await UserAsync("owner");
        var topicId = await ProposingTopicAsync(owner);
        var a = await ProposalAsync(topicId, owner, "A toll fee is suggested.");
        var b = await ProposalAsync(topicId, owner, "Bus frequency should double.");

        await CommentOnTopicAsync(topicId, owner, "Overall this needs enforcement");
        await CommentOnProposalAsync(topicId, a, owner, "Enforcement cameras would be needed");

        using (var scope = Scope())
        {
            var groups = scope.ServiceProvider.GetRequiredService<GroupService>();
            var group = await groups.CreateAsync(topicId, owner, "Charge and fund", [a, b], null);
            await scope.ServiceProvider.GetRequiredService<CommentService>()
                .PostToGroupAsync(topicId, group.Id, owner, "Enforcement is the weak point of this");
        }

        var results = await SearchAsync(topicId, "enforcement");

        Assert.Equal(3, results.Comments.Count);
        Assert.Contains(results.Comments, c => c.ProposalId is not null);
        Assert.Contains(results.Comments, c => c.GroupId is not null);
        Assert.Contains(results.Comments, c => c.ProposalId is null && c.GroupId is null);
    }

    [Fact]
    public async Task A_search_never_reaches_into_another_topic()
    {
        var owner = await UserAsync("owner");
        var here = await ProposingTopicAsync(owner);
        var elsewhere = await ProposingTopicAsync(owner);
        var mine = await ProposalAsync(here, owner, "A toll fee is suggested.");
        var theirs = await ProposalAsync(elsewhere, owner, "Something else");

        await CommentOnProposalAsync(here, mine, owner, "distinctive marker word");
        await CommentOnProposalAsync(elsewhere, theirs, owner, "distinctive marker word");

        var results = await SearchAsync(here, "distinctive");

        Assert.Single(results.Comments);
    }

    [Fact]
    public async Task Withdrawn_comments_do_not_come_back_in_results()
    {
        var owner = await UserAsync("owner");
        var topicId = await ProposingTopicAsync(owner);
        var proposal = await ProposalAsync(topicId, owner, "A toll fee is suggested.");
        await CommentOnProposalAsync(topicId, proposal, owner, "regrettable remark about enforcement");

        await using (var context = database.CreateContext())
        {
            var comment = await context.Comments.SingleAsync();
            comment.Delete(DateTime.UtcNow);
            await context.SaveChangesAsync();
        }

        var results = await SearchAsync(topicId, "enforcement");

        Assert.Empty(results.Comments);
    }

    [Fact]
    public async Task Words_the_index_cannot_match_are_reported_back()
    {
        var owner = await UserAsync("owner");
        var topicId = await ProposingTopicAsync(owner);
        var proposal = await ProposalAsync(topicId, owner, "A toll fee is suggested.");
        await CommentOnProposalAsync(topicId, proposal, owner, "The bus is late again");

        var results = await SearchAsync(topicId, "bus is late");

        Assert.Single(results.Comments);
        Assert.Equal(["is"], results.IgnoredShortTerms);
    }

    [Fact]
    public async Task A_malformed_search_explains_itself_rather_than_failing()
    {
        var owner = await UserAsync("owner");
        var topicId = await ProposingTopicAsync(owner);

        var results = await SearchAsync(topicId, "toll AND (fee");

        Assert.NotNull(results.Error);
        Assert.Empty(results.Comments);
    }
}
