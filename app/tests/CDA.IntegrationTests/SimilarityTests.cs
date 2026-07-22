using CDA.Application.Topics;
using CDA.Domain.Topics;
using CDA.Infrastructure.Identity;
using CDA.Infrastructure.Proposals;
using CDA.Infrastructure.Similarity;
using CDA.Infrastructure.Topics;
using CDA.Infrastructure.Voting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CDA.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class SimilarityTests(DatabaseFixture database) : IAsyncLifetime
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

    private async Task<Guid> LockedProposalAsync(Guid topicId, Guid authorId, string text)
    {
        Guid id;

        using (var scope = Scope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<ProposalService>()
                .CreateAsync(topicId, authorId, text, null);
            Assert.True(result.Succeeded, result.Error);
            id = result.Id;
        }

        await using var context = database.CreateContext();
        var proposal = await context.Proposals.SingleAsync(p => p.Id == id);
        proposal.LockNow(DateTime.UtcNow);
        await context.SaveChangesAsync();

        return id;
    }

    private async Task<SimilarityResult> ReportAsync(
        Guid topicId, Guid a, Guid b, Guid userId, Guid? betterWritten = null)
    {
        using var scope = Scope();
        return await scope.ServiceProvider.GetRequiredService<SimilarityService>()
            .ReportAsync(topicId, a, b, userId, betterWritten, "They say the same thing");
    }

    private async Task AgreeAsync(Guid similarityId, Guid userId, short value = 1)
    {
        using var scope = Scope();
        await scope.ServiceProvider.GetRequiredService<SimilarityVotingService>()
            .CastAsync(similarityId, userId, value);
    }

    private async Task VoteOnProposalAsync(Guid proposalId, Guid userId, short value)
    {
        using var scope = Scope();
        await scope.ServiceProvider.GetRequiredService<ProposalVotingService>()
            .CastAsync(proposalId, userId, value);
    }

    private async Task<ProposalPage> ListAsync(Guid topicId, Guid viewerId, int? threshold)
    {
        using var scope = Scope();
        return await scope.ServiceProvider.GetRequiredService<ProposalService>()
            .ListAsync(topicId, new TopicViewer(viewerId, false, TopicRole.Facilitator),
                ProposalSort.Score, null, null, threshold);
    }

    [Fact]
    public async Task The_same_pair_cannot_be_reported_twice_in_either_direction()
    {
        // A second row would split the votes that decide whether the claim takes effect.
        var owner = await UserAsync("owner");
        var other = await UserAsync("other");
        var topicId = await ProposingTopicAsync(owner);
        var a = await LockedProposalAsync(topicId, owner, "A toll fee is suggested.");
        var b = await LockedProposalAsync(topicId, owner, "Drivers should pay to enter.");

        var first = await ReportAsync(topicId, a, b, owner);
        var mirrored = await ReportAsync(topicId, b, a, other);

        Assert.True(first.Succeeded, first.Error);
        Assert.False(mirrored.Succeeded);
        Assert.Contains("already reported", mirrored.Error!);

        await using var context = database.CreateContext();
        Assert.Equal(1, await context.SimilarityReports.CountAsync());
    }

    [Fact]
    public async Task Nothing_is_folded_unless_the_reader_asks_for_it()
    {
        // The platform reports similarity rather than deciding it; nothing vanishes unasked.
        var owner = await UserAsync("owner");
        var topicId = await ProposingTopicAsync(owner);
        var a = await LockedProposalAsync(topicId, owner, "A toll fee is suggested.");
        var b = await LockedProposalAsync(topicId, owner, "Drivers should pay to enter.");

        var report = await ReportAsync(topicId, a, b, owner);
        await AgreeAsync(report.Id, owner);

        var page = await ListAsync(topicId, owner, threshold: null);

        Assert.Equal(2, page.Items.Count);
        Assert.All(page.Items, item => Assert.Equal(0, item.CollapsedDuplicates));
    }

    [Fact]
    public async Task A_report_folds_the_pair_only_once_it_clears_the_readers_threshold()
    {
        var owner = await UserAsync("owner");
        var second = await UserAsync("second");
        var topicId = await ProposingTopicAsync(owner);
        var a = await LockedProposalAsync(topicId, owner, "A toll fee is suggested.");
        var b = await LockedProposalAsync(topicId, owner, "Drivers should pay to enter.");

        var report = await ReportAsync(topicId, a, b, owner);
        await AgreeAsync(report.Id, owner);

        // One vote of agreement: folds for a reader who accepts one, not for one who wants two.
        Assert.Single((await ListAsync(topicId, owner, threshold: 1)).Items);
        Assert.Equal(2, (await ListAsync(topicId, owner, threshold: 2)).Items.Count);

        await AgreeAsync(report.Id, second);

        Assert.Single((await ListAsync(topicId, owner, threshold: 2)).Items);
    }

    [Fact]
    public async Task A_report_the_crowd_rejects_never_folds_anything()
    {
        var owner = await UserAsync("owner");
        var sceptic = await UserAsync("sceptic");
        var topicId = await ProposingTopicAsync(owner);
        var a = await LockedProposalAsync(topicId, owner, "A toll fee is suggested.");
        var b = await LockedProposalAsync(topicId, owner, "Ban cars entirely.");

        var report = await ReportAsync(topicId, a, b, owner);
        await AgreeAsync(report.Id, sceptic, -1);

        Assert.Equal(2, (await ListAsync(topicId, owner, threshold: 1)).Items.Count);
    }

    [Fact]
    public async Task A_chain_of_reports_folds_into_a_single_entry()
    {
        // A~B and B~C means all three are one idea; leaving A and C listed separately would
        // still split its support.
        var owner = await UserAsync("owner");
        var topicId = await ProposingTopicAsync(owner);
        var a = await LockedProposalAsync(topicId, owner, "A toll fee is suggested.");
        var b = await LockedProposalAsync(topicId, owner, "Drivers should pay to enter.");
        var c = await LockedProposalAsync(topicId, owner, "Entering the centre should cost money.");

        var first = await ReportAsync(topicId, a, b, owner);
        var secondReport = await ReportAsync(topicId, b, c, owner);
        await AgreeAsync(first.Id, owner);
        await AgreeAsync(secondReport.Id, owner);

        var page = await ListAsync(topicId, owner, threshold: 1);

        Assert.Single(page.Items);
        Assert.Equal(2, page.Items[0].CollapsedDuplicates);
    }

    [Fact]
    public async Task A_folded_group_reports_the_support_of_the_whole_idea()
    {
        // Support that was split across duplicates belongs to the idea, not to whichever
        // wording happened to be listed.
        var owner = await UserAsync("owner");
        var v1 = await UserAsync("v1");
        var v2 = await UserAsync("v2");
        var topicId = await ProposingTopicAsync(owner);
        var a = await LockedProposalAsync(topicId, owner, "A toll fee is suggested.");
        var b = await LockedProposalAsync(topicId, owner, "Drivers should pay to enter.");

        await VoteOnProposalAsync(a, v1, 1);
        await VoteOnProposalAsync(b, v2, 1);

        var report = await ReportAsync(topicId, a, b, owner);
        await AgreeAsync(report.Id, owner);

        var page = await ListAsync(topicId, owner, threshold: 1);

        Assert.Single(page.Items);
        Assert.Equal(2, page.Items[0].GroupScoreSum);
    }

    [Fact]
    public async Task The_wording_judged_better_is_the_one_shown()
    {
        var owner = await UserAsync("owner");
        var voter = await UserAsync("voter");
        var topicId = await ProposingTopicAsync(owner);
        var clumsy = await LockedProposalAsync(topicId, owner, "toll thing for cars maybe");
        var clear = await LockedProposalAsync(topicId, owner, "A toll fee is suggested.");

        // The crowd happens to back the clumsy wording more, but the reporter read both.
        await VoteOnProposalAsync(clumsy, voter, 1);

        var report = await ReportAsync(topicId, clumsy, clear, owner, betterWritten: clear);
        await AgreeAsync(report.Id, owner);

        var page = await ListAsync(topicId, owner, threshold: 1);

        Assert.Single(page.Items);
        Assert.Equal(clear, page.Items[0].Id);
    }

    [Fact]
    public async Task A_report_cannot_span_two_topics()
    {
        var owner = await UserAsync("owner");
        var firstTopic = await ProposingTopicAsync(owner);
        var secondTopic = await ProposingTopicAsync(owner);
        var here = await LockedProposalAsync(firstTopic, owner, "A toll fee is suggested.");
        var elsewhere = await LockedProposalAsync(secondTopic, owner, "Something in another topic");

        var result = await ReportAsync(firstTopic, here, elsewhere, owner);

        Assert.False(result.Succeeded);
        Assert.Contains("belong to this topic", result.Error!);
    }

    [Fact]
    public async Task The_page_shows_when_a_reader_supports_two_proposals_they_call_identical()
    {
        // The split the mechanism exists to prevent.
        var owner = await UserAsync("owner");
        var topicId = await ProposingTopicAsync(owner);
        var a = await LockedProposalAsync(topicId, owner, "A toll fee is suggested.");
        var b = await LockedProposalAsync(topicId, owner, "Drivers should pay to enter.");

        var report = await ReportAsync(topicId, a, b, owner);
        await AgreeAsync(report.Id, owner);
        await VoteOnProposalAsync(a, owner, 1);
        await VoteOnProposalAsync(b, owner, -1);

        using var scope = Scope();
        var views = await scope.ServiceProvider.GetRequiredService<SimilarityService>()
            .ForProposalAsync(a, owner, threshold: 1);

        Assert.Single(views);
        Assert.Equal((short)1, views[0].MyVote);
        Assert.Equal((short)1, views[0].MyVoteOnThis);
        Assert.Equal((short)-1, views[0].MyVoteOnOther);
    }

    [Fact]
    public async Task The_database_refuses_a_pair_stored_out_of_order()
    {
        // Belt and braces for the canonical ordering: no other route can create the
        // mirror-image row the ordering exists to prevent.
        var owner = await UserAsync("owner");
        var topicId = await ProposingTopicAsync(owner);
        var a = await LockedProposalAsync(topicId, owner, "First");
        var b = await LockedProposalAsync(topicId, owner, "Second");

        var (low, high) = a.CompareTo(b) < 0 ? (a, b) : (b, a);

        await using var context = database.CreateContext();

        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO SimilarityReports (Id, TopicId, ProposalAId, ProposalBId, ReportedByUserId, " +
                "CreatedAtUtc, ScoreSum, VoteCount) VALUES (UUID(), {0}, {1}, {2}, {3}, UTC_TIMESTAMP(), 0, 0)",
                topicId, high, low, owner));
    }
}
