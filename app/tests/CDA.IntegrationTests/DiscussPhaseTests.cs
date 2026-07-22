using CDA.Application.Topics;
using CDA.Domain.Topics;
using CDA.Infrastructure.Discussion;
using CDA.Infrastructure.Identity;
using CDA.Infrastructure.Topics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CDA.IntegrationTests;

/// <summary>
/// The DISCUSS-to-TOPIC flow the platform grew out of: agree what a solution must achieve,
/// then open the pool of proposals against that fixed list.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class DiscussPhaseTests(DatabaseFixture database) : IAsyncLifetime
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

    private async Task<RequirementChange> AddRequirementAsync(Guid topicId, string text)
    {
        using var scope = Scope();
        return await scope.ServiceProvider.GetRequiredService<RequirementService>().AddAsync(topicId, text);
    }

    private async Task<CommentResult> CommentAsync(Guid topicId, Guid userId, string body)
    {
        using var scope = Scope();
        return await scope.ServiceProvider.GetRequiredService<CommentService>()
            .PostToTopicAsync(topicId, userId, body);
    }

    [Fact]
    public async Task Requirements_keep_the_order_they_were_agreed_in()
    {
        var owner = await UserAsync("owner");
        var topicId = await TopicAsync(owner);

        await AddRequirementAsync(topicId, "Must not increase journey times");
        await AddRequirementAsync(topicId, "Must be affordable to implement");
        await AddRequirementAsync(topicId, "Must not penalise outer suburbs");

        using var scope = Scope();
        var list = await scope.ServiceProvider.GetRequiredService<RequirementService>().ListAsync(topicId);

        Assert.Equal(
            ["Must not increase journey times", "Must be affordable to implement", "Must not penalise outer suburbs"],
            list.Select(r => r.Text));
    }

    [Fact]
    public async Task Reordering_renumbers_the_whole_list_so_gaps_do_not_accumulate()
    {
        var owner = await UserAsync("owner");
        var topicId = await TopicAsync(owner);

        await AddRequirementAsync(topicId, "First");
        await AddRequirementAsync(topicId, "Second");
        await AddRequirementAsync(topicId, "Third");

        using var scope = Scope();
        var service = scope.ServiceProvider.GetRequiredService<RequirementService>();

        var list = await service.ListAsync(topicId);
        await service.MoveAsync(topicId, list[2].Id, up: true);

        var reordered = await service.ListAsync(topicId);

        Assert.Equal(["First", "Third", "Second"], reordered.Select(r => r.Text));
        Assert.Equal([1, 2, 3], reordered.Select(r => r.Order));
    }

    [Fact]
    public async Task A_topic_cannot_open_for_proposals_with_no_requirements()
    {
        var owner = await UserAsync("owner");
        var topicId = await TopicAsync(owner);

        await using var context = database.CreateContext();
        var topic = await context.Topics.SingleAsync(t => t.Id == topicId);

        Assert.Throws<InvalidOperationException>(() => topic.OpenForProposals(requirementCount: 0));
    }

    [Fact]
    public async Task Opening_for_proposals_freezes_the_requirement_list()
    {
        // Groups get scored against these later; moving the goalposts afterwards would
        // invalidate evaluations people already made, without telling them.
        var owner = await UserAsync("owner");
        var topicId = await TopicAsync(owner);
        await AddRequirementAsync(topicId, "Must be affordable");

        await using (var context = database.CreateContext())
        {
            var topic = await context.Topics.SingleAsync(t => t.Id == topicId);
            topic.OpenForProposals(requirementCount: 1);
            await context.SaveChangesAsync();
        }

        var added = await AddRequirementAsync(topicId, "Sneaked in afterwards");

        Assert.False(added.Succeeded);
        Assert.Contains("settled", added.Error!, StringComparison.OrdinalIgnoreCase);

        using var scope = Scope();
        var list = await scope.ServiceProvider.GetRequiredService<RequirementService>().ListAsync(topicId);
        Assert.Single(list);
    }

    [Fact]
    public async Task A_requirement_cannot_be_reached_through_another_topic()
    {
        // The requirement id travels in the route. Looking it up by id alone would let the
        // facilitator of one topic edit another topic's list.
        var owner = await UserAsync("owner");
        var intruder = await UserAsync("intruder");

        var victimTopic = await TopicAsync(owner);
        var ownTopic = await TopicAsync(intruder);

        await AddRequirementAsync(victimTopic, "Belongs to someone else");

        using var scope = Scope();
        var service = scope.ServiceProvider.GetRequiredService<RequirementService>();
        var victimRequirement = (await service.ListAsync(victimTopic))[0];

        var removed = await service.RemoveAsync(ownTopic, victimRequirement.Id);
        var edited = await service.EditAsync(ownTopic, victimRequirement.Id, "Tampered");

        Assert.False(edited.Succeeded);

        // Still there, still untouched.
        var survivors = await service.ListAsync(victimTopic);
        Assert.Single(survivors);
        Assert.Equal("Belongs to someone else", survivors[0].Text);
    }

    [Fact]
    public async Task Commenting_on_a_public_topic_joins_it()
    {
        var owner = await UserAsync("owner");
        var newcomer = await UserAsync("newcomer");
        var topicId = await TopicAsync(owner);

        var result = await CommentAsync(topicId, newcomer, "What counts as the city centre here?");

        Assert.True(result.Succeeded, result.Error);

        await using var context = database.CreateContext();
        Assert.True(await context.TopicMembers.AnyAsync(m => m.TopicId == topicId && m.UserId == newcomer));
    }

    [Fact]
    public async Task A_non_member_cannot_comment_on_an_invite_only_topic()
    {
        var owner = await UserAsync("owner");
        var outsider = await UserAsync("outsider");
        var topicId = await TopicAsync(owner, TopicVisibility.InviteOnly);

        var result = await CommentAsync(topicId, outsider, "Let me in");

        Assert.False(result.Succeeded);

        await using var context = database.CreateContext();
        Assert.Equal(0, await context.Comments.CountAsync(c => c.TopicId == topicId));
    }

    [Fact]
    public async Task A_closed_topic_takes_no_further_comments()
    {
        var owner = await UserAsync("owner");
        var topicId = await TopicAsync(owner);

        await using (var context = database.CreateContext())
        {
            var topic = await context.Topics.SingleAsync(t => t.Id == topicId);
            topic.Close();
            await context.SaveChangesAsync();
        }

        var result = await CommentAsync(topicId, owner, "One last thought");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task An_empty_comment_is_refused()
    {
        var owner = await UserAsync("owner");
        var topicId = await TopicAsync(owner);

        Assert.False((await CommentAsync(topicId, owner, "   ")).Succeeded);
    }

    [Fact]
    public async Task A_withdrawn_comment_stays_in_the_thread_as_a_tombstone()
    {
        // Replies below it stop making sense if the remark they answer disappears.
        var owner = await UserAsync("owner");
        var topicId = await TopicAsync(owner);
        await CommentAsync(topicId, owner, "Something I regret saying");

        using var scope = Scope();
        var service = scope.ServiceProvider.GetRequiredService<CommentService>();
        var viewer = new TopicViewer(owner, false, TopicRole.Facilitator);

        var posted = (await service.ForTopicAsync(topicId, viewer))[0];
        await service.DeleteAsync(topicId, posted.Id, viewer);

        var after = await service.ForTopicAsync(topicId, viewer);

        Assert.Single(after);
        Assert.True(after[0].IsDeleted);
        // The body is not returned once withdrawn.
        Assert.Equal(string.Empty, after[0].Body);

        await using var context = database.CreateContext();
        Assert.Equal(1, await context.Comments.CountAsync(c => c.TopicId == topicId));
    }

    [Fact]
    public async Task A_facilitator_of_one_topic_cannot_moderate_another()
    {
        var owner = await UserAsync("owner");
        var intruder = await UserAsync("intruder");
        var victimTopic = await TopicAsync(owner);
        var ownTopic = await TopicAsync(intruder);

        await CommentAsync(victimTopic, owner, "A remark in someone else's topic");

        using var scope = Scope();
        var service = scope.ServiceProvider.GetRequiredService<CommentService>();

        var ownerView = new TopicViewer(owner, false, TopicRole.Facilitator);
        var target = (await service.ForTopicAsync(victimTopic, ownerView))[0];

        // Facilitator of their own topic, quoting a comment id from another one.
        var intruderView = new TopicViewer(intruder, false, TopicRole.Facilitator);
        await service.DeleteAsync(ownTopic, target.Id, intruderView);

        var survivors = await service.ForTopicAsync(victimTopic, ownerView);
        Assert.False(survivors[0].IsDeleted);
    }

    [Fact]
    public async Task Only_the_author_can_edit_their_own_words()
    {
        var owner = await UserAsync("owner");
        var other = await UserAsync("other");
        var topicId = await TopicAsync(owner);
        await CommentAsync(topicId, owner, "Original wording");

        using var scope = Scope();
        var service = scope.ServiceProvider.GetRequiredService<CommentService>();
        var viewer = new TopicViewer(owner, false, TopicRole.Facilitator);
        var comment = (await service.ForTopicAsync(topicId, viewer))[0];

        var result = await service.EditAsync(topicId, comment.Id, other, "Words put in their mouth");

        Assert.False(result.Succeeded);
        Assert.Equal("Original wording", (await service.ForTopicAsync(topicId, viewer))[0].Body);
    }

    [Fact]
    public async Task Full_text_search_over_comments_is_available_for_the_search_phase()
    {
        // The index is declared in raw SQL in the migration; this proves it exists and that
        // boolean-mode AND works against it, which is what the query parser will emit.
        var owner = await UserAsync("owner");
        var topicId = await TopicAsync(owner);

        await CommentAsync(topicId, owner, "Congestion charging worked well in other cities");
        await CommentAsync(topicId, owner, "Cycle lanes are cheaper than charging schemes");

        await using var context = database.CreateContext();

        var matches = await context.Comments
            .FromSqlRaw(
                "SELECT * FROM Comments WHERE MATCH(Body) AGAINST ({0} IN BOOLEAN MODE)",
                "+congestion +charging")
            .CountAsync();

        Assert.Equal(1, matches);
    }
}
