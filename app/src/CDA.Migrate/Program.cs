using CDA.Configure;
using CDA.Migrate;

// Brings the database schema up to date on a machine that has the published app but no .NET SDK,
// so `dotnet ef database update` is not available. The migrations are compiled into the app, so
// this only needs the connection string — which it reads from the same place the app does.
//
// Exit codes: 0 success, 1 bad input or no connection string, 2 the database operation failed.

var command = args.FirstOrDefault(a => !a.StartsWith('-')) ?? "update";
var wantsHelp = args.Any(a => a is "--help" or "-h" or "-?");
var connectionFlag = ValueOf("--connection");

if (wantsHelp || command is "help")
{
    Help();
    return 0;
}

if (command is not ("update" or "status"))
{
    Console.Error.WriteLine($"error: unknown command '{command}'. Run --help for usage.");
    return 1;
}

var (connectionString, source) = Migrator.ResolveConnection(connectionFlag);

if (connectionString is null)
{
    Console.Error.WriteLine(
        "error: no connection string. Set one first with cda-configure, or pass --connection, " +
        "or set the ConnectionStrings__Cda environment variable.");
    return 1;
}

Console.WriteLine($"Database:   {Connection.Mask(connectionString)}");
Console.WriteLine($"From:       {Describe(source)}");
Console.WriteLine();

try
{
    return command is "status"
        ? await ShowStatus(connectionString)
        : await Update(connectionString);
}
catch (InvalidOperationException error)
{
    // The transport guard rejects an unverified connection string with this.
    Console.Error.WriteLine($"error: {error.Message}");
    return 1;
}
catch (Exception error)
{
    Console.Error.WriteLine($"error: could not reach or migrate the database.");
    Console.Error.WriteLine($"  {error.Message}");
    return 2;
}

static async Task<int> ShowStatus(string connectionString)
{
    var status = await Migrator.StatusAsync(connectionString);

    Console.WriteLine($"Applied ({status.Applied.Count}):");
    foreach (var migration in status.Applied)
    {
        Console.WriteLine($"  ✓ {migration}");
    }

    Console.WriteLine();

    if (status.Pending.Count == 0)
    {
        Console.WriteLine("Pending: none — the database is up to date.");
    }
    else
    {
        Console.WriteLine($"Pending ({status.Pending.Count}):");
        foreach (var migration in status.Pending)
        {
            Console.WriteLine($"  … {migration}");
        }
    }

    return 0;
}

static async Task<int> Update(string connectionString)
{
    var status = await Migrator.StatusAsync(connectionString);

    if (status.Pending.Count == 0)
    {
        Console.WriteLine("The database is already up to date. Nothing to apply.");
        return 0;
    }

    Console.WriteLine($"Applying {status.Pending.Count} migration(s):");
    foreach (var migration in status.Pending)
    {
        Console.WriteLine($"  … {migration}");
    }

    var applied = await Migrator.UpdateAsync(connectionString);

    Console.WriteLine();
    Console.WriteLine($"Done. Applied {applied.Count} migration(s).");
    return 0;
}

// Reads "--name value" or "--name=value" from the arguments, or null if absent.
string? ValueOf(string name)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i] == name)
        {
            return i + 1 < args.Length ? args[i + 1] : null;
        }

        if (args[i].StartsWith(name + "=", StringComparison.Ordinal))
        {
            return args[i][(name.Length + 1)..];
        }
    }

    return null;
}

static string Describe(ConnectionSource source) => source switch
{
    ConnectionSource.Flag => "--connection flag",
    ConnectionSource.Environment => "ConnectionStrings__Cda environment variable",
    ConnectionSource.UserSecrets => "user-secrets store (set by cda-configure)",
    _ => "nowhere",
};

static void Help()
{
    Console.WriteLine(
        """
        cda-migrate — apply the database schema migrations for Crowd Discusses Alternatives.

        Does the same job as `dotnet ef database update`, but needs no .NET SDK: the migrations
        are compiled into the app, so this only needs the connection string. It reads that from
        --connection, then the ConnectionStrings__Cda environment variable, then the user-secrets
        store that cda-configure writes — the same place the app reads it.

        USAGE
          cda-migrate                    Apply every pending migration (same as `update`).
          cda-migrate update             Apply every pending migration.
          cda-migrate status             Show which migrations are applied and which are pending.
          cda-migrate --help             Show this help.

        OPTIONS
          --connection "<full string>"   Use this connection string instead of the saved one.

        Run this once after cda-configure and before starting the app for the first time, and
        again after deploying a build that adds migrations.
        """);
}
