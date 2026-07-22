namespace CDA.Application.Localization;

/// <summary>
/// Turns a source (English) string into the reader's language.
/// </summary>
/// <remarks>
/// <para>
/// The key <em>is</em> the English text. A missing translation therefore falls back to readable
/// English rather than to a symbolic token like <c>Nav.Home</c> — a half-translated page is
/// still a usable page, and a developer who writes a new string gets working English for free
/// without having to seed anything.
/// </para>
/// <para>
/// This is the contract the views and controllers use. Infrastructure backs it with a cached,
/// database-driven implementation that also satisfies the framework's
/// <c>IStringLocalizer</c>, so validation messages localize through the same store.
/// </para>
/// </remarks>
public interface IAppLocalizer
{
    /// <summary>The reader's-language version of <paramref name="key"/>, or the key itself.</summary>
    string this[string key] { get; }

    /// <summary>
    /// Translates <paramref name="key"/>, then fills its <c>%name%</c> holes from the system.
    /// </summary>
    /// <remarks>
    /// The holes are filled <em>after</em> translation, so a translator is free to move
    /// "%count% new" to wherever the target language wants it. See
    /// <see cref="PlaceholderFormatter"/>.
    /// </remarks>
    string Format(string key, params (string Name, object? Value)[] data);
}
