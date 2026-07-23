using CDA.Configure;
using MySqlConnector;

namespace CDA.UnitTests.Configure;

public class ConnectionTests
{
    [Fact]
    public void A_built_string_carries_every_part()
    {
        var built = Connection.Build("db.example.com", 3306, "CrowdDiscussesAlternatives",
            "crowd", "a secret", MySqlSslMode.VerifyFull);

        var parsed = Connection.Parse(built);

        Assert.Equal("db.example.com", parsed.Server);
        Assert.Equal(3306u, parsed.Port);
        Assert.Equal("CrowdDiscussesAlternatives", parsed.Database);
        Assert.Equal("crowd", parsed.UserID);
        Assert.Equal("a secret", parsed.Password);
        Assert.Equal(MySqlSslMode.VerifyFull, parsed.SslMode);
    }

    [Fact]
    public void An_unverified_transport_to_a_network_host_is_refused()
    {
        // Mirrors the application's own guard: it would refuse to start on this string.
        var built = Connection.Build("db.example.com", 3306, "db", "u", "p", MySqlSslMode.None);

        Assert.NotNull(Connection.RejectionReason(Connection.Parse(built)));
    }

    [Fact]
    public void Verified_transport_to_a_network_host_is_accepted()
    {
        var built = Connection.Build("db.example.com", 3306, "db", "u", "p", MySqlSslMode.VerifyFull);

        Assert.Null(Connection.RejectionReason(Connection.Parse(built)));
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("127.0.0.1")]
    public void A_loopback_host_may_skip_verification(string host)
    {
        // Nothing crosses a network, so there is nothing to verify against.
        var built = Connection.Build(host, 3306, "db", "u", "p", MySqlSslMode.None);

        Assert.Null(Connection.RejectionReason(Connection.Parse(built)));
    }

    [Fact]
    public void A_missing_host_or_user_is_refused()
    {
        Assert.NotNull(Connection.RejectionReason(Connection.Parse("User ID=u;Password=p")));
        Assert.NotNull(Connection.RejectionReason(Connection.Parse("Server=localhost;Password=p")));
    }

    [Fact]
    public void Masking_hides_the_password_and_keeps_the_rest()
    {
        var built = Connection.Build("db.example.com", 3306, "db", "crowd", "hunter2", MySqlSslMode.VerifyFull);

        var masked = Connection.Mask(built);

        Assert.DoesNotContain("hunter2", masked);
        Assert.Contains("db.example.com", masked);
        Assert.Contains("crowd", masked);
    }

    [Fact]
    public void The_secrets_path_targets_the_apps_store()
    {
        var path = SecretsStore.Path();

        Assert.Contains(SecretsStore.UserSecretsId, path);
        Assert.EndsWith("secrets.json", path);
    }
}
