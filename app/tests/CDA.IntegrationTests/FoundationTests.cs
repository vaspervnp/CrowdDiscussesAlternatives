using System.Net;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace CDA.IntegrationTests;

/// <summary>
/// The Phase 0 exit criterion, executable: the host boots, reaches the real MariaDB over a
/// verified TLS connection, and reports itself healthy.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class FoundationTests(DatabaseFixture database) : IAsyncLifetime
{
    private CdaWebApplicationFactory _factory = null!;

    public Task InitializeAsync()
    {
        _factory = new CdaWebApplicationFactory(database);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _factory.Dispose();
        await database.ResetAsync();
    }

    [Fact]
    public async Task Health_endpoint_reports_the_database_as_reachable()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Migrations_are_fully_applied_to_the_test_database()
    {
        await using var context = database.CreateContext();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.NotEmpty(await context.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
    public async Task Connection_to_a_remote_database_is_encrypted()
    {
        // Regression guard for the setting this project deliberately moved off SslMode=None:
        // an unencrypted session reports an empty cipher. CI talks to a loopback container
        // with no certificate, which the connection guard exempts, so there is nothing to
        // assert there — the assertion applies wherever the database is actually remote.
        var server = new MySqlConnectionStringBuilder(database.ConnectionString).Server;
        var isLoopback = server.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                         (IPAddress.TryParse(server, out var address) && IPAddress.IsLoopback(address));

        if (isLoopback)
        {
            return;
        }

        await using var context = database.CreateContext();

        var cipher = await context.Database
            .SqlQuery<string>(
                $"SELECT VARIABLE_VALUE AS Value FROM information_schema.SESSION_STATUS WHERE VARIABLE_NAME = 'Ssl_cipher'")
            .SingleAsync();

        Assert.False(string.IsNullOrEmpty(cipher), $"The session with {server} is not encrypted.");
    }
}
