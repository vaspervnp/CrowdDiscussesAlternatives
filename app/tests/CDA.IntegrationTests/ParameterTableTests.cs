using CDA.Domain.Parameters;
using CDA.Domain.Topics;
using CDA.Infrastructure.Identity;
using CDA.Infrastructure.Parameters;
using CDA.Infrastructure.Topics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

// The grid is addressed by a pair of factor ids; the alias keeps the call sites readable.
using Cells = System.Collections.Generic.Dictionary<
    (System.Guid From, System.Guid To),
    (CDA.Domain.Parameters.InfluenceEffect Effect, string? Note)>;

namespace CDA.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class ParameterTableTests(DatabaseFixture database) : IAsyncLifetime
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

    private async Task<Guid> TopicAsync(Guid ownerId)
    {
        using var scope = Scope();
        var topic = await scope.ServiceProvider.GetRequiredService<TopicService>()
            .CreateAsync("How should we reduce traffic?", "", ownerId, TopicVisibility.Public, null);
        return topic.Id;
    }

    private async Task<ParameterResult> CreateAsync(
        Guid topicId, Guid userId, string name, params string[] factors)
    {
        using var scope = Scope();
        return await scope.ServiceProvider.GetRequiredService<ParameterTableService>()
            .CreateAsync(topicId, userId, name, factors);
    }

    private async Task<ParameterTableView?> GetAsync(Guid topicId, Guid tableId, Guid? viewerId)
    {
        using var scope = Scope();
        return await scope.ServiceProvider.GetRequiredService<ParameterTableService>()
            .GetAsync(topicId, tableId, viewerId);
    }

    [Fact]
    public async Task A_table_records_factors_in_the_order_they_were_named()
    {
        var owner = await UserAsync("owner");
        var topicId = await TopicAsync(owner);

        var result = await CreateAsync(topicId, owner, "Traffic factors",
            "Journey time", "Air quality", "Cost to residents");

        Assert.True(result.Succeeded, result.Error);

        var table = await GetAsync(topicId, result.Id, owner);

        Assert.NotNull(table);
        Assert.Equal(["Journey time", "Air quality", "Cost to residents"],
            table.Parameters.Select(p => p.Name));
    }

    [Fact]
    public async Task A_table_needs_at_least_two_factors()
    {
        var owner = await UserAsync("owner");
        var topicId = await TopicAsync(owner);

        var result = await CreateAsync(topicId, owner, "Just one thing", "Journey time");

        Assert.False(result.Succeeded);
        Assert.Contains("at least two", result.Error!);
    }

    [Fact]
    public async Task More_factors_than_the_grid_can_carry_are_refused()
    {
        // The grid is square, so the count that matters is the number of cells to fill in.
        var owner = await UserAsync("owner");
        var topicId = await TopicAsync(owner);

        var tooMany = Enumerable.Range(1, ParameterTable.MaxParameters + 1)
            .Select(n => $"Factor {n}").ToArray();

        var result = await CreateAsync(topicId, owner, "Everything", tooMany);

        Assert.False(result.Succeeded);
        Assert.Contains("read", result.Error!);
    }

    [Fact]
    public async Task Repeated_factor_names_are_folded_together()
    {
        var owner = await UserAsync("owner");
        var topicId = await TopicAsync(owner);

        var result = await CreateAsync(topicId, owner, "Traffic factors",
            "Journey time", "journey time", "Air quality");

        Assert.True(result.Succeeded, result.Error);

        var table = await GetAsync(topicId, result.Id, owner);
        Assert.Equal(2, table!.Parameters.Count);
    }

    [Fact]
    public async Task An_influence_runs_one_way_and_the_reverse_is_a_separate_judgement()
    {
        // "Charging harms shop takings" and "shop takings harm charging" are different claims,
        // and a grid that could not hold both would lose most of its point.
        var owner = await UserAsync("owner");
        var topicId = await TopicAsync(owner);
        var created = await CreateAsync(topicId, owner, "Traffic factors", "Charging", "Shop takings");
        var table = await GetAsync(topicId, created.Id, owner);
        var charging = table!.Parameters[0].Id;
        var takings = table.Parameters[1].Id;

        using (var scope = Scope())
        {
            await scope.ServiceProvider.GetRequiredService<ParameterTableService>()
                .SaveInfluencesAsync(topicId, created.Id, owner, new Cells()
                {
                    [(charging, takings)] = (InfluenceEffect.StronglyNegative, "Fewer people drive in to shop"),
                    [(takings, charging)] = (InfluenceEffect.Positive, "Healthy shops make the charge easier to defend"),
                });
        }

        var updated = await GetAsync(topicId, created.Id, owner);

        Assert.Equal(InfluenceEffect.StronglyNegative, updated!.Influences[(charging, takings)].Effect);
        Assert.Equal(InfluenceEffect.Positive, updated.Influences[(takings, charging)].Effect);
    }

    [Fact]
    public async Task A_cell_set_back_to_no_effect_is_removed_rather_than_stored()
    {
        // Otherwise a filled-in grid stores a row for every empty cell — noise that grows with
        // the square of the factor count.
        var owner = await UserAsync("owner");
        var topicId = await TopicAsync(owner);
        var created = await CreateAsync(topicId, owner, "Traffic factors", "Charging", "Shop takings");
        var table = await GetAsync(topicId, created.Id, owner);
        var a = table!.Parameters[0].Id;
        var b = table.Parameters[1].Id;

        using (var scope = Scope())
        {
            var service = scope.ServiceProvider.GetRequiredService<ParameterTableService>();
            await service.SaveInfluencesAsync(topicId, created.Id, owner,
                new Cells() { [(a, b)] = (InfluenceEffect.Negative, null) });
            await service.SaveInfluencesAsync(topicId, created.Id, owner,
                new Cells() { [(a, b)] = (InfluenceEffect.None, null) });
        }

        await using var context = database.CreateContext();
        Assert.Equal(0, await context.ParameterInfluences.CountAsync(i => i.TableId == created.Id));
    }

    [Fact]
    public async Task A_note_is_kept_even_when_the_effect_is_none()
    {
        // "I looked at this and concluded nothing happens" is worth recording.
        var owner = await UserAsync("owner");
        var topicId = await TopicAsync(owner);
        var created = await CreateAsync(topicId, owner, "Traffic factors", "Charging", "Shop takings");
        var table = await GetAsync(topicId, created.Id, owner);
        var a = table!.Parameters[0].Id;
        var b = table.Parameters[1].Id;

        using (var scope = Scope())
        {
            await scope.ServiceProvider.GetRequiredService<ParameterTableService>()
                .SaveInfluencesAsync(topicId, created.Id, owner,
                    new Cells() { [(a, b)] = (InfluenceEffect.None, "Checked the 2024 data — no link") });
        }

        var updated = await GetAsync(topicId, created.Id, owner);
        Assert.Equal("Checked the 2024 data — no link", updated!.Influences[(a, b)].Note);
    }

    [Fact]
    public async Task A_table_starts_private_and_stays_so_until_shared()
    {
        var owner = await UserAsync("owner");
        var other = await UserAsync("other");
        var topicId = await TopicAsync(owner);
        var created = await CreateAsync(topicId, owner, "My working sketch", "Charging", "Shop takings");

        Assert.Null(await GetAsync(topicId, created.Id, other));

        using (var scope = Scope())
        {
            await scope.ServiceProvider.GetRequiredService<ParameterTableService>()
                .ShareAsync(topicId, created.Id, owner, shared: true);
        }

        Assert.NotNull(await GetAsync(topicId, created.Id, other));
    }

    [Fact]
    public async Task A_shared_table_stays_its_authors_to_change()
    {
        // Shared means readable, not editable: it is one participant's reading of the problem,
        // not a document the topic agrees on.
        var owner = await UserAsync("owner");
        var other = await UserAsync("other");
        var topicId = await TopicAsync(owner);
        var created = await CreateAsync(topicId, owner, "Traffic factors", "Charging", "Shop takings");
        var table = await GetAsync(topicId, created.Id, owner);
        var a = table!.Parameters[0].Id;
        var b = table.Parameters[1].Id;

        using var scope = Scope();
        var service = scope.ServiceProvider.GetRequiredService<ParameterTableService>();
        await service.ShareAsync(topicId, created.Id, owner, shared: true);

        var result = await service.SaveInfluencesAsync(topicId, created.Id, other,
            new Cells() { [(a, b)] = (InfluenceEffect.StronglyPositive, "Not my table") });

        Assert.False(result.Succeeded);
        Assert.Contains("Only the person who made this table", result.Error!);
    }

    [Fact]
    public async Task The_list_shows_my_own_tables_and_everyone_elses_shared_ones()
    {
        var owner = await UserAsync("owner");
        var other = await UserAsync("other");
        var topicId = await TopicAsync(owner);

        await CreateAsync(topicId, owner, "Mine, private", "A", "B");
        var theirsShared = await CreateAsync(topicId, other, "Theirs, shared", "A", "B");
        await CreateAsync(topicId, other, "Theirs, private", "A", "B");

        using var scope = Scope();
        var service = scope.ServiceProvider.GetRequiredService<ParameterTableService>();
        await service.ShareAsync(topicId, theirsShared.Id, other, shared: true);

        var visible = await service.ListAsync(topicId, owner);

        Assert.Equal(2, visible.Count);
        Assert.Contains(visible, t => t.Name == "Mine, private" && t.IsMine);
        Assert.Contains(visible, t => t.Name == "Theirs, shared" && !t.IsMine);
        Assert.DoesNotContain(visible, t => t.Name == "Theirs, private");
    }

    [Fact]
    public async Task Factors_from_another_table_are_discarded_rather_than_stored()
    {
        var owner = await UserAsync("owner");
        var topicId = await TopicAsync(owner);
        var mine = await CreateAsync(topicId, owner, "Mine", "A", "B");
        var otherTable = await CreateAsync(topicId, owner, "Other", "C", "D");

        var foreign = (await GetAsync(topicId, otherTable.Id, owner))!.Parameters;
        var here = (await GetAsync(topicId, mine.Id, owner))!.Parameters;

        using (var scope = Scope())
        {
            await scope.ServiceProvider.GetRequiredService<ParameterTableService>()
                .SaveInfluencesAsync(topicId, mine.Id, owner, new Cells()
                {
                    [(here[0].Id, foreign[0].Id)] = (InfluenceEffect.Negative, "Not in this table"),
                });
        }

        await using var context = database.CreateContext();
        Assert.Equal(0, await context.ParameterInfluences.CountAsync(i => i.TableId == mine.Id));
    }

    [Fact]
    public async Task A_factor_cannot_influence_itself()
    {
        var owner = await UserAsync("owner");
        var topicId = await TopicAsync(owner);
        var created = await CreateAsync(topicId, owner, "Traffic factors", "Charging", "Shop takings");
        var a = (await GetAsync(topicId, created.Id, owner))!.Parameters[0].Id;

        using (var scope = Scope())
        {
            await scope.ServiceProvider.GetRequiredService<ParameterTableService>()
                .SaveInfluencesAsync(topicId, created.Id, owner,
                    new Cells() { [(a, a)] = (InfluenceEffect.StronglyPositive, "Nonsense") });
        }

        await using var context = database.CreateContext();
        Assert.Equal(0, await context.ParameterInfluences.CountAsync(i => i.TableId == created.Id));
    }

    [Fact]
    public async Task The_database_refuses_a_self_influence_by_any_route()
    {
        var owner = await UserAsync("owner");
        var topicId = await TopicAsync(owner);
        var created = await CreateAsync(topicId, owner, "Traffic factors", "Charging", "Shop takings");
        var a = (await GetAsync(topicId, created.Id, owner))!.Parameters[0].Id;

        await using var context = database.CreateContext();

        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO ParameterInfluences (TableId, FromParameterId, ToParameterId, Effect) " +
                "VALUES ({0}, {1}, {2}, 1)",
                created.Id, a, a));
    }
}
