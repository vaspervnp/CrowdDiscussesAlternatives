using System.Globalization;
using CDA.Application.Localization;
using Microsoft.Extensions.Localization;

namespace CDA.Infrastructure.Localization;

/// <summary>
/// The application's translator: turns an English source string into the reader's language,
/// reading from the in-memory <see cref="LocalizationStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// It answers to two contracts at once. <see cref="IAppLocalizer"/> is what the views and
/// controllers use — a plain indexer plus named-placeholder formatting. <see cref="IStringLocalizer"/>
/// is the framework's, so the same store also localizes validation messages without a second
/// mechanism.
/// </para>
/// <para>
/// The language is read from <see cref="CultureInfo.CurrentUICulture"/> at the moment of the
/// call, not captured when this object is built. That is why a single shared instance is safe:
/// each request has its own current culture, set by the localization middleware.
/// </para>
/// </remarks>
public sealed class DatabaseLocalizer(LocalizationStore store) : IAppLocalizer, IStringLocalizer
{
    public string this[string key] => Translate(key);

    string IAppLocalizer.Format(string key, params (string Name, object? Value)[] data) =>
        PlaceholderFormatter.Apply(Translate(key), data);

    private string Translate(string key) =>
        store.Find(CultureInfo.CurrentUICulture.Name, key) ?? key;

    // --- IStringLocalizer -------------------------------------------------------------------

    LocalizedString IStringLocalizer.this[string name]
    {
        get
        {
            var value = store.Find(CultureInfo.CurrentUICulture.Name, name);
            return new LocalizedString(name, value ?? name, resourceNotFound: value is null);
        }
    }

    LocalizedString IStringLocalizer.this[string name, params object[] arguments]
    {
        get
        {
            // The framework path is positional ({0}); the named-placeholder path is Format above.
            // Both are honoured so a caller can use either, but the app itself prefers Format.
            var translated = store.Find(CultureInfo.CurrentUICulture.Name, name);
            var text = string.Format(
                CultureInfo.CurrentCulture, translated ?? name, arguments);
            return new LocalizedString(name, text, resourceNotFound: translated is null);
        }
    }

    IEnumerable<LocalizedString> IStringLocalizer.GetAllStrings(bool includeParentCultures) =>
        store.ForCulture(CultureInfo.CurrentUICulture.Name)
            .Select(pair => new LocalizedString(pair.Key, pair.Value, resourceNotFound: false));
}

/// <summary>
/// Hands out the one <see cref="DatabaseLocalizer"/> for every request the framework makes for a
/// localizer, whatever resource type or name it asks about.
/// </summary>
/// <remarks>
/// There is a single flat namespace of strings — a whole sentence is its own key — so the
/// resource grouping the framework expects (per type, per path) does not apply here. Every
/// factory call returns the same store-backed localizer.
/// </remarks>
public sealed class DatabaseLocalizerFactory(DatabaseLocalizer localizer) : IStringLocalizerFactory
{
    public IStringLocalizer Create(Type resourceSource) => localizer;

    public IStringLocalizer Create(string baseName, string location) => localizer;
}
