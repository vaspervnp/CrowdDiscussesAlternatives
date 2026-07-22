using System.Net;
using CDA.Application.Abstractions;
using CDA.Infrastructure.Identity;
using CDA.Infrastructure.Persistence;
using CDA.Infrastructure.Topics;
using CDA.Infrastructure.Voting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;

namespace CDA.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Configuration key holding the application's connection string.</summary>
    public const string ConnectionStringName = "Cda";

    /// <summary>
    /// The MariaDB version to generate SQL for. Pinned rather than auto-detected:
    /// AutoDetect opens a connection during startup, which turns a transient network
    /// blip into a failure to boot, and makes the generated SQL depend on whichever
    /// server happened to answer.
    /// </summary>
    public static readonly MariaDbServerVersion ServerVersion = new(new Version(11, 4, 3));

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"No connection string named '{ConnectionStringName}' was found. It is deliberately " +
                "absent from appsettings.json — set it in user secrets for local development:\n\n" +
                "  dotnet user-secrets set \"ConnectionStrings:Cda\" \"<connection string>\" " +
                "--project app/src/CDA.Web\n\n" +
                "or supply it as the environment variable ConnectionStrings__Cda.");
        }

        services.AddDbContext<CdaDbContext>(options =>
            options.UseMySql(
                RequireEncryptedConnection(connectionString),
                ServerVersion,
                mySql => mySql
                    .MigrationsAssembly(typeof(CdaDbContext).Assembly.FullName)
                    // The database is remote and reached over the public internet, so a
                    // dropped connection is an expected event rather than a bug.
                    .EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), null)));

        services.AddSingleton<IClock, SystemClock>();
        services.AddMemoryCache();
        services.AddScoped<PresenceTracker>();
        services.AddScoped<UserAccountService>();
        services.AddScoped<TopicService>();
        services.AddScoped<TopicVotingService>();

        return services;
    }

    /// <summary>
    /// Identity options shared by every host. The cookie and scheme wiring that goes with
    /// them lives in the web project, since it is ASP.NET Core rather than persistence.
    /// </summary>
    /// <remarks>
    /// Account confirmation is not required to sign in, because there is no mail transport
    /// until Phase 12. That is a deliberate temporary state: turn
    /// <c>SignIn.RequireConfirmedAccount</c> on in the same change that introduces email,
    /// otherwise anyone can register under an address they do not control.
    /// </remarks>
    public static void ConfigureIdentity(IdentityOptions options)
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;

        // Length is the requirement; composition rules are not. Demanding a digit and a
        // capital pushes people towards "Password1!" and rules out a long passphrase, which
        // is the stronger secret of the two. This follows NIST SP 800-63B rather than
        // Identity's defaults.
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;

        options.Lockout.MaxFailedAccessAttempts = 10;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    }

    /// <summary>
    /// Rejects any connection string that would send credentials in the clear over a
    /// network, and caps the connection pool.
    /// </summary>
    /// <remarks>
    /// The application's database sits on a shared, publicly reachable host whose certificate
    /// passes full chain and hostname validation, so no weaker SslMode is ever the right
    /// answer for it. Enforcing that in code rather than in documentation means a
    /// copy-pasted connection string cannot quietly downgrade the transport — the
    /// application refuses to start instead.
    ///
    /// The pool cap matters because max_connections is 151 for the whole shared server;
    /// an unbounded pool lets this one application exhaust a resource it does not own.
    /// </remarks>
    public static string RequireEncryptedConnection(string connectionString)
    {
        var builder = new MySqlConnectionStringBuilder(connectionString);

        if (!IsLoopback(builder.Server) &&
            builder.SslMode is not (MySqlSslMode.VerifyFull or MySqlSslMode.VerifyCA))
        {
            throw new InvalidOperationException(
                $"The connection string points at '{builder.Server}' with SslMode={builder.SslMode}, " +
                "which does not verify the server's certificate. That database is reached over a " +
                "network and its certificate validates, so use SslMode=VerifyFull. If VerifyFull " +
                "has started failing, the certificate needs renewing — do not weaken this setting.");
        }

        if (!builder.ContainsKey("MaximumPoolSize"))
        {
            builder.MaximumPoolSize = 20;
        }

        return builder.ConnectionString;
    }

    /// <summary>
    /// Whether the server is reached without touching a network.
    /// </summary>
    /// <remarks>
    /// The transport requirement above protects traffic crossing a network. A loopback
    /// address never leaves the machine, so there is nothing on the wire to intercept and
    /// nothing a certificate could attest to — this is how the CI job talks to its
    /// throwaway MariaDB container, which has no CA to trust. The exception is deliberately
    /// narrow: it turns on the address alone, so it cannot be reached by a remote host.
    /// </remarks>
    private static bool IsLoopback(string server) =>
        string.Equals(server, "localhost", StringComparison.OrdinalIgnoreCase) ||
        (IPAddress.TryParse(server, out var address) && IPAddress.IsLoopback(address));
}
