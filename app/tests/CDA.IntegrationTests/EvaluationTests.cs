using CDA.Domain.Topics;
using CDA.Infrastructure.Evaluation;
using CDA.Infrastructure.Groups;
using CDA.Infrastructure.Identity;
using CDA.Infrastructure.Proposals;
using CDA.Infrastructure.Topics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CDA.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class EvaluationTests(DatabaseFixture database) : IAsyncLifetime
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

    /// <summary>A topic with two requirements, open for proposals.</summary>
    private async Task<(Guid TopicId, List<Guid> Requirements)> TopicAsync(Guid ownerId)
    {
        Guid topicId;

        using (var scope = Scope())
        {
            var topics = scope.ServiceProvider.GetRequiredService<TopicService>();
            var topic = await topics.CreateAsync("How should we reduce traffic?", "", ownerId, TopicVisibility.Public, null);
            topicId = topic.Id;

            var requirements = scope.ServiceProvider.GetRequiredService<RequirementService>();
            await requirements.AddAsync(topicId, "Must not increase journey times");
            await requirements.AddAsync(topicId, "Must be affordable to implement");
        }

        await using var context = database.CreateContext();
        var stored = await context.Topics.SingleAsync(t => t.Id == topicId);
        stored.OpenForProposals(requirementCount: 2);
        await context.SaveChangesAsync();

        var ids = await context.Requirements.AsNoTracking()
            .Where(r => r.TopicId == topicId).OrderBy(r => r.Order).Select(r => r.Id).ToListAsync();

        return (topicId, ids);
    }

    private async Task<Guid> GroupAsync(Guid topicId, Guid userId, string description)
    {
        using var scope = Scope();
        var proposals = scope.ServiceProvider.GetRequiredService<ProposalService>();
        var a = await proposals.CreateAsync(topicId, userId, $"{description} — first part", null);
        var b = await proposals.CreateAsync(topicId, userId, $"{description} — second part", null);

        var groups = scope.ServiceProvider.GetRequiredService<GroupService>();
        var result = await groups.CreateAsync(topicId, userId, description, [a.Id, b.Id], null);
        Assert.True(result.Succeeded, result.Error);
        return result.Id;
    }

    private async Task<EvaluationResult> SaveAsync(
        Guid topicId, Guid groupId, Guid userId,
        Dictionary<Guid, int> weights, Dictionary<Guid, int> scores)
    {
        using var scope = Scope();
        return await scope.ServiceProvider.GetRequiredService<EvaluationService>()
            .SaveAsync(topicId, groupId, userId, weights, scores);
    }

    private async Task<EvaluationView?> GetAsync(Guid topicId, Guid groupId, Guid userId)
    {
        using var scope = Scope();
        return await scope.ServiceProvider.GetRequiredService<EvaluationService>()
            .GetAsync(topicId, groupId, userId);
    }

    [Fact]
    public async Task An_evaluation_scores_the_alternative_against_every_requirement()
    {
        var owner = await UserAsync("owner");
        var (topicId, requirements) = await TopicAsync(owner);
        var group = await GroupAsync(topicId, owner, "Charge for entry");

        var result = await SaveAsync(topicId, group, owner,
            weights: new() { [requirements[0]] = 5, [requirements[1]] = 2 },
            scores: new() { [requirements[0]] = 4, [requirements[1]] = 1 });

        Assert.True(result.Succeeded, result.Error);

        var view = await GetAsync(topicId, group, owner);

        Assert.NotNull(view);
        Assert.True(view.HasBeenEvaluated);
        Assert.Equal(22, view.Total);             // 5×4 + 2×1
        Assert.Equal(63, view.Percentage);        // 22 of a possible 35
    }

    [Fact]
    public async Task Weights_carry_across_the_topic_rather_than_belonging_to_one_alternative()
    {
        // This is what makes the side-by-side comparison mean anything: each alternative is
        // scored on the same scale. Per-alternative weights would let someone reach any
        // conclusion they liked by adjusting the weights to suit.
        var owner = await UserAsync("owner");
        var (topicId, requirements) = await TopicAsync(owner);
        var first = await GroupAsync(topicId, owner, "Charge for entry");
        var second = await GroupAsync(topicId, owner, "Widen the cycle lanes");

        await SaveAsync(topicId, first, owner,
            weights: new() { [requirements[0]] = 5, [requirements[1]] = 1 },
            scores: new() { [requirements[0]] = 3, [requirements[1]] = 3 });

        var other = await GetAsync(topicId, second, owner);

        Assert.NotNull(other);
        Assert.Equal(5, other.Rows[0].Weight);
        Assert.Equal(1, other.Rows[1].Weight);
        // Not yet scored, though.
        Assert.False(other.HasBeenEvaluated);
    }

    [Fact]
    public async Task Changing_a_weight_reweighs_every_alternative_in_the_topic()
    {
        var owner = await UserAsync("owner");
        var (topicId, requirements) = await TopicAsync(owner);
        var first = await GroupAsync(topicId, owner, "Charge for entry");
        var second = await GroupAsync(topicId, owner, "Widen the cycle lanes");

        await SaveAsync(topicId, first, owner,
            weights: new() { [requirements[0]] = 1, [requirements[1]] = 1 },
            scores: new() { [requirements[0]] = 5, [requirements[1]] = 0 });
        await SaveAsync(topicId, second, owner,
            weights: new() { [requirements[0]] = 1, [requirements[1]] = 1 },
            scores: new() { [requirements[0]] = 0, [requirements[1]] = 5 });

        // Both score 5 while the requirements weigh the same. Deciding the first requirement
        // matters far more should separate them.
        await SaveAsync(topicId, first, owner,
            weights: new() { [requirements[0]] = 5, [requirements[1]] = 1 },
            scores: new() { [requirements[0]] = 5, [requirements[1]] = 0 });

        using var scope = Scope();
        var comparison = await scope.ServiceProvider.GetRequiredService<EvaluationService>()
            .CompareAsync(topicId, owner);

        var evaluated = comparison.Alternatives.Where(a => a.HasBeenEvaluated).ToList();

        Assert.Equal(2, evaluated.Count);
        Assert.Equal(first, evaluated[0].GroupId);
        Assert.Equal(25, evaluated[0].Total);   // 5×5 + 1×0
        Assert.Equal(5, evaluated[1].Total);    // 5×0 + 1×5
    }

    [Fact]
    public async Task Re_evaluating_replaces_the_previous_judgement_rather_than_adding_to_it()
    {
        var owner = await UserAsync("owner");
        var (topicId, requirements) = await TopicAsync(owner);
        var group = await GroupAsync(topicId, owner, "Charge for entry");

        await SaveAsync(topicId, group, owner,
            weights: new() { [requirements[0]] = 3 },
            scores: new() { [requirements[0]] = 5 });
        await SaveAsync(topicId, group, owner,
            weights: new() { [requirements[0]] = 3 },
            scores: new() { [requirements[0]] = 1 });

        await using var context = database.CreateContext();
        var rows = await context.RequirementScores.AsNoTracking()
            .Where(s => s.UserId == owner && s.GroupId == group).ToListAsync();

        Assert.Single(rows);
        Assert.Equal(1, rows[0].Score);
    }

    [Fact]
    public async Task An_evaluation_is_private_to_whoever_made_it()
    {
        // The vote is the public act; the reasoning behind it is not.
        var owner = await UserAsync("owner");
        var other = await UserAsync("other");
        var (topicId, requirements) = await TopicAsync(owner);
        var group = await GroupAsync(topicId, owner, "Charge for entry");

        await SaveAsync(topicId, group, owner,
            weights: new() { [requirements[0]] = 5, [requirements[1]] = 5 },
            scores: new() { [requirements[0]] = 5, [requirements[1]] = 5 });

        var theirView = await GetAsync(topicId, group, other);

        Assert.NotNull(theirView);
        Assert.False(theirView.HasBeenEvaluated);
        Assert.Equal(0, theirView.Total);
        Assert.All(theirView.Rows, row => Assert.Equal(0, row.Score));
    }

    [Fact]
    public async Task An_unweighted_requirement_starts_in_the_middle_rather_than_at_zero()
    {
        // An unconsidered criterion is not the same as one judged irrelevant; defaulting to
        // zero would silently exclude it from the score.
        var owner = await UserAsync("owner");
        var (topicId, _) = await TopicAsync(owner);
        var group = await GroupAsync(topicId, owner, "Charge for entry");

        var view = await GetAsync(topicId, group, owner);

        Assert.NotNull(view);
        Assert.All(view.Rows, row => Assert.Equal(3, row.Weight));
    }

    [Fact]
    public async Task Requirements_from_another_topic_are_discarded_rather_than_stored()
    {
        var owner = await UserAsync("owner");
        var (topicId, requirements) = await TopicAsync(owner);
        var (_, foreignRequirements) = await TopicAsync(owner);
        var group = await GroupAsync(topicId, owner, "Charge for entry");

        await SaveAsync(topicId, group, owner,
            weights: new() { [requirements[0]] = 5, [foreignRequirements[0]] = 5 },
            scores: new() { [requirements[0]] = 5, [foreignRequirements[0]] = 5 });

        await using var context = database.CreateContext();
        var stored = await context.RequirementScores.AsNoTracking()
            .Where(s => s.UserId == owner && s.GroupId == group).ToListAsync();

        Assert.Single(stored);
        Assert.Equal(requirements[0], stored[0].RequirementId);
    }

    [Fact]
    public async Task An_alternative_cannot_be_evaluated_through_another_topic()
    {
        var owner = await UserAsync("owner");
        var (victimTopic, _) = await TopicAsync(owner);
        var (ownTopic, ownRequirements) = await TopicAsync(owner);
        var victimGroup = await GroupAsync(victimTopic, owner, "Belongs elsewhere");

        var result = await SaveAsync(ownTopic, victimGroup, owner,
            weights: new() { [ownRequirements[0]] = 5 },
            scores: new() { [ownRequirements[0]] = 5 });

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task The_comparison_lists_unevaluated_alternatives_separately()
    {
        var owner = await UserAsync("owner");
        var (topicId, requirements) = await TopicAsync(owner);
        var evaluated = await GroupAsync(topicId, owner, "Charge for entry");
        await GroupAsync(topicId, owner, "Never looked at");

        await SaveAsync(topicId, evaluated, owner,
            weights: new() { [requirements[0]] = 4 },
            scores: new() { [requirements[0]] = 4 });

        using var scope = Scope();
        var comparison = await scope.ServiceProvider.GetRequiredService<EvaluationService>()
            .CompareAsync(topicId, owner);

        Assert.Equal(2, comparison.Alternatives.Count);
        Assert.True(comparison.Alternatives[0].HasBeenEvaluated);
        Assert.False(comparison.Alternatives[1].HasBeenEvaluated);
    }

    [Fact]
    public async Task The_database_refuses_a_score_outside_the_scale()
    {
        var owner = await UserAsync("owner");
        var (topicId, requirements) = await TopicAsync(owner);
        var group = await GroupAsync(topicId, owner, "Charge for entry");

        await using var context = database.CreateContext();

        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO RequirementScores (UserId, GroupId, TopicId, RequirementId, Score, UpdatedAtUtc) " +
                "VALUES ({0}, {1}, {2}, {3}, 99, UTC_TIMESTAMP())",
                owner, group, topicId, requirements[0]));
    }
}
