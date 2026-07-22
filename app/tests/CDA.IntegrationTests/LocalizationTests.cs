using System.Net;
using CDA.Application.Localization;
using CDA.Infrastructure.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace CDA.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class LocalizationTests(DatabaseFixture database) : IAsyncLifetime
{
    private CdaWebApplicationFactory _factory = null!;

    public async Task InitializeAsync()
    {
        // ResetAsync empties LocalizedTexts along with everything else. Seed the shipped Greek
        // back explicitly and reload the cache, so a pure-read test does not depend on the exact
        // moment the startup seeder happens to run. SeedAsync tolerates the startup seeder
        // racing it, so running both is safe.
        await database.ResetAsync();
        _factory = new CdaWebApplicationFactory(database);

        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<LocalizationService>().SeedAsync();
        _factory.Services.GetRequiredService<LocalizationStore>().Invalidate();
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private LocalizationStore Store => _factory.Services.GetRequiredService<LocalizationStore>();

    [Fact]
    public void A_seeded_string_comes_back_in_greek()
    {
        Assert.Equal("Θέματα", Store.Find("el-GR", "Topics"));
    }

    [Fact]
    public async Task A_region_reader_is_served_a_bare_language_translation()
    {
        // A translation filed under bare "el" answers a reader who asks as "el-GR", so a string
        // need not be repeated for every regional variant. (The probe key has no "el-GR" row of
        // its own, so only the fallback can satisfy it.)
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<LocalizationService>()
            .SetAsync("region fallback probe", "el", "ταιριάζει");

        Assert.Equal("ταιριάζει", Store.Find("el-GR", "region fallback probe"));
    }

    [Fact]
    public void An_unknown_language_has_nothing_to_offer()
    {
        // The caller falls back to the key, which is English — a usable page, not a broken one.
        Assert.Null(Store.Find("fr-FR", "Topics"));
    }

    [Fact]
    public void A_string_with_no_translation_is_reported_as_missing()
    {
        Assert.Null(Store.Find("el-GR", "a string nobody has ever translated"));
    }

    [Fact]
    public async Task Editing_a_translation_takes_effect_at_once()
    {
        // The cache is rebuilt on save; a reader on the next request sees the new value, not the
        // one from when the app started.
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<LocalizationService>();

        await service.SetAsync("Topics", "el-GR", "Ζητήματα");

        Assert.Equal("Ζητήματα", Store.Find("el-GR", "Topics"));
    }

    [Fact]
    public async Task Clearing_a_translation_falls_the_string_back_to_english()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<LocalizationService>();

        await service.SetAsync("Topics", "el-GR", "");

        Assert.Null(Store.Find("el-GR", "Topics"));
    }

    [Fact]
    public async Task Re_seeding_never_overwrites_an_edit()
    {
        // A translator's correction must survive the next deploy's seed run.
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<LocalizationService>();

        await service.SetAsync("Topics", "el-GR", "Ζητήματα");
        await service.SeedAsync();

        Assert.Equal("Ζητήματα", Store.Find("el-GR", "Topics"));
    }

    [Fact]
    public async Task The_admin_list_shows_a_row_for_every_string_whether_translated_or_not()
    {
        using var scope = _factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<LocalizationService>();

        var rows = await service.RowsAsync("el-GR");

        Assert.Contains(rows, r => r.Key == "Topics" && r.IsTranslated);
        Assert.All(rows, r => Assert.False(string.IsNullOrEmpty(r.Key)));
    }

    [Fact]
    public void Named_placeholders_survive_translation()
    {
        // The formatter fills the holes after translation, so the Greek can order them its way.
        var localizer = _factory.Services.GetRequiredService<IAppLocalizer>();

        // Seeded with a %count% hole; whatever the wording, the number lands in it.
        var text = localizer.Format("%count% new", ("count", 3));

        Assert.Contains("3", text);
    }

    [Fact]
    public async Task A_page_renders_in_greek_when_that_culture_is_chosen()
    {
        var client = _factory.CreateClient();

        // The query-string culture provider is enough to prove the pipeline end to end.
        var response = await client.GetAsync("/topics?culture=el-GR&ui-culture=el-GR");
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();

        // Greek renders as itself, not as &#x…; entities — the encoder is widened for exactly
        // this. Both the nav ("Topics") and the sort pill ("Most important") come back translated.
        Assert.Contains("Θέματα", html);
        Assert.Contains("Πιο σημαντικά", html);
        Assert.Contains("lang=\"el\"", html);
    }

    [Fact]
    public async Task The_same_page_stays_english_by_default()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/topics");
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Most important", html);
        Assert.DoesNotContain("Πιο σημαντικά", html);
    }

    [Fact]
    public async Task Choosing_a_language_is_a_post_that_a_get_cannot_stand_in_for()
    {
        // The switch changes state (a cookie), so it must not be reachable by a bare GET that a
        // foreign page could trigger.
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/culture?culture=el-GR");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
