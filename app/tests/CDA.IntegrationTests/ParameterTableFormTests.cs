using System.Net;
using System.Text.RegularExpressions;
using CDA.Domain.Topics;
using CDA.Infrastructure.Identity;
using CDA.Infrastructure.Parameters;
using CDA.Infrastructure.Topics;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CDA.IntegrationTests;

/// <summary>
/// Drives the sharing form over HTTP rather than calling the service.
/// </summary>
/// <remarks>
/// These exist because the service tests all passed while sharing was broken in the browser.
/// The view wrote <c>value="@(!table.IsShared)"</c>, and Razor treats a boolean-valued
/// attribute as an HTML boolean attribute — <c>true</c> renders <c>value="value"</c> and
/// <c>false</c> omits the attribute entirely — so the form posted the literal string "value"
/// and the flag never changed. Nothing below the view could have caught that.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public partial class ParameterTableFormTests(DatabaseFixture database) : IAsyncLifetime
{
    private const string Password = "a long spoken passphrase";

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

    [GeneratedRegex("""name="__RequestVerificationToken"[^>]*value="([^"]+)""")]
    private static partial Regex AntiforgeryToken();

    [GeneratedRegex("""""<input type="hidden" name="shared" value="([^"]*)""""")]
    private static partial Regex SharedField();

    private static string TokenFrom(string html)
    {
        var match = AntiforgeryToken().Match(html);
        Assert.True(match.Success, "no antiforgery token in the page");
        return match.Groups[1].Value;
    }

    /// <summary>A client already signed in as a freshly registered participant.</summary>
    private async Task<(HttpClient Client, Guid UserId)> SignedInClientAsync(string name)
    {
        Guid userId;

        using (var scope = _factory.Services.CreateScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<UserAccountService>()
                .RegisterAsync($"{name}@example.com", name, Password);
            Assert.True(result.Succeeded, string.Join("; ", result.Errors));
            userId = result.UserId;
        }

        // https, not http: the authentication cookie is issued with Secure always set, so a
        // client on a plain-http base address silently drops it and every request afterwards
        // arrives anonymous.
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true,
            BaseAddress = new Uri("https://localhost"),
        });

        var login = await client.GetStringAsync("/Account/Login");

        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("Email", $"{name}@example.com"),
            new KeyValuePair<string, string>("Password", Password),
            new KeyValuePair<string, string>("__RequestVerificationToken", TokenFrom(login)),
        ]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (client, userId);
    }

    private async Task<(Guid TopicId, Guid TableId)> TableAsync(Guid ownerId)
    {
        using var scope = _factory.Services.CreateScope();

        var topic = await scope.ServiceProvider.GetRequiredService<TopicService>()
            .CreateAsync("How should we reduce traffic?", "", ownerId, TopicVisibility.Public, null);

        var table = await scope.ServiceProvider.GetRequiredService<ParameterTableService>()
            .CreateAsync(topic.Id, ownerId, "Traffic factors", ["Charging", "Shop takings"]);

        Assert.True(table.Succeeded, table.Error);

        return (topic.Id, table.Id);
    }

    [Fact]
    public async Task The_sharing_form_posts_a_value_that_binds_to_a_boolean()
    {
        var (client, owner) = await SignedInClientAsync("owner");
        var (topicId, tableId) = await TableAsync(owner);

        var html = await client.GetStringAsync($"/topics/{topicId}/factors/{tableId}");
        var field = SharedField().Match(html);

        Assert.True(field.Success, "the sharing form has no hidden 'shared' field");

        // The bug this guards against rendered the literal string "value" here.
        Assert.True(
            bool.TryParse(field.Groups[1].Value, out _),
            $"'shared' rendered as \"{field.Groups[1].Value}\", which will not bind to a boolean");
    }

    [Fact]
    public async Task Submitting_the_form_actually_shares_the_table()
    {
        var (client, owner) = await SignedInClientAsync("owner");
        var (topicId, tableId) = await TableAsync(owner);

        var html = await client.GetStringAsync($"/topics/{topicId}/factors/{tableId}");

        var response = await client.PostAsync(
            $"/topics/{topicId}/factors/{tableId}/share",
            new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("shared", SharedField().Match(html).Groups[1].Value),
                new KeyValuePair<string, string>("__RequestVerificationToken", TokenFrom(html)),
            ]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var context = database.CreateContext();
        var table = await context.ParameterTables.AsNoTracking().SingleAsync(t => t.Id == tableId);

        Assert.True(table.IsShared);
    }

    [Fact]
    public async Task Submitting_it_again_stops_sharing()
    {
        // The same form toggles, so the second post has to carry the opposite value.
        var (client, owner) = await SignedInClientAsync("owner");
        var (topicId, tableId) = await TableAsync(owner);

        for (var round = 0; round < 2; round++)
        {
            var html = await client.GetStringAsync($"/topics/{topicId}/factors/{tableId}");

            await client.PostAsync(
                $"/topics/{topicId}/factors/{tableId}/share",
                new FormUrlEncodedContent(
                [
                    new KeyValuePair<string, string>("shared", SharedField().Match(html).Groups[1].Value),
                    new KeyValuePair<string, string>("__RequestVerificationToken", TokenFrom(html)),
                ]));
        }

        await using var context = database.CreateContext();
        var table = await context.ParameterTables.AsNoTracking().SingleAsync(t => t.Id == tableId);

        Assert.False(table.IsShared);
    }

    [Fact]
    public async Task An_unshared_table_is_not_reachable_by_anyone_else()
    {
        var (_, owner) = await SignedInClientAsync("owner");
        var (topicId, tableId) = await TableAsync(owner);
        var (stranger, _) = await SignedInClientAsync("stranger");

        var response = await stranger.GetAsync($"/topics/{topicId}/factors/{tableId}");

        // The same answer as "no such table", so the response does not confirm that someone
        // has a private sketch here.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
