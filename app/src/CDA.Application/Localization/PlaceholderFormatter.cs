using System.Globalization;
using System.Text;

namespace CDA.Application.Localization;

/// <summary>
/// Fills the <c>%name%</c> holes in a translated string with values from the system.
/// </summary>
/// <remarks>
/// <para>
/// The source specification calls for text like "text text %data% text", so that a string with
/// a value dropped into it can still be translated. Named holes rather than positional ones are
/// the whole point: languages order their words differently, and a translator has to be free to
/// write "%count% messages" as "messages: %count%" — or to move the subject to the end — without
/// the substitution breaking. Positional <c>{0}</c> formatting cannot express that, because the
/// order is fixed in the code, not the translation.
/// </para>
/// <para>
/// A hole with no matching value is left exactly as written. That is deliberate: a visible
/// <c>%count%</c> in the page is a loud, obvious sign that a caller forgot an argument, where a
/// silent blank would hide it. A value with no matching hole is simply unused — harmless, and it
/// lets a translator drop a detail a particular language does not need.
/// </para>
/// </remarks>
public static class PlaceholderFormatter
{
    /// <summary>
    /// Replaces each <c>%name%</c> in <paramref name="template"/> with the matching value.
    /// </summary>
    /// <remarks>
    /// Values are formatted with <see cref="CultureInfo.CurrentCulture"/>, so a number or date
    /// dropped into a Greek sentence is written the Greek way. Matching is case-insensitive so a
    /// translator need not remember the exact casing a developer chose.
    /// </remarks>
    public static string Apply(string template, IReadOnlyList<(string Name, object? Value)> data)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(data);

        // No holes possible without a pair of delimiters, so the common case does no work.
        if (data.Count == 0 || template.IndexOf('%') < 0)
        {
            return template;
        }

        var result = new StringBuilder(template.Length + 16);
        var index = 0;

        while (index < template.Length)
        {
            var open = template.IndexOf('%', index);

            if (open < 0)
            {
                result.Append(template, index, template.Length - index);
                break;
            }

            var close = template.IndexOf('%', open + 1);

            if (close < 0)
            {
                // A lone, unclosed '%' — literal text, not the start of a hole.
                result.Append(template, index, template.Length - index);
                break;
            }

            var name = template.Substring(open + 1, close - open - 1);
            var match = Find(data, name);

            if (name.Length > 0 && match is { } value)
            {
                result.Append(template, index, open - index);
                result.Append(Convert.ToString(value.Value, CultureInfo.CurrentCulture));
                index = close + 1;
            }
            else
            {
                // Not a known hole (an empty %% or an unmatched name): keep the text through the
                // opening '%' verbatim and carry on from just after it, so a later '%' can still
                // open a real hole.
                result.Append(template, index, open + 1 - index);
                index = open + 1;
            }
        }

        return result.ToString();
    }

    private static (string Name, object? Value)? Find(
        IReadOnlyList<(string Name, object? Value)> data, string name)
    {
        foreach (var pair in data)
        {
            if (string.Equals(pair.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return pair;
            }
        }

        return null;
    }
}
