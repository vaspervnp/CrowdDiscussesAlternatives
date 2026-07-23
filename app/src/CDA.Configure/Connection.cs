using System.Net;
using MySqlConnector;

namespace CDA.Configure;

/// <summary>Builds, checks and tests the database connection string.</summary>
public static class Connection
{
    /// <summary>The database the application expects, offered as the default.</summary>
    public const string DefaultDatabase = "CrowdDiscussesAlternatives";

    public const uint DefaultPort = 3306;

    /// <summary>
    /// The only transport the application accepts for a networked database, so it is the default
    /// here. See <c>RequireEncryptedConnection</c> in the app.
    /// </summary>
    public const MySqlSslMode DefaultSslMode = MySqlSslMode.VerifyFull;

    /// <summary>Assembles a connection string from its parts, applying the defaults above.</summary>
    public static string Build(
        string host, uint port, string database, string user, string password, MySqlSslMode sslMode)
    {
        var builder = new MySqlConnectionStringBuilder
        {
            Server = host,
            Port = port,
            Database = database,
            UserID = user,
            Password = password,
            SslMode = sslMode,
        };

        return builder.ConnectionString;
    }

    /// <summary>Parses a full connection string, throwing if it is malformed.</summary>
    public static MySqlConnectionStringBuilder Parse(string connectionString) => new(connectionString);

    /// <summary>
    /// The reason the application would refuse this connection string, or null if it would accept
    /// it. Mirrors the app's own guard so a bad setting is caught here, not at first boot.
    /// </summary>
    public static string? RejectionReason(MySqlConnectionStringBuilder builder)
    {
        if (string.IsNullOrWhiteSpace(builder.Server))
        {
            return "No server (host) was given.";
        }

        if (string.IsNullOrWhiteSpace(builder.UserID))
        {
            return "No user was given.";
        }

        if (!IsLoopback(builder.Server) &&
            builder.SslMode is not (MySqlSslMode.VerifyFull or MySqlSslMode.VerifyCA))
        {
            return $"SslMode={builder.SslMode} does not verify the server's certificate, and the " +
                "database is reached over a network. The application will refuse to start. Use " +
                "SslMode=VerifyFull.";
        }

        return null;
    }

    /// <summary>The connection string with the password blanked out, for printing.</summary>
    public static string Mask(string connectionString)
    {
        var builder = new MySqlConnectionStringBuilder(connectionString);

        if (!string.IsNullOrEmpty(builder.Password))
        {
            builder.Password = "********";
        }

        return builder.ConnectionString;
    }

    /// <summary>
    /// Opens the connection and runs a trivial query, so a wrong host, credential or certificate
    /// is found now rather than when the application first starts.
    /// </summary>
    public static async Task<(bool Ok, string Detail)> TestAsync(string connectionString)
    {
        try
        {
            await using var connection = new MySqlConnection(connectionString);

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await connection.OpenAsync(timeout.Token);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(timeout.Token);

            return (true, $"connected to {connection.ServerVersion}");
        }
        catch (Exception error)
        {
            return (false, error.Message);
        }
    }

    private static bool IsLoopback(string? server)
    {
        if (string.IsNullOrWhiteSpace(server))
        {
            return false;
        }

        if (server.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(server, out var address) && IPAddress.IsLoopback(address);
    }
}
