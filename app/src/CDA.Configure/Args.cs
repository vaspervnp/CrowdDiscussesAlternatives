namespace CDA.Configure;

/// <summary>Signals bad command-line input, mapped to a non-zero exit code.</summary>
public sealed class BadInputException(string message) : Exception(message);

/// <summary>The parsed command line.</summary>
public sealed class Args
{
    public bool Help { get; private init; }

    public bool Show { get; private init; }

    public bool ShowPath { get; private init; }

    public bool NoTest { get; private init; }

    public string? Connection { get; private init; }

    public string? Host { get; private init; }

    public string? Port { get; private init; }

    public string? Database { get; private init; }

    public string? User { get; private init; }

    public string? Password { get; private init; }

    public string? SslMode { get; private init; }

    /// <summary>Whether the caller gave enough to build a connection string without prompting.</summary>
    public bool HasBuildInputs =>
        Connection is not null || Host is not null || User is not null || Password is not null
        || Port is not null || Database is not null || SslMode is not null;

    /// <summary>
    /// Reads <c>--flag value</c>, <c>--flag=value</c> and bare switches. Unknown options are an
    /// error rather than being ignored, so a typo does not quietly leave a setting unchanged.
    /// </summary>
    public static Args Parse(string[] args)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var switches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var valueOptions = new[]
        {
            "connection", "host", "port", "database", "user", "password", "sslmode",
        };
        var switchOptions = new[] { "help", "show", "path", "no-test" };

        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i];

            if (token is "-h" or "-?")
            {
                switches.Add("help");
                continue;
            }

            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                throw new BadInputException($"unexpected argument '{token}'. Run --help for usage.");
            }

            var name = token[2..];
            string? inlineValue = null;

            var equals = name.IndexOf('=');
            if (equals >= 0)
            {
                inlineValue = name[(equals + 1)..];
                name = name[..equals];
            }

            if (switchOptions.Contains(name))
            {
                switches.Add(name);
            }
            else if (valueOptions.Contains(name))
            {
                var value = inlineValue ?? (i + 1 < args.Length ? args[++i] : null)
                    ?? throw new BadInputException($"--{name} needs a value.");
                values[name] = value;
            }
            else
            {
                throw new BadInputException($"unknown option '--{name}'. Run --help for usage.");
            }
        }

        return new Args
        {
            Help = switches.Contains("help"),
            Show = switches.Contains("show"),
            ShowPath = switches.Contains("path"),
            NoTest = switches.Contains("no-test"),
            Connection = values.GetValueOrDefault("connection"),
            Host = values.GetValueOrDefault("host"),
            Port = values.GetValueOrDefault("port"),
            Database = values.GetValueOrDefault("database"),
            User = values.GetValueOrDefault("user"),
            Password = values.GetValueOrDefault("password"),
            SslMode = values.GetValueOrDefault("sslmode"),
        };
    }
}
