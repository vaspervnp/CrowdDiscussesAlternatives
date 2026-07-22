using CDA.Infrastructure.Localization;

namespace CDA.Web.Models;

public sealed class TranslationsViewModel
{
    public required IReadOnlyList<TranslationRow> Rows { get; init; }

    /// <summary>The language being edited. English is the source, so it is never edited here.</summary>
    public required string Culture { get; init; }

    public required string CultureName { get; init; }

    /// <summary>Whether the list is narrowed to the strings still missing a translation.</summary>
    public required bool MissingOnly { get; init; }

    public required int TotalCount { get; init; }

    public required int MissingCount { get; init; }
}
