using CDA.Domain.Localization;
using CDA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CDA.Infrastructure.Localization;

/// <summary>One translatable string as the admin screen shows it.</summary>
/// <param name="Key">The English source, which is also the identity of the string.</param>
/// <param name="Translation">The current translation, or empty if none has been written.</param>
/// <param name="IsTranslated">Whether a translation exists at all — drives the "missing" filter.</param>
public sealed record TranslationRow(string Key, string Translation, bool IsTranslated);

/// <summary>
/// Reads and writes translations, and primes the table on first run.
/// </summary>
/// <remarks>
/// The set of translatable strings is defined in code (the strings the views actually use), and
/// this service reconciles the database against it: it fills in anything missing from the seed
/// and never overwrites a value already there, so a translator's correction through the admin
/// screen always wins over the developer's first guess.
/// </remarks>
public sealed class LocalizationService(CdaDbContext database, LocalizationStore store)
{
    /// <summary>The one non-English language the platform ships with.</summary>
    public const string GreekCulture = "el-GR";

    /// <summary>
    /// Inserts a translation for every seeded string that has none yet.
    /// </summary>
    /// <remarks>
    /// Insert-only, on purpose. Re-running it after a translator has edited a string must not
    /// undo their work, so an existing row is left exactly as it is.
    /// </remarks>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var existing = await database.LocalizedTexts
            .Where(t => t.Culture == GreekCulture)
            .Select(t => t.Key)
            .ToListAsync(cancellationToken);

        var have = existing.ToHashSet();
        var added = false;

        foreach (var (key, value) in GreekSeed.Translations)
        {
            if (have.Add(key))
            {
                database.LocalizedTexts.Add(new LocalizedText(key, GreekCulture, value));
                added = true;
            }
        }

        if (!added)
        {
            return;
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another instance (or the startup seeder alongside a manual run) inserted the same
            // rows first. The rows exist either way, which is all that matters; drop this
            // attempt and read the winner's.
            database.ChangeTracker.Clear();
        }

        store.Invalidate();
    }

    /// <summary>
    /// Every translatable string, with its current translation, for the admin screen.
    /// </summary>
    /// <remarks>
    /// The universe of keys is the code's, not the table's: a string a developer just added shows
    /// up as untranslated straight away, rather than being invisible until someone thinks to add
    /// a row for it.
    /// </remarks>
    public async Task<IReadOnlyList<TranslationRow>> RowsAsync(
        string culture, bool missingOnly = false, CancellationToken cancellationToken = default)
    {
        var stored = await database.LocalizedTexts
            .Where(t => t.Culture == culture)
            .ToDictionaryAsync(t => t.Key, t => t.Value, cancellationToken);

        var rows = GreekSeed.Translations.Keys
            .Select(key =>
            {
                var translated = stored.TryGetValue(key, out var value);
                return new TranslationRow(key, translated ? value! : string.Empty, translated);
            });

        if (missingOnly)
        {
            rows = rows.Where(row => !row.IsTranslated);
        }

        return [.. rows.OrderBy(row => row.Key, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Sets one translation and refreshes the cache so the change is seen at once.</summary>
    public async Task SetAsync(
        string key, string culture, string value, CancellationToken cancellationToken = default)
    {
        var row = await database.LocalizedTexts
            .SingleOrDefaultAsync(t => t.Key == key && t.Culture == culture, cancellationToken);

        if (string.IsNullOrWhiteSpace(value))
        {
            // Clearing a translation removes the row, so the string falls back to English rather
            // than rendering as an empty gap.
            if (row is not null)
            {
                database.LocalizedTexts.Remove(row);
            }
        }
        else if (row is null)
        {
            database.LocalizedTexts.Add(new LocalizedText(key, culture, value));
        }
        else
        {
            row.Retranslate(value);
        }

        await database.SaveChangesAsync(cancellationToken);
        store.Invalidate();
    }
}
