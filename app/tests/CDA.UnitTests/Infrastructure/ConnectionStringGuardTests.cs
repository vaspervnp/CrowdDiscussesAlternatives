using CDA.Infrastructure;
using MySqlConnector;

namespace CDA.UnitTests.Infrastructure;

/// <summary>
/// The guard exists because the database is reached over the public internet and the
/// original connection string shipped with <c>SslMode=None</c>. A downgrade is silent at
/// runtime — everything still works, it is just unencrypted — so it has to fail loudly here.
/// </summary>
public class ConnectionStringGuardTests
{
    private const string Base =
        "Server=example.test;Port=3306;Database=Cda;User ID=u;Password=p;";

    [Theory]
    [InlineData("None")]
    [InlineData("Preferred")]
    [InlineData("Required")]
    public void Rejects_modes_that_do_not_verify_the_certificate(string sslMode)
    {
        var act = () => DependencyInjection.RequireEncryptedConnection($"{Base}SslMode={sslMode};");

        var exception = Assert.Throws<InvalidOperationException>(act);
        Assert.Contains("VerifyFull", exception.Message);
    }

    [Theory]
    [InlineData("VerifyFull")]
    [InlineData("VerifyCA")]
    public void Accepts_modes_that_verify_the_certificate(string sslMode)
    {
        var result = DependencyInjection.RequireEncryptedConnection($"{Base}SslMode={sslMode};");

        Assert.Equal(sslMode, new MySqlConnectionStringBuilder(result).SslMode.ToString());
    }

    [Fact]
    public void Rejects_a_connection_string_that_omits_SslMode_entirely()
    {
        // MySqlConnector defaults to Preferred, which encrypts but verifies nothing.
        Assert.Throws<InvalidOperationException>(
            () => DependencyInjection.RequireEncryptedConnection(Base));
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("LOCALHOST")]
    [InlineData("127.0.0.1")]
    [InlineData("127.0.0.2")]
    [InlineData("::1")]
    public void Allows_an_unverified_transport_only_when_the_server_is_loopback(string server)
    {
        // Loopback traffic never reaches a network, and CI's throwaway MariaDB container has
        // no CA to verify against. Nothing is protected by demanding a certificate there.
        var connectionString = $"Server={server};Port=3306;Database=Cda;User ID=u;Password=p;SslMode=None;";

        var result = DependencyInjection.RequireEncryptedConnection(connectionString);

        Assert.Equal(MySqlSslMode.None, new MySqlConnectionStringBuilder(result).SslMode);
    }

    [Theory]
    [InlineData("db.example.com")]
    [InlineData("10.0.0.5")]
    [InlineData("192.168.1.10")]
    [InlineData("localhost.attacker.example")]
    public void Does_not_extend_the_loopback_exception_to_anything_reachable_over_a_network(string server)
    {
        // Private and look-alike addresses are still networks; only true loopback qualifies.
        var connectionString = $"Server={server};Port=3306;Database=Cda;User ID=u;Password=p;SslMode=None;";

        Assert.Throws<InvalidOperationException>(
            () => DependencyInjection.RequireEncryptedConnection(connectionString));
    }

    [Fact]
    public void Caps_the_pool_when_the_connection_string_does_not()
    {
        // max_connections is 151 for the whole shared server; an uncapped pool lets this
        // one application exhaust a resource it does not own.
        var result = DependencyInjection.RequireEncryptedConnection($"{Base}SslMode=VerifyFull;");

        Assert.Equal(20u, new MySqlConnectionStringBuilder(result).MaximumPoolSize);
    }

    [Fact]
    public void Leaves_an_explicit_pool_size_alone()
    {
        var result = DependencyInjection.RequireEncryptedConnection(
            $"{Base}SslMode=VerifyFull;MaximumPoolSize=5;");

        Assert.Equal(5u, new MySqlConnectionStringBuilder(result).MaximumPoolSize);
    }
}
