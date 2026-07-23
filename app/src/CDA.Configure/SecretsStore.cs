using System.Text.Json;
using System.Text.Json.Nodes;

namespace CDA.Configure;

/// <summary>
/// Reads and writes the ASP.NET Core user-secrets store — the same file
/// <c>dotnet user-secrets set</c> writes, so the web app reads exactly what this tool saves.
/// </summary>
/// <remarks>
/// <para>
/// The store is a plain <c>secrets.json</c> under the running user's profile, keyed by the
/// application's <see cref="UserSecretsId"/>. Keeping the connection string here rather than in an
/// <c>appsettings</c> file means the credential never sits in the deployment folder next to the
/// executable, and never in the repository.
/// </para>
/// <para>
/// Because the store lives in a <em>user</em> profile, the account that runs this tool must be the
/// same account that runs the web app. That is the one sharp edge of user secrets, and the tool
/// prints the exact path it wrote so the point is hard to miss.
/// </para>
/// </remarks>
public static class SecretsStore
{
    /// <summary>
    /// Must match <c>&lt;UserSecretsId&gt;</c> in <c>src/CDA.Web/CDA.Web.csproj</c>. If that
    /// changes, change it here too, or the app and this tool will read different files.
    /// </summary>
    public const string UserSecretsId = "cda-web-secrets";

    /// <summary>The configuration key the web app reads its connection string from.</summary>
    public const string ConnectionStringKey = "ConnectionStrings:Cda";

    /// <summary>
    /// The full path to <c>secrets.json</c> for this application and the current user.
    /// </summary>
    /// <remarks>
    /// This reproduces the layout the .NET SDK uses, so the file is interchangeable with one
    /// written by <c>dotnet user-secrets</c>: <c>%APPDATA%\Microsoft\UserSecrets\&lt;id&gt;\secrets.json</c>
    /// on Windows, <c>~/.microsoft/usersecrets/&lt;id&gt;/secrets.json</c> elsewhere.
    /// </remarks>
    public static string Path()
    {
        var appData = Environment.GetEnvironmentVariable("APPDATA");

        var root = !string.IsNullOrEmpty(appData)
            ? System.IO.Path.Combine(appData, "Microsoft", "UserSecrets")
            : System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".microsoft", "usersecrets");

        return System.IO.Path.Combine(root, UserSecretsId, "secrets.json");
    }

    /// <summary>Reads a single value from the store, or null if the file or key is absent.</summary>
    public static string? Read(string key)
    {
        var path = Path();

        if (!File.Exists(path))
        {
            return null;
        }

        var root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
        return root?[key]?.GetValue<string>();
    }

    /// <summary>
    /// Sets one key, leaving every other secret in the file untouched.
    /// </summary>
    /// <remarks>
    /// The key is written flat (<c>"ConnectionStrings:Cda"</c>), exactly as
    /// <c>dotnet user-secrets set</c> writes it, and the configuration system reads flat and
    /// nested forms alike. On Unix the file is left readable only by its owner.
    /// </remarks>
    public static void Write(string key, string value)
    {
        var path = Path();
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);

        var root = File.Exists(path)
            ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? new JsonObject()
            : new JsonObject();

        root[key] = value;

        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        if (!OperatingSystem.IsWindows())
        {
            // rw for the owner, nothing for anyone else — a credential should not be world-readable.
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
