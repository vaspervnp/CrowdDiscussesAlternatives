using MySqlConnector;

namespace CDA.IntegrationTests;

/// <summary>
/// Guards which database the integration tests are allowed to touch.
/// </summary>
/// <remarks>
/// Integration tests truncate every table between test classes. There is no Docker and no
/// per-run schema on this server, so the only thing standing between a test run and the
/// development database is the name of the database in the connection string. That makes
/// this check a hard precondition rather than a convention: every other mistake here is
/// recoverable, this one silently destroys work.
/// </remarks>
public static class TestDatabase
{
    /// <summary>Suffix every database used by the integration tests must carry.</summary>
    public const string RequiredSuffix = "_Test";

    public static string EnsureIsTestDatabase(string connectionString)
    {
        var database = new MySqlConnectionStringBuilder(connectionString).Database;

        if (string.IsNullOrWhiteSpace(database))
        {
            throw new InvalidOperationException(
                "The integration-test connection string names no database. Expected one ending " +
                $"in '{RequiredSuffix}'.");
        }

        if (!database.EndsWith(RequiredSuffix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Refusing to run integration tests against database '{database}': these tests " +
                $"truncate every table, and only a database whose name ends in '{RequiredSuffix}' " +
                "is considered disposable. Point ConnectionStrings:CdaTest at " +
                "CrowdDiscussesAlternatives_Test.");
        }

        return connectionString;
    }
}
