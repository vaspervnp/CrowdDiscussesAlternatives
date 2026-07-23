using CDA.Configure;
using CDA.Infrastructure;
using CDA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CDA.Migrate;

/// <summary>Where the connection string was found, for a clear message to the operator.</summary>
public enum ConnectionSource
{
    None,
    Flag,
    Environment,
    UserSecrets,
}

/// <summary>The migration state of the database.</summary>
/// <param name="Applied">Migrations already recorded in the database, oldest first.</param>
/// <param name="Pending">Migrations in the app but not yet applied, in the order they will run.</param>
public sealed record MigrationStatus(
    IReadOnlyList<string> Applied, IReadOnlyList<string> Pending);

/// <summary>
/// Brings the database schema up to date, without the .NET SDK or <c>dotnet ef</c>.
/// </summary>
/// <remarks>
/// The migrations are compiled into <c>CDA.Infrastructure</c>, so applying them is a runtime call
/// (<see cref="RelationalDatabaseFacadeExtensions.MigrateAsync"/>) that needs nothing installed on
/// the machine — which is the point on a self-contained deployment. The database options are built
/// exactly as the application builds them, reusing the application's own transport guard and
/// server version, so this tool cannot migrate against a connection the app would then refuse.
/// </remarks>
public static class Migrator
{
    /// <summary>
    /// The connection string to use, and where it came from: an explicit flag wins, then the
    /// <c>ConnectionStrings__Cda</c> environment variable, then the user-secrets store the
    /// <c>cda-configure</c> tool writes and the app reads.
    /// </summary>
    public static (string? ConnectionString, ConnectionSource Source) ResolveConnection(string? flag) =>
        Resolve(
            flag,
            Environment.GetEnvironmentVariable($"ConnectionStrings__{DependencyInjection.ConnectionStringName}"),
            SecretsStore.Read(SecretsStore.ConnectionStringKey));

    /// <summary>The precedence, as a pure function of the three candidate sources.</summary>
    public static (string? ConnectionString, ConnectionSource Source) Resolve(
        string? flag, string? environmentValue, string? userSecretsValue)
    {
        if (!string.IsNullOrWhiteSpace(flag))
        {
            return (flag, ConnectionSource.Flag);
        }

        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            return (environmentValue, ConnectionSource.Environment);
        }

        if (!string.IsNullOrWhiteSpace(userSecretsValue))
        {
            return (userSecretsValue, ConnectionSource.UserSecrets);
        }

        return (null, ConnectionSource.None);
    }

    private static CdaDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<CdaDbContext>()
            .UseMySql(
                // The same guard the app applies: an unverified transport to a networked
                // database is refused here rather than half-migrated and then rejected at boot.
                DependencyInjection.RequireEncryptedConnection(connectionString),
                DependencyInjection.ServerVersion,
                mySql => mySql
                    .MigrationsAssembly(typeof(CdaDbContext).Assembly.FullName)
                    .EnableRetryOnFailure(
                        maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), null))
            .Options;

        return new CdaDbContext(options);
    }

    /// <summary>Reads what is applied and what is pending, without changing anything.</summary>
    public static async Task<MigrationStatus> StatusAsync(
        string connectionString, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext(connectionString);

        var applied = await context.Database.GetAppliedMigrationsAsync(cancellationToken);
        var pending = await context.Database.GetPendingMigrationsAsync(cancellationToken);

        return new MigrationStatus([.. applied], [.. pending]);
    }

    /// <summary>
    /// Applies every pending migration, returning the ones it ran (empty if already up to date).
    /// </summary>
    public static async Task<IReadOnlyList<string>> UpdateAsync(
        string connectionString, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext(connectionString);

        var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

        if (pending.Count > 0)
        {
            await context.Database.MigrateAsync(cancellationToken);
        }

        return pending;
    }
}
