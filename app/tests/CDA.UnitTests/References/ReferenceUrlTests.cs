using CDA.Domain.References;

namespace CDA.UnitTests.References;

/// <summary>
/// The rule "a source appears once per topic" is only worth anything if near-identical
/// addresses collide. Otherwise the same study accumulates several entries, each with its own
/// votes, and the reference ratings stop meaning anything.
/// </summary>
public class ReferenceUrlTests
{
    private static string Canonical(string input)
    {
        Assert.True(ReferenceUrl.TryCanonicalize(input, out var result, out var error), error);
        return result;
    }

    [Theory]
    [InlineData("https://example.com/article", "https://example.com/article")]
    [InlineData("HTTPS://EXAMPLE.COM/article", "https://example.com/article")]
    [InlineData("https://Example.Com/Article", "https://example.com/Article")]
    public void The_scheme_and_host_are_lowercased_but_the_path_is_not(string input, string expected)
    {
        // Hosts are case-insensitive; paths are not, and "/Article" may genuinely differ.
        Assert.Equal(expected, Canonical(input));
    }

    [Theory]
    [InlineData("https://example.com/article/", "https://example.com/article")]
    [InlineData("https://example.com/", "https://example.com/")]
    public void A_trailing_slash_is_dropped_except_at_the_root(string input, string expected)
    {
        Assert.Equal(expected, Canonical(input));
    }

    [Fact]
    public void A_default_port_is_removed()
    {
        Assert.Equal("https://example.com/a", Canonical("https://example.com:443/a"));
        Assert.Equal("http://example.com/a", Canonical("http://example.com:80/a"));
    }

    [Fact]
    public void A_non_default_port_is_kept()
    {
        Assert.Equal("https://example.com:8443/a", Canonical("https://example.com:8443/a"));
    }

    [Fact]
    public void A_fragment_is_removed()
    {
        // It addresses a place within a document, not a different document.
        Assert.Equal("https://example.com/a", Canonical("https://example.com/a#section-3"));
    }

    [Theory]
    [InlineData("https://example.com/a?utm_source=twitter")]
    [InlineData("https://example.com/a?utm_medium=social&utm_campaign=spring")]
    [InlineData("https://example.com/a?fbclid=abc123")]
    [InlineData("https://example.com/a?gclid=xyz")]
    public void Tracking_parameters_are_stripped(string input)
    {
        Assert.Equal("https://example.com/a", Canonical(input));
    }

    [Fact]
    public void Meaningful_parameters_are_kept()
    {
        Assert.Equal("https://example.com/a?page=3", Canonical("https://example.com/a?page=3&utm_source=x"));
    }

    [Fact]
    public void Parameters_are_ordered_so_that_the_same_page_shared_two_ways_collides()
    {
        var first = Canonical("https://example.com/a?b=2&a=1");
        var second = Canonical("https://example.com/a?a=1&b=2");

        Assert.Equal(first, second);
    }

    [Fact]
    public void A_bare_host_is_assumed_to_be_https()
    {
        // People paste "example.com/article" far more often than they type a scheme.
        Assert.Equal("https://example.com/article", Canonical("example.com/article"));
    }

    [Fact]
    public void A_bare_host_with_a_port_is_not_mistaken_for_a_scheme()
    {
        Assert.Equal("https://example.com:8443/x", Canonical("example.com:8443/x"));
    }

    [Fact]
    public void The_whole_point_two_people_citing_the_same_source_collide()
    {
        var pasted = Canonical("HTTPS://Example.com/Article/?utm_source=twitter#intro");
        var typed = Canonical("example.com/Article");

        Assert.Equal(typed, pasted);
    }

    [Fact]
    public void Http_and_https_are_kept_distinct()
    {
        // They are usually the same document, and merging them would dedupe more. But the
        // stored address is what other people click: rewriting someone's http citation to
        // https would break it outright on a host that has no TLS. A split rating is the
        // lesser harm than a dead link.
        Assert.NotEqual(Canonical("http://example.com/a"), Canonical("https://example.com/a"));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://example.com/x")]
    public void Only_web_addresses_are_accepted(string input)
    {
        // A javascript: or file: "source" is either an attack or a mistake, and neither is
        // something another participant can go and check.
        Assert.False(ReferenceUrl.TryCanonicalize(input, out _, out var error));
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void An_empty_address_is_refused(string? input)
    {
        Assert.False(ReferenceUrl.TryCanonicalize(input, out _, out _));
    }

    [Fact]
    public void An_over_long_address_is_refused()
    {
        var tooLong = "https://example.com/" + new string('x', ReferenceUrl.MaxLength);

        Assert.False(ReferenceUrl.TryCanonicalize(tooLong, out _, out var error));
        Assert.Contains("longer than", error!);
    }
}
