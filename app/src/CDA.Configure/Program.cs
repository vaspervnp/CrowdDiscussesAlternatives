using CDA.Configure;
using MySqlConnector;

// A tiny configuration tool for a machine that has the published app but no .NET SDK — so
// `dotnet user-secrets set` is not available. It writes the database connection string into the
// very same user-secrets store the app reads, either from a short wizard or from flags.
//
// Exit codes: 0 saved (or a read-only command succeeded), 1 bad input, 2 the connection test
// failed and nothing was saved.

var options = Args.Parse(args);

if (options.Help)
{
    Help();
    return 0;
}

if (options.ShowPath)
{
    Console.WriteLine(SecretsStore.Path());
    return 0;
}

if (options.Show)
{
    var saved = SecretsStore.Read(SecretsStore.ConnectionStringKey);
    Console.WriteLine(saved is null
        ? $"No connection string is saved yet.\nStore: {SecretsStore.Path()}"
        : $"{SecretsStore.ConnectionStringKey} = {Connection.Mask(saved)}\nStore: {SecretsStore.Path()}");
    return 0;
}

string connectionString;

try
{
    connectionString = options.HasBuildInputs
        ? FromFlags(options)
        : RunWizard();
}
catch (BadInputException error)
{
    Console.Error.WriteLine($"error: {error.Message}");
    return 1;
}

// Refuse the same connection strings the application would refuse, so a bad setting is caught
// here rather than at first boot.
var rejection = Connection.RejectionReason(Connection.Parse(connectionString));
if (rejection is not null)
{
    Console.Error.WriteLine($"error: {rejection}");
    return 1;
}

Console.WriteLine();
Console.WriteLine($"Connection string: {Connection.Mask(connectionString)}");

if (!options.NoTest)
{
    Console.Write("Testing the connection… ");
    var (ok, detail) = await Connection.TestAsync(connectionString);
    Console.WriteLine(ok ? $"OK ({detail})" : "FAILED");

    if (!ok)
    {
        Console.Error.WriteLine($"  {detail}");

        // In the wizard, offer to save anyway (the server may simply be down right now). Driven
        // by flags, do not silently persist something that does not work — exit and let the
        // caller pass --no-test if that is what they meant.
        if (options.HasBuildInputs || !Confirm("Save it anyway?", defaultYes: false))
        {
            Console.Error.WriteLine("Nothing was saved. Re-run with --no-test to skip this check.");
            return 2;
        }
    }
}

SecretsStore.Write(SecretsStore.ConnectionStringKey, connectionString);

Console.WriteLine();
Console.WriteLine($"Saved to {SecretsStore.Path()}");
Console.WriteLine($"Running as user: {Environment.UserName}");
Console.WriteLine("The app must run as this same user, since user secrets live in the user's profile.");
return 0;

// --- building the connection string ----------------------------------------------------------

static string FromFlags(Args options)
{
    if (options.Connection is not null)
    {
        // A full connection string given verbatim; validate it by parsing.
        try
        {
            _ = Connection.Parse(options.Connection);
        }
        catch (Exception error)
        {
            throw new BadInputException($"--connection is not a valid connection string: {error.Message}");
        }

        return options.Connection;
    }

    var host = options.Host ?? throw new BadInputException("--host is required (or pass --connection).");
    var user = options.User ?? throw new BadInputException("--user is required (or pass --connection).");
    var password = options.Password
        ?? throw new BadInputException("--password is required (or pass --connection).");

    return Connection.Build(
        host,
        ParsePort(options.Port) ?? Connection.DefaultPort,
        options.Database ?? Connection.DefaultDatabase,
        user,
        password,
        ParseSslMode(options.SslMode) ?? Connection.DefaultSslMode);
}

