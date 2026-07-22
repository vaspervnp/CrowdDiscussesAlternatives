using System.Net;
using CDA.Domain.Users;
using CDA.Infrastructure.Identity;
using CDA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CDA.IntegrationTests;

[Collection(DatabaseCollection.Name)]
public class AccountTests(DatabaseFixture database) : IAsyncLifetime
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

    private async Task<RegistrationResult> RegisterAsync(string email, string displayName)
    {
        using var scope = _factory.Services.CreateScope();
        var accounts = scope.ServiceProvider.GetRequiredService<UserAccountService>();

        return await accounts.RegisterAsync(email, displayName, "correct-horse-battery");
    }

    [Fact]
    public async Task Registering_creates_an_account_and_a_profile()
    {
        var result = await RegisterAsync("maria@example.com", "Maria");

        Assert.True(result.Succeeded, string.Join("; ", result.Errors));

        await using var context = database.CreateContext();
        var profile = await context.UserProfiles.SingleAsync(p => p.Id == result.UserId);

        Assert.Equal("Maria", profile.DisplayName);
        // The account is usable immediately, and reveals nothing until its owner says so.
        Assert.Equal(ProfileVisibility.Private, profile.VisibilityOf(ProfileField.Email));
    }

    [Fact]
    public async Task A_display_name_cannot_be_taken_twice()
    {
        // Two participants under one name could be mistaken for each other mid-discussion.
        await RegisterAsync("first@example.com", "Nikos");

        var second = await RegisterAsync("second@example.com", "Nikos");

        Assert.False(second.Succeeded);
        Assert.Contains(second.Errors, error => error.Contains("already taken"));
    }

    [Fact]
    public async Task A_failed_registration_leaves_no_account_behind()
    {
        // The Identity user and the profile are written in one transaction; without it, the
        // rejected registration below would still leave a sign-in-able account.
        await RegisterAsync("first@example.com", "Eleni");

        await RegisterAsync("orphan@example.com", "Eleni");

        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CdaDbContext>();

        Assert.Null(await context.Users.SingleOrDefaultAsync(u => u.Email == "orphan@example.com"));
    }

    [Fact]
    public async Task An_email_cannot_be_registered_twice()
    {
        await RegisterAsync("duplicate@example.com", "Yiannis");

        var second = await RegisterAsync("duplicate@example.com", "Different Name");

        Assert.False(second.Succeeded);
    }

    [Theory]
    [InlineData("short", false)]
    [InlineData("elevenchars", false)]           // 11 — one under the minimum
    [InlineData("twelvecharss", true)]           // 12 — no digit, no capital, still fine
    [InlineData("a long spoken passphrase", true)]
    public async Task Password_strength_is_judged_on_length_alone(string password, bool expected)
    {
        using var scope = _factory.Services.CreateScope();
        var accounts = scope.ServiceProvider.GetRequiredService<UserAccountService>();

        var result = await accounts.RegisterAsync(
            $"pw-{Guid.NewGuid():N}@example.com", $"User {Guid.NewGuid():N}"[..20], password);

        Assert.Equal(expected, result.Succeeded);
    }

    [Fact]
    public async Task An_anonymous_visitor_sees_only_the_display_name_of_a_new_profile()
    {
        var result = await RegisterAsync("private@example.com", "Dimitra");

        await using (var context = database.CreateContext())
        {
            var profile = await context.UserProfiles.SingleAsync(p => p.Id == result.UserId);
            profile.EditDetails("Dimitra K.", "dimitra@example.com", "Athens", null, "Hello.");
            await context.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var html = await client.GetStringAsync($"/profiles/{result.UserId}");

        Assert.Contains("Dimitra", html);
        // Defaults are Private, so none of the details reach an anonymous response body.
        Assert.DoesNotContain("Dimitra K.", html);
        Assert.DoesNotContain("dimitra@example.com", html);
        Assert.DoesNotContain("Athens", html);
    }

    [Fact]
    public async Task Editing_a_profile_requires_signing_in()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/profiles/me");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task An_unknown_profile_is_not_found()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/profiles/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
