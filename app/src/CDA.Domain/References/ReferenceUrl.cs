using System.Diagnostics.CodeAnalysis;

namespace CDA.Domain.References;

/// <summary>
/// Reduces a URL to a canonical form, so that two people citing the same source are recognised
/// as having cited the same source.
/// </summary>
/// <remarks>
/// The platform's rule is that a URL appears once per topic. That rule is only worth anything
/// if <c>HTTP://Example.com/Article/?utm_source=twitter</c> and
/// <c>https://example.com/article</c> collide — otherwise the same source accumulates several
/// entries, each with its own votes, and the reference tallies stop meaning anything.
/// </remarks>
public static class ReferenceUrl
{
    public const int MaxLength = 400;

    /// <summary>Tracking parameters that never identify a different document.</summary>
    private static readonly string[] TrackingParameters =
        ["utm_source", "utm_medium", "utm_campaign", "utm_term", "utm_content",
         "utm_id", "fbclid", "gclid", "msclkid", "mc_cid", "mc_eid", "igshid", "ref_src"];

    /// <summary>
    /// Produces the canonical form, or explains why the URL is unusable.
    /// </summary>
    public static bool TryCanonicalize(
        string? input,
        [NotNullWhen(true)] out string? canonical,
        [NotNullWhen(false)] out string? error)
    {
        canonical = null;
        error = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "A reference needs a web address.";
            return false;
        }

        var trimmed = input.Trim();

        // People paste bare hosts far more often than they type a scheme, so supply one — but
        // only when there is genuinely no scheme. Testing for "://" instead would turn
        // "javascript:alert(1)" into "https://javascript:alert(1)", which is refused for the
        // wrong reason and hides what was actually submitted.
        if (!HasScheme(trimmed))
        {
            trimmed = "https://" + trimmed;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            error = "That does not look like a web address.";
            return false;
        }

        // Only the web. A javascript: or file: reference is either an attack or a mistake, and
        // neither is a source anyone else can check.
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            error = "References must be http or https addresses.";
            return false;
        }

        if (string.IsNullOrEmpty(uri.Host))
        {
            error = "That web address has no host.";
            return false;
        }

        var builder = new UriBuilder(uri)
        {
            Scheme = uri.Scheme.ToLowerInvariant(),
            Host = uri.Host.ToLowerInvariant(),
            // Fragments address a place within a document, not a different document.
            Fragment = string.Empty,
        };

        // Default ports carry no information.
        builder.Port = uri.IsDefaultPort ? -1 : uri.Port;

        builder.Path = NormalisePath(uri.AbsolutePath);
        builder.Query = NormaliseQuery(uri.Query);

        var result = builder.Uri.ToString();

        if (result.Length > MaxLength)
        {
            error = $"That web address is longer than {MaxLength} characters.";
            return false;
        }

        canonical = result;
        return true;
    }

    /// <summary>Whether the text already begins with a URI scheme, per RFC 3986.</summary>
    private static bool HasScheme(string value)
    {
        var colon = value.IndexOf(':', StringComparison.Ordinal);

        if (colon <= 0 || !char.IsAsciiLetter(value[0]))
        {
            return false;
        }

        if (!value[..colon].All(c => char.IsAsciiLetterOrDigit(c) || c is '+' or '-' or '.'))
        {
            return false;
        }

        // "example.com:8443/x" is a host and a port, not a scheme. A port is all that can
        // sensibly follow the colon with a digit.
        var rest = value[(colon + 1)..];

        return rest.Length > 0 && !char.IsAsciiDigit(rest[0]);
    }

    private static string NormalisePath(string path)
    {
        // "/article/" and "/article" are the same document in practice; "/" must stay.
        var trimmed = path.Length > 1 ? path.TrimEnd('/') : path;

        return trimmed.Length == 0 ? "/" : trimmed;
    }

    /// <summary>
    /// Drops tracking parameters and orders the rest.
    /// </summary>
    /// <remarks>
    /// Ordering matters: two people sharing the same page from different places routinely
    /// produce the same parameters in a different order, and without sorting those would be
    /// stored as two distinct references.
    /// </remarks>
    private static string NormaliseQuery(string query)
    {
        if (string.IsNullOrEmpty(query) || query == "?")
        {
            return string.Empty;
        }

        var pairs = query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair =>
            {
                var separator = pair.IndexOf('=', StringComparison.Ordinal);
                return separator < 0
                    ? (Key: pair, Value: (string?)null)
                    : (Key: pair[..separator], Value: pair[(separator + 1)..]);
            })
            .Where(pair => !TrackingParameters.Contains(pair.Key.ToLowerInvariant()))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ThenBy(pair => pair.Value, StringComparer.Ordinal)
            .Select(pair => pair.Value is null ? pair.Key : $"{pair.Key}={pair.Value}")
            .ToList();

        return pairs.Count == 0 ? string.Empty : "?" + string.Join('&', pairs);
    }
}
