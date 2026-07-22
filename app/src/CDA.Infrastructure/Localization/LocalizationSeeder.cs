using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CDA.Infrastructure.Localization;

/// <summary>
/// Fills in any missing shipped translations once, as the application starts.
/// </summary>
/// <remarks>
/// Seeding here rather than in a migration keeps the translations as data the app owns, editable
/// afterwards, instead of baking a first guess into the schema history. It is best-effort: a
/// database that is unreachable or not yet migrated must not stop the app from booting, because
/// the strings all fall back to English regardless.
/// </remarks>
public sealed class LocalizationSeeder(
    IServiceScopeFactory scopes,
    ILogger<LocalizationSeeder> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<LocalizationService>()
                .SeedAsync(cancellationToken);
        }
        catch (Exception error)
        {
            // English still works; a failure here is a missing-Greek problem, not a down-site one.
            logger.LogError(error, "Could not seed the translation table; Greek may be incomplete");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
