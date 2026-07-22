using CDA.Domain.Localization;
using CDA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CDA.Infrastructure.Localization;

/// <summary>
/// Holds every translation in memory, so that a page render is not a database round trip per
/// string.
/// </summary>
/// <remarks>
/// <para>
/// A page can ask for a hundred strings; going to the database for each would make translation
/// cost more than everything else on the page put together. The whole table is small — a few
/// hundred rows per language — so it lives in memory, loaded once and rebuilt only when the
/// admin screen changes something.
/// </para>
/// <para>
/// The cache is swapped by replacing one reference, never by mutating the live dictionary, so a
/// request reading translations while the admin saves an edit sees either the whole old snapshot
/// or the whole new one, never a half-built one.
/// </para>
/// </remarks>
public sealed class LocalizationStore(IServiceScopeFactory scopes)
{
    private readonly Lock _reload = new();

    // Replaced wholesale on reload; reads take the current reference and are never blocked.
    private volatile IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? _byCulture;

    /// <summary>
    /// The translation of <paramref name="key"/> into <paramref name="culture"/>, or null if
    /// there is none — in which case the caller falls back to the key, which is English.
    /// </summary>
    /// <remarks>
    /// A request for a specific culture ("el-GR") also accepts a translation filed under its
    /// bare language ("el"), so a translation need not be repeated for every regional variant.
    /// </remarks>
    public string? Find(string culture, string key)
    {
        var snapshot = _byCulture ?? Load();

        if (snapshot.TryGetValue(culture, out var exact) && exact.TryGetValue(key, out var value))
        {
            return value;
        }

        var dash = culture.IndexOf('-');

        if (dash > 0)
        {
            var language = culture[..dash];

            if (snapshot.TryGetValue(language, out var neutral) && neutral.TryGetValue(key, out var byLanguage))
            {
                return byLanguage;
            }
        }

        return null;
    }

    /// <summary>Every key known in a culture, for priming the framework localizer.</summary>
    public IReadOnlyDictionary<string, string> ForCulture(string culture)
    {
        var snapshot = _byCulture ?? Load();

        return snapshot.TryGetValue(culture, out var exact)
            ? exact
            : new Dictionary<string, string>();
    }

    /// <summary>Rebuilds the cache from the database. Called after an edit lands.</summary>
    public void Invalidate() => Load();

    private IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Load()
    {
        lock (_reload)
        {
            using var scope = scopes.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<CdaDbContext>();

            var rows = database.Set<LocalizedText>().AsNoTracking().ToList();

            var built = rows
                .GroupBy(row => row.Culture)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyDictionary<string, string>)group.ToDictionary(
                        row => row.Key, row => row.Value),
                    StringComparer.OrdinalIgnoreCase);

            var snapshot = (IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>)built;
            _byCulture = snapshot;
            return snapshot;
        }
    }
}
