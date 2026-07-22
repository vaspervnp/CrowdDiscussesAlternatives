using CDA.Infrastructure;
using CDA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MySqlConnector;

namespace CDA.IntegrationTests;

/// <summary>
/// Owns the integration-test database: resolves its connection string, verifies it is
/// disposable, and brings the schema up to date once per test run.
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    public const string ConnectionStringName = "CdaTest";

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<DatabaseFixture>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"No connection string named '{ConnectionStringName}' was found. Set it in user " +
                "secrets:\n\n  dotnet user-secrets set \"ConnectionStrings:CdaTest\" " +
                "\"<connection string>\" --project app/tests/CDA.IntegrationTests\n\n" +
                $"or supply ConnectionStrings__{ConnectionStringName} in the environment. It must " +
                $"name a database ending in '{TestDatabase.RequiredSuffix}'.");
        }

        // Both guards apply: the transport must be verified, and the target must be disposable.
        ConnectionString = TestDatabase.EnsureIsTestDatabase(
            DependencyInjection.RequireEncryptedConnection(connectionString));

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public CdaDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CdaDbContext>()
            .UseMySql(ConnectionString, DependencyInjection.ServerVersion)
            .Options;

        return new CdaDbContext(options);
    }

    /// <summary>
    /// Empties every table. Called between test classes rather than per test, because the
    /// suite shares one schema and cannot create a fresh one per run.
    /// </summary>
    public async Task ResetAsync()
    {
        await using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync();

        var database = connection.Database;
        var tables = new List<string>();

        await using (var read = new MySqlCommand(
            """
            SELECT table_name FROM information_schema.tables
            WHERE table_schema = @schema AND table_type = 'BASE TABLE'
              AND table_name <> '__EFMigrationsHistory'
            """, connection))
        {
            read.Parameters.AddWithValue("@schema", database);
            await using var reader = await read.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
        }

        if (tables.Count == 0)
        {
            return;
        }

        // Foreign keys are cyclic across the model (proposals reference topics, groups
        // reference both), so ordering the truncations is not possible in general.
        await using var truncate = connection.CreateCommand();
        truncate.CommandText =
            "SET FOREIGN_KEY_CHECKS = 0; " +
            string.Concat(tables.Select(t => $"TRUNCATE TABLE `{t}`; ")) +
            "SET FOREIGN_KEY_CHECKS = 1;";
        await truncate.ExecuteNonQueryAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

/// <summary>
/// One shared schema means database tests cannot run concurrently. Pure unit tests live in
/// CDA.UnitTests and keep parallelising.
/// </summary>
[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "Database";
}