static string RunWizard()
{
    if (Console.IsInputRedirected)
    {
        throw new BadInputException(
            "No input available for the wizard. Pass the settings as flags, e.g.\n" +
            "  cda-configure --host db.example.com --user crowd --password '…'");
    }

    Console.WriteLine("Configure the database connection for Crowd Discusses Alternatives.");
    Console.WriteLine("Press Enter to accept the [default] shown.");
    Console.WriteLine();

    var host = AskRequired("Database host");
    var port = ParsePort(Ask("Port", Connection.DefaultPort.ToString())) ?? Connection.DefaultPort;
    var database = Ask("Database name", Connection.DefaultDatabase);
    var user = AskRequired("User");
    var password = AskPassword("Password");
    var sslMode = ParseSslMode(Ask("SslMode", Connection.DefaultSslMode.ToString())) ?? Connection.DefaultSslMode;

    return Connection.Build(host, port, database, user, password, sslMode);
}

static uint? ParsePort(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return null;
    }

    return uint.TryParse(value, out var port)
        ? port
        : throw new BadInputException($"'{value}' is not a valid port.");
}

static MySqlSslMode? ParseSslMode(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return null;
    }

    return Enum.TryParse<MySqlSslMode>(value, ignoreCase: true, out var mode)
        ? mode
        : throw new BadInputException(
            $"'{value}' is not a valid SslMode. Use one of: {string.Join(", ", Enum.GetNames<MySqlSslMode>())}.");
}

// --- console prompts ---------------------------------------------------------------------------

static string Ask(string label, string? initial)
{
    Console.Write(initial is null ? $"{label}: " : $"{label} [{initial}]: ");
    var entered = Console.ReadLine();
    return string.IsNullOrWhiteSpace(entered) ? initial ?? string.Empty : entered.Trim();
}

static string AskRequired(string label)
{
    while (true)
    {
        var value = Ask(label, null);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        Console.WriteLine($"  {label} is required.");
    }
}

static string AskPassword(string label)
{
    Console.Write($"{label}: ");

    if (Console.IsInputRedirected)
    {
        return Console.ReadLine() ?? string.Empty;
    }

    var password = new System.Text.StringBuilder();

    while (true)
    {
        var key = Console.ReadKey(intercept: true);

        if (key.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            return password.ToString();
        }

        if (key.Key == ConsoleKey.Backspace)
        {
            if (password.Length > 0)
            {
                password.Length--;
                Console.Write("\b \b");
            }
        }
        else if (!char.IsControl(key.KeyChar))
        {
            password.Append(key.KeyChar);
            Console.Write('*');
        }
    }
}

static bool Confirm(string question, bool defaultYes)
{
    Console.Write($"{question} [{(defaultYes ? "Y/n" : "y/N")}]: ");
    var answer = Console.ReadLine()?.Trim();

    if (string.IsNullOrEmpty(answer))
    {
        return defaultYes;
    }

    return answer.StartsWith('y') || answer.StartsWith('Y');
}

static void Help()
{
    Console.WriteLine(
        """
        cda-configure — set the database connection string for Crowd Discusses Alternatives.

        Writes into the app's user-secrets store, so the credential stays out of the deployment
        folder and out of the repository. Does the same job as:

          dotnet user-secrets set "ConnectionStrings:Cda" "…" --project src/CDA.Web

        but needs no .NET SDK on the machine.

        USAGE
          cda-configure                       Run the interactive wizard.
          cda-configure [build flags]         Set it non-interactively.
          cda-configure --show                Show the saved connection string (password masked).
          cda-configure --path                Print the path to the secrets file.
          cda-configure --help                Show this help.

        BUILD FLAGS
          --connection "<full string>"        Use a complete connection string verbatim.
          --host <host>                       Database host                (required)
          --user <user>                       Database user                (required)
          --password <password>               Database password            (required)
          --port <port>                       default 3306
          --database <name>                   default CrowdDiscussesAlternatives
          --sslmode <mode>                    default VerifyFull
          --no-test                           Do not open a test connection before saving.

        NOTES
          User secrets live in the running user's profile, so run this as the same account that
          runs the app. The tool prints the exact file it wrote.
        """);
}
