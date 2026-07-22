namespace CDA.Domain.Localization;

/// <summary>
/// One string, in one language.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="Key"/> is the English source text, and the table holds only the
/// <em>translations</em> of it — there is no row for English, because English is the key. That
/// keeps a single source of truth (the string a developer wrote in the view) and means a page
/// with no translation yet still renders, in English, rather than breaking.
/// </para>
/// <para>
/// Text lives in the database rather than in resource files so that it can be corrected by a
/// translator through the admin screen without a redeployment — which is exactly what the source
/// specification asks for ("text will be in a table and translatable").
/// </para>
/// </remarks>
public sealed class LocalizedText
{
    /// <summary>
    /// The longest key stored. Keys are whole sentences, not identifiers, so this is generous.
    /// </summary>
    public const int KeyMaxLength = 512;

    private LocalizedText()
    {
        // EF Core.
        Key = null!;
        Culture = null!;
        Value = null!;
    }

    public LocalizedText(string key, string culture, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);
        ArgumentNullException.ThrowIfNull(value);

        Key = key;
        Culture = culture;
        Value = value;
    }

    /// <summary>The English source string this translates.</summary>
    public string Key { get; private set; }

    /// <summary>The culture this value is written in, e.g. <c>el-GR</c>.</summary>
    public string Culture { get; private set; }

    /// <summary>The translated text, with any <c>%name%</c> holes carried over from the key.</summary>
    public string Value { get; private set; }

    public void Retranslate(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }
}
