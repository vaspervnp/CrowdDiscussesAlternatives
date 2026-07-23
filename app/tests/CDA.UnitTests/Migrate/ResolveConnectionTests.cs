using CDA.Migrate;

namespace CDA.UnitTests.Migrate;

public class ResolveConnectionTests
{
    [Fact]
    public void The_flag_wins_over_everything()
    {
        var (value, source) = Migrator.Resolve("from-flag", "from-env", "from-secrets");

        Assert.Equal("from-flag", value);
        Assert.Equal(ConnectionSource.Flag, source);
    }

    [Fact]
    public void The_environment_variable_beats_the_saved_secret()
    {
        var (value, source) = Migrator.Resolve(flag: null, "from-env", "from-secrets");

        Assert.Equal("from-env", value);
        Assert.Equal(ConnectionSource.Environment, source);
    }

    [Fact]
    public void The_user_secret_is_used_when_it_is_all_there_is()
    {
        var (value, source) = Migrator.Resolve(flag: null, environmentValue: null, "from-secrets");

        Assert.Equal("from-secrets", value);
        Assert.Equal(ConnectionSource.UserSecrets, source);
    }

    [Fact]
    public void Nothing_configured_is_reported_as_such()
    {
        var (value, source) = Migrator.Resolve(null, null, null);

        Assert.Null(value);
        Assert.Equal(ConnectionSource.None, source);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_source_is_skipped(string blank)
    {
        // An empty environment variable must not shadow a real saved secret.
        var (value, source) = Migrator.Resolve(flag: blank, environmentValue: blank, "from-secrets");

        Assert.Equal("from-secrets", value);
        Assert.Equal(ConnectionSource.UserSecrets, source);
    }
}
