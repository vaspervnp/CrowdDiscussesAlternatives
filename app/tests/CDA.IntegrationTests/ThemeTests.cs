namespace CDA.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class ThemeTests(DatabaseFixture database) : IAsyncLifetime
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

    [Fact]
    public async Task Every_page_carries_the_theme_switch()
    {
        var html = await (await _factory.CreateClient().GetAsync("/")).Content.ReadAsStringAsync();

        Assert.Contains("id=\"theme-switch\"", html);
    }

    [Fact]
    public async Task The_theme_is_set_before_the_body_paints()
    {
        // The inline head script is what prevents a dark-mode reader seeing a flash of light. It
        // must sit in <head>, ahead of the body, and set data-bs-theme from the cookie or the OS
        // preference — so its presence and position are worth pinning.
        var html = await (await _factory.CreateClient().GetAsync("/")).Content.ReadAsStringAsync();

        var scriptAt = html.IndexOf("data-bs-theme", StringComparison.Ordinal);
        var bodyAt = html.IndexOf("<body", StringComparison.Ordinal);

        Assert.True(scriptAt >= 0, "the theme-setting script is missing");
        Assert.True(bodyAt >= 0 && scriptAt < bodyAt, "the theme must be set before the body");
        Assert.Contains("prefers-color-scheme", html);
    }
}
